using Google.Cloud.Storage.V1;
using MPM.Shared.Services;

namespace MPM.Api.Services;

public class GcsStorageService : IStorageService
{
    private readonly StorageClient _storage;
    private readonly string _bucketName;

    public GcsStorageService(StorageClient storage, string bucketName)
    {
        _storage = storage;
        _bucketName = bucketName;
    }

    public async Task<string> UploadAsync(string path, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var blobName = $"{path}/{fileName}".TrimStart('/');
        var obj = await _storage.UploadObjectAsync(_bucketName, blobName, contentType, content, cancellationToken: ct);
        return $"gs://{_bucketName}/{blobName}";
    }

    public async Task<Stream?> DownloadAsync(string fullPath, CancellationToken ct = default)
    {
        if (!fullPath.StartsWith("gs://"))
            return null;

        var parts = fullPath.Replace("gs://", "").Split('/', 2);
        if (parts.Length < 2) return null;

        var bucket = parts[0];
        var objectName = parts[1];

        var stream = new MemoryStream();
        try
        {
            await _storage.DownloadObjectAsync(bucket, objectName, stream, cancellationToken: ct);
            stream.Position = 0;
            return stream;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string fullPath, CancellationToken ct = default)
    {
        if (!fullPath.StartsWith("gs://"))
            return false;

        var parts = fullPath.Replace("gs://", "").Split('/', 2);
        if (parts.Length < 2) return false;

        try
        {
            var obj = await _storage.GetObjectAsync(parts[0], parts[1], cancellationToken: ct);
            return obj != null;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string fullPath, CancellationToken ct = default)
    {
        if (!fullPath.StartsWith("gs://"))
            return;

        var parts = fullPath.Replace("gs://", "").Split('/', 2);
        if (parts.Length < 2) return;

        await _storage.DeleteObjectAsync(parts[0], parts[1], cancellationToken: ct);
    }
}
