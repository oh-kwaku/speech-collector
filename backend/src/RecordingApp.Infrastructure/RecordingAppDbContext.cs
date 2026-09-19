using Microsoft.EntityFrameworkCore;
using RecordingApp.Domain;

namespace RecordingApp.Infrastructure;

public class RecordingAppDbContext(DbContextOptions<RecordingAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Speaker> Speakers => Set<Speaker>();
    public DbSet<RecordingSession> RecordingSessions => Set<RecordingSession>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<Annotation> Annotations => Set<Annotation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
            e.Property(u => u.Status).HasConversion<string>();
        });

        modelBuilder.Entity<UserRoleAssignment>(e =>
        {
            e.HasKey(r => new { r.UserId, r.Role });
            e.Property(r => r.Role).HasConversion<string>();
            e.HasOne(r => r.User).WithMany(u => u.Roles).HasForeignKey(r => r.UserId);
        });

        modelBuilder.Entity<OtpCode>(e =>
        {
            e.Property(o => o.Channel).HasConversion<string>();
            e.HasIndex(o => o.UserId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId);
            e.HasIndex(r => r.TokenHash).IsUnique();
        });

        modelBuilder.Entity<Speaker>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Gender).HasConversion<string>();
        });

        modelBuilder.Entity<RecordingSession>(e =>
        {
            e.HasOne(s => s.Speaker).WithMany(sp => sp.Sessions).HasForeignKey(s => s.SpeakerId);
            e.Property(s => s.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Photo>(e =>
        {
            e.HasIndex(p => p.S3Key).IsUnique();
        });

        modelBuilder.Entity<Recording>(e =>
        {
            e.HasOne(r => r.Session).WithMany(s => s.Recordings).HasForeignKey(r => r.SessionId);
            e.HasOne(r => r.Photo).WithMany().HasForeignKey(r => r.PhotoId);
            e.HasIndex(r => r.S3Key).IsUnique();
        });

        modelBuilder.Entity<Annotation>(e =>
        {
            e.HasOne(a => a.Recording).WithOne(r => r.Annotation).HasForeignKey<Annotation>(a => a.RecordingId);
            e.HasIndex(a => a.RecordingId).IsUnique();
        });
    }
}
