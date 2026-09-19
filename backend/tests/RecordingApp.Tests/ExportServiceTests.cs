using System.Linq;
using System.Text;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Export;
using Xunit;

namespace RecordingApp.Tests;

public class ExportServiceTests
{
    private static async Task<RecordingAppDbContext> SeedAsync()
    {
        var db = TestDbContextFactory.Create();

        var collector = new User { Email = "collector@example.com", Status = UserStatus.Active };
        var annotator = new User { Email = "annotator@example.com", Status = UserStatus.Active };
        db.Users.AddRange(collector, annotator);

        var speaker = new Speaker { Id = "SPK-TEST01", Gender = SpeakerGender.Female, AgeYears = 7, CreatedByUserId = collector.Id };
        db.Speakers.Add(speaker);

        var session = new RecordingSession { SpeakerId = speaker.Id, CreatedByUserId = collector.Id };
        db.RecordingSessions.Add(session);

        var photo = new Photo { S3Key = "photos/cat.jpg" };
        db.Photos.Add(photo);

        var confirmedWithAnnotation = new Recording
        {
            SessionId = session.Id,
            SpeakerId = speaker.Id,
            PhotoId = photo.Id,
            S3Key = "audio/1.webm",
            CreatedByUserId = collector.Id,
            IsConfirmed = true,
        };
        var confirmedWithoutAnnotation = new Recording
        {
            SessionId = session.Id,
            SpeakerId = speaker.Id,
            PhotoId = photo.Id,
            S3Key = "audio/2.webm",
            CreatedByUserId = collector.Id,
            IsConfirmed = true,
        };
        var unconfirmed = new Recording
        {
            SessionId = session.Id,
            SpeakerId = speaker.Id,
            PhotoId = photo.Id,
            S3Key = "audio/3.webm",
            CreatedByUserId = collector.Id,
            IsConfirmed = false,
        };
        db.Recordings.AddRange(confirmedWithAnnotation, confirmedWithoutAnnotation, unconfirmed);

        db.Annotations.Add(new Annotation
        {
            RecordingId = confirmedWithAnnotation.Id,
            AnnotatorUserId = annotator.Id,
            Text = "A cat sitting on a mat",
        });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task ExportCsvAsync_IncludesAllConfirmedRecordingsRegardlessOfAnnotation()
    {
        using var db = await SeedAsync();
        var service = new ExportService(db);

        var csv = Encoding.UTF8.GetString(await service.ExportCsvAsync());
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header + one row per confirmed recording (annotated or not); unconfirmed is excluded.
        Assert.Equal(3, lines.Length);
        Assert.Equal("photoId,sessionId,userId,speakerId,annotation,created_at,speaker_gender,speaker_age", lines[0]);

        var annotatedLine = Assert.Single(lines, l => l.Contains("A cat sitting on a mat"));
        Assert.Contains("Female", annotatedLine);
        Assert.Contains(",7", annotatedLine);

        var unannotatedLine = Assert.Single(lines.Skip(1), l => !l.Contains("A cat sitting on a mat"));
        Assert.Contains("SPK-TEST01", unannotatedLine);
        Assert.Contains(",,", unannotatedLine);
    }

    [Fact]
    public async Task ExportXlsxAsync_ProducesNonEmptyWorkbook()
    {
        using var db = await SeedAsync();
        var service = new ExportService(db);

        var bytes = await service.ExportXlsxAsync();

        Assert.NotEmpty(bytes);
    }
}
