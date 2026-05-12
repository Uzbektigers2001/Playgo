using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Playgo.Application.Common.Interfaces;

namespace Playgo.Infrastructure.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IHostEnvironment _env;

    public SmtpEmailService(
        IOptions<EmailSettings> settings,
        ILogger<SmtpEmailService> logger,
        IHostEnvironment env)
    {
        _settings = settings.Value;
        _logger = logger;
        _env = env;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureSocket = _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_settings.Host, _settings.Port, secureSocket, ct);

            if (!string.IsNullOrEmpty(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }

    public async Task SendTemplateAsync(string to, string templateName, IDictionary<string, string> placeholders, CancellationToken ct = default)
    {
        var html = await EmailTemplateLoader.LoadAsync(_env, _settings.TemplatesPath, templateName, placeholders, ct);
        var subject = ResolveSubject(templateName, placeholders);
        await SendAsync(to, subject, html, ct);
    }

    private static string ResolveSubject(string templateName, IDictionary<string, string> placeholders)
    {
        if (placeholders.TryGetValue("subject", out var s) && !string.IsNullOrWhiteSpace(s)) return s;

        return templateName switch
        {
            "verify-email" => "Verify your Playgo email",
            "password-reset" => "Reset your Playgo password",
            "welcome" => "Welcome to Playgo",
            _ => "Playgo notification",
        };
    }
}
