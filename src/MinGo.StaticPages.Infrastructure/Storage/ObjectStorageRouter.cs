using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Infrastructure.Storage;

/// <summary>
/// Resolves the active IObjectStorage from current settings.
/// Uses a scope per settings load so it can be registered as a singleton.
/// </summary>
public sealed class ObjectStorageRouter(IServiceScopeFactory scopeFactory) : IObjectStorage, IDisposable
{
    private readonly Lock _gate = new();
    private IObjectStorage? _inner;
    private string? _fingerprint;

    private async Task<IObjectStorage> GetInnerAsync(CancellationToken ct)
    {
        StorageSettings settings;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            settings = await db.StorageSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new StorageSettings();
        }

        var fp = $"{settings.Provider}|{settings.ServiceUrl}|{settings.Region}|{settings.Bucket}|{settings.Prefix}|{settings.ForcePathStyle}|{settings.LocalRoot}|{settings.AccessKey}";

        lock (_gate)
        {
            if (_inner is not null && _fingerprint == fp)
                return _inner;

            if (_inner is IDisposable d)
                d.Dispose();

            _inner = settings.Provider == StorageProviderKind.S3
                ? new S3ObjectStorage(
                    settings.ServiceUrl ?? throw new InvalidOperationException("ServiceURL missing."),
                    settings.Region,
                    settings.AccessKey ?? string.Empty,
                    settings.SecretKey ?? string.Empty,
                    settings.Bucket,
                    settings.Prefix,
                    settings.ForcePathStyle)
                : new LocalObjectStorage(Path.GetFullPath(settings.LocalRoot));

            _fingerprint = fp;
            return _inner;
        }
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).UploadAsync(key, content, contentType, ct);

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).DownloadAsync(key, ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).ExistsAsync(key, ct);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).DeleteAsync(key, ct);

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).DeletePrefixAsync(prefix, ct);

    public async Task<IReadOnlyList<ObjectInfo>> ListAsync(string prefix, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).ListAsync(prefix, ct);

    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).GetSizeAsync(key, ct);

    public async Task<string?> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => await (await GetInnerAsync(ct)).GetPresignedUrlAsync(key, expiry, ct);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_inner is IDisposable d)
                d.Dispose();
            _inner = null;
        }
    }
}
