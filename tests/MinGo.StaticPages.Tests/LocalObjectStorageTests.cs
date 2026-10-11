using MinGo.StaticPages.Infrastructure.Storage;

namespace MinGo.StaticPages.Tests;

public class LocalObjectStorageTests
{
    [Fact]
    public async Task Upload_download_list_delete_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "mingo-storage-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalObjectStorage(root);
        try
        {
            var key = "projects/p1/versions/v1/files/index.html";
            await using (var content = new MemoryStream("<html>ok</html>"u8.ToArray()))
                await storage.UploadAsync(key, content, "text/html");

            Assert.True(await storage.ExistsAsync(key));
            await using var downloaded = await storage.DownloadAsync(key);
            using var reader = new StreamReader(downloaded);
            Assert.Equal("<html>ok</html>", await reader.ReadToEndAsync());

            var listed = await storage.ListAsync("projects/p1/versions/v1/files");
            Assert.Contains(listed, o => o.Key.EndsWith("index.html"));

            await storage.DeletePrefixAsync("projects/p1/versions/v1");
            Assert.False(await storage.ExistsAsync(key));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Rejects_path_traversal_key()
    {
        var root = Path.Combine(Path.GetTempPath(), "mingo-storage-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalObjectStorage(root);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await using var content = new MemoryStream("x"u8.ToArray());
                await storage.UploadAsync("../escape.txt", content, "text/plain");
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
