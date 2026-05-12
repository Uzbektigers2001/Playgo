using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Playgo.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Playgo API",
                Version = "v1",
                Description = "Playgo video streaming platform — REST API.\n\n" +
                              "JWT Bearer auth: log in via `POST /api/auth/login`, then click **Authorize** and paste the access token.",
                Contact = new OpenApiContact
                {
                    Name = "Playgo Team",
                    Email = "support@playgo.uz",
                },
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Bearer token. Example: \"Authorization: Bearer {token}\".",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });

            // Pull XML docs from API + Application + Domain so action / DTO comments are surfaced.
            foreach (var fileName in new[] { "Playgo.API.xml", "Playgo.Application.xml", "Playgo.Domain.xml" })
            {
                var path = Path.Combine(AppContext.BaseDirectory, fileName);
                if (File.Exists(path))
                    options.IncludeXmlComments(path, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();
            options.UseInlineDefinitionsForEnums();

            // Use full type names in schemaIds so DTOs from different namespaces don't collide.
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));

            options.OrderActionsBy(api => $"{api.GroupName}_{api.RelativePath}");
        });

        return services;
    }
}
