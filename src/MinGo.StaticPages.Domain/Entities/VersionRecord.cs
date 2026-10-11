namespace MinGo.StaticPages.Domain.Entities;

public enum VersionStatus
{
    Active = 0,
    Deleted = 1
}

public class VersionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string StoragePrefix { get; set; } = string.Empty;
    public string? PackageKey { get; set; }
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public VersionStatus Status { get; set; } = VersionStatus.Active;
    public bool SpaEnabled { get; set; } = true;
    public string SpaFallback { get; set; } = "index.html";
    public string? CommitSha { get; set; }
    public string? Branch { get; set; }
    public string? Message { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Project? Project { get; set; }
    public List<VersionFile> Files { get; set; } = [];
}

public class VersionFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Sha256 { get; set; }

    public VersionRecord? Version { get; set; }
}
