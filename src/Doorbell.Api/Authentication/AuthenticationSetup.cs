using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Doorbell.Api.Authentication;

public static class AuthenticationSetup
{
    public static bool IsDevelopmentAuthEnabled(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration
            .GetSection(AuthenticationSettings.SectionName)
            .Get<AuthenticationSettings>();

        return environment.IsDevelopment()
            && settings?.Development.Enabled == true;
    }

    public static IServiceCollection AddDoorbellAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration
            .GetSection(AuthenticationSettings.SectionName)
            .Get<AuthenticationSettings>() ?? new AuthenticationSettings();

        services.Configure<AuthenticationSettings>(
            configuration.GetSection(AuthenticationSettings.SectionName));

        var developmentEnabled = IsDevelopmentAuthEnabled(configuration, environment);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(settings.Jwt.Audience),
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                var configured = false;

                if (!string.IsNullOrWhiteSpace(settings.Jwt.Authority))
                {
                    options.Authority = settings.Jwt.Authority;
                    options.RequireHttpsMetadata = settings.Jwt.RequireHttpsMetadata;
                    options.TokenValidationParameters.ValidateIssuerSigningKey = false;
                    options.TokenValidationParameters.ValidIssuer =
                        string.IsNullOrWhiteSpace(settings.Jwt.ValidIssuer)
                            ? null
                            : settings.Jwt.ValidIssuer;
                    options.TokenValidationParameters.ValidAudience = settings.Jwt.Audience;
                    configured = true;
                }
                else if (!string.IsNullOrWhiteSpace(settings.Jwt.IssuerSigningKey))
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(settings.Jwt.IssuerSigningKey!));
                    options.TokenValidationParameters.ValidateIssuer =
                        !string.IsNullOrWhiteSpace(settings.Jwt.ValidIssuer);
                    options.TokenValidationParameters.ValidIssuer = settings.Jwt.ValidIssuer;
                    configured = true;
                }
                else if (developmentEnabled)
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(settings.Development.SigningKey));
                    options.TokenValidationParameters.ValidateIssuer = false;
                    configured = true;
                }

                if (!configured)
                {
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Guid.NewGuid().ToByteArray());
                    options.TokenValidationParameters.ValidIssuer = "doorbell-unconfigured";
                }

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        if (developmentEnabled)
        {
            services.AddSingleton<DevelopmentTokenIssuer>();
        }

        return services;
    }
}
