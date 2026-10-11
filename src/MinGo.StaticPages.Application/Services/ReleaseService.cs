using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Application.Services;

public sealed class ReleaseService(IAppDbContext db)
{
    public async Task<ReleaseDto> PublishAsync(Guid projectId, PublishRequest request, string operatorName, CancellationToken ct = default)
    {
        return await ExecuteReleaseAsync(projectId, request.Channel, request.VersionId, ReleaseAction.Publish, operatorName, ct);
    }

    public async Task<ReleaseDto> RollbackAsync(Guid projectId, RollbackRequest request, string operatorName, CancellationToken ct = default)
    {
        var channel = await GetChannelAsync(projectId, request.Channel, ct);
        Guid toVersionId;

        if (request.ToVersionId is Guid explicitId)
        {
            toVersionId = explicitId;
        }
        else
        {
        var releases = await db.Releases
                .Where(r => r.ProjectId == projectId && r.ChannelName == channel.Name)
                .ToListAsync(ct);
            var prev = releases
                .OrderByDescending(r => r.CreatedAt)
                .Skip(1)
                .FirstOrDefault();

            // Prefer previous release target; if none, use previous version of current from history
            if (prev is not null)
                toVersionId = prev.ToVersionId;
            else
            {
                var history = releases;
                var older = history
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault(r => r.FromVersionId is not null);
                if (older?.FromVersionId is Guid fromId)
                    toVersionId = fromId;
                else
                    throw new InvalidOperationException("No previous release to roll back to.");
            }
        }

        return await ExecuteReleaseAsync(projectId, request.Channel, toVersionId, ReleaseAction.Rollback, operatorName, ct);
    }

    public async Task<IReadOnlyList<ReleaseDto>> HistoryAsync(Guid projectId, CancellationToken ct = default)
    {
        var list = await db.Releases
            .Where(r => r.ProjectId == projectId)
            .Take(100)
            .ToListAsync(ct);

        return list.OrderByDescending(r => r.CreatedAt).Select(r => new ReleaseDto(
            r.Id,
            r.ChannelName,
            r.Action == ReleaseAction.Publish ? "publish" : "rollback",
            r.FromVersionId,
            r.ToVersionId,
            r.OperatorName,
            r.CreatedAt)).ToList();
    }

    private async Task<ReleaseDto> ExecuteReleaseAsync(
        Guid projectId,
        string channelName,
        Guid toVersionId,
        ReleaseAction action,
        string operatorName,
        CancellationToken ct)
    {
        var channel = await GetChannelAsync(projectId, channelName, ct);

        var version = await db.Versions.FirstOrDefaultAsync(v => v.Id == toVersionId && v.ProjectId == projectId, ct)
            ?? throw new InvalidOperationException("Version not found in project.");
        if (version.Status != VersionStatus.Active)
            throw new InvalidOperationException("Version is not active.");

        var from = channel.CurrentVersionId;
        channel.CurrentVersionId = toVersionId;

        var record = new ReleaseRecord
        {
            ProjectId = projectId,
            ChannelName = channel.Name,
            Action = action,
            FromVersionId = from,
            ToVersionId = toVersionId,
            OperatorName = operatorName
        };
        db.Add(record);
        await db.SaveChangesAsync(ct);

        return new ReleaseDto(record.Id, record.ChannelName, action == ReleaseAction.Publish ? "publish" : "rollback",
            record.FromVersionId, record.ToVersionId, record.OperatorName, record.CreatedAt);
    }

    private async Task<Channel> GetChannelAsync(Guid projectId, string channelName, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(channelName) ? ChannelNames.Production : channelName.Trim().ToLowerInvariant();
        return await db.Channels.FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Name == name, ct)
            ?? throw new InvalidOperationException($"Channel '{name}' not found.");
    }
}

public sealed class ResolveService(IAppDbContext db, IStorageSettingsService settingsService)
{
    public async Task<ResolveResponse?> ResolveAsync(string projectName, string? channel, string? version, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Name == projectName && !p.IsDeleted, ct);
        if (project is null)
            return null;

        var settings = await settingsService.GetAsync(ct);
        VersionRecord? versionRecord;
        string access;
        string? resolvedChannel = null;

        if (!string.IsNullOrWhiteSpace(version))
        {
            versionRecord = await db.Versions.FirstOrDefaultAsync(v =>
                v.ProjectId == project.Id &&
                v.Status == VersionStatus.Active &&
                (v.Id.ToString() == version || v.Number == version), ct);
            access = "version";
        }
        else
        {
            var channelName = string.IsNullOrWhiteSpace(channel) ? ChannelNames.Production : channel.Trim().ToLowerInvariant();
            resolvedChannel = channelName;
            var ch = await db.Channels.FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Name == channelName, ct);
            if (ch?.CurrentVersionId is not Guid currentId)
                return null;

            versionRecord = await db.Versions.FirstOrDefaultAsync(v =>
                v.Id == currentId && v.Status == VersionStatus.Active, ct);
            access = "published";
        }

        if (versionRecord is null)
            return null;

        var cacheSeconds = access == "version" ? settings.VersionCacheSeconds : settings.PublishedCacheSeconds;
        var immutable = access == "version";

        return new ResolveResponse(
            project.Name,
            project.Id,
            versionRecord.Id,
            versionRecord.Number,
            access,
            resolvedChannel,
            new StoragePayload(
                settings.Provider == StorageProviderKind.S3 ? "s3" : "local",
                settings.Bucket,
                versionRecord.StoragePrefix,
                settings.ServiceUrl,
                settings.ForcePathStyle),
            new SpaPayload(versionRecord.SpaEnabled, versionRecord.SpaFallback),
            new CachePayload(cacheSeconds, immutable));
    }
}
