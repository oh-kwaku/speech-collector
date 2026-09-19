using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace RecordingApp.Infrastructure.Storage;

public class S3StorageService(IAmazonS3 s3Client, IOptions<S3Options> options) : IStorageService
{
    private readonly S3Options _options = options.Value;

    public async Task<IReadOnlyList<string>> ListPhotoKeysAsync(CancellationToken ct = default)
    {
        var keys = new List<string>();
        string? continuationToken = null;
        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.PhotosBucket,
                Prefix = _options.PhotosPrefix,
                ContinuationToken = continuationToken,
            }, ct);

            keys.AddRange(response.S3Objects
                .Where(o => !o.Key.EndsWith('/') && o.Size > 0)
                .Select(o => o.Key));

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken != null);

        return keys;
    }

    public string GetPhotoReadUrl(string s3Key) => Presign(_options.PhotosBucket, s3Key, HttpVerb.GET, null);

    public string GetAudioUploadUrl(string s3Key, string contentType) =>
        Presign(_options.AudioBucket, s3Key, HttpVerb.PUT, contentType);

    public string GetAudioReadUrl(string s3Key) => Presign(_options.AudioBucket, s3Key, HttpVerb.GET, null);

    public string BuildAudioKey(string speakerId, Guid sessionId, Guid recordingId, string gender)
    {
        var prefix = string.IsNullOrEmpty(_options.AudioPrefix) ? "" : $"{_options.AudioPrefix!.Trim('/')}/";
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{speakerId}_{recordingId}_data_{gender}_{timestamp}.webm";
        return $"{prefix}audio/{speakerId}/{sessionId}/{fileName}";
    }

    private string Presign(string bucket, string key, HttpVerb verb, string? contentType)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = verb,
            Expires = DateTime.UtcNow.AddMinutes(_options.PresignedUrlMinutes),
        };
        if (contentType != null) request.ContentType = contentType;
        var url = s3Client.GetPreSignedURL(request);

        // GetPreSignedURL always emits an https:// URL, even against a plain-http
        // custom ServiceURL (e.g. a local MinIO with no TLS cert). The scheme isn't
        // part of what's signed, so it's safe to correct it after the fact.
        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl) &&
            _options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url["https://".Length..];
        }

        return url;
    }
}
