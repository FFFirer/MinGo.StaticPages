using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MinGo.StaticPages.Application.Storage;

namespace MinGo.StaticPages.Infrastructure.Storage;

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _prefix;

    public S3ObjectStorage(string serviceUrl, string region, string accessKey, string secretKey, string bucket, string prefix, bool forcePathStyle)
    {
        _bucket = bucket;
        _prefix = prefix.Trim('/');

        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = forcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region,
            UseHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        };

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        }
        else
        {
            _client = new AmazonS3Client(config);
        }
    }

    private string FullKey(string key)
    {
        var k = key.Replace('\\', '/').TrimStart('/');
        return string.IsNullOrEmpty(_prefix) ? k : $"{_prefix}/{k}";
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = FullKey(key),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_bucket, FullKey(key), ct);
        var ms = new MemoryStream();
        await using (response.ResponseStream)
        {
            await response.ResponseStream.CopyToAsync(ms, ct);
        }
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, FullKey(key), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _client.DeleteObjectAsync(_bucket, FullKey(key), ct);

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        var full = FullKey(prefix).TrimEnd('/') + "/";
        string? token = null;
        do
        {
            var list = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = full,
                ContinuationToken = token
            }, ct);

            if (list.S3Objects.Count > 0)
            {
                await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucket,
                    Objects = list.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                }, ct);
            }
            token = list.IsTruncated == true ? list.NextContinuationToken : null;
        } while (token is not null);
    }

    public async Task<IReadOnlyList<ObjectInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var full = FullKey(prefix).TrimEnd('/') + "/";
        var results = new List<ObjectInfo>();
        string? token = null;
        do
        {
            var list = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = full,
                ContinuationToken = token
            }, ct);

            foreach (var o in list.S3Objects)
            {
                var rel = string.IsNullOrEmpty(_prefix) ? o.Key : o.Key[_prefix.Length..].TrimStart('/');
                results.Add(new ObjectInfo(rel, o.Size ?? 0, o.LastModified));
            }
            token = list.IsTruncated == true ? list.NextContinuationToken : null;
        } while (token is not null);

        return results;
    }

    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var meta = await _client.GetObjectMetadataAsync(_bucket, FullKey(key), ct);
            return meta.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return -1;
        }
    }

    public async Task<string?> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = FullKey(key),
            Expires = DateTime.UtcNow.Add(expiry)
        };
        return await _client.GetPreSignedURLAsync(request);
    }

    public void Dispose() => _client.Dispose();
}
