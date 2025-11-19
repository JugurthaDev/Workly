using Microsoft.EntityFrameworkCore;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;

namespace tp_aspire_samy_jugurtha.ApiService.Data;
using Microsoft.EntityFrameworkCore;

public class WorklyDbContext : DbContext
{
    public WorklyDbContext(DbContextOptions<WorklyDbContext> options) : base(options) {}

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Desk> Desks => Set<Desk>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Tables & clés
        b.Entity<AppUser>(e =>
        {
            e.ToTable("T_AppUsers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        });

        b.Entity<Workspace>(e =>
        {
            e.ToTable("T_Workspaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.City).IsRequired().HasMaxLength(100);
        });

        b.Entity<Room>(e =>
        {
            e.ToTable("T_Rooms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Location).IsRequired().HasMaxLength(200);
            e.Property(x => x.Capacity).IsRequired();

            e.HasOne(x => x.Workspace)
             .WithMany(w => w.Rooms)
             .HasForeignKey(x => x.WorkspaceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.WorkspaceId, x.Name }).IsUnique();
        });

        b.Entity<Desk>(e =>
        {
            e.ToTable("T_Desks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(50);

            e.HasOne(x => x.Workspace)
             .WithMany(w => w.Desks)
             .HasForeignKey(x => x.WorkspaceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.WorkspaceId, x.Code }).IsUnique();
        });

        b.Entity<Booking>(e =>
        {
            e.ToTable("T_Bookings", t =>
            {
                t.HasCheckConstraint("CK_Bookings_StartBeforeEnd", "\"StartUtc\" < \"EndUtc\"");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.ResourceType).IsRequired();
            e.Property(x => x.StartUtc).IsRequired();
            e.Property(x => x.EndUtc).IsRequired();

            e.HasOne(x => x.AppUser)
             .WithMany(u => u.Bookings)
             .HasForeignKey(x => x.AppUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.ResourceType, x.ResourceId, x.StartUtc, x.EndUtc }).IsUnique();
        });

        base.OnModelCreating(b);
    }
}
