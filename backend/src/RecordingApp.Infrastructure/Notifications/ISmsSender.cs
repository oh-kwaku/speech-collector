using Microsoft.Extensions.Logging;

namespace RecordingApp.Infrastructure.Notifications;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// Stub implementation: logs the SMS to the console instead of sending it via a real
/// carrier. Swap in a Twilio/SNS-backed implementation later without touching callers.
/// </summary>
public class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        logger.LogInformation("[SMS to {PhoneNumber}] {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
