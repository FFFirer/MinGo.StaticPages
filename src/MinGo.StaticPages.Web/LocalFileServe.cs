using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure.Data;

namespace MinGo.StaticPages.Web;

/// <summary>
/// Local FileProvider static read outlet (L2). Serves version files for Local-bound projects only.
/// </summary>
public static class LocalFileServe
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>Base path advertised in resolve.storage.endpoint for Local instances.</summary>
    public static string EndpointBase(string projectName) => $"/s/{Uri.EscapeDataString(projectName)}";

    public static async Task<IResult> ServeAsync(
        HttpContext http,
        AppDbContext db,
        string projectName,
        string? versionOrChannel,
        string? filePath,
        bool byVersion,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return Results.NotFound();

        var project = await db.Projects
            .Include(p => p.StorageInstance)
            .FirstOrDefaultAsync(p => p.Name == projectName && !p.IsDeleted, ct);
        if (project?.StorageInstance is null)
            return Results.NotFound();

        var instance = project.StorageInstance;
        if (instance.Kind != StorageInstanceKind.Local)
            return Results.NotFound(new { error = "local_file_provider_not_available_for_s3" });

        VersionRecord? version;
        bool immutable;
        string? cacheChannel = null;

        if (byVersion)
        {
            version = await db.Versions.FirstOrDefaultAsync(v =>
                v.ProjectId == project.Id &&
                v.Status == VersionStatus.Active &&
                (v.Id.ToString() == versionOrChannel || v.Number == versionOrChannel), ct);
            immutable = true;
        }
        else
        {
            var channelName = string.IsNullOrWhiteSpace(versionOrChannel)
                ? ChannelNames.Production
                : versionOrChannel.Trim().ToLowerInvariant();
            cacheChannel = channelName;
            var ch = await db.Channels.FirstOrDefaultAsync(
                c => c.ProjectId == project.Id && c.Name == channelName, ct);
            if (ch?.CurrentVersionId is not Guid currentId)
                return Results.NotFound(new { error = "not_published" });

            version = await db.Versions.FirstOrDefaultAsync(
                v => v.Id == currentId && v.Status == VersionStatus.Active, ct);
            immutable = false;
        }

        if (version is null)
            return Results.NotFound(new { error = "version_not_found" });

        var relPath = (filePath ?? string.Empty).Replace('\\', '/').Trim('/');
        if (relPath.Contains("..", StringComparison.Ordinal))
            return Results.BadRequest(new { error = "invalid_path" });

        // Logical files root: projects/{pid}/versions/{vid}/files
        var logicalRoot = version.StoragePrefix; // already projects/{pid}/versions/{vid}/files
        var fullRoot = Path.GetFullPath(Path.Combine(instance.LocalRoot, instance.Prefix.Replace('/', Path.DirectorySeparatorChar), logicalRoot.Replace('/', Path.DirectorySeparatorChar)));
        var localRootFull = Path.GetFullPath(instance.LocalRoot);
        if (!fullRoot.StartsWith(localRootFull, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "invalid_path" });

        var provider = new PhysicalFileProvider(fullRoot);
        var file = provider.GetFileInfo(relPath.Length == 0 ? version.SpaFallback : relPath);

        if (!file.Exists || file.IsDirectory)
        {
            // SPA fallback for extension-less paths
            if (version.SpaEnabled && !Path.HasExtension(relPath))
            {
                file = provider.GetFileInfo(version.SpaFallback);
                if (!file.Exists)
                    return Results.NotFound();
            }
            else
            {
                return Results.NotFound();
            }
        }

        if (!ContentTypes.TryGetContentType(file.Name, out var contentType))
            contentType = "application/octet-stream";

        // Prefer manifest content type when available
        if (!string.IsNullOrEmpty(relPath))
        {
            var manifestType = await db.VersionFiles
                .Where(f => f.VersionId == version.Id && f.RelativePath == relPath)
                .Select(f => f.ContentType)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(manifestType))
                contentType = manifestType;
        }

        var settings = await db.StorageSettings.FirstOrDefaultAsync(ct) ?? new StorageSettings();
        var seconds = immutable ? settings.VersionCacheSeconds : settings.PublishedCacheSeconds;
        var cacheControl = immutable
            ? $"public, max-age={seconds}, immutable"
            : $"public, max-age={seconds}";
        http.Response.Headers.CacheControl = cacheControl;
        if (cacheChannel is not null)
            http.Response.Headers["X-MinGo-Channel"] = cacheChannel;
        http.Response.Headers["X-MinGo-Version"] = version.Number;

        return Results.File(file.PhysicalPath!, contentType, enableRangeProcessing: true);
    }
}
