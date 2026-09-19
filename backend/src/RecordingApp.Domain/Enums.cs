namespace RecordingApp.Domain;

public enum UserRole
{
    Admin,
    Collector,
    Annotator,
}

public enum UserStatus
{
    Invited,
    Active,
    Disabled,
}

public enum OtpChannel
{
    Email,
    Sms,
}

public enum SpeakerGender
{
    Male,
    Female,
    Other,
}

public enum SessionStatus
{
    Active,
    Completed,
}
