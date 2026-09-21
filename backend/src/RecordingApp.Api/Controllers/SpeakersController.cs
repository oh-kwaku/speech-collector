using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Dtos;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Security;

namespace RecordingApp.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "Collector")]
public class SpeakersController(RecordingAppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("speakers")]
    public async Task<ActionResult<List<SpeakerDto>>> List(CancellationToken ct)
    {
        var speakers = await db.Speakers
            .Where(s => s.CreatedByUserId == currentUser.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return Ok(speakers.Select(SpeakerDto.From));
    }

    [HttpPost("speakers")]
    public async Task<ActionResult<SpeakerDto>> Create(CreateSpeakerDto dto, CancellationToken ct)
    {
        if (dto.AgeYears is < 3 or > 12) return BadRequest("Age must be between 3 and 12.");

        string id;
        do
        {
            id = CryptoHelpers.GenerateSpeakerId();
        } while (await db.Speakers.AnyAsync(s => s.Id == id, ct));

        var speaker = new Speaker
        {
            Id = id,
            Gender = dto.Gender,
            AgeYears = dto.AgeYears,
            CreatedByUserId = currentUser.Id,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync(ct);
        return Ok(SpeakerDto.From(speaker));
    }

    [HttpPost("speakers/{speakerId}/sessions/active")]
    public async Task<ActionResult<SessionDto>> GetOrCreateActiveSession(string speakerId, CancellationToken ct)
    {
        var speaker = await db.Speakers.FirstOrDefaultAsync(
            s => s.Id == speakerId && s.CreatedByUserId == currentUser.Id, ct);
        if (speaker is null) return NotFound();

        var session = await db.RecordingSessions
            .Where(s => s.SpeakerId == speakerId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            session = new RecordingSession { SpeakerId = speakerId, CreatedByUserId = currentUser.Id };
            db.RecordingSessions.Add(session);
            await db.SaveChangesAsync(ct);
        }

        var count = await db.Recordings.CountAsync(r => r.SessionId == session.Id && r.IsConfirmed, ct);
        return Ok(SessionDto.From(session, count));
    }

    [HttpGet("speakers/{speakerId}/sessions/{sessionId:guid}")]
    public async Task<ActionResult<SessionDto>> GetSession(string speakerId, Guid sessionId, CancellationToken ct)
    {
        var session = await db.RecordingSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.SpeakerId == speakerId && s.CreatedByUserId == currentUser.Id, ct);
        if (session is null) return NotFound();

        var count = await db.Recordings.CountAsync(r => r.SessionId == session.Id && r.IsConfirmed, ct);
        return Ok(SessionDto.From(session, count));
    }
}
