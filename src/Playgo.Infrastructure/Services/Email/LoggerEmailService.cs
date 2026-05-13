using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Playgo.Application.Common.Interfaces;

namespace Playgo.Infrastructure.Services.Email;

public class LoggerEmailService : IEmailService
{
    private readonly ILogger<LoggerEmailService> _logger;
    private readonly EmailSettings _settings;
    private readonly IHostEnvironment _env;

    public LoggerEmailService(
        ILogger<LoggerEmailService> logger,
        IOptions<EmailSettings> settings,
        IHostEnvironment env)
    {
        _logger = logger;
        _settings = settings.Value;
        _env = env;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL] To={To}; From={From}; Subject={Subject}\n---BODY---\n{Body}\n---END---",
            to, _settings.FromEmail, subject, htmlBody);
        return Task.CompletedTask;
    }

    public async Task SendTemplateAsync(string to, string templateName, IDictionary<string, string> placeholders, CancellationToken ct = default)
    {
        var html = await EmailTemplateLoader.LoadAsync(_env, _settings.TemplatesPath, templateName, placeholders, ct);
        var subject = placeholders.TryGetValue("subject", out var s) && !string.IsNullOrWhiteSpace(s)
            ? s
            : $"[Template:{templateName}]";
        await SendAsync(to, subject, html, ct);
    }
}
