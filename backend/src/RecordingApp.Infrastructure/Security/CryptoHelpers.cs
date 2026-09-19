using System.Security.Cryptography;
using System.Text;

namespace RecordingApp.Infrastructure.Security;

public static class CryptoHelpers
{
    public static string GenerateNumericCode(int length)
    {
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(length);
        foreach (var b in bytes) sb.Append((b % 10).ToString());
        return sb.ToString();
    }

    public static string GenerateUrlSafeToken(int byteLength = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string GenerateSpeakerId()
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        return $"SPK-{suffix}";
    }
}
