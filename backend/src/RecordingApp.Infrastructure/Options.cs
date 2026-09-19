namespace RecordingApp.Infrastructure;

public class JwtOptions
{
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "RecordingApp";
    public string Audience { get; set; } = "RecordingApp";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class S3Options
{
    /// <summary>AWS region name (e.g. "us-east-1"). Ignored when <see cref="ServiceUrl"/> is set.</summary>
    public string? Region { get; set; }

    /// <summary>
    /// Endpoint of an S3-compatible provider (MinIO, DigitalOcean Spaces, Cloudflare R2,
    /// Backblaze B2, Wasabi, ...). Leave unset to talk to AWS S3 via <see cref="Region"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Use path-style bucket addressing (required by most non-AWS S3-compatible providers).</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Explicit credentials. Leave unset to fall back to the default AWS credential chain
    /// (env vars, shared config, container/instance role) — mainly useful for AWS deployments.
    /// </summary>
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }

    public string PhotosBucket { get; set; } = default!;
    public string AudioBucket { get; set; } = default!;
    public string? PhotosPrefix { get; set; }
    public string? AudioPrefix { get; set; }
    public int PresignedUrlMinutes { get; set; } = 15;
}

public class SmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = default!;
    public string AppPassword { get; set; } = default!;
    public string FromAddress { get; set; } = default!;
    public string FromName { get; set; } = "Speech Collector";
}

public class OtpOptions
{
    public int CodeLength { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
}
