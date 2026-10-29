namespace RecordingApp.Infrastructure.Security;

// Canonical forms for the email/phone identifiers used for OTP login, applied
// when users are stored and when they're looked up.
public static class Identifiers
{
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var normalized = new string(phone.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')')).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }
}
