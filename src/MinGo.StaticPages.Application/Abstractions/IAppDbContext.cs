using MinGo.StaticPages.Domain.Entities;

namespace MinGo.StaticPages.Application.Abstractions;

public interface IAppDbContext
{
    IQueryable<Project> Projects { get; }
    IQueryable<VersionRecord> Versions { get; }
    IQueryable<VersionFile> VersionFiles { get; }
    IQueryable<Channel> Channels { get; }
    IQueryable<ReleaseRecord> Releases { get; }
    IQueryable<User> Users { get; }
    IQueryable<ApiKey> ApiKeys { get; }
    IQueryable<StorageSettings> StorageSettings { get; }
    IQueryable<StorageInstance> StorageInstances { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Global policy (limits / resolve auth / cache). Connection config lives on StorageInstance.</summary>
public interface IStorageSettingsService
{
    Task<StorageSettings> GetAsync(CancellationToken ct = default);
    Task<StorageSettings> UpdateAsync(StorageSettings settings, CancellationToken ct = default);
    Task<bool> TestConnectivityAsync(Guid storageInstanceId, CancellationToken ct = default);
}
