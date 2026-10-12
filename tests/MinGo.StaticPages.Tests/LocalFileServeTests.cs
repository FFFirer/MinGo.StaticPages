using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure.Data;
using MinGo.StaticPages.Web;

namespace MinGo.StaticPages.Tests;

public class LocalFileServeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _root;
    private readonly AppDbContext _db;
    private readonly Guid _localInstanceId;
    private readonly Guid _s3InstanceId;
    private readonly Guid _localProjectId;
    private readonly Guid _s3ProjectId;
    private readonly Guid _versionId;

    public LocalFileServeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mingo-fp-{Guid.NewGuid():N}.db");
        _root = Path.Combine(Path.GetTempPath(), $"mingo-fp-store-{Guid.NewGuid():N}");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var localInst = new StorageInstance
        {
            Name = "local",
            Kind = StorageInstanceKind.Local,
            LocalRoot = _root,
            Prefix = "www"
        };
        var s3Inst = new StorageInstance
        {
            Name = "minio",
            Kind = StorageInstanceKind.S3,
            ServiceUrl = "http://127.0.0.1:9000",
            Bucket = "static-pages",
            Prefix = "t"
        };
        _db.AddRange(localInst, s3Inst);
        _db.SaveChanges();
        _localInstanceId = localInst.Id;
        _s3InstanceId = s3Inst.Id;

        var localProject = new Project { Name = "site", StorageInstanceId = _localInstanceId };
        var s3Project = new Project { Name = "remote", StorageInstanceId = _s3InstanceId };
        _db.AddRange(localProject, s3Project);
        _db.SaveChanges();
        _localProjectId = localProject.Id;
        _s3ProjectId = s3Project.Id;

        foreach (var pid in new[] { _localProjectId, _s3ProjectId })
        {
            _db.Add(new Channel { ProjectId = pid, Name = ChannelNames.Production });
            _db.Add(new Channel { ProjectId = pid, Name = ChannelNames.Preview });
        }
        _db.SaveChanges();

        _versionId = Guid.NewGuid();
        var version = new VersionRecord
        {
            Id = _versionId,
            ProjectId = _localProjectId,
            Number = "1.0.0",
            StoragePrefix = $"projects/{_localProjectId}/versions/{_versionId}/files",
            SpaEnabled = true,
            SpaFallback = "index.html"
        };
        version.Files.Add(new VersionFile
        {
            VersionId = _versionId,
            RelativePath = "index.html",
            SizeBytes = 10,
            ContentType = "text/html; charset=utf-8"
        });
        _db.Add(version);
        _db.SaveChanges();

        var ch = _db.Channels.First(c => c.ProjectId == _localProjectId && c.Name == ChannelNames.Production);
        ch.CurrentVersionId = _versionId;
        _db.SaveChanges();

        var filesDir = Path.Combine(_root, "www", "projects", _localProjectId.ToString(), "versions", _versionId.ToString(), "files");
        Directory.CreateDirectory(filesDir);
        File.WriteAllText(Path.Combine(filesDir, "index.html"), "<html>hi</html>");
    }

    [Fact]
    public async Task Serves_published_local_file()
    {
        var http = new DefaultHttpContext();
        var result = await LocalFileServe.ServeAsync(http, _db, "site", null, "index.html", byVersion: false, default);
        var file = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult>(result);
        Assert.Equal("text/html; charset=utf-8", file.ContentType);
    }

    [Fact]
    public async Task Spa_fallback_for_extensionless_path()
    {
        var http = new DefaultHttpContext();
        var result = await LocalFileServe.ServeAsync(http, _db, "site", null, "dashboard/settings", byVersion: false, default);
        var file = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult>(result);
        Assert.EndsWith("index.html", file.FileName);
    }

    [Fact]
    public async Task Rejects_path_traversal()
    {
        var http = new DefaultHttpContext();
        var result = await LocalFileServe.ServeAsync(http, _db, "site", null, "../secret", byVersion: false, default);
        Assert.False(result is Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult);
    }

    [Fact]
    public async Task S3_project_not_served()
    {
        var http = new DefaultHttpContext();
        var result = await LocalFileServe.ServeAsync(http, _db, "remote", null, "index.html", byVersion: false, default);
        Assert.False(result is Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult);
    }

    [Fact]
    public async Task Missing_version_not_found()
    {
        var http = new DefaultHttpContext();
        var result = await LocalFileServe.ServeAsync(http, _db, "site", "9.9.9", "index.html", byVersion: true, default);
        Assert.False(result is Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
