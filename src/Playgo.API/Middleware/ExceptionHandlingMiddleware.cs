using FluentValidation;

namespace Playgo.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        int statusCode;
        string message;
        List<string>? errors = null;

        switch (ex)
        {
            case ValidationException ve:
                statusCode = StatusCodes.Status400BadRequest;
                message = "Validation failed.";
                errors = ve.Errors.Select(e => e.ErrorMessage).ToList();
                break;
            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                message = "Unauthorized.";
                break;
            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                message = "Resource not found.";
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                message = _environment.IsDevelopment()
                    ? ex.Message
                    : "An unexpected error occurred.";
                break;
        }

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            error = message,
            statusCode,
            traceId = context.TraceIdentifier,
            errors,
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
