namespace MinGo.StaticPages.Domain.Entities;

public static class ChannelNames
{
    public const string Production = "production";
    public const string Preview = "preview";
    public static readonly string[] Defaults = [Production, Preview];
}

public class Channel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CurrentVersionId { get; set; }

    public Project? Project { get; set; }
    public VersionRecord? CurrentVersion { get; set; }
}

public enum ReleaseAction
{
    Publish = 0,
    Rollback = 1
}

public class ReleaseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public ReleaseAction Action { get; set; }
    public Guid? FromVersionId { get; set; }
    public Guid ToVersionId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Project? Project { get; set; }
}
