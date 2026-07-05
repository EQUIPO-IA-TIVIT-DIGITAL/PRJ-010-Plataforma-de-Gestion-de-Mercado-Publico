using Microsoft.Extensions.Logging;

namespace MPM.Shared.Services;

public class LocalStorageService(ILogger<LocalStorageService> logger) : IStorageService
{
    private readonly ILogger<LocalStorageService> _logger = logger;
    private readonly string _basePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public async Task<string> UploadAsync(string path, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var dirPath = Path.Combine(_basePath, path);
        Directory.CreateDirectory(dirPath);
        var fullPath = Path.Combine(dirPath, fileName);
        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await content.CopyToAsync(stream, ct);
        _logger.LogInformation("Archivo guardado localmente: {Path}", fullPath);
        return fullPath;
    }

    public Task<Stream?> DownloadAsync(string fullPath, CancellationToken ct = default)
    {
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true));
    }

    public Task<bool> ExistsAsync(string fullPath, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(string fullPath, CancellationToken ct = default)
    {
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
