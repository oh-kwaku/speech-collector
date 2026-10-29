using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RecordingApp.Api.Auth;
using RecordingApp.Api.Controllers;
using RecordingApp.Domain;
using RecordingApp.Infrastructure;
using RecordingApp.Infrastructure.Storage;
using Xunit;

namespace RecordingApp.Tests;

public class DeleteRecordingTests
{
    private sealed class FakeStorage : IStorageService
    {
        public List<string> DeletedKeys { get; } = [];
        public Task<IReadOnlyList<string>> ListPhotoKeysAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public string GetPhotoReadUrl(string s3Key) => s3Key;
        public string GetAudioUploadUrl(string s3Key, string contentType) => s3Key;
        public string GetAudioReadUrl(string s3Key) => s3Key;
        public Task DeleteAudioAsync(string s3Key, CancellationToken ct = default)
        {
            DeletedKeys.Add(s3Key);
            return Task.CompletedTask;
        }
        public string BuildAudioKey(string speakerId, Guid sessionId, Guid recordingId, string gender) => "";
    }

    private sealed class FakeCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public IReadOnlyList<string> Roles => ["Admin"];
    }

    private static async Task<(RecordingAppDbContext db, Recording recording, Annotation first, Annotation second)> SeedAsync()
    {
        var db = TestDbContextFactory.Create();
        var session = new RecordingSession { SpeakerId = "SPK-TEST01", CreatedByUserId = Guid.NewGuid() };
        var photo = new Photo { S3Key = "photos/cat.jpg" };
        var recording = new Recording
        {
            SessionId = session.Id,
            SpeakerId = session.SpeakerId,
            PhotoId = photo.Id,
            S3Key = "audio/SPK-TEST01/x.wav",
            CreatedByUserId = session.CreatedByUserId,
            IsConfirmed = true,
            RequiredAnnotatorCount = 2,
        };
        var first = new Annotation
        {
            RecordingId = recording.Id, AnnotatorUserId = Guid.NewGuid(), Text = "a cat",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        var second = new Annotation
        {
            RecordingId = recording.Id, AnnotatorUserId = Guid.NewGuid(), Text = "the cat",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        recording.CanonicalAnnotationId = first.Id;
        db.AddRange(session, photo, recording, first, second);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, recording, first, second);
    }

    [Fact]
    public async Task DeleteRecording_RemovesRecordingAnnotationsAndAudio()
    {
        var (db, recording, _, _) = await SeedAsync();
        var storage = new FakeStorage();
        var controller = new RecordingsController(
            db, storage, new FakeCurrentUser(Guid.NewGuid()), new ConfigurationBuilder().Build(),
            NullLogger<RecordingsController>.Instance);

        var result = await controller.Delete(recording.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Recordings.AnyAsync());
        Assert.False(await db.Annotations.AnyAsync());
        Assert.Equal([recording.S3Key], storage.DeletedKeys);
    }

    [Fact]
    public async Task DeleteRecording_Unknown_ReturnsNotFound()
    {
        var (db, _, _, _) = await SeedAsync();
        var controller = new RecordingsController(
            db, new FakeStorage(), new FakeCurrentUser(Guid.NewGuid()), new ConfigurationBuilder().Build(),
            NullLogger<RecordingsController>.Instance);

        Assert.IsType<NotFoundResult>(await controller.Delete(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteCanonicalAnnotation_RepointsToRemainingAnnotation()
    {
        var (db, recording, first, second) = await SeedAsync();
        var controller = new AnnotationsController(db, new FakeCurrentUser(Guid.NewGuid()), new FakeStorage());

        var result = await controller.Delete(first.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await db.Recordings.Include(r => r.Annotations).SingleAsync(r => r.Id == recording.Id);
        Assert.Equal(second.Id, Assert.Single(reloaded.Annotations).Id);
        Assert.Equal(second.Id, reloaded.CanonicalAnnotationId);
    }

    [Fact]
    public async Task DeleteLastAnnotation_ClearsCanonical()
    {
        var (db, recording, first, second) = await SeedAsync();
        var controller = new AnnotationsController(db, new FakeCurrentUser(Guid.NewGuid()), new FakeStorage());

        await controller.Delete(second.Id, CancellationToken.None);
        await controller.Delete(first.Id, CancellationToken.None);

        var reloaded = await db.Recordings.SingleAsync(r => r.Id == recording.Id);
        Assert.Null(reloaded.CanonicalAnnotationId);
        Assert.False(await db.Annotations.AnyAsync());
    }
}
