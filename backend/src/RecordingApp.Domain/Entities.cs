namespace RecordingApp.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Invited;
    public DateTimeOffset InvitedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<UserRoleAssignment> Roles { get; set; } = [];
}

// A user can hold several roles at once (e.g. Collector + Annotator) except
// Admin, which is always exclusive - enforced where roles are assigned, not here.
public class UserRoleAssignment
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public UserRole Role { get; set; }
}

public class Invite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    // Comma-separated UserRole names, audit-only (not queried/parsed back).
    public string? Roles { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = default!;
    public OtpChannel Channel { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Speaker
{
    // e.g. "SPK-4F2A9C" - deliberately contains no PII.
    public string Id { get; set; } = default!;
    public SpeakerGender Gender { get; set; }
    public int AgeYears { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<RecordingSession> Sessions { get; set; } = [];
}

public class RecordingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SpeakerId { get; set; } = default!;
    public Speaker? Speaker { get; set; }
    public Guid CreatedByUserId { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Recording> Recordings { get; set; } = [];
}

public class Photo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string S3Key { get; set; } = default!;
    public string? Label { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Recording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public RecordingSession? Session { get; set; }
    public string SpeakerId { get; set; } = default!;
    public Guid PhotoId { get; set; }
    public Photo? Photo { get; set; }
    public string S3Key { get; set; } = default!;
    public int DurationSeconds { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // IRR: a recording can be independently annotated by several annotators
    // (each annotator at most once - enforced by a unique (RecordingId,
    // AnnotatorUserId) index), not just one. See AnnotationsController's
    // queue/target-count logic for how many are collected per recording.
    public List<Annotation> Annotations { get; set; } = [];

    // How many independent annotators this recording needs before it's fully
    // annotated. Decided once, at confirm time: normally 1, but a random
    // sample (Irr:SampleRatePercent) is raised to Irr:TargetAnnotatorsPerRecording
    // so only a configurable fraction of recordings pay the double-annotation
    // cost, rather than every recording being annotated twice.
    public int RequiredAnnotatorCount { get; set; } = 1;

    // Which of this recording's (possibly several) independent Annotations is
    // used for the training-data export. Defaults to the first one submitted;
    // an Admin can repoint it after reviewing rater disagreement in the IRR
    // report (see AnnotationsController.SetCanonical).
    public Guid? CanonicalAnnotationId { get; set; }
    public Annotation? CanonicalAnnotation { get; set; }
}

public class Annotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordingId { get; set; }
    public Recording? Recording { get; set; }
    public Guid AnnotatorUserId { get; set; }
    public string Text { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
