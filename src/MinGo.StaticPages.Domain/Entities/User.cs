namespace MinGo.StaticPages.Domain.Entities;

public static class RoleNames
{
    public const string Admin = "admin";
    public const string Publisher = "publisher";
    public const string Viewer = "viewer";
    public static readonly string[] All = [Admin, Publisher, Viewer];
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = RoleNames.Viewer;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string Role { get; set; } = RoleNames.Publisher;
    public Guid? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}
