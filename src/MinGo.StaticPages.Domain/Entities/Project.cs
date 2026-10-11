namespace MinGo.StaticPages.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }

    public List<VersionRecord> Versions { get; set; } = [];
    public List<Channel> Channels { get; set; } = [];
    public List<ReleaseRecord> Releases { get; set; } = [];
}
