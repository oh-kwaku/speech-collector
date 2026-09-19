using RecordingApp.Domain;

namespace RecordingApp.Api.Dtos;

public record CreateSpeakerDto(SpeakerGender Gender, int AgeYears);

public record SpeakerDto(string Id, SpeakerGender Gender, int AgeYears, DateTimeOffset CreatedAt)
{
    public static SpeakerDto From(Speaker s) => new(s.Id, s.Gender, s.AgeYears, s.CreatedAt);
}

public record SessionDto(Guid Id, string SpeakerId, SessionStatus Status, DateTimeOffset StartedAt, int RecordingCount)
{
    public static SessionDto From(RecordingSession s, int count) =>
        new(s.Id, s.SpeakerId, s.Status, s.StartedAt, count);
}

public record PhotoDto(Guid Id, string Url);

public record UploadUrlDto(Guid RecordingId, string UploadUrl, string S3Key);
public record ConfirmRecordingDto(string S3Key, int DurationSeconds);

public record RecordingDto(
    Guid Id,
    Guid SessionId,
    string SpeakerId,
    Guid PhotoId,
    string PhotoUrl,
    string AudioUrl,
    int DurationSeconds,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool IsAnnotated);
