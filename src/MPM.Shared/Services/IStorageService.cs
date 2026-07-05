namespace MPM.Shared.Services;

public interface IStorageService
{
    Task<string> UploadAsync(string path, string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string fullPath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string fullPath, CancellationToken ct = default);
    Task DeleteAsync(string fullPath, CancellationToken ct = default);
}
