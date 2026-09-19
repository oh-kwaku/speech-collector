namespace RecordingApp.Api.Dtos;

public record CreateAnnotationDto(string Text);
public record UpdateAnnotationDto(string Text);

public record AnnotationDto(
    Guid Id,
    Guid RecordingId,
    Guid AnnotatorUserId,
    string? AnnotatorEmail,
    string? AnnotatorPhoneNumber,
    Guid CollectorUserId,
    string? CollectorEmail,
    string? CollectorPhoneNumber,
    string Text,
    string AudioUrl,
    string PhotoUrl,
    string SpeakerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
