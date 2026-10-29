using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecordingApp.Domain;

namespace RecordingApp.Infrastructure.Security;

public interface IOtpService
{
    // lifetime overrides Otp:ExpiryMinutes, e.g. for invite codes, which must
    // stay valid as long as the invite link they're sent with.
    Task<string> IssueAsync(Guid userId, OtpChannel channel, CancellationToken ct = default, TimeSpan? lifetime = null);
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default);
}

public class OtpService(RecordingAppDbContext db, IOptions<OtpOptions> options) : IOtpService
{
    private readonly OtpOptions _options = options.Value;

    // How many of a user's most recent outstanding codes are accepted. Pressing
    // "Send code" twice (or an SMS/email arriving late) used to invalidate the
    // first code even though that's often the one the user receives and types.
    private const int AcceptedRecentCodes = 3;

    public async Task<string> IssueAsync(Guid userId, OtpChannel channel, CancellationToken ct = default, TimeSpan? lifetime = null)
    {
        var code = CryptoHelpers.GenerateNumericCode(_options.CodeLength);
        db.OtpCodes.Add(new OtpCode
        {
            UserId = userId,
            Channel = channel,
            CodeHash = CryptoHelpers.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(_options.ExpiryMinutes)),
        });
        await db.SaveChangesAsync(ct);
        return code;
    }

    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        // Tolerate pasted codes with spaces/dashes ("123 456").
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return false;

        var now = DateTimeOffset.UtcNow;
        var candidates = await db.OtpCodes
            .Where(o => o.UserId == userId && o.ConsumedAt == null && o.ExpiresAt >= now)
            .OrderByDescending(o => o.CreatedAt)
            .Take(AcceptedRecentCodes)
            .ToListAsync(ct);

        candidates = candidates.Where(o => o.Attempts < _options.MaxAttempts).ToList();
        if (candidates.Count == 0) return false;

        var hash = CryptoHelpers.Hash(digits);
        var isMatch = candidates.Any(o => o.CodeHash == hash);

        if (isMatch)
        {
            // Consume every outstanding code so none can be reused afterwards.
            var outstanding = await db.OtpCodes
                .Where(o => o.UserId == userId && o.ConsumedAt == null)
                .ToListAsync(ct);
            foreach (var o in outstanding) o.ConsumedAt = now;
        }
        else
        {
            foreach (var o in candidates) o.Attempts++;
        }

        await db.SaveChangesAsync(ct);
        return isMatch;
    }
}
