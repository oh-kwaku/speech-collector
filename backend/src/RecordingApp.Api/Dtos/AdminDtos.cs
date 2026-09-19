using RecordingApp.Domain;

namespace RecordingApp.Api.Dtos;

public record InviteUserDto(
    string? Name, string? Email, string? PhoneNumber, string? Location, List<UserRole> Roles);

public record UpdateUserProfileDto(string? Name, string? PhoneNumber, string? Location, List<UserRole>? Roles);

public record AdminUserDto(
    Guid Id,
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? Location,
    List<UserRole> Roles,
    UserStatus Status,
    DateTimeOffset CreatedAt)
{
    public static AdminUserDto From(User u) => new(
        u.Id, u.Name, u.Email, u.PhoneNumber, u.Location,
        u.Roles.Select(r => r.Role).OrderBy(r => r).ToList(), u.Status, u.CreatedAt);
}

public record PhotoSyncResultDto(int Added, int Total);
