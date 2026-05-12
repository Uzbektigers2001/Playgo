using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Playgo.API.Extensions;
using Playgo.API.Middleware;
using Playgo.Application;
using Playgo.Infrastructure;
using Playgo.Infrastructure.Identity;
using Playgo.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Suppress default model validation (validators registered via Application; manual call sites use them)
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

// JWT Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("JwtSettings");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    opts.AddPolicy("AdminOrModerator", p => p.RequireRole("Admin", "Moderator"));
});

// Swagger
builder.Services.AddSwaggerWithJwt();

// CORS
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    p.WithOrigins(origins)
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials();
}));

// Response compression
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

// Rate limiting (.NET 8 native)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("Login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));

    options.AddPolicy("Register", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));

    options.AddPolicy("Upload", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.Identity?.Name
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));

    options.AddPolicy("Default", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.Identity?.Name
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, tags: new[] { "ready" })
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, tags: new[] { "ready" });

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()],
});

app.MapControllers().RequireRateLimiting("Default");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Auto-migrate in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// Setup Hangfire recurring jobs
HangfireJobsSetup.SetupRecurringJobs(app.Services);

app.Run();

public partial class Program { }
