using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenGate.Data.EFCore.Entities;

namespace OpenGate.Data.EFCore;

/// <summary>
/// Main EF Core DbContext for OpenGate Identity Server.
/// Inherits ASP.NET Core Identity tables and adds OpenGate-specific entities.
/// All OpenGate tables live in the <c>opengate</c> schema.
/// </summary>
public class OpenGateDbContext : IdentityDbContext<OpenGateUser>
{
    public OpenGateDbContext(DbContextOptions<OpenGateDbContext> options)
        : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Move all Identity tables to the opengate schema
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (entity.GetSchema() is null)
            {
                entity.SetSchema("opengate");
            }
        }

        ConfigureUserProfile(builder);
        ConfigureAuditLog(builder);
        ConfigureUserSession(builder);
    }

    private static void ConfigureUserProfile(ModelBuilder builder)
    {
        builder.Entity<UserProfile>(e =>
        {
            e.ToTable("UserProfiles", "opengate");
            e.HasKey(p => p.Id);

            e.Property(p => p.UserId).IsRequired();
            e.Property(p => p.FirstName).HasMaxLength(100);
            e.Property(p => p.LastName).HasMaxLength(100);
            e.Property(p => p.DisplayName).HasMaxLength(200);
            e.Property(p => p.AvatarUrl).HasMaxLength(2048);
            e.Property(p => p.Locale).HasMaxLength(10);
            e.Property(p => p.TimeZone).HasMaxLength(64);

            e.HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => p.UserId).IsUnique();
        });
    }

    private static void ConfigureAuditLog(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "opengate");
            e.HasKey(a => a.Id);

            e.Property(a => a.EventType).IsRequired().HasMaxLength(100);
            e.Property(a => a.ClientId).HasMaxLength(200);
            e.Property(a => a.IpAddress).HasMaxLength(45);   // IPv6 max
            e.Property(a => a.UserAgent).HasMaxLength(512);
            e.Property(a => a.Details);

            e.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Most common query: recent events for a specific user
            e.HasIndex(a => new { a.UserId, a.OccurredAt });
            // Querying by event type across all users (admin views)
            e.HasIndex(a => new { a.EventType, a.OccurredAt });
        });
    }

    private static void ConfigureUserSession(ModelBuilder builder)
    {
        builder.Entity<UserSession>(e =>
        {
            e.ToTable("UserSessions", "opengate");
            e.HasKey(s => s.Id);

            e.Property(s => s.UserId).IsRequired();
            e.Property(s => s.ClientId).HasMaxLength(200);
            e.Property(s => s.IpAddress).HasMaxLength(45);
            e.Property(s => s.UserAgent).HasMaxLength(512);
            e.Property(s => s.DeviceInfo).HasMaxLength(256);

            // IsActive is a computed property — do not map to a column
            e.Ignore(s => s.IsActive);

            e.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Active sessions lookup per user
            e.HasIndex(s => new { s.UserId, s.RevokedAt, s.ExpiresAt });
        });
    }
}
