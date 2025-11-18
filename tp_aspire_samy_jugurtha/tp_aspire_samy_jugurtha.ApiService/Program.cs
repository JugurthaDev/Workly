using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using tp_aspire_samy_jugurtha.ApiService.Data;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<WorklyDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("workly")));

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorklyDbContext>();
    db.Database.Migrate();
    await SeedAsync(db);
}

app.MapGet("/api/rooms", [Authorize] async (WorklyDbContext db) => await db.Rooms.AsNoTracking().ToListAsync());

app.MapPost("/api/rooms", [Authorize] async (WorklyDbContext db, Room room) =>
{
    db.Rooms.Add(room);
    await db.SaveChangesAsync();
    return Results.Created($"/api/rooms/{room.Id}", room);
});

app.MapGet("/api/bookings", [Authorize] async (WorklyDbContext db) => await db.Bookings.AsNoTracking().ToListAsync());

app.MapGet("/api/bookings/all", [Authorize(Roles = "admin")] async (WorklyDbContext db) => 
    await db.Bookings.AsNoTracking().ToListAsync());

app.MapPost("/api/bookings", [Authorize] async (WorklyDbContext db, Booking b) =>
{
    var overlap = await db.Bookings.AnyAsync(x =>
        x.ResourceType == b.ResourceType &&
        x.ResourceId == b.ResourceId &&
        x.Status != BookingStatus.Cancelled &&
        b.StartUtc < x.EndUtc && x.StartUtc < b.EndUtc);

    if (overlap) return Results.Conflict("Créneau déjà pris.");

    db.Bookings.Add(b);
    await db.SaveChangesAsync();
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
        db.Workspaces.Add(new Workspace { Id = 1, Name = "HQ Paris" });
        await db.SaveChangesAsync();
    }

    if (!await db.Rooms.AnyAsync())
    {
        db.Rooms.Add(new Room { WorkspaceId = 1, Name = "Salle Volt", Capacity = 6 });
        await db.SaveChangesAsync();
    }
}