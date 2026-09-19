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
[Authorize]
public class RecordingsController(
    RecordingAppDbContext db,
    IStorageService storage,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("photos/next")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<PhotoDto>> NextPhoto([FromQuery] Guid sessionId, CancellationToken ct)
    {
        var session = await db.RecordingSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.CreatedByUserId == currentUser.Id, ct);
        if (session is null) return NotFound();

        var usedPhotoIds = await db.Recordings
            .Where(r => r.SessionId == sessionId && r.IsConfirmed)
            .Select(r => r.PhotoId)
            .ToListAsync(ct);

        var unused = await db.Photos.Where(p => p.Active && !usedPhotoIds.Contains(p.Id)).ToListAsync(ct);
        // If every photo has already been used in this session, allow repeats.
        var pool = unused.Count > 0 ? unused : await db.Photos.Where(p => p.Active).ToListAsync(ct);

        if (pool.Count == 0) return NotFound("No photos are available. Ask an admin to sync the photo bucket.");
        var candidate = pool[Random.Shared.Next(pool.Count)];

        return Ok(new PhotoDto(candidate.Id, storage.GetPhotoReadUrl(candidate.S3Key)));
    }

    [HttpGet("sessions/{sessionId:guid}/recordings")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<List<RecordingDto>>> ListForSession(Guid sessionId, CancellationToken ct)
    {
        var session = await db.RecordingSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.CreatedByUserId == currentUser.Id, ct);
        if (session is null) return NotFound();

        var recordings = await db.Recordings
            .Where(r => r.SessionId == sessionId && r.IsConfirmed)
            .Include(r => r.Photo)
            .Include(r => r.Annotation)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(recordings.Select(ToDto));
    }

    [HttpGet("recordings/mine")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<List<RecordingDto>>> ListMine(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = ApplyDateRange(
            db.Recordings.Where(r => r.CreatedByUserId == currentUser.Id && r.IsConfirmed),
            from, to)
            .Include(r => r.Photo)
            .Include(r => r.Annotation);

        var recordings = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return Ok(recordings.Select(ToDto));
    }

    [HttpGet("recordings/stats/mine")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<CollectorStatsDto>> MyStats(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var mine = ApplyDateRange(db.Recordings.Where(r => r.CreatedByUserId == currentUser.Id), from, to);

        var confirmed = await mine.CountAsync(r => r.IsConfirmed, ct);
        var unconfirmed = await mine.CountAsync(r => !r.IsConfirmed, ct);
        var speakersRecorded = await mine
            .Where(r => r.IsConfirmed)
            .Select(r => r.SpeakerId)
            .Distinct()
            .CountAsync(ct);

        return Ok(new CollectorStatsDto(speakersRecorded, confirmed, unconfirmed));
    }

    private static IQueryable<Recording> ApplyDateRange(IQueryable<Recording> query, DateOnly? from, DateOnly? to)
    {
        if (from is not null)
        {
            var fromUtc = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(r => r.CreatedAt >= fromUtc);
        }
        if (to is not null)
        {
            var toUtc = new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(r => r.CreatedAt <= toUtc);
        }
        return query;
    }

    [HttpPost("sessions/{sessionId:guid}/recordings/upload-url")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<UploadUrlDto>> CreateUploadUrl(
        Guid sessionId, [FromBody] CreateUploadUrlDto dto, CancellationToken ct)
    {
        var session = await db.RecordingSessions.Include(s => s.Speaker).FirstOrDefaultAsync(
            s => s.Id == sessionId && s.CreatedByUserId == currentUser.Id, ct);
        if (session is null) return NotFound();

        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == dto.PhotoId, ct);
        if (photo is null) return NotFound("Photo not found.");

        var recording = new Recording
        {
            SessionId = sessionId,
            SpeakerId = session.SpeakerId,
            PhotoId = photo.Id,
            CreatedByUserId = currentUser.Id,
            IsConfirmed = false,
        };
        recording.S3Key = storage.BuildAudioKey(session.SpeakerId, sessionId, recording.Id, session.Speaker!.Gender.ToString());
        db.Recordings.Add(recording);
        await db.SaveChangesAsync(ct);

        var uploadUrl = storage.GetAudioUploadUrl(recording.S3Key, "audio/webm");
        return Ok(new UploadUrlDto(recording.Id, uploadUrl, recording.S3Key));
    }

    [HttpPost("recordings/{recordingId:guid}/confirm")]
    [Authorize(Roles = "Collector")]
    public async Task<ActionResult<RecordingDto>> Confirm(
        Guid recordingId, ConfirmRecordingDto dto, CancellationToken ct)
    {
        var recording = await db.Recordings
            .Include(r => r.Photo)
            .FirstOrDefaultAsync(r => r.Id == recordingId && r.CreatedByUserId == currentUser.Id, ct);
        if (recording is null) return NotFound();

        recording.S3Key = dto.S3Key;
        recording.DurationSeconds = dto.DurationSeconds;
        recording.IsConfirmed = true;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(recording));
    }

    [HttpGet("recordings")]
    [Authorize(Roles = "Annotator,Admin")]
    public async Task<ActionResult<List<RecordingDto>>> ListAll([FromQuery] bool? annotated, CancellationToken ct)
    {
        var query = db.Recordings.Where(r => r.IsConfirmed).Include(r => r.Photo).Include(r => r.Annotation).AsQueryable();
        if (annotated is true) query = query.Where(r => r.Annotation != null);
        if (annotated is false) query = query.Where(r => r.Annotation == null);

        var recordings = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return Ok(recordings.Select(ToDto));
    }

    private RecordingDto ToDto(Recording r) => new(
        r.Id,
        r.SessionId,
        r.SpeakerId,
        r.PhotoId,
        storage.GetPhotoReadUrl(r.Photo!.S3Key),
        storage.GetAudioReadUrl(r.S3Key),
        r.DurationSeconds,
        r.CreatedByUserId,
        r.CreatedAt,
        r.Annotation != null);
}

public record CreateUploadUrlDto(Guid PhotoId);
