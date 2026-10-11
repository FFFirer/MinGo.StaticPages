namespace MinGo.StaticPages.Application.Storage;

public sealed record ObjectInfo(string Key, long Size, DateTimeOffset? LastModified = null);

public interface IObjectStorage
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task DeletePrefixAsync(string prefix, CancellationToken ct = default);
    Task<IReadOnlyList<ObjectInfo>> ListAsync(string prefix, CancellationToken ct = default);
    Task<long> GetSizeAsync(string key, CancellationToken ct = default);
    Task<string?> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}

public sealed record StorageBackendInfo(
    string Provider,
    string? Bucket,
    string Prefix,
    string? Endpoint,
    bool ForcePathStyle,
    string? LocalRoot);
