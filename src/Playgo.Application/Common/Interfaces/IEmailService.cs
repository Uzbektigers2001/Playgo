namespace Playgo.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplateAsync(string to, string templateName, IDictionary<string, string> placeholders, CancellationToken ct = default);
}

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@playgo.uz";
    public string FromName { get; set; } = "Playgo";
    public string TemplatesPath { get; set; } = "Templates/Emails";
}
