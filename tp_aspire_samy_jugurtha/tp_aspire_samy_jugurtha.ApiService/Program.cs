using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using tp_aspire_samy_jugurtha.ApiService.Data;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;

namespace tp_aspire_samy_jugurtha.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        ConfigureDatabase(builder);
        ConfigureAuthentication(builder);

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        await EnsureDatabaseSeededAsync(app);

        MapEndpoints(app);

        await app.RunAsync();
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder)
    {
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
    }

    private static void ConfigureAuthentication(WebApplicationBuilder builder)
    {
        var authority = builder.Configuration["Authentication:OIDC:Authority"];
        var audience = builder.Configuration["Authentication:OIDC:Audience"];
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = !isDevelopment;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = authority,
                    ValidateIssuer = true,
                    ValidAudience = audience,
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
                        if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity claimsIdentity)
                        {
                            var realmAccessClaim = context.Principal.FindFirst(c => c.Type == "realm_access");
                            if (!string.IsNullOrEmpty(realmAccessClaim?.Value))
                            {
                                var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                                if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                                {
                                    foreach (var role in rolesElement.EnumerateArray())
                                    {
                                        claimsIdentity.AddClaim(new System.Security.Claims.Claim("realm_roles", role.GetString() ?? string.Empty));
                                    }
                                }
                            }
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }

    private static void MapEndpoints(WebApplication app)
    {
        // ---- Workspaces ----
        app.MapGet("/api/workspaces", [Authorize] async (WorklyDbContext db) =>
            await db.Workspaces.AsNoTracking().ToListAsync());

        app.MapPost("/api/workspaces", [Authorize(Roles = "admin")] async (WorklyDbContext db, Workspace ws) =>
        {
            db.Workspaces.Add(ws);
            await db.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{ws.Id}", ws);
        });

        app.MapGet("/api/rooms", [Authorize] async (WorklyDbContext db) => await db.Rooms.AsNoTracking().ToListAsync());

        app.MapPost("/api/rooms", [Authorize] async (WorklyDbContext db, Room room) =>
        {
            // Vérifie que le workspace cible existe avant l'insertion pour éviter l'erreur FK
            var workspaceExists = await db.Workspaces.AnyAsync(w => w.Id == room.WorkspaceId);
            if (!workspaceExists)
            {
                return Results.NotFound(new { Message = $"Workspace inexistant: {room.WorkspaceId}" });
            }
            db.Rooms.Add(room);
            await db.SaveChangesAsync();
            return Results.Created($"/api/rooms/{room.Id}", room);
        });

        app.MapGet("/api/bookings", [Authorize] async (WorklyDbContext db) => await db.Bookings.AsNoTracking().ToListAsync());

        app.MapGet("/api/bookings/all", [Authorize(Roles = "admin")] async (WorklyDbContext db) =>
            await db.Bookings.AsNoTracking().ToListAsync());

        app.MapPost("/api/bookings", [Authorize] async (ClaimsPrincipal principal, WorklyDbContext db, Booking booking) =>
        {
            var overlap = await db.Bookings.AnyAsync(existing =>
                existing.ResourceType == booking.ResourceType &&
                existing.ResourceId == booking.ResourceId &&
                existing.Status != BookingStatus.Cancelled &&
                booking.StartUtc < existing.EndUtc && existing.StartUtc < booking.EndUtc);

            if (overlap)
            {
                return Results.Conflict(new
                {
                    Message = "Ce créneau est déjà réservé.",
                    ExistingStartUtc = booking.StartUtc,
                    ExistingEndUtc = booking.EndUtc
                });
            }

            var resolvedUser = await ResolveAppUserAsync(principal, db);
            if (resolvedUser is null)
            {
                return Results.BadRequest(new { Message = "Impossible de déterminer l'utilisateur courant." });
            }

            await db.SaveChangesAsync();

            booking.Id = 0;
            booking.AppUserId = resolvedUser.Id;
            booking.AppUser = null; // éviter les cycles de sérialisation
            if (booking.Status == default)
            {
                booking.Status = BookingStatus.Confirmed;
            }

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var dto = new
            {
                booking.Id,
                booking.AppUserId,
                booking.ResourceType,
                booking.ResourceId,
                booking.StartUtc,
                booking.EndUtc,
                booking.Status
            };

            return Results.Created($"/api/bookings/{booking.Id}", dto);
        });

        app.MapDelete("/api/bookings/{id}", [Authorize(Roles = "admin")] async (WorklyDbContext db, int id) =>
        {
            var booking = await db.Bookings.FindAsync(id);
            if (booking is null)
            {
                return Results.NotFound();
            }

            db.Bookings.Remove(booking);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task EnsureDatabaseSeededAsync(WebApplication app)
    {
        var runMigrations = app.Configuration.GetValue<bool>("RunMigrations", true);
        if (!runMigrations)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorklyDbContext>();
        db.Database.Migrate();
        await SeedAsync(db);
    }

    // ---- seed simple pour la démo ----
    private static async Task SeedAsync(WorklyDbContext db)
    {
        if (!await db.AppUsers.AnyAsync())
        {
            db.AppUsers.Add(new AppUser { DisplayName = "demo", Email = "demo@workly.test" });
            await db.SaveChangesAsync();
        }

        if (!await db.Workspaces.AnyAsync())
        {
            db.Workspaces.Add(new Workspace { Name = "HQ Paris" });
            await db.SaveChangesAsync();
        }

        if (!await db.Rooms.AnyAsync())
        {
            db.Rooms.Add(new Room { WorkspaceId = 1, Name = "Salle Volt", Capacity = 6 });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<AppUser?> ResolveAppUserAsync(ClaimsPrincipal principal, WorklyDbContext db)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.Email)
                         ?? principal.FindFirst("preferred_username")?.Value
                         ?? principal.FindFirstValue(ClaimTypes.Name)
                         ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var email = identifier.Contains('@', StringComparison.Ordinal)
            ? identifier
            : $"{identifier}@workly.local";

        var displayName = principal.FindFirstValue("name")
                           ?? principal.Identity?.Name
                           ?? identifier;

        var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (existing != null)
        {
            return existing;
        }

        var created = new AppUser
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName
        };

        db.AppUsers.Add(created);
        return created;
    }
}