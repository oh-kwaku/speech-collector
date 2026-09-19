using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Security;
using Xunit;

namespace RecordingApp.Tests;

public class TokenServiceTests
{
    private static TokenService CreateService(RecordingAppDbContext db) => new(db, Options.Create(new JwtOptions
    {
        SigningKey = "unit-test-signing-key-that-is-long-enough-1234567890",
        Issuer = "RecordingApp.Tests",
        Audience = "RecordingApp.Tests",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    }));

    [Fact]
    public async Task IssueAsync_EmbedsRoleClaimForAuthorization()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "admin@example.com", Status = UserStatus.Active };
        user.Roles.Add(new UserRoleAssignment { UserId = user.Id, Role = UserRole.Admin });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var issued = await service.IssueAsync(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

        Assert.NotNull(roleClaim);
        Assert.Equal(nameof(UserRole.Admin), roleClaim!.Value);
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesAndReturnsNewTokens()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Email = "collector@example.com", Status = UserStatus.Active };
        user.Roles.Add(new UserRoleAssignment { UserId = user.Id, Role = UserRole.Collector });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var issued = await service.IssueAsync(user);

        var refreshed = await service.RefreshAsync(issued.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(issued.RefreshToken, refreshed!.Value.Tokens.RefreshToken);

        // The original refresh token must now be revoked (single use).
        var reused = await service.RefreshAsync(issued.RefreshToken);
        Assert.Null(reused);
    }
}
