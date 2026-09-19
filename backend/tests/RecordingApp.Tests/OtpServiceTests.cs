using Microsoft.Extensions.Options;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Security;
using Xunit;

namespace RecordingApp.Tests;

public class OtpServiceTests
{
    private static OtpService CreateService(RecordingAppDbContext db, OtpOptions? options = null) =>
        new(db, Options.Create(options ?? new OtpOptions { CodeLength = 6, ExpiryMinutes = 5, MaxAttempts = 5 }));

    [Fact]
    public async Task VerifyAsync_WithCorrectCode_Succeeds()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "kid-collector@example.com", Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var code = await service.IssueAsync(user.Id, OtpChannel.Email);

        var result = await service.VerifyAsync(user.Id, code);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WithWrongCode_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "a@example.com", Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.IssueAsync(user.Id, OtpChannel.Email);

        var result = await service.VerifyAsync(user.Id, "000000");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_SameCodeTwice_SecondAttemptFails()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "b@example.com", Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var code = await service.IssueAsync(user.Id, OtpChannel.Email);

        Assert.True(await service.VerifyAsync(user.Id, code));
        Assert.False(await service.VerifyAsync(user.Id, code));
    }

    [Fact]
    public async Task VerifyAsync_AfterExpiry_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "c@example.com", Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db, new OtpOptions { CodeLength = 6, ExpiryMinutes = 0, MaxAttempts = 5 });
        var code = await service.IssueAsync(user.Id, OtpChannel.Email);

        // ExpiryMinutes = 0 means the code expires immediately.
        await Task.Delay(10);
        var result = await service.VerifyAsync(user.Id, code);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_ExceedsMaxAttempts_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "d@example.com", Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db, new OtpOptions { CodeLength = 6, ExpiryMinutes = 5, MaxAttempts = 2 });
        var code = await service.IssueAsync(user.Id, OtpChannel.Email);

        Assert.False(await service.VerifyAsync(user.Id, "wrong1"));
        Assert.False(await service.VerifyAsync(user.Id, "wrong2"));
        // Third attempt is beyond MaxAttempts even with the correct code.
        Assert.False(await service.VerifyAsync(user.Id, code));
    }
}
