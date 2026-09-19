using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecordingApp.Domain;

namespace RecordingApp.Infrastructure.Security;

public interface IOtpService
{
    Task<string> IssueAsync(Guid userId, OtpChannel channel, CancellationToken ct = default);
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default);
}

public class OtpService(RecordingAppDbContext db, IOptions<OtpOptions> options) : IOtpService
{
    private readonly OtpOptions _options = options.Value;

    public async Task<string> IssueAsync(Guid userId, OtpChannel channel, CancellationToken ct = default)
    {
        var code = CryptoHelpers.GenerateNumericCode(_options.CodeLength);
        db.OtpCodes.Add(new OtpCode
        {
            UserId = userId,
            Channel = channel,
            CodeHash = CryptoHelpers.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiryMinutes),
        });
        await db.SaveChangesAsync(ct);
        return code;
    }

    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var candidate = await db.OtpCodes
            .Where(o => o.UserId == userId && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (candidate is null || candidate.ExpiresAt < DateTimeOffset.UtcNow)
            return false;

        if (candidate.Attempts >= _options.MaxAttempts)
            return false;

        candidate.Attempts++;

        var isMatch = candidate.CodeHash == CryptoHelpers.Hash(code);
        if (isMatch)
        {
            candidate.ConsumedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return isMatch;
    }
}
