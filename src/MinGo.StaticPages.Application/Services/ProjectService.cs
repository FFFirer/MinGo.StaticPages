using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Application.Services;

public sealed class ProjectService(IAppDbContext db)
{
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Project name is required.");
        if (!IsValidName(name))
            throw new InvalidOperationException("Project name must be lowercase alphanumeric with - _ .");

        var exists = await db.Projects.AnyAsync(p => p.Name == name, ct);
        if (exists)
            throw new InvalidOperationException($"Project '{name}' already exists.");

        var project = new Project
        {
            Name = name,
            DisplayName = request.DisplayName,
            Description = request.Description
        };
        db.Add(project);

        foreach (var ch in ChannelNames.Defaults)
            db.Add(new Channel { ProjectId = project.Id, Name = ch });

        await db.SaveChangesAsync(ct);
        return await GetAsync(project.Id, ct) ?? throw new InvalidOperationException("Create failed.");
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default)
    {
        var projects = await db.Projects
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var result = new List<ProjectDto>();
        foreach (var p in projects)
            result.Add(await ToDtoAsync(p, ct));
        return result;
    }

    public async Task<ProjectDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return p is null ? null : await ToDtoAsync(p, ct);
    }

    public async Task<ProjectDto?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted, ct);
        return p is null ? null : await ToDtoAsync(p, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Project not found.");

        var versions = await db.Versions.Where(v => v.ProjectId == id).ToListAsync(ct);
        foreach (var v in versions)
            v.Status = VersionStatus.Deleted;

        p.IsDeleted = true;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static bool IsValidName(string name)
    {
        if (name.Length is < 1 or > 64) return false;
        return name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    }

    private async Task<ProjectDto> ToDtoAsync(Project p, CancellationToken ct)
    {
        var versionCount = await db.Versions.CountAsync(v => v.ProjectId == p.Id && v.Status == VersionStatus.Active, ct);
        var storageBytes = await db.Versions
            .Where(v => v.ProjectId == p.Id && v.Status == VersionStatus.Active)
            .SumAsync(v => (long?)v.SizeBytes, ct) ?? 0;

        var channels = await db.Channels
            .Where(c => c.ProjectId == p.Id)
            .Select(c => new { c.Name, c.CurrentVersionId })
            .ToListAsync(ct);

        var channelDtos = new List<ChannelDto>();
        foreach (var c in channels)
        {
            string? num = null;
            if (c.CurrentVersionId is Guid vid)
            {
                num = await db.Versions
                    .Where(v => v.Id == vid)
                    .Select(v => v.Number)
                    .FirstOrDefaultAsync(ct);
            }
            channelDtos.Add(new ChannelDto(c.Name, c.CurrentVersionId, num));
        }

        return new ProjectDto(p.Id, p.Name, p.DisplayName, p.Description, versionCount, storageBytes, p.CreatedAt, channelDtos);
    }
}

public sealed class VersionService(IAppDbContext db, IObjectStorage storage, IStorageSettingsService settingsService)
{
    public async Task<VersionDto> UploadAsync(Guid projectId, Stream zipStream, UploadVersionRequest request, string operatorName, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Project not found.");

        var settings = await settingsService.GetAsync(ct);
        if (request.Number is { Length: > 0 } num)
        {
            if (!ProjectService.IsValidName(num) && !IsValidVersionNumber(num))
                throw new InvalidOperationException("Invalid version number.");
            var dup = await db.Versions.AnyAsync(v => v.ProjectId == projectId && v.Number == num, ct);
            if (dup)
                throw new InvalidOperationException($"Version '{num}' already exists.");
        }

        var versionId = Guid.NewGuid();
        var number = string.IsNullOrWhiteSpace(request.Number)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "-" + versionId.ToString("N")[..6]
            : request.Number.Trim();

        var prefix = $"projects/{projectId}/versions/{versionId}/files";

        try
        {
            var files = await ZipPackageProcessor.ProcessAsync(
                zipStream,
                settings.MaxFileCount,
                settings.MaxSingleFileBytes,
                uploadEntryAsync: (rel, stream, contentType, token) =>
                {
                    var key = $"{prefix}/{rel}";
                    return storage.UploadAsync(key, stream, contentType, token);
                },
                uploadPackageAsync: (name, stream, contentType, token) =>
                {
                    var key = $"projects/{projectId}/versions/{versionId}/{name}";
                    return storage.UploadAsync(key, stream, contentType, token);
                },
                ct);

            var version = new VersionRecord
            {
                Id = versionId,
                ProjectId = projectId,
                Number = number,
                StoragePrefix = prefix,
                PackageKey = $"projects/{projectId}/versions/{versionId}/package.zip",
                SizeBytes = files.Sum(f => f.SizeBytes),
                FileCount = files.Count,
                SpaEnabled = request.SpaEnabled,
                SpaFallback = string.IsNullOrWhiteSpace(request.SpaFallback) ? "index.html" : request.SpaFallback,
                CommitSha = request.CommitSha,
                Branch = request.Branch,
                Message = request.Message,
                CreatedBy = operatorName
            };

            foreach (var f in files)
            {
                version.Files.Add(new VersionFile
                {
                    VersionId = version.Id,
                    RelativePath = f.RelativePath,
                    SizeBytes = f.SizeBytes,
                    ContentType = f.ContentType,
                    Sha256 = f.Sha256
                });
            }

            db.Add(version);
            await db.SaveChangesAsync(ct);
            return ToDto(version);
        }
        catch
        {
            // best-effort cleanup of partial upload
            try { await storage.DeletePrefixAsync($"projects/{projectId}/versions/{versionId}", ct); } catch { /* ignore */ }
            throw;
        }
    }

    public async Task<IReadOnlyList<VersionDto>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var list = await db.Versions
            .Where(v => v.ProjectId == projectId)
            .ToListAsync(ct);
        return list.OrderByDescending(v => v.CreatedAt).Select(ToDto).ToList();
    }

    public async Task<VersionDetailDto?> GetAsync(Guid versionId, CancellationToken ct = default)
    {
        var v = await db.Versions
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == versionId, ct);
        return v is null ? null : new VersionDetailDto(ToDto(v), v.Files
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .Select(f => new VersionFileDto(f.RelativePath, f.SizeBytes, f.ContentType))
            .ToList());
    }

    public async Task DeleteAsync(Guid versionId, CancellationToken ct = default)
    {
        var v = await db.Versions.FirstOrDefaultAsync(x => x.Id == versionId, ct)
            ?? throw new InvalidOperationException("Version not found.");

        if (v.Status == VersionStatus.Deleted)
            return;

        var pointed = await db.Channels.AnyAsync(c => c.CurrentVersionId == versionId, ct);
        if (pointed)
            throw new InvalidOperationException("Version is referenced by a channel. Change publish pointer first.");

        v.Status = VersionStatus.Deleted;
        await db.SaveChangesAsync(ct);
    }

    public static bool IsValidVersionNumber(string number)
    {
        if (number.Length is < 1 or > 64) return false;
        return number.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '+');
    }

    private static VersionDto ToDto(VersionRecord v) => new(
        v.Id,
        v.ProjectId,
        v.Number,
        (VersionStatusDto)(int)v.Status,
        v.SizeBytes,
        v.FileCount,
        v.SpaEnabled,
        v.SpaFallback,
        v.CommitSha,
        v.Branch,
        v.Message,
        v.CreatedBy,
        v.CreatedAt);
}
