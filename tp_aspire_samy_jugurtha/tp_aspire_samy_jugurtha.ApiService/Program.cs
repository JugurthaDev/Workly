using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json.Serialization;
using tp_aspire_samy_jugurtha.ApiService.Data;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDatabase", false);
builder.Services.AddDbContext<WorklyDbContext>(opt =>
{
    if (useInMemoryDb)
    {
        opt.UseInMemoryDatabase("WorklyTests");
    }
    else
    {
        opt.UseNpgsql(builder.Configuration.GetConnectionString("workly"));
    }
});

var authority = builder.Configuration["Authentication:OIDC:Authority"];
var audience  = builder.Configuration["Authentication:OIDC:Audience"];
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer   = authority,
            ValidateIssuer = true,
            ValidAudience  = audience,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            RoleClaimType = "realm_roles",
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                if (claimsIdentity != null)
                {
                    var realmAccessClaim = context.Principal?.FindFirst(c => c.Type == "realm_access");
                    if (realmAccessClaim?.Value != null)
                    {
                        var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                        if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                        {
                            foreach (var role in rolesElement.EnumerateArray())
                            {
                                claimsIdentity.AddClaim(new System.Security.Claims.Claim("realm_roles", role.GetString() ?? ""));
                            }
                        }
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Option to skip migrations/seeding (useful for tests)
var runMigrations = app.Configuration.GetValue<bool>("RunMigrations", true);
if (runMigrations)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<WorklyDbContext>();
        db.Database.Migrate();
        await SeedAsync(db);
    }
}

app.MapGet("/api/rooms", [Authorize] async (WorklyDbContext db) => await db.Rooms.AsNoTracking().ToListAsync());

app.MapPost("/api/rooms", [Authorize] async (WorklyDbContext db, Room room) =>
{
    // Forcer WorkspaceId = 1 pour toutes les créations
    room.WorkspaceId = 1;
    
    db.Rooms.Add(room);
    await db.SaveChangesAsync();
    return Results.Created($"/api/rooms/{room.Id}", room);
});

app.MapGet("/api/bookings", [Authorize] async (WorklyDbContext db) => await db.Bookings.AsNoTracking().ToListAsync());

app.MapGet("/api/bookings/all", [Authorize(Roles = "admin")] async (WorklyDbContext db) => 
    await db.Bookings.AsNoTracking().ToListAsync());

app.MapPost("/api/bookings", [Authorize] async (ClaimsPrincipal user, WorklyDbContext db, Booking b, ILogger<Program> logger) =>
{
    var email = user.FindFirst("email")?.Value
        ?? user.FindFirst("preferred_username")?.Value
        ?? user.Identity?.Name;

    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.BadRequest("Impossible de déterminer l'utilisateur connecté.");
    }

    var displayName = user.FindFirst("name")?.Value
        ?? user.Identity?.Name
        ?? email;

    var appUser = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
    if (appUser is null)
    {
        appUser = new AppUser
        {
            Email = email,
            DisplayName = displayName
        };

        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();
    }

    b.AppUserId = appUser.Id;
    var normalizedStartUtc = NormalizeUtc(b.StartUtc);
    var normalizedEndUtc = NormalizeUtc(b.EndUtc);

    if (normalizedEndUtc <= normalizedStartUtc)
    {
        return Results.BadRequest("La date de fin doit être postérieure à la date de début.");
    }

    b.StartUtc = normalizedStartUtc;
    b.EndUtc = normalizedEndUtc;

    var existingBookings = await db.Bookings
        .Where(x =>
            x.ResourceType == b.ResourceType &&
            x.ResourceId == b.ResourceId &&
            x.Status != BookingStatus.Cancelled)
        .ToListAsync();

    var conflictingBooking = existingBookings
        .Select(x => new
        {
            Booking = x,
            StartUtc = NormalizeUtc(x.StartUtc),
            EndUtc = NormalizeUtc(x.EndUtc)
        })
        .FirstOrDefault(x => normalizedStartUtc < x.EndUtc && x.StartUtc < normalizedEndUtc);

    if (conflictingBooking is not null)
    {
        logger.LogWarning("Booking conflict for resource {ResourceType}:{ResourceId} requested {Start:o} - {End:o} overlaps with booking #{ExistingId} ({ExistingStart:o} - {ExistingEnd:o})",
            b.ResourceType,
            b.ResourceId,
            normalizedStartUtc,
            normalizedEndUtc,
            conflictingBooking.Booking.Id,
            conflictingBooking.StartUtc,
            conflictingBooking.EndUtc);

        return Results.Conflict(new BookingConflictPayload(
            "Ce créneau est déjà réservé.",
            conflictingBooking.StartUtc,
            conflictingBooking.EndUtc));
    }

    db.Bookings.Add(b);
    await db.SaveChangesAsync();
    logger.LogInformation("Booking #{BookingId} created for resource {ResourceType}:{ResourceId} ({Start:o} - {End:o})",
        b.Id,
        b.ResourceType,
        b.ResourceId,
        b.StartUtc,
        b.EndUtc);
    return Results.Created($"/api/bookings/{b.Id}", b);
});

app.MapDelete("/api/bookings/{id}", [Authorize(Roles = "admin")] async (WorklyDbContext db, int id) =>
{
    var booking = await db.Bookings.FindAsync(id);
    if (booking is null) return Results.NotFound();
    
    db.Bookings.Remove(booking);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// ---- seed simple pour la démo ----
static async Task SeedAsync(WorklyDbContext db)
{
    if (!await db.AppUsers.AnyAsync())
    {
        db.AppUsers.Add(new AppUser { Id = 1, DisplayName = "demo", Email = "demo@workly.test" });
        await db.SaveChangesAsync();
    }

    if (!await db.Workspaces.AnyAsync())
    {
        db.Workspaces.Add(new Workspace { Id = 1, Name = "HQ Paris", City = "Paris" });
        await db.SaveChangesAsync();
    }

    if (!await db.Rooms.AnyAsync())
    {
        db.Rooms.Add(new Room { WorkspaceId = 1, Name = "Salle Volt", Location = "Étage 2", Capacity = 6 });
        await db.SaveChangesAsync();
    }
}

static DateTime NormalizeUtc(DateTime value)
{
    var utc = value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };

    return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
}

public sealed record BookingConflictPayload(string Message, DateTime ExistingStartUtc, DateTime ExistingEndUtc);
