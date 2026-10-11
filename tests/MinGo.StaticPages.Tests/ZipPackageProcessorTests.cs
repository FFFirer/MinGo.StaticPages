using System.IO.Compression;
using System.Text;
using MinGo.StaticPages.Application.Services;

namespace MinGo.StaticPages.Tests;

public class ZipPackageProcessorTests
{
    [Theory]
    [InlineData("index.html", true)]
    [InlineData("assets/app.js", true)]
    [InlineData("a/b/c.png", true)]
    [InlineData("../evil.txt", false)]
    [InlineData("a/../b.txt", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("C:\\windows\\system32", false)]
    public void IsSafeRelativePath_rejects_traversal(string path, bool expected)
    {
        var ok = ZipPackageProcessor.IsSafeRelativePath(path, out var normalized);
        Assert.Equal(expected, ok);
        if (expected)
            Assert.DoesNotContain("..", normalized);
    }

    [Fact]
    public async Task ProcessAsync_rejects_path_traversal_entries()
    {
        await using var zip = CreateZip([("../evil.txt", "bad")]);
        var uploaded = new List<string>();
        await Assert.ThrowsAsync<ZipValidationException>(() => ZipPackageProcessor.ProcessAsync(
            zip, 100, 1024 * 1024,
            (rel, _, _, _) => { uploaded.Add(rel); return Task.CompletedTask; },
            (_, _, _, _) => Task.CompletedTask));
        Assert.Empty(uploaded);
    }

    [Fact]
    public async Task ProcessAsync_rejects_too_many_files()
    {
        var entries = Enumerable.Range(0, 5).Select(i => ($"f{i}.txt", "x")).ToArray();
        await using var zip = CreateZip(entries);
        await Assert.ThrowsAsync<ZipValidationException>(() => ZipPackageProcessor.ProcessAsync(
            zip, 3, 1024 * 1024,
            (_, _, _, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task ProcessAsync_extracts_valid_package()
    {
        await using var zip = CreateZip([("index.html", "<html>hi</html>"), ("a/app.js", "console.log(1)")]);
        var files = new List<string>();
        var packageSaved = false;

        var result = await ZipPackageProcessor.ProcessAsync(
            zip, 100, 1024 * 1024,
            (rel, _, _, _) => { files.Add(rel); return Task.CompletedTask; },
            (_, _, _, _) => { packageSaved = true; return Task.CompletedTask; });

        Assert.True(packageSaved);
        Assert.Equal(2, result.Count);
        Assert.Contains("index.html", files);
        Assert.Contains("a/app.js", files);
        Assert.Equal("text/html; charset=utf-8", result.First(f => f.RelativePath == "index.html").ContentType);
    }

    private static MemoryStream CreateZip((string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var s = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes);
            }
        }
        ms.Position = 0;
        return ms;
    }
}
