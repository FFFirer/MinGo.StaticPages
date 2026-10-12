using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Application.Services;

public sealed class StorageInstanceService(IAppDbContext db)
{
    public async Task<IReadOnlyList<StorageInstanceDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await db.StorageInstances.OrderBy(i => i.Name).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<StorageInstanceDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var i = await db.StorageInstances.FirstOrDefaultAsync(x => x.Id == id, ct);
        return i is null ? null : ToDto(i);
    }

    public async Task<StorageInstanceDto> CreateAsync(CreateStorageInstanceRequest req, CancellationToken ct = default)
    {
        var kind = ParseKind(req.Kind);
        Validate(kind, req.LocalRoot, req.ServiceUrl, req.Bucket, req.Name);

        var name = req.Name.Trim().ToLowerInvariant();
        if (await db.StorageInstances.AnyAsync(i => i.Name == name, ct))
            throw new InvalidOperationException($"Storage instance '{name}' already exists.");

        var entity = new StorageInstance
        {
            Name = name,
            Kind = kind,
            LocalRoot = string.IsNullOrWhiteSpace(req.LocalRoot) ? "storage-data" : req.LocalRoot.Trim(),
            Prefix = NormalizePrefix(req.Prefix),
            ServiceUrl = req.ServiceUrl?.Trim(),
            Region = string.IsNullOrWhiteSpace(req.Region) ? "us-east-1" : req.Region.Trim(),
            AccessKey = req.AccessKey,
            SecretKey = req.SecretKey,
            Bucket = string.IsNullOrWhiteSpace(req.Bucket) ? "static-pages" : req.Bucket.Trim(),
            ForcePathStyle = req.ForcePathStyle
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<StorageInstanceDto> UpdateAsync(Guid id, UpdateStorageInstanceRequest req, CancellationToken ct = default)
    {
        var entity = await db.StorageInstances.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Storage instance not found.");

        Validate(entity.Kind, req.LocalRoot, req.ServiceUrl, req.Bucket, req.Name);

        var name = req.Name.Trim().ToLowerInvariant();
        if (await db.StorageInstances.AnyAsync(i => i.Name == name && i.Id != id, ct))
            throw new InvalidOperationException($"Storage instance '{name}' already exists.");

        entity.Name = name;
        if (entity.Kind == StorageInstanceKind.Local && !string.IsNullOrWhiteSpace(req.LocalRoot))
            entity.LocalRoot = req.LocalRoot.Trim();
        entity.Prefix = NormalizePrefix(req.Prefix);
        if (entity.Kind == StorageInstanceKind.S3)
        {
            entity.ServiceUrl = req.ServiceUrl?.Trim();
            entity.Region = string.IsNullOrWhiteSpace(req.Region) ? "us-east-1" : req.Region.Trim();
            if (!string.IsNullOrEmpty(req.AccessKey))
                entity.AccessKey = req.AccessKey;
            if (!string.IsNullOrEmpty(req.SecretKey))
                entity.SecretKey = req.SecretKey;
            if (!string.IsNullOrWhiteSpace(req.Bucket))
                entity.Bucket = req.Bucket.Trim();
            entity.ForcePathStyle = req.ForcePathStyle;
        }
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.StorageInstances.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Storage instance not found.");

        var referenced = await db.Projects.AnyAsync(p => p.StorageInstanceId == id && !p.IsDeleted, ct);
        if (referenced)
            throw new InvalidOperationException("Storage instance is referenced by one or more projects.");

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public static StorageInstanceKind ParseKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "local" => StorageInstanceKind.Local,
        "s3" => StorageInstanceKind.S3,
        _ => throw new InvalidOperationException("Kind must be 'local' or 's3'.")
    };

    private static void Validate(StorageInstanceKind kind, string? localRoot, string? serviceUrl, string bucket, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Instance name is required.");
        if (kind == StorageInstanceKind.Local && string.IsNullOrWhiteSpace(localRoot))
            throw new InvalidOperationException("LocalRoot is required for local instances.");
        if (kind == StorageInstanceKind.S3)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new InvalidOperationException("ServiceURL is required for S3.");
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("Bucket is required for S3.");
        }
    }

    private static string NormalizePrefix(string? prefix)
        => (prefix ?? string.Empty).Replace('\\', '/').Trim('/');

    private static StorageInstanceDto ToDto(StorageInstance i) => new(
        i.Id,
        i.Name,
        i.Kind == StorageInstanceKind.S3 ? "s3" : "local",
        i.Kind == StorageInstanceKind.Local ? i.LocalRoot : null,
        i.Prefix,
        i.ServiceUrl,
        i.Region,
        i.AccessKey,
        i.Bucket,
        i.ForcePathStyle,
        i.CreatedAt,
        i.UpdatedAt);
}
