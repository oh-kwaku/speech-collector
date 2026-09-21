namespace RecordingApp.Api.Dtos;

public record AdminStatsDto(
    int TotalRecordings,
    int TotalAnnotations,
    long TotalAudioDurationSeconds,
    Dictionary<string, int> RecordingsByGender,
    Dictionary<string, int> RecordingsByAge);

public record AnnotatorStatsDto(int TotalAnnotations);

public record CollectorStatsDto(int SpeakersRecorded, int Confirmed, int Unconfirmed);

public record UserWorkStatsDto(
    Guid UserId,
    string? Name,
    string? Location,
    int TotalRecordings,
    int TotalSpeakers,
    int TotalAnnotations);
