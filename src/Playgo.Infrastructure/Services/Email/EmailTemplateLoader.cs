using Microsoft.Extensions.Hosting;

namespace Playgo.Infrastructure.Services.Email;

internal static class EmailTemplateLoader
{
    public static async Task<string> LoadAsync(
        IHostEnvironment env,
        string templatesPath,
        string templateName,
        IDictionary<string, string> placeholders,
        CancellationToken ct)
    {
        var fileName = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : templateName + ".html";

        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, templatesPath, fileName),
            Path.Combine(AppContext.BaseDirectory, templatesPath, fileName),
        };

        string? path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            throw new FileNotFoundException($"Email template '{fileName}' not found in any of the expected paths.");

        var html = await File.ReadAllTextAsync(path, ct);

        foreach (var (key, value) in placeholders)
            html = html.Replace("{{" + key + "}}", value ?? string.Empty);

        return html;
    }
}
