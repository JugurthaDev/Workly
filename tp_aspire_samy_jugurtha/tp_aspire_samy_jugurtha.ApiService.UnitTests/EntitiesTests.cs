using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using tp_aspire_samy_jugurtha.ApiService.Data;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;
using Xunit;

namespace tp_aspire_samy_jugurtha.ApiService.UnitTests;

public class EntitiesTests
{
    [Fact]
    public void Booking_Default_Status_Pending()
    {
        var b = new Booking();
        b.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task Booking_CheckConstraint_StartBeforeEnd_enforced()
    {
        using var con = new SqliteConnection("DataSource=:memory:");
        con.Open();
        var options = new DbContextOptionsBuilder<WorklyDbContext>().UseSqlite(con).Options;
        using var db = new WorklyDbContext(options);
        db.Database.EnsureCreated();

        db.AppUsers.Add(new AppUser{ Id = 1, Email = "u@test", DisplayName = "U" });
        await db.SaveChangesAsync();

        var invalid = new Booking
        {
            AppUserId = 1,
            ResourceType = ResourceType.Room,
            ResourceId = 1,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddMinutes(-30),
            Status = BookingStatus.Pending
        };
        db.Bookings.Add(invalid);
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
