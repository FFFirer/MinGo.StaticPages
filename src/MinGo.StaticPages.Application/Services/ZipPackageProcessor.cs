using System.IO.Compression;
using System.Security.Cryptography;

namespace MinGo.StaticPages.Application.Services;

public sealed record ZipEntryRecord(string RelativePath, long SizeBytes, string ContentType, string Sha256);

public sealed class ZipValidationException(string message) : Exception(message);

public static class ZipPackageProcessor
{
    private static readonly HashSet<string> DangerousSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "..", ".", ""
    };

    public static bool IsSafeRelativePath(string path, out string normalized)
    {
        normalized = path.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        // reject rooted / drive-qualified paths before normalization
        if (normalized.StartsWith('/') || normalized.StartsWith('\\') || Path.IsPathRooted(path))
            return false;
        if (normalized.Contains(':'))
            return false;

        normalized = normalized.TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (DangerousSegments.Contains(seg))
                return false;
            if (seg.Contains('\0'))
                return false;
        }

        normalized = string.Join('/', segments);
        return normalized.Length > 0;
    }

    public static string GetContentType(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".map" => "application/json; charset=utf-8",
            ".xml" => "application/xml; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".txt" => "text/plain; charset=utf-8",
            ".wasm" => "application/wasm",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Validates and extracts zip entries to the provided upload callback. Returns file records.
    /// Caller must ensure package size limit before invoking.
    /// </summary>
    public static async Task<IReadOnlyList<ZipEntryRecord>> ProcessAsync(
        Stream zipStream,
        int maxFileCount,
        int maxSingleFileBytes,
        Func<string, Stream, string, CancellationToken, Task> uploadEntryAsync,
        Func<string, Stream, string, CancellationToken, Task> uploadPackageAsync,
        CancellationToken ct = default)
    {
        var files = new List<ZipEntryRecord>();

        // Keep a copy of the raw zip for package.zip
        using var packageMs = new MemoryStream();
        await zipStream.CopyToAsync(packageMs, ct);
        packageMs.Position = 0;

        if (packageMs.Length == 0)
            throw new ZipValidationException("Empty package.");

        using var zipStream2 = new MemoryStream(packageMs.ToArray(), writable: false);
        using var archive = new ZipArchive(zipStream2, ZipArchiveMode.Read, leaveOpen: false);

        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        if (entries.Count == 0)
            throw new ZipValidationException("Package contains no files.");
        if (entries.Count > maxFileCount)
            throw new ZipValidationException($"Package exceeds max file count {maxFileCount}.");

        foreach (var entry in entries)
        {
            if (!IsSafeRelativePath(entry.FullName, out var rel))
                throw new ZipValidationException($"Unsafe path in package: {entry.FullName}");

            if (entry.Length > maxSingleFileBytes)
                throw new ZipValidationException($"File exceeds max size: {rel}");

            await using var es = entry.Open();
            using var buffer = new MemoryStream();
            await es.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            var sha = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
            buffer.Position = 0;
            await uploadEntryAsync(rel, buffer, GetContentType(rel), ct);

            files.Add(new ZipEntryRecord(rel, buffer.Length, GetContentType(rel), sha));
        }

        packageMs.Position = 0;
        await uploadPackageAsync("package.zip", packageMs, "application/zip", ct);
        return files;
    }
}
