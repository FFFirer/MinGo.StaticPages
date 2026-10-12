namespace MinGo.StaticPages.Application.Dtos;

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? Description,
    Guid StorageInstanceId,
    string StorageInstanceName,
    int VersionCount,
    long StorageBytes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ChannelDto> Channels);

public sealed record ChannelDto(string Name, Guid? CurrentVersionId, string? CurrentVersionNumber);

public sealed record CreateProjectRequest(
    string Name,
    Guid StorageInstanceId,
    string? DisplayName = null,
    string? Description = null);

public sealed record VersionDto(
    Guid Id,
    Guid ProjectId,
    string Number,
    VersionStatusDto Status,
    long SizeBytes,
    int FileCount,
    bool SpaEnabled,
    string SpaFallback,
    string? CommitSha,
    string? Branch,
    string? Message,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public enum VersionStatusDto
{
    Active = 0,
    Deleted = 1
}

public sealed record VersionDetailDto(
    VersionDto Version,
    IReadOnlyList<VersionFileDto> Files);

public sealed record VersionFileDto(string RelativePath, long SizeBytes, string ContentType);

public sealed record UploadVersionRequest(
    string? Number,
    string? CommitSha,
    string? Branch,
    string? Message,
    bool SpaEnabled = true,
    string SpaFallback = "index.html");

public sealed record PublishRequest(Guid VersionId, string Channel = "production");
public sealed record RollbackRequest(string Channel = "production", Guid? ToVersionId = null);

public sealed record ReleaseDto(
    Guid Id,
    string ChannelName,
    string Action,
    Guid? FromVersionId,
    Guid ToVersionId,
    string OperatorName,
    DateTimeOffset CreatedAt,
    string? ToVersionNumber = null,
    string? FromVersionNumber = null);

public sealed record ResolveResponse(
    string Project,
    Guid ProjectId,
    Guid VersionId,
    string VersionNumber,
    string Access,
    string? Channel,
    StoragePayload Storage,
    SpaPayload Spa,
    CachePayload Cache);

public sealed record StoragePayload(
    string Provider,
    string? Bucket,
    string Prefix,
    string? Endpoint,
    bool ForcePathStyle,
    string? Instance = null);

public sealed record SpaPayload(bool Enabled, string Fallback);

public sealed record CachePayload(int Seconds, bool Immutable);

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string Username, string Role);

public sealed record CreateApiKeyRequest(string Name, string Role);
public sealed record ApiKeyCreatedDto(Guid Id, string Name, string Role, string KeyPrefix, string PlaintextKey, DateTimeOffset CreatedAt);
public sealed record ApiKeyDto(Guid Id, string Name, string Role, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

/// <summary>Global policy only. Connection fields live on StorageInstanceDto.</summary>
public sealed record StorageSettingsDto(
    int MaxPackageBytes,
    int MaxFileCount,
    int MaxSingleFileBytes,
    bool ResolveRequireAuth,
    int PublishedCacheSeconds,
    int VersionCacheSeconds);

public sealed record UpdateStorageSettingsRequest(
    int MaxPackageBytes,
    int MaxFileCount,
    int MaxSingleFileBytes,
    bool ResolveRequireAuth,
    int PublishedCacheSeconds,
    int VersionCacheSeconds);

public sealed record StorageInstanceDto(
    Guid Id,
    string Name,
    string Kind,
    string? LocalRoot,
    string Prefix,
    string? ServiceUrl,
    string Region,
    string? AccessKey,
    string Bucket,
    bool ForcePathStyle,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateStorageInstanceRequest(
    string Name,
    string Kind,
    string? LocalRoot = null,
    string Prefix = "",
    string? ServiceUrl = null,
    string Region = "us-east-1",
    string? AccessKey = null,
    string? SecretKey = null,
    string Bucket = "static-pages",
    bool ForcePathStyle = true);

public sealed record UpdateStorageInstanceRequest(
    string Name,
    string? LocalRoot = null,
    string Prefix = "",
    string? ServiceUrl = null,
    string Region = "us-east-1",
    string? AccessKey = null,
    string? SecretKey = null,
    string Bucket = "static-pages",
    bool ForcePathStyle = true);
