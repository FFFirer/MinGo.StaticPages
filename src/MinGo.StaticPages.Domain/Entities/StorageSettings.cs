namespace MinGo.StaticPages.Domain.Entities;

public enum StorageProviderKind
{
    Local = 0,
    S3 = 1
}

public class StorageSettings
{
    public int Id { get; set; } = 1;
    public StorageProviderKind Provider { get; set; } = StorageProviderKind.Local;
    public string? ServiceUrl { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Bucket { get; set; } = "static-pages";
    public string Prefix { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = true;
    public string LocalRoot { get; set; } = "storage-data";
    public int MaxPackageBytes { get; set; } = 200 * 1024 * 1024;
    public int MaxFileCount { get; set; } = 10_000;
    public int MaxSingleFileBytes { get; set; } = 50 * 1024 * 1024;
    public bool ResolveRequireAuth { get; set; }
    public int PublishedCacheSeconds { get; set; } = 60;
    public int VersionCacheSeconds { get; set; } = 31_536_000;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
