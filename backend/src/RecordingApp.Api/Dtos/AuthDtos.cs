using RecordingApp.Domain;

namespace RecordingApp.Api.Dtos;

public record OtpRequestDto(string Identifier, OtpChannel Channel);
public record OtpVerifyDto(string Identifier, OtpChannel Channel, string Code);
public record AcceptInviteDto(string Token, string Code);
public record RefreshDto(string RefreshToken);
public record LogoutDto(string RefreshToken);

public record UserDto(Guid Id, string? Email, string? PhoneNumber, List<UserRole> Roles)
{
    public static UserDto From(User u) =>
        new(u.Id, u.Email, u.PhoneNumber, u.Roles.Select(r => r.Role).OrderBy(r => r).ToList());
}

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User);
