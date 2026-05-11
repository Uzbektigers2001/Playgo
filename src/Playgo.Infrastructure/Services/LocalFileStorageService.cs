using Microsoft.Extensions.Configuration;
using Playgo.Application.Common.Interfaces;

namespace Playgo.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _baseUrl;
    private readonly string _uploadPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _baseUrl = (configuration["FileStorage:BaseUrl"] ?? string.Empty).TrimEnd('/');
        _uploadPath = configuration["FileStorage:UploadPath"] ?? "uploads";
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder)
    {
        var safeFolder = SanitizeFolder(folder);
        var targetDirectory = string.IsNullOrEmpty(safeFolder)
            ? _uploadPath
            : Path.Combine(_uploadPath, safeFolder);

        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, uniqueName);

        await using var output = File.Create(fullPath);
        await fileStream.CopyToAsync(output);

        var relativePath = string.IsNullOrEmpty(safeFolder)
            ? uniqueName
            : $"{safeFolder}/{uniqueName}";

        return GetPublicUrl(relativePath);
    }

    public Task DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        var relative = !string.IsNullOrEmpty(_baseUrl)
                       && fileUrl.StartsWith(_baseUrl, StringComparison.OrdinalIgnoreCase)
            ? fileUrl.Substring(_baseUrl.Length).TrimStart('/')
            : fileUrl.TrimStart('/');

        var path = Path.Combine(_uploadPath, relative);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string filePath)
    {
        var trimmed = (filePath ?? string.Empty).TrimStart('/');
        return string.IsNullOrEmpty(_baseUrl) ? $"/{trimmed}" : $"{_baseUrl}/{trimmed}";
    }

    private static string SanitizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return string.Empty;
        var cleaned = folder.Replace("..", "").Replace('\\', '/').Trim('/', ' ');
        return cleaned;
    }
}
