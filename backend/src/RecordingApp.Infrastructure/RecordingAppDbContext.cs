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
            // IRR: recordings that existed before this column was added, and
            // any inserted without setting it explicitly, are single-annotator
            // (0 would wrongly mean "needs no annotator" and drop them out of
            // every queue immediately).
            e.Property(r => r.RequiredAnnotatorCount).HasDefaultValue(1);
            // Which of this recording's annotations is canonical for export.
            // No inverse nav (an Annotation doesn't need to know it's canonical),
            // and SetNull so deleting an annotation never blocks on this FK.
            e.HasOne(r => r.CanonicalAnnotation).WithMany()
                .HasForeignKey(r => r.CanonicalAnnotationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Annotation>(e =>
        {
            // IRR: many annotations per recording, but at most one per
            // (recording, annotator) pair - a given annotator can't annotate
            // the same recording twice.
            e.HasOne(a => a.Recording).WithMany(r => r.Annotations).HasForeignKey(a => a.RecordingId);
            e.HasIndex(a => new { a.RecordingId, a.AnnotatorUserId }).IsUnique();
        });
    }
}
