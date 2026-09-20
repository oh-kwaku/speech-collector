using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Dtos;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Notifications;
using RecordingApp.Infrastructure.Security;

namespace RecordingApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    RecordingAppDbContext db,
    IOtpService otp,
    ITokenService tokens,
    IEmailSender emailSender,
    ISmsSender smsSender,
    ILogger<AuthController> logger,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("otp/request")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> RequestOtp(OtpRequestDto dto, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(dto.Identifier, ct);
        // Always return 202 even if not found, to avoid leaking which identifiers are registered.
        if (user is null || user.Status != UserStatus.Active) return Accepted();

        var code = await otp.IssueAsync(user.Id, dto.Channel, ct);
        await SendCodeAsync(user, dto.Channel, code, ct);
        return Accepted();
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting("otp")]
    public async Task<ActionResult<AuthResponseDto>> VerifyOtp(OtpVerifyDto dto, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(dto.Identifier, ct);
        if (user is null || user.Status != UserStatus.Active) return Unauthorized();

        var ok = await otp.VerifyAsync(user.Id, dto.Code, ct);
        if (!ok) return Unauthorized();

        var issued = await tokens.IssueAsync(user, ct);
        return Ok(new AuthResponseDto(issued.AccessToken, issued.RefreshToken, issued.ExpiresAt, UserDto.From(user)));
    }

    [HttpPost("invites/accept")]
    [EnableRateLimiting("otp")]
    public async Task<ActionResult<AuthResponseDto>> AcceptInvite(AcceptInviteDto dto, CancellationToken ct)
    {
        var tokenHash = CryptoHelpers.Hash(dto.Token);
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

        if (invite is null || invite.AcceptedAt != null || invite.ExpiresAt < DateTimeOffset.UtcNow)
            return Unauthorized();

        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == invite.UserId, ct);
        if (user is null) return Unauthorized();

        var ok = await otp.VerifyAsync(user.Id, dto.Code, ct);
        if (!ok) return Unauthorized();

        user.Status = UserStatus.Active;
        user.ActivatedAt = DateTimeOffset.UtcNow;
        invite.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var issued = await tokens.IssueAsync(user, ct);
        return Ok(new AuthResponseDto(issued.AccessToken, issued.RefreshToken, issued.ExpiresAt, UserDto.From(user)));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<object>> Refresh(RefreshDto dto, CancellationToken ct)
    {
        var result = await tokens.RefreshAsync(dto.RefreshToken, ct);
        if (result is null) return Unauthorized();
        return Ok(new { accessToken = result.Value.Tokens.AccessToken, refreshToken = result.Value.Tokens.RefreshToken, expiresAt = result.Value.Tokens.ExpiresAt });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutDto dto, CancellationToken ct)
    {
        await tokens.RevokeAsync(dto.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == currentUser.Id, ct);
        if (user is null) return NotFound();
        return Ok(UserDto.From(user));
    }

    private Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        var normalized = identifier.Trim();
        return db.Users.Include(u => u.Roles).FirstOrDefaultAsync(
            u => u.Email == normalized || u.PhoneNumber == normalized, ct);
    }

    private async Task SendCodeAsync(User user, OtpChannel channel, string code, CancellationToken ct)
    {
        if (channel == OtpChannel.Email && user.Email != null)
        {
            await emailSender.SendAsync(user.Email, "Your sign-in code",
                $"Your verification code is {code}. It expires in a few minutes.", ct);
        }
        else if (channel == OtpChannel.Sms && user.PhoneNumber != null)
        {
            await smsSender.SendAsync(user.PhoneNumber, $"Your verification code is {code}.", ct);
        }
    }
}
