using Microsoft.AspNetCore.Http;
using Playgo.Application.Common.Interfaces;

namespace Playgo.Infrastructure.Services;

public class LocalizationContext : ILocalizationContext
{
    public const string DefaultLanguage = "en";
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "uz", "ru", "en",
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalizationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentLanguage
    {
        get
        {
            var http = _httpContextAccessor.HttpContext;
            if (http is null) return DefaultLanguage;

            if (http.Request.Query.TryGetValue("lang", out var queryValue))
            {
                var lang = queryValue.ToString();
                if (TryNormalize(lang, out var fromQuery))
                    return fromQuery;
            }

            var header = http.Request.Headers["Accept-Language"].ToString();
            if (!string.IsNullOrWhiteSpace(header))
            {
                foreach (var part in header.Split(','))
                {
                    var langOnly = part.Split(';')[0].Trim();
                    var primary = langOnly.Split('-')[0];
                    if (TryNormalize(primary, out var fromHeader))
                        return fromHeader;
                }
            }

            return DefaultLanguage;
        }
    }

    private static bool TryNormalize(string? input, out string normalized)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            var lower = input.Trim().ToLowerInvariant();
            if (Supported.Contains(lower))
            {
                normalized = lower;
                return true;
            }
        }
        normalized = DefaultLanguage;
        return false;
    }
}
