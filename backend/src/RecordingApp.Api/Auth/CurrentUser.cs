using System.Security.Claims;

namespace RecordingApp.Api.Auth;

public interface ICurrentUser
{
    Guid Id { get; }
    IReadOnlyList<string> Roles { get; }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id => Guid.Parse(accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext!.User.FindFirstValue("sub")!);

    public IReadOnlyList<string> Roles =>
        accessor.HttpContext!.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
}
