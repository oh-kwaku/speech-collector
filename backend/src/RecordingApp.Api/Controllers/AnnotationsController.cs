using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Dtos;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Irr;
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

    // IRR-aware queue: recordings the current user hasn't annotated yet AND
    // that still need more independent annotators (< RequiredAnnotatorCount,
    // decided per-recording at confirm time - see RecordingsController.Confirm).
    // A recording never shows its existing annotation text here, so annotators
    // stay blind to each other's answers while the target is being filled.
    // Ordered so recordings closest to reaching their target are surfaced
    // first (helps finish IRR pairs/triples rather than spreading thin).
    [HttpGet("annotations/queue")]
    public async Task<ActionResult<List<RecordingQueueDto>>> Queue(CancellationToken ct)
    {
        var recordings = await db.Recordings
            .Where(r => r.IsConfirmed)
            .Where(r => r.Annotations.Count < r.RequiredAnnotatorCount)
            .Where(r => !r.Annotations.Any(a => a.AnnotatorUserId == currentUser.Id))
            .Include(r => r.Photo)
            .OrderByDescending(r => r.Annotations.Count)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new RecordingQueueDto(
                r.Id,
                r.SpeakerId,
                storage.GetPhotoReadUrl(r.Photo!.S3Key),
                storage.GetAudioReadUrl(r.S3Key),
                r.CreatedAt,
                r.Annotations.Count,
                r.RequiredAnnotatorCount))
            .ToListAsync(ct);

        return Ok(recordings);
    }

    [HttpPost("recordings/{recordingId:guid}/annotations")]
    public async Task<ActionResult<AnnotationDto>> Create(Guid recordingId, CreateAnnotationDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Annotation text is required.");

        var recording = await db.Recordings.Include(r => r.Photo).Include(r => r.Annotations)
            .FirstOrDefaultAsync(r => r.Id == recordingId && r.IsConfirmed, ct);
        if (recording is null) return NotFound();

        if (recording.Annotations.Any(a => a.AnnotatorUserId == currentUser.Id))
            return Conflict("You have already annotated this recording.");
        if (recording.Annotations.Count >= recording.RequiredAnnotatorCount)
            return Conflict("This recording already has its required number of annotations.");

        var annotation = new Annotation
        {
            RecordingId = recordingId,
            AnnotatorUserId = currentUser.Id,
            Text = dto.Text.Trim(),
        };
        db.Annotations.Add(annotation);

        // The first annotation submitted becomes canonical (used for export)
        // by default; an Admin can repoint it later via SetCanonical below
        // after reviewing disagreement in the IRR report.
        if (recording.CanonicalAnnotationId is null)
            recording.CanonicalAnnotationId = annotation.Id;

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

        // Independence matters for IRR: only the rater who wrote this
        // annotation (or an Admin correcting a mistake) may change it -
        // otherwise one annotator could silently overwrite another's rating.
        if (annotation.AnnotatorUserId != currentUser.Id && !currentUser.Roles.Contains("Admin"))
            return Forbid();

        annotation.Text = dto.Text.Trim();
        annotation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var users = await db.Users
            .Where(u => u.Id == annotation.AnnotatorUserId || u.Id == annotation.Recording!.CreatedByUserId)
            .ToDictionaryAsync(u => u.Id, ct);
        return Ok(ToDto(annotation, users));
    }

    // Lets an Admin/adjudicator pick which of a recording's independent
    // annotations is used for the training-data export, after reviewing
    // disagreement in the IRR report below. Defaults to the first submitted
    // (set in Create above) until an Admin overrides it.
    [HttpPut("recordings/{recordingId:guid}/canonical-annotation")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetCanonical(Guid recordingId, SetCanonicalAnnotationDto dto, CancellationToken ct)
    {
        var recording = await db.Recordings.Include(r => r.Annotations)
            .FirstOrDefaultAsync(r => r.Id == recordingId, ct);
        if (recording is null) return NotFound();

        if (!recording.Annotations.Any(a => a.Id == dto.AnnotationId))
            return BadRequest("That annotation does not belong to this recording.");

        recording.CanonicalAnnotationId = dto.AnnotationId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // IRR report: every recording that has reached at least 2 annotations,
    // with each annotator's text side by side plus a word-level agreement
    // score, sorted lowest-agreement-first so the pairs most needing
    // adjudication surface first. Deliberately Admin-only (unlike the rest of
    // this controller) since it's the one place raw text from multiple
    // annotators on the same recording is shown together - annotators stay
    // blind to each other's answers everywhere else (the queue only ever
    // shows a recording they haven't personally annotated yet).
    [HttpGet("annotations/irr")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<IrrRecordingDto>>> IrrReport(CancellationToken ct)
    {
        var recordings = await db.Recordings
            .Where(r => r.IsConfirmed && r.Annotations.Count > 1)
            .Include(r => r.Photo)
            .Include(r => r.Annotations)
            .ToListAsync(ct);

        var userIds = recordings.SelectMany(r => r.Annotations.Select(a => a.AnnotatorUserId)).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var result = recordings.Select(r =>
        {
            var anns = r.Annotations.OrderBy(a => a.CreatedAt).ToList();
            var entries = anns.Select(a =>
            {
                users.TryGetValue(a.AnnotatorUserId, out var annotator);
                return new IrrAnnotationEntryDto(
                    a.Id, a.AnnotatorUserId, annotator?.Email, annotator?.PhoneNumber, a.Text, a.UpdatedAt,
                    IsCanonical: a.Id == r.CanonicalAnnotationId);
            }).ToList();

            var pairwiseScores = new List<double>();
            for (var i = 0; i < anns.Count; i++)
                for (var j = i + 1; j < anns.Count; j++)
                    pairwiseScores.Add(TextSimilarity.WordLevelSimilarity(anns[i].Text, anns[j].Text));

            return new IrrRecordingDto(
                r.Id,
                r.SpeakerId,
                storage.GetPhotoReadUrl(r.Photo!.S3Key),
                storage.GetAudioReadUrl(r.S3Key),
                entries,
                AgreementScore: Math.Round(pairwiseScores.Average(), 3));
        })
        .OrderBy(r => r.AgreementScore)
        .ToList();

        return Ok(result);
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
            a.UpdatedAt,
            IsCanonical: a.Id == a.Recording.CanonicalAnnotationId);
    }
}
