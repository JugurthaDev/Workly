using Microsoft.EntityFrameworkCore;
using tp_aspire_samy_jugurtha.ApiService.Data.Entities;

namespace tp_aspire_samy_jugurtha.ApiService.Data;

public class WorklyDbContext : DbContext
{
    public WorklyDbContext(DbContextOptions<WorklyDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Desk> Desks => Set<Desk>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("T_AppUsers");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        });

        builder.Entity<Workspace>(entity =>
        {
            entity.ToTable("T_Workspaces");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.City).IsRequired().HasMaxLength(100);
        });

        builder.Entity<Room>(entity =>
        {
            entity.ToTable("T_Rooms");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Location).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Capacity).IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithMany(w => w.Rooms)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.WorkspaceId, x.Name }).IsUnique();
        });

        builder.Entity<Desk>(entity =>
        {
            entity.ToTable("T_Desks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);

            entity.HasOne(x => x.Workspace)
                .WithMany(w => w.Desks)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.WorkspaceId, x.Code }).IsUnique();
        });

        builder.Entity<Booking>(entity =>
        {
            entity.ToTable("T_Bookings", table =>
            {
                table.HasCheckConstraint("CK_Bookings_StartBeforeEnd", "\"StartUtc\" < \"EndUtc\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceType).IsRequired();
            entity.Property(x => x.StartUtc).IsRequired();
            entity.Property(x => x.EndUtc).IsRequired();

            entity.HasOne(x => x.AppUser)
                .WithMany(u => u.Bookings)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.ResourceType, x.ResourceId, x.StartUtc, x.EndUtc, x.Status });
        });

        base.OnModelCreating(builder);
    }
}
