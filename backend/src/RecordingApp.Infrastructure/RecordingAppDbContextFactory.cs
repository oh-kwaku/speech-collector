using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecordingApp.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations` build the DbContext directly, without spinning up the
/// full API host (which would otherwise call Database.Migrate() against a live DB).
/// </summary>
public class RecordingAppDbContextFactory : IDesignTimeDbContextFactory<RecordingAppDbContext>
{
    public RecordingAppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES")
            ?? "Host=localhost;Port=5432;Database=recordingapp;Username=recordingapp;Password=changeme";

        var options = new DbContextOptionsBuilder<RecordingAppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RecordingAppDbContext(options);
    }
}
