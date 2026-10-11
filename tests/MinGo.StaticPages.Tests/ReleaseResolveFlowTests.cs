using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Abstractions;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Application.Services;
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
    private readonly ProjectService _projects;
    private readonly VersionService _versions;
    private readonly ReleaseService _releases;
    private readonly ResolveService _resolve;

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
        var settings = new FixedStorageSettings(_storageRoot);
        _projects = new ProjectService(_db);
        _versions = new VersionService(_db, _storage, settings);
        _releases = new ReleaseService(_db);
        _resolve = new ResolveService(_db, settings);
    }

    [Fact]
    public async Task Upload_publish_resolve_published_and_by_version()
    {
        var project = await _projects.CreateAsync(new CreateProjectRequest("docs", null, null));
        Assert.Equal(2, project.Channels.Count);

        await using var zip = CreateZip([("index.html", "<html>v1</html>")]);
        var v1 = await _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester");

        await using var zip2 = CreateZip([("index.html", "<html>v2</html>")]);
        var v2 = await _versions.UploadAsync(project.Id, zip2, new UploadVersionRequest(Number: "1.1.0", CommitSha: null, Branch: null, Message: null), "tester");

        // Not published yet
        var missing = await _resolve.ResolveAsync("docs", null, null);
        Assert.Null(missing);

        await _releases.PublishAsync(project.Id, new PublishRequest(v2.Id, "production"), "tester");

        var published = await _resolve.ResolveAsync("docs", null, null);
        Assert.NotNull(published);
        Assert.Equal("published", published!.Access);
        Assert.Equal(v2.Id, published.VersionId);
        Assert.False(published.Cache.Immutable);
        Assert.Equal(60, published.Cache.Seconds);

        var byVersion = await _resolve.ResolveAsync("docs", null, "1.0.0");
        Assert.NotNull(byVersion);
        Assert.Equal("version", byVersion!.Access);
        Assert.Equal(v1.Id, byVersion.VersionId);
        Assert.True(byVersion.Cache.Immutable);

        // Rollback to previous (1.0.0 was not released first; rollback after second publish)
        await _releases.PublishAsync(project.Id, new PublishRequest(v1.Id, "production"), "tester");
        await _releases.RollbackAsync(project.Id, new RollbackRequest("production"), "tester");
        var after = await _resolve.ResolveAsync("docs", null, null);
        Assert.NotNull(after);
        // rollback goes to previous release target which was v2
        Assert.Equal(v2.Id, after!.VersionId);
    }

    [Fact]
    public async Task Delete_published_version_blocked()
    {
        var project = await _projects.CreateAsync(new CreateProjectRequest("app", null, null));
        await using var zip = CreateZip([("index.html", "x")]);
        var v1 = await _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester");
        await _releases.PublishAsync(project.Id, new PublishRequest(v1.Id, "production"), "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _versions.DeleteAsync(v1.Id));
    }

    [Fact]
    public async Task Duplicate_project_name_rejected()
    {
        await _projects.CreateAsync(new CreateProjectRequest("dup", null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _projects.CreateAsync(new CreateProjectRequest("dup", null, null)));
    }

    [Fact]
    public async Task Zip_traversal_rejected_and_no_version_created()
    {
        var project = await _projects.CreateAsync(new CreateProjectRequest("evil", null, null));
        await using var zip = CreateZip([("../escape.txt", "bad")]);
        await Assert.ThrowsAsync<ZipValidationException>(() =>
            _versions.UploadAsync(project.Id, zip, new UploadVersionRequest(Number: "1.0.0", CommitSha: null, Branch: null, Message: null), "tester"));

        var versions = await _versions.ListAsync(project.Id);
        Assert.Empty(versions);
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

    private sealed class FixedStorageSettings(string localRoot) : IStorageSettingsService
    {
        public Task<StorageSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new StorageSettings
            {
                Provider = StorageProviderKind.Local,
                LocalRoot = localRoot,
                PublishedCacheSeconds = 60,
                VersionCacheSeconds = 31_536_000
            });

        public Task<StorageSettings> UpdateAsync(StorageSettings settings, CancellationToken ct = default) =>
            Task.FromResult(settings);

        public Task<bool> TestConnectivityAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
