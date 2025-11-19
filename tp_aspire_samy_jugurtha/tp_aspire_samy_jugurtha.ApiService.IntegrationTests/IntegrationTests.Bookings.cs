using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tp_aspire_samy_jugurtha.ApiService.Data;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;
using Xunit;

namespace tp_aspire_samy_jugurtha.ApiService.IntegrationTests;

public class IntegrationTests_Bookings : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IntegrationTests_Bookings(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorklyDbContext>();
        if (!await db.Workspaces.AnyAsync())
        {
            db.Workspaces.Add(new Workspace { Id = 1, Name = "HQ Paris", City = "Paris" });
            await db.SaveChangesAsync();
        }
        if (!await db.Rooms.AnyAsync())
        {
            db.Rooms.Add(new Room { Id = 1, WorkspaceId = 1, Name = "Salle Volt", Capacity = 6 });
            await db.SaveChangesAsync();
        }
        if (!await db.AppUsers.AnyAsync())
        {
            db.AppUsers.Add(new AppUser { Id = 42, Email = "test@workly", DisplayName = "Test User" });
            await db.SaveChangesAsync();
        }
    }

    private HttpClient CreateClient(string? roles = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User", "test");
        if (!string.IsNullOrWhiteSpace(roles))
        {
            client.DefaultRequestHeaders.Add("X-Roles", roles);
        }
        return client;
    }

    [Fact]
    public async Task Get_Rooms_returns_seeded_room()
    {
        var client = CreateClient();
        var rooms = await client.GetFromJsonAsync<List<Room>>("/api/rooms");
        rooms.Should().NotBeNull();
        rooms!.Should().ContainSingle(r => r.Name == "Salle Volt");
    }

    [Fact]
    public async Task Post_Bookings_creates_when_no_overlap()
    {
        var client = CreateClient();
        var now = DateTime.UtcNow; 
        var b = new Booking
        {
            AppUserId = 42,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = now.AddHours(1),
            EndUtc = now.AddHours(2),
            Status = BookingStatus.Pending
        };

        var resp = await client.PostAsJsonAsync("/api/bookings", b);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<Booking>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Post_Bookings_conflict_on_overlap()
    {
        var client = CreateClient();
        var baseStart = DateTime.UtcNow.AddHours(3);
        var b1 = new Booking
        {
            AppUserId = 42,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = baseStart,
            EndUtc = baseStart.AddHours(1)
        };
        var r1 = await client.PostAsJsonAsync("/api/bookings", b1);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        var b2 = new Booking
        {
            AppUserId = 42,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = baseStart.AddMinutes(30), // overlap
            EndUtc = baseStart.AddHours(2)
        };
        var r2 = await client.PostAsJsonAsync("/api/bookings", b2);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_Bookings_all_requires_admin()
    {
        var client = CreateClient();
        var r1 = await client.GetAsync("/api/bookings/all");
        r1.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = CreateClient("admin");
        var r2 = await admin.GetAsync("/api/bookings/all");
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Booking_admin_only()
    {
        var client = CreateClient("admin");
        var now = DateTime.UtcNow;
        var b = new Booking
        {
            AppUserId = 42,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = now.AddHours(5),
            EndUtc = now.AddHours(6)
        };
        var create = await client.PostAsJsonAsync("/api/bookings", b);
        var created = await create.Content.ReadFromJsonAsync<Booking>();
        created.Should().NotBeNull();

        var del = await client.DeleteAsync($"/api/bookings/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var nonAdmin = CreateClient();
        var create2 = await nonAdmin.PostAsJsonAsync("/api/bookings", new Booking
        {
            AppUserId = 42,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = now.AddHours(7),
            EndUtc = now.AddHours(8)
        });
        var created2 = await create2.Content.ReadFromJsonAsync<Booking>();
        var del2 = await nonAdmin.DeleteAsync($"/api/bookings/{created2!.Id}");
        del2.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
