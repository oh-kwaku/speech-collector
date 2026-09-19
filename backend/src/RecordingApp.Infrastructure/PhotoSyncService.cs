using Microsoft.EntityFrameworkCore;
using RecordingApp.Domain;
using RecordingApp.Infrastructure.Storage;

namespace RecordingApp.Infrastructure;

public record PhotoSyncResult(int Added, int Total);

public interface IPhotoSyncService
{
    Task<PhotoSyncResult> SyncAsync(CancellationToken ct = default);
}

public class PhotoSyncService(RecordingAppDbContext db, IStorageService storage) : IPhotoSyncService
{
    public async Task<PhotoSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var keys = await storage.ListPhotoKeysAsync(ct);
        var existingKeys = await db.Photos.Select(p => p.S3Key).ToListAsync(ct);
        var newKeys = keys.Except(existingKeys).ToList();

        foreach (var key in newKeys)
        {
            db.Photos.Add(new Photo { S3Key = key });
        }
        await db.SaveChangesAsync(ct);

        var total = await db.Photos.CountAsync(ct);
        return new PhotoSyncResult(newKeys.Count, total);
    }
}
