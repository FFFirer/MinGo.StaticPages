using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure.Data;

namespace MinGo.StaticPages.Infrastructure.Storage;

/// <summary>Global policy settings (limits, resolve auth, cache). Connection config lives on StorageInstance.</summary>
public sealed class StorageSettingsService(AppDbContext db, IStorageFactory storageFactory) : IStorageSettingsService
{
    public async Task<StorageSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.StorageSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new StorageSettings();
            db.Add(settings);
            await db.SaveChangesAsync(ct);
        }
        return settings;
    }

    public async Task<StorageSettings> UpdateAsync(StorageSettings settings, CancellationToken ct = default)
    {
        if (settings.MaxPackageBytes <= 0 || settings.MaxFileCount <= 0 || settings.MaxSingleFileBytes <= 0)
            throw new InvalidOperationException("Upload limits must be positive.");

        var existing = await GetAsync(ct);
        existing.MaxPackageBytes = settings.MaxPackageBytes;
        existing.MaxFileCount = settings.MaxFileCount;
        existing.MaxSingleFileBytes = settings.MaxSingleFileBytes;
        existing.ResolveRequireAuth = settings.ResolveRequireAuth;
        existing.PublishedCacheSeconds = settings.PublishedCacheSeconds;
        existing.VersionCacheSeconds = settings.VersionCacheSeconds;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> TestConnectivityAsync(Guid storageInstanceId, CancellationToken ct = default)
    {
        try
        {
            var storage = await storageFactory.ForInstanceAsync(storageInstanceId, ct);
            await storage.UploadAsync("healthcheck/ping.txt", new MemoryStream("ok"u8.ToArray()), "text/plain", ct);
            await storage.DeleteAsync("healthcheck/ping.txt", ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
