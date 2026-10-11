using MinGo.StaticPages.Application.Storage;

namespace MinGo.StaticPages.Infrastructure.Storage;

public sealed class LocalObjectStorage(string rootPath) : IObjectStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    private string Resolve(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid object key.");

        var full = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes storage root.");
        return full;
    }

    public Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return WriteFileAsync(path, content, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (!File.Exists(path))
            throw new FileNotFoundException("Object not found.", key);
        var ms = new MemoryStream();
        await using var fs = File.OpenRead(path);
        await fs.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        var dir = Resolve(prefix);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ObjectInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var dir = Resolve(prefix);
        var results = new List<ObjectInfo>();
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var info = new FileInfo(file);
                results.Add(new ObjectInfo(rel, info.Length, info.LastWriteTimeUtc));
            }
        }
        return Task.FromResult<IReadOnlyList<ObjectInfo>>(results);
    }

    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        return Task.FromResult(File.Exists(path) ? new FileInfo(path).Length : -1L);
    }

    public Task<string?> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    private static async Task WriteFileAsync(string path, Stream content, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
    }
}
