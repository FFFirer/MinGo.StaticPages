namespace MinGo.StaticPages.Domain.Entities;

public enum StorageInstanceKind
{
    Local = 0,
    S3 = 1
}

public class StorageInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public StorageInstanceKind Kind { get; set; } = StorageInstanceKind.Local;

    // Local
    public string LocalRoot { get; set; } = "storage-data";

    // Shared key-root prefix (isomorphic for Local and S3)
    public string Prefix { get; set; } = string.Empty;

    // S3-compatible
    public string? ServiceUrl { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Bucket { get; set; } = "static-pages";
    public bool ForcePathStyle { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
