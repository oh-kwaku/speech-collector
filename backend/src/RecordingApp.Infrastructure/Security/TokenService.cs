using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RecordingApp.Domain;

namespace RecordingApp.Infrastructure.Security;

public record IssuedTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    Task<IssuedTokens> IssueAsync(User user, CancellationToken ct = default);
    Task<(User User, IssuedTokens Tokens)?> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

public class TokenService(RecordingAppDbContext db, IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<IssuedTokens> IssueAsync(User user, CancellationToken ct = default)
    {
        var accessToken = CreateAccessToken(user);
        var refreshToken = CryptoHelpers.GenerateUrlSafeToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = CryptoHelpers.Hash(refreshToken),
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(ct);

        return new IssuedTokens(accessToken, refreshToken, DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes));
    }

    public async Task<(User User, IssuedTokens Tokens)?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = CryptoHelpers.Hash(refreshToken);
        var stored = await db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (stored is null || stored.RevokedAt != null || stored.ExpiresAt < DateTimeOffset.UtcNow || stored.User is null)
            return null;

        stored.RevokedAt = DateTimeOffset.UtcNow;
        var tokens = await IssueAsync(stored.User, ct);
        return (stored.User, tokens);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = CryptoHelpers.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored != null && stored.RevokedAt == null)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private string CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        };
        foreach (var role in user.Roles.Select(r => r.Role.ToString()).Distinct())
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (user.Email != null) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (user.PhoneNumber != null) claims.Add(new Claim("phone_number", user.PhoneNumber));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
