using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
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

        var annotation = new Annotation
        {
            RecordingId = confirmedWithAnnotation.Id,
            AnnotatorUserId = annotator.Id,
            Text = "A cat sitting on a mat",
        };
        db.Annotations.Add(annotation);
        // Mirrors AnnotationsController.Create, which sets this on the first
        // annotation submitted for a recording.
        confirmedWithAnnotation.CanonicalAnnotationId = annotation.Id;

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

    private static async Task<(RecordingAppDbContext Db, Recording Recording, User FirstAnnotator, User SecondAnnotator)>
        SeedWithTwoAnnotatorsAsync()
    {
        var db = TestDbContextFactory.Create();

        var collector = new User { Email = "collector@example.com", Status = UserStatus.Active };
        var firstAnnotator = new User { Email = "first@example.com", Status = UserStatus.Active };
        var secondAnnotator = new User { Email = "second@example.com", Status = UserStatus.Active };
        db.Users.AddRange(collector, firstAnnotator, secondAnnotator);

        var speaker = new Speaker { Id = "SPK-TEST02", Gender = SpeakerGender.Male, AgeYears = 9, CreatedByUserId = collector.Id };
        db.Speakers.Add(speaker);

        var session = new RecordingSession { SpeakerId = speaker.Id, CreatedByUserId = collector.Id };
        db.RecordingSessions.Add(session);

        var photo = new Photo { S3Key = "photos/dog.jpg" };
        db.Photos.Add(photo);

        var recording = new Recording
        {
            SessionId = session.Id,
            SpeakerId = speaker.Id,
            PhotoId = photo.Id,
            S3Key = "audio/irr.webm",
            CreatedByUserId = collector.Id,
            IsConfirmed = true,
        };
        recording.RequiredAnnotatorCount = 2;
        db.Recordings.Add(recording);

        // IRR: two independent annotators on the SAME recording must both be
        // persisted - this is exactly what the old unique-per-recording index
        // used to forbid.
        var firstAnnotation = new Annotation
        {
            RecordingId = recording.Id,
            AnnotatorUserId = firstAnnotator.Id,
            Text = "A dog running in a field",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        db.Annotations.Add(firstAnnotation);
        db.Annotations.Add(new Annotation
        {
            RecordingId = recording.Id,
            AnnotatorUserId = secondAnnotator.Id,
            Text = "A dog running outside",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        // Mirrors AnnotationsController.Create, which defaults the canonical
        // annotation to the first one submitted.
        recording.CanonicalAnnotationId = firstAnnotation.Id;

        await db.SaveChangesAsync();
        return (db, recording, firstAnnotator, secondAnnotator);
    }

    [Fact]
    public async Task Recording_CanHaveMultipleAnnotationsFromDifferentAnnotators()
    {
        var (db, recording, _, _) = await SeedWithTwoAnnotatorsAsync();
        using var _ = db;

        var count = await db.Annotations.CountAsync(a => a.RecordingId == recording.Id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ExportCsvAsync_UsesCanonicalAnnotationWhenMultipleExist()
    {
        var (db, _, _, _) = await SeedWithTwoAnnotatorsAsync();
        using var _ = db;
        var service = new ExportService(db);

        // Defaults to the first annotation submitted (set in the seed helper,
        // mirroring AnnotationsController.Create).
        var csv = Encoding.UTF8.GetString(await service.ExportCsvAsync());
        Assert.Contains("A dog running in a field", csv);
        Assert.DoesNotContain("A dog running outside", csv);
    }

    [Fact]
    public async Task ExportCsvAsync_ReflectsAdminOverrideOfCanonicalAnnotation()
    {
        var (db, recording, _, secondAnnotator) = await SeedWithTwoAnnotatorsAsync();
        using var _ = db;

        // Mirrors AnnotationsController.SetCanonical: an Admin/adjudicator
        // repoints the export to a different annotator's text.
        var secondAnnotation = await db.Annotations.SingleAsync(a => a.AnnotatorUserId == secondAnnotator.Id);
        recording.CanonicalAnnotationId = secondAnnotation.Id;
        await db.SaveChangesAsync();

        var service = new ExportService(db);
        var csv = Encoding.UTF8.GetString(await service.ExportCsvAsync());

        Assert.Contains("A dog running outside", csv);
        Assert.DoesNotContain("A dog running in a field", csv);
    }

    [Fact]
    public async Task ExportIrrCsvAsync_IncludesOneRowPerAnnotatorForMultiplyAnnotatedRecordings()
    {
        var (db, recording, firstAnnotator, secondAnnotator) = await SeedWithTwoAnnotatorsAsync();
        using var _ = db;
        var service = new ExportService(db);

        var csv = Encoding.UTF8.GetString(await service.ExportIrrCsvAsync());
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("recordingId,photoId,sessionId,speakerId,annotatorUserId,annotation,created_at", lines[0]);
        Assert.Equal(3, lines.Length); // header + 2 annotator rows for the one IRR-eligible recording
        Assert.Contains(lines, l => l.StartsWith(recording.Id.ToString()) && l.Contains(firstAnnotator.Id.ToString()));
        Assert.Contains(lines, l => l.StartsWith(recording.Id.ToString()) && l.Contains(secondAnnotator.Id.ToString()));
    }

    [Fact]
    public async Task ExportIrrCsvAsync_ExcludesRecordingsWithOnlyOneAnnotation()
    {
        using var db = await SeedAsync(); // has one recording with exactly one annotation, one with none
        var service = new ExportService(db);

        var csv = Encoding.UTF8.GetString(await service.ExportIrrCsvAsync());
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines); // header only - no recording here has 2+ annotations
    }
}
