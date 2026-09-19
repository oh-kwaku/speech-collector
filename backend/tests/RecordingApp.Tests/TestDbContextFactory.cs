using Microsoft.EntityFrameworkCore;
using RecordingApp.Infrastructure;

namespace RecordingApp.Tests;

public static class TestDbContextFactory
{
    public static RecordingAppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<RecordingAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RecordingAppDbContext(options);
    }
}
