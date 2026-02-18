using Microsoft.OpenApi.Models;

namespace LogMyDay.App.Extensions;

internal static class SwaggerExtensions
{
    internal static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "LogMyDay API",
                Version = "v1",
                Description = "LogMyDay API with Cookie Authentication"
            });

            options.AddSecurityDefinition("cookie", new OpenApiSecurityScheme
            {
                Name = "lmd.auth",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Description = "Cookie-based authentication"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "cookie"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    internal static IApplicationBuilder UseSwaggerWithUi(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        return app;
    }
}
