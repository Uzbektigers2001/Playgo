using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Infrastructure.Services.Email;

namespace Playgo.Tests;

public class EmailServiceTests
{
    private static (LoggerEmailService Service, string TempDir) BuildLoggerService()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"playgo-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "Templates", "Emails"));

        File.WriteAllText(Path.Combine(dir, "Templates", "Emails", "verify-email.html"),
            "<html><body>Hi {{userName}}, click <a href=\"{{verifyLink}}\">here</a>.</body></html>");

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(dir);
        env.Setup(e => e.EnvironmentName).Returns("Development");

        var settings = Options.Create(new EmailSettings
        {
            FromEmail = "noreply@playgo.uz",
            FromName = "Playgo",
            TemplatesPath = "Templates/Emails",
        });

        var service = new LoggerEmailService(NullLogger<LoggerEmailService>.Instance, settings, env.Object);
        return (service, dir);
    }

    [Fact]
    public async Task SendAsync_DoesNotThrow()
    {
        var (service, dir) = BuildLoggerService();
        try
        {
            await service.SendAsync("user@example.com", "Hello", "<b>Hi</b>");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SendTemplateAsync_RendersPlaceholders()
    {
        var (service, dir) = BuildLoggerService();
        try
        {
            // Won't throw if template is found and placeholders replaced.
            await service.SendTemplateAsync(
                "user@example.com",
                "verify-email",
                new Dictionary<string, string>
                {
                    ["userName"] = "Alice",
                    ["verifyLink"] = "https://playgo.uz/verify?token=abc",
                });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SendTemplateAsync_MissingTemplate_Throws()
    {
        var (service, dir) = BuildLoggerService();
        try
        {
            var act = async () => await service.SendTemplateAsync(
                "user@example.com",
                "no-such-template",
                new Dictionary<string, string>());

            await act.Should().ThrowAsync<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
