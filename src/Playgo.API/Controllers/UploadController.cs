using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.Common.Interfaces;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize(Roles = "Admin")]
public class UploadController : ControllerBase
{
    private const long MaxImageBytes = 10L * 1024 * 1024;
    private const long MaxVideoBytes = 2L * 1024 * 1024 * 1024;

    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedVideoTypes = { "video/mp4", "video/webm" };

    private readonly IFileStorageService _fileStorage;

    public UploadController(IFileStorageService fileStorage)
    {
        _fileStorage = fileStorage;
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        var error = ValidateFile(file, MaxImageBytes, AllowedImageTypes);
        if (error is not null) return BadRequest(new { error });

        await using var stream = file!.OpenReadStream();
        var url = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "images");
        return Ok(new { url });
    }

    [HttpPost("video")]
    [RequestSizeLimit(MaxVideoBytes)]
    public async Task<IActionResult> UploadVideo(IFormFile? file, CancellationToken ct)
    {
        var error = ValidateFile(file, MaxVideoBytes, AllowedVideoTypes);
        if (error is not null) return BadRequest(new { error });

        await using var stream = file!.OpenReadStream();
        var url = await _fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "videos");
        return Ok(new { url });
    }

    private static string? ValidateFile(IFormFile? file, long maxBytes, string[] allowedContentTypes)
    {
        if (file is null || file.Length == 0)
            return "No file uploaded.";

        if (file.Length > maxBytes)
            return $"File exceeds maximum allowed size of {maxBytes} bytes.";

        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return $"Content type '{file.ContentType}' is not allowed.";

        return null;
    }
}
