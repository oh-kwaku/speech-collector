namespace RecordingApp.Infrastructure.Storage;

public interface IStorageService
{
    /// <summary>Lists every object key under the photos bucket/prefix.</summary>
    Task<IReadOnlyList<string>> ListPhotoKeysAsync(CancellationToken ct = default);

    string GetPhotoReadUrl(string s3Key);

    /// <summary>
    /// Pre-signed PUT URL the browser uploads audio bytes to directly. Content-Type is
    /// part of what a presigned URL signs, so the caller's upload request must send the
    /// exact same Content-Type header or S3/MinIO rejects it with 403 SignatureDoesNotMatch.
    /// </summary>
    string GetAudioUploadUrl(string s3Key, string contentType);

    string GetAudioReadUrl(string s3Key);

    string BuildAudioKey(string speakerId, Guid sessionId, Guid recordingId, string gender);
}
