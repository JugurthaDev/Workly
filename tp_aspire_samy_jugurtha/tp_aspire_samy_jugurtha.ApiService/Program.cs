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

        app.MapGet("/api/bookings", [Authorize] async (ClaimsPrincipal principal, WorklyDbContext db) =>
        {
            // On récupère l'utilisateur applicatif correspondant à l'utilisateur connecté
            var resolvedUser = await ResolveAppUserAsync(principal, db);
            if (resolvedUser is null)
            {
                return Results.BadRequest(new { Message = "Impossible de déterminer l'utilisateur courant." });
            }

            var userBookings = await db.Bookings
                .AsNoTracking()
                .Where(b => b.AppUserId == resolvedUser.Id)
                .OrderByDescending(b => b.StartUtc)
                .Select(b => new Booking
                {
                    Id = b.Id,
                    AppUserId = b.AppUserId,
                    ResourceType = b.ResourceType,
                    ResourceId = b.ResourceId,
                    StartUtc = b.StartUtc,
                    EndUtc = b.EndUtc,
                    Status = b.Status,
                    AppUser = null
                })
                .ToListAsync();

            return Results.Ok(userBookings);
        });

        app.MapGet("/api/bookings/all", [Authorize(Roles = "admin")] async (WorklyDbContext db) =>
            await db.Bookings.AsNoTracking().Select(b => new Booking
            {
                Id = b.Id,
                AppUserId = b.AppUserId,
                ResourceType = b.ResourceType,
                ResourceId = b.ResourceId,
                StartUtc = b.StartUtc,
                EndUtc = b.EndUtc,
                Status = b.Status,
                AppUser = null
            }).ToListAsync());

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
            booking.AppUser = null;
            if (booking.Status == default)
            {
                booking.Status = BookingStatus.Confirmed;
            }

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var responseBooking = new Booking
            {
                Id = booking.Id,
                AppUserId = booking.AppUserId,
                ResourceType = booking.ResourceType,
                ResourceId = booking.ResourceId,
                StartUtc = booking.StartUtc,
                EndUtc = booking.EndUtc,
                Status = booking.Status,
                AppUser = null
            };

            return Results.Created($"/api/bookings/{booking.Id}", responseBooking);
        });

        app.MapDelete("/api/bookings/{id}", [Authorize] async (WorklyDbContext db, HttpContext ctx, int id) =>
        {
            var booking = await db.Bookings.FindAsync(id);
            if (booking is null)
            {
                return Results.NotFound();
            }

            if (!ctx.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
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

    // ---- seed enrichi pour la démo ----
    private static async Task SeedAsync(WorklyDbContext db)
    {
        // Utilisateurs de test correspondant aux comptes Keycloak
        // admin / admin123 (rôle: admin)
        // user / user123 (rôle: user)
        if (!await db.AppUsers.AnyAsync())
        {
            var users = new[]
            {
                new AppUser { DisplayName = "Admin User", Email = "admin@workly.test" },
                new AppUser { DisplayName = "Regular User", Email = "user@workly.test" }
            };
            db.AppUsers.AddRange(users);
            await db.SaveChangesAsync();
        }

        // Un seul workspace
        if (!await db.Workspaces.AnyAsync())
        {
            db.Workspaces.Add(new Workspace { Name = "HQ Paris", City = "Paris" });
            await db.SaveChangesAsync();
        }

        // Salles avec noms de grandes villes mondiales
        // Note: On vérifie individuellement pour permettre l'ajout de nouvelles salles
        var existingRoomNames = await db.Rooms.Select(r => r.Name).ToListAsync();
        var roomsToAdd = new[]
        {
            new { Name = "Paris", Location = "Étage 2, Aile Est", Capacity = 8 },
            new { Name = "New York", Location = "Étage 2, Aile Ouest", Capacity = 10 },
            new { Name = "Tokyo", Location = "Étage 3, Centre", Capacity = 12 },
            new { Name = "Londres", Location = "Étage 3, Aile Est", Capacity = 6 },
            new { Name = "Berlin", Location = "Étage 3, Aile Ouest", Capacity = 8 },
            new { Name = "Sydney", Location = "Étage 4, Centre", Capacity = 10 },
            new { Name = "Dubai", Location = "Étage 4, Aile Est", Capacity = 6 },
            new { Name = "Singapour", Location = "Étage 4, Aile Ouest", Capacity = 8 },
            new { Name = "Shanghai", Location = "Étage 5, Centre", Capacity = 12 },
            new { Name = "Los Angeles", Location = "Étage 5, Aile Est", Capacity = 10 }
        };

        var workspaceId = (await db.Workspaces.FirstOrDefaultAsync())?.Id ?? 1;
        
        foreach (var roomData in roomsToAdd)
        {
            if (!existingRoomNames.Contains(roomData.Name))
            {
                db.Rooms.Add(new Room 
                { 
                    WorkspaceId = workspaceId, 
                    Name = roomData.Name, 
                    Location = roomData.Location, 
                    Capacity = roomData.Capacity 
                });
            }
        }
        
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }

        // Réservations de démo (toutes confirmées automatiquement)
        if (!await db.Bookings.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            
            var bookings = new[]
            {
                // Réservations d'aujourd'hui
                new Booking 
                { 
                    AppUserId = 1, 
                    ResourceType = ResourceType.Room, 
                    ResourceId = 1, 
                    StartUtc = today.AddHours(9), 
                    EndUtc = today.AddHours(11),
                    Status = BookingStatus.Confirmed 
                },
                new Booking 
                { 
                    AppUserId = 1, 
                    ResourceType = ResourceType.Room, 
                    ResourceId = 3, 
                    StartUtc = today.AddHours(14), 
                    EndUtc = today.AddHours(16),
                    Status = BookingStatus.Confirmed 
                },
                
                // Réservations de demain
                new Booking 
                { 
                    AppUserId = 1, 
                    ResourceType = ResourceType.Room, 
                    ResourceId = 2, 
                    StartUtc = today.AddDays(1).AddHours(10), 
                    EndUtc = today.AddDays(1).AddHours(12),
                    Status = BookingStatus.Confirmed 
                },
                new Booking 
                { 
                    AppUserId = 1, 
                    ResourceType = ResourceType.Room, 
                    ResourceId = 5, 
                    StartUtc = today.AddDays(1).AddHours(15), 
                    EndUtc = today.AddDays(1).AddHours(17),
                    Status = BookingStatus.Confirmed 
                },
                
                // Réservations après-demain
                new Booking 
                { 
                    AppUserId = 1, 
                    ResourceType = ResourceType.Room, 
                    ResourceId = 6, 
                    StartUtc = today.AddDays(2).AddHours(9), 
                    EndUtc = today.AddDays(2).AddHours(11),
                    Status = BookingStatus.Confirmed 
                }
            };
            db.Bookings.AddRange(bookings);
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
            // Fallback pour les environnements de test où aucun claim n'est présent
            identifier = "test";
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