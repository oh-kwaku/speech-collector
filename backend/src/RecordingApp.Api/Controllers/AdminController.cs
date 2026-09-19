using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Dtos;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Export;
using RecordingApp.Infrastructure.Notifications;
using RecordingApp.Infrastructure.Security;

namespace RecordingApp.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    RecordingAppDbContext db,
    IOtpService otp,
    IEmailSender emailSender,
    ISmsSender smsSender,
    IExportService export,
    IPhotoSyncService photoSync,
    IConfiguration config,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> ListUsers(CancellationToken ct)
    {
        var users = await db.Users.Include(u => u.Roles).OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        return Ok(users.Select(AdminUserDto.From));
    }

    [HttpPost("invites")]
    public async Task<ActionResult<AdminUserDto>> Invite(InviteUserDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) && string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest("An email or phone number is required.");

        var rolesError = ValidateRoles(dto.Roles, out var roles);
        if (rolesError is not null) return BadRequest(rolesError);

        var email = dto.Email?.Trim();
        var phone = dto.PhoneNumber?.Trim();

        var exists = await db.Users.AnyAsync(
            u => (email != null && u.Email == email) || (phone != null && u.PhoneNumber == phone), ct);
        if (exists) return Conflict("A user with that email or phone already exists.");

        var user = new User
        {
            Name = dto.Name?.Trim(),
            Email = email,
            PhoneNumber = phone,
            Location = dto.Location?.Trim(),
            Status = UserStatus.Invited,
        };
        user.Roles = roles.Select(r => new UserRoleAssignment { UserId = user.Id, Role = r }).ToList();
        db.Users.Add(user);

        var token = CryptoHelpers.GenerateUrlSafeToken();
        var invite = new Invite
        {
            Email = email,
            PhoneNumber = phone,
            Roles = string.Join(',', roles),
            TokenHash = CryptoHelpers.Hash(token),
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            InvitedByUserId = currentUser.Id,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);

        var channel = email != null ? OtpChannel.Email : OtpChannel.Sms;
        var code = await otp.IssueAsync(user.Id, channel, ct);
        var webBaseUrl = config["App:WebBaseUrl"] ?? "https://localhost:5173";
        var link = $"{webBaseUrl}/invite?token={Uri.EscapeDataString(token)}";
        var body = $"You've been invited as {string.Join(" & ", roles)}. Open {link} and enter code {code} to activate your account.";

        if (channel == OtpChannel.Email)
            await emailSender.SendAsync(email!, "You're invited", body, ct);
        else
            await smsSender.SendAsync(phone!, body, ct);

        return Ok(AdminUserDto.From(user));
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDto>> UpdateProfile(
        Guid userId, UpdateUserProfileDto dto, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();

        var phone = dto.PhoneNumber?.Trim();
        if (!string.IsNullOrEmpty(phone))
        {
            var phoneTaken = await db.Users.AnyAsync(u => u.Id != userId && u.PhoneNumber == phone, ct);
            if (phoneTaken) return Conflict("A user with that phone number already exists.");
        }

        if (dto.Roles is not null)
        {
            var rolesError = ValidateRoles(dto.Roles, out var roles);
            if (rolesError is not null) return BadRequest(rolesError);

            db.UserRoleAssignments.RemoveRange(user.Roles);
            user.Roles = roles.Select(r => new UserRoleAssignment { UserId = user.Id, Role = r }).ToList();
        }

        user.Name = dto.Name?.Trim();
        user.PhoneNumber = phone;
        user.Location = dto.Location?.Trim();
        await db.SaveChangesAsync(ct);

        return Ok(AdminUserDto.From(user));
    }

    private static string? ValidateRoles(List<UserRole>? input, out List<UserRole> roles)
    {
        roles = (input ?? []).Distinct().ToList();
        if (roles.Count == 0) return "At least one role is required.";
        if (roles.Contains(UserRole.Admin) && roles.Count > 1) return "Admin cannot be combined with other roles.";
        return null;
    }

    [HttpPost("users/{userId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();
        user.Status = UserStatus.Disabled;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("photos/sync")]
    public async Task<ActionResult<PhotoSyncResultDto>> SyncPhotos(CancellationToken ct)
    {
        var result = await photoSync.SyncAsync(ct);
        return Ok(new PhotoSyncResultDto(result.Added, result.Total));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats(CancellationToken ct)
    {
        var confirmed = db.Recordings.Where(r => r.IsConfirmed);

        var totalRecordings = await confirmed.CountAsync(ct);
        var totalAnnotations = await db.Annotations.CountAsync(ct);

        var byGender = await confirmed
            .GroupBy(r => r.Session!.Speaker!.Gender)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var byAge = await confirmed
            .GroupBy(r => r.Session!.Speaker!.AgeYears)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new AdminStatsDto(
            totalRecordings,
            totalAnnotations,
            byGender.ToDictionary(x => x.Key, x => x.Count),
            byAge.ToDictionary(x => x.Key, x => x.Count)));
    }

    [HttpGet("user-stats")]
    public async Task<ActionResult<List<UserWorkStatsDto>>> UserStats(CancellationToken ct)
    {
        var users = await db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync(ct);

        var recordingCounts = await db.Recordings
            .Where(r => r.IsConfirmed)
            .GroupBy(r => r.CreatedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var speakerCounts = await db.Recordings
            .Where(r => r.IsConfirmed)
            .Select(r => new { r.CreatedByUserId, r.SpeakerId })
            .Distinct()
            .GroupBy(r => r.CreatedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var annotationCounts = await db.Annotations
            .GroupBy(a => a.AnnotatorUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var result = users.Select(u => new UserWorkStatsDto(
            u.Id,
            u.Name,
            u.Location,
            recordingCounts.GetValueOrDefault(u.Id),
            speakerCounts.GetValueOrDefault(u.Id),
            annotationCounts.GetValueOrDefault(u.Id)));

        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format, CancellationToken ct)
    {
        if (format == "xlsx")
        {
            var bytes = await export.ExportXlsxAsync(ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "recordings-export.xlsx");
        }
        var csv = await export.ExportCsvAsync(ct);
        return File(csv, "text/csv", "recordings-export.csv");
    }
}
