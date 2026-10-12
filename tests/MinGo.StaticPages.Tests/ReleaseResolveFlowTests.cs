using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Application.Services;
using MinGo.StaticPages.Application.Storage;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure.Data;
using MinGo.StaticPages.Infrastructure.Storage;

namespace MinGo.StaticPages.Tests;

/// <summary>
/// End-to-end style tests against SQLite + LocalObjectStorage (no web host required).
/// </summary>
public class ReleaseResolveFlowTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _storageRoot;
    private readonly AppDbContext _db;
    private readonly LocalObjectStorage _storage;
    private readonly FixedStorageFactory _factory;
    private readonly ProjectService _projects;
    private readonly VersionService _versions;
    private readonly ReleaseService _releases;
    private readonly ResolveService _resolve;
    private readonly StorageInstanceService _instances;
    private readonly Guid _instanceId;

    public ReleaseResolveFlowTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mingo-{Guid.NewGuid():N}.db");
        _storageRoot = Path.Combine(Path.GetTempPath(), $"mingo-store-{Guid.NewGuid():N}");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _storage = new LocalObjectStorage(_storageRoot);
        var settings = new FixedStorageSettings();
        _factory = new FixedStorageFactory(_storage);
        _projects = new ProjectService(_db);
        _versions = new VersionService(_db, _factory, settings);
        _releases = new ReleaseService(_db);
        _resolve = new ResolveService(_db, settings);
        _instances = new StorageInstanceService(_db);

        var instance = new StorageInstance
        {
            Name = "local",
            Kind = StorageInstanceKind.Local,
            LocalRoot = _storageRoot,
            Prefix = string.Empty
        };
        _db.Add(instance);
        _db.SaveChanges();
        _instanceId = instance.Id;
    }

    private async Task<ProjectDto> CreateProjectAsync(string name)
        => await _projects.CreateAsync(new CreateProjectRequest(name, _instanceId));

    [Fact]
    public async Task Upload_publish_resolve_published_and_by_version()
    {
        var project = await CreateProjectAsync("docs");
        Assert.Equal(2, project.Channels.Count);
        Assert.Equal(_instanceId, project.StorageInstanceId);
        Assert.Equal("local", project.StorageInstanceName);

        await using var zip = CreateZip([("index.html", "<html>v1</html>")]);
        var v1 = await _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester");

        await using var zip2 = CreateZip([("index.html", "<html>v2</html>")]);
        var v2 = await _versions.UploadAsync(project.Id, zip2, new UploadVersionRequest(Number: "1.1.0", CommitSha: null, Branch: null, Message: null), "tester");

        var missing = await _resolve.ResolveAsync("docs", null, null);
        Assert.Null(missing);

        await _releases.PublishAsync(project.Id, new PublishRequest(v2.Id, "production"), "tester");

        var published = await _resolve.ResolveAsync("docs", null, null);
        Assert.NotNull(published);
        Assert.Equal("published", published!.Access);
        Assert.Equal(v2.Id, published.VersionId);
        Assert.False(published.Cache.Immutable);
        Assert.Equal(60, published.Cache.Seconds);
        Assert.Equal("local", published.Storage.Provider);
        Assert.Equal("/s/docs", published.Storage.Endpoint);
        Assert.Equal("local", published.Storage.Instance);

        var byVersion = await _resolve.ResolveAsync("docs", null, "1.0.0");
        Assert.NotNull(byVersion);
        Assert.Equal("version", byVersion!.Access);
        Assert.Equal(v1.Id, byVersion.VersionId);
        Assert.True(byVersion.Cache.Immutable);

        await _releases.PublishAsync(project.Id, new PublishRequest(v1.Id, "production"), "tester");
        await _releases.RollbackAsync(project.Id, new RollbackRequest("production"), "tester");
        var after = await _resolve.ResolveAsync("docs", null, null);
        Assert.NotNull(after);
        Assert.Equal(v2.Id, after!.VersionId);
    }

    [Fact]
    public async Task Create_project_requires_storage_instance()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _projects.CreateAsync(new CreateProjectRequest("orphan", Guid.Empty)));
    }

    [Fact]
    public async Task Delete_published_version_blocked()
    {
        var project = await CreateProjectAsync("app");
        await using var zip = CreateZip([("index.html", "x")]);
        var v1 = await _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester");
        await _releases.PublishAsync(project.Id, new PublishRequest(v1.Id, "production"), "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _versions.DeleteAsync(v1.Id));
    }

    [Fact]
    public async Task Duplicate_project_name_rejected()
    {
        await CreateProjectAsync("dup");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProjectAsync("dup"));
    }

    [Fact]
    public async Task Zip_traversal_rejected_and_no_version_created()
    {
        var project = await CreateProjectAsync("evil");
        await using var zip = CreateZip([("../escape.txt", "bad")]);
        await Assert.ThrowsAsync<ZipValidationException>(() =>
            _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester"));

        var versions = await _versions.ListAsync(project.Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task Two_local_instances_write_disjoint_locations()
    {
        var rootA = Path.Combine(Path.GetTempPath(), $"mingo-a-{Guid.NewGuid():N}");
        var rootB = Path.Combine(Path.GetTempPath(), $"mingo-b-{Guid.NewGuid():N}");
        try
        {
            var instA = new StorageInstance { Name = "a", Kind = StorageInstanceKind.Local, LocalRoot = rootA, Prefix = "www" };
            var instB = new StorageInstance { Name = "b", Kind = StorageInstanceKind.Local, LocalRoot = rootB, Prefix = "sites" };
            _db.AddRange(instA, instB);
            await _db.SaveChangesAsync();

            var storageA = new LocalObjectStorage(rootA, "www");
            var storageB = new LocalObjectStorage(rootB, "sites");
            await storageA.UploadAsync("projects/p/versions/v/files/index.html", new MemoryStream("A"u8.ToArray()), "text/html");
            await storageB.UploadAsync("projects/p/versions/v/files/index.html", new MemoryStream("B"u8.ToArray()), "text/html");

            Assert.True(File.Exists(Path.Combine(rootA, "www", "projects", "p", "versions", "v", "files", "index.html")));
            Assert.True(File.Exists(Path.Combine(rootB, "sites", "projects", "p", "versions", "v", "files", "index.html")));
            Assert.False(File.Exists(Path.Combine(rootA, "sites", "projects", "p", "versions", "v", "files", "index.html")));
        }
        finally
        {
            try { if (Directory.Exists(rootA)) Directory.Delete(rootA, true); } catch { }
            try { if (Directory.Exists(rootB)) Directory.Delete(rootB, true); } catch { }
        }
    }

    [Fact]
    public async Task Instance_delete_blocked_when_referenced()
    {
        await CreateProjectAsync("bound");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _instances.DeleteAsync(_instanceId));
    }

    [Fact]
    public async Task Resolve_payload_has_no_secrets()
    {
        var project = await CreateProjectAsync("sec");
        await using var zip = CreateZip([("index.html", "x")]);
        var v1 = await _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester");
        await _releases.PublishAsync(project.Id, new PublishRequest(v1.Id, "production"), "tester");

        var resolved = await _resolve.ResolveAsync("sec", null, null);
        Assert.NotNull(resolved);
        var json = System.Text.Json.JsonSerializer.Serialize(resolved);
        Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_storageRoot, json);
    }

    private static MemoryStream CreateZip((string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var s = entry.Open();
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                s.Write(bytes);
            }
        }
        ms.Position = 0;
        return ms;
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true); } catch { }
    }

    private sealed class FixedStorageSettings : IStorageSettingsService
    {
        public Task<StorageSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new StorageSettings
            {
                PublishedCacheSeconds = 60,
                VersionCacheSeconds = 31_536_000
            });

        public Task<StorageSettings> UpdateAsync(StorageSettings settings, CancellationToken ct = default) =>
            Task.FromResult(settings);

        public Task<bool> TestConnectivityAsync(Guid storageInstanceId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FixedStorageFactory(IObjectStorage storage) : IStorageFactory
    {
        public Task<IObjectStorage> ForInstanceAsync(Guid instanceId, CancellationToken ct = default)
            => Task.FromResult(storage);

        public Task<IObjectStorage> ForProjectAsync(Guid projectId, CancellationToken ct = default)
            => Task.FromResult(storage);

        public Task<IObjectStorage> ForInstanceAsync(StorageInstance instance, CancellationToken ct = default)
            => Task.FromResult(storage);
    }
}
