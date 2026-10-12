using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Infrastructure.Storage;

/// <summary>
/// Resolves IObjectStorage per storage instance. Caches backends by instance config fingerprint.
/// </summary>
public sealed class StorageFactory(IServiceScopeFactory scopeFactory) : IStorageFactory, IDisposable
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<Guid, (string Fingerprint, IObjectStorage Storage)> _cache = new();

    public async Task<IObjectStorage> ForInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        StorageInstance instance;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            instance = await db.StorageInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId, ct)
                ?? throw new InvalidOperationException($"Storage instance '{instanceId}' not found.");
        }
        return await ForInstanceAsync(instance, ct);
    }

    public async Task<IObjectStorage> ForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        Guid instanceId;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            instanceId = await db.Projects.AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => p.StorageInstanceId)
                .FirstOrDefaultAsync(ct);
        }
        if (instanceId == Guid.Empty)
            throw new InvalidOperationException("Project not found.");
        return await ForInstanceAsync(instanceId, ct);
    }

    public Task<IObjectStorage> ForInstanceAsync(StorageInstance instance, CancellationToken ct = default)
    {
        var fp = Fingerprint(instance);
        lock (_gate)
        {
            if (_cache.TryGetValue(instance.Id, out var entry) && entry.Fingerprint == fp)
                return Task.FromResult(entry.Storage);

            if (entry.Storage is IDisposable d)
                d.Dispose();

            var storage = Create(instance);
            _cache[instance.Id] = (fp, storage);
            return Task.FromResult(storage);
        }
    }

    private static string Fingerprint(StorageInstance i)
        => $"{i.Kind}|{i.ServiceUrl}|{i.Region}|{i.Bucket}|{i.Prefix}|{i.ForcePathStyle}|{i.LocalRoot}|{i.AccessKey}|{i.SecretKey}";

    private static IObjectStorage Create(StorageInstance instance)
    {
        if (instance.Kind == StorageInstanceKind.S3)
        {
            return new S3ObjectStorage(
                instance.ServiceUrl ?? throw new InvalidOperationException("ServiceURL missing."),
                instance.Region,
                instance.AccessKey ?? string.Empty,
                instance.SecretKey ?? string.Empty,
                instance.Bucket,
                instance.Prefix,
                instance.ForcePathStyle);
        }

        return new LocalObjectStorage(Path.GetFullPath(instance.LocalRoot), instance.Prefix);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var entry in _cache.Values)
            {
                if (entry.Storage is IDisposable d)
                    d.Dispose();
            }
            _cache.Clear();
        }
    }
}
