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
    DateTimeOffset UpdatedAt,
    // IRR: whether this is the annotation used for the training-data export
    // (defaults to the first one submitted; an Admin can repoint it - see
    // AnnotationsController.SetCanonical).
    bool IsCanonical);

// IRR: an entry in the annotate-next queue. Carries no annotation text (the
// recording may already have one from another annotator) so the current
// annotator stays blind to it while writing their own.
public record RecordingQueueDto(
    Guid RecordingId,
    string SpeakerId,
    string PhotoUrl,
    string AudioUrl,
    DateTimeOffset CreatedAt,
    int AnnotationCount,
    int TargetAnnotatorCount);

public record IrrAnnotationEntryDto(
    Guid AnnotationId,
    Guid AnnotatorUserId,
    string? AnnotatorEmail,
    string? AnnotatorPhoneNumber,
    string Text,
    DateTimeOffset UpdatedAt,
    bool IsCanonical);

public record IrrRecordingDto(
    Guid RecordingId,
    string SpeakerId,
    string PhotoUrl,
    string AudioUrl,
    List<IrrAnnotationEntryDto> Annotations,
    // Average pairwise word-level similarity across all annotators (1.0 =
    // identical, 0.0 = completely different). Report is sorted by this
    // ascending so the recordings needing adjudication most surface first.
    double AgreementScore);

public record SetCanonicalAnnotationDto(Guid AnnotationId);
