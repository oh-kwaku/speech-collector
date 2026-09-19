using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Dtos;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Storage;

namespace RecordingApp.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "Annotator,Admin")]
public class AnnotationsController(
    RecordingAppDbContext db, ICurrentUser currentUser, IStorageService storage) : ControllerBase
{
    [HttpGet("annotations")]
    public async Task<ActionResult<List<AnnotationDto>>> List(CancellationToken ct)
    {
        var annotations = await db.Annotations
            .Include(a => a.Recording).ThenInclude(r => r!.Photo)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(ct);

        var userIds = annotations
            .SelectMany(a => new[] { a.AnnotatorUserId, a.Recording!.CreatedByUserId })
            .Distinct()
            .ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        return Ok(annotations.Select(a => ToDto(a, users)));
    }

    [HttpGet("annotations/stats/mine")]
    public async Task<ActionResult<AnnotatorStatsDto>> MyStats(CancellationToken ct)
    {
        var count = await db.Annotations.CountAsync(a => a.AnnotatorUserId == currentUser.Id, ct);
        return Ok(new AnnotatorStatsDto(count));
    }

    [HttpPost("recordings/{recordingId:guid}/annotations")]
    public async Task<ActionResult<AnnotationDto>> Create(Guid recordingId, CreateAnnotationDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Annotation text is required.");

        var recording = await db.Recordings.Include(r => r.Photo)
            .FirstOrDefaultAsync(r => r.Id == recordingId && r.IsConfirmed, ct);
        if (recording is null) return NotFound();

        var existing = await db.Annotations.AnyAsync(a => a.RecordingId == recordingId, ct);
        if (existing) return Conflict("This recording already has an annotation.");

        var annotation = new Annotation
        {
            RecordingId = recordingId,
            AnnotatorUserId = currentUser.Id,
            Text = dto.Text.Trim(),
        };
        db.Annotations.Add(annotation);
        await db.SaveChangesAsync(ct);

        annotation.Recording = recording;
        var users = await db.Users
            .Where(u => u.Id == currentUser.Id || u.Id == recording.CreatedByUserId)
            .ToDictionaryAsync(u => u.Id, ct);
        return Ok(ToDto(annotation, users));
    }

    [HttpPut("annotations/{annotationId:guid}")]
    public async Task<ActionResult<AnnotationDto>> Update(Guid annotationId, UpdateAnnotationDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Annotation text is required.");

        var annotation = await db.Annotations.Include(a => a.Recording).ThenInclude(r => r!.Photo)
            .FirstOrDefaultAsync(a => a.Id == annotationId, ct);
        if (annotation is null) return NotFound();

        annotation.Text = dto.Text.Trim();
        annotation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var users = await db.Users
            .Where(u => u.Id == annotation.AnnotatorUserId || u.Id == annotation.Recording!.CreatedByUserId)
            .ToDictionaryAsync(u => u.Id, ct);
        return Ok(ToDto(annotation, users));
    }

    private AnnotationDto ToDto(Annotation a, Dictionary<Guid, User> users)
    {
        users.TryGetValue(a.AnnotatorUserId, out var annotator);
        users.TryGetValue(a.Recording!.CreatedByUserId, out var collector);
        return new(
            a.Id,
            a.RecordingId,
            a.AnnotatorUserId,
            annotator?.Email,
            annotator?.PhoneNumber,
            a.Recording.CreatedByUserId,
            collector?.Email,
            collector?.PhoneNumber,
            a.Text,
            storage.GetAudioReadUrl(a.Recording.S3Key),
            storage.GetPhotoReadUrl(a.Recording.Photo!.S3Key),
            a.Recording.SpeakerId,
            a.CreatedAt,
            a.UpdatedAt);
    }
}
