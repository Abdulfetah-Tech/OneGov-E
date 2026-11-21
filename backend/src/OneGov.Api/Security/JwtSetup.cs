using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OneGov.Infrastructure.Identity;

namespace OneGov.Api.Security;

public static class JwtSetup
{
    /// <summary>
    /// Registers JWT bearer authentication. Everything reads configuration lazily, so user secrets, environment variables
    /// and test overrides are always honoured. A missing signing key is generated per process in Development only.
    /// </summary>
    public static IServiceCollection AddOneGovJwtAuthentication(this IServiceCollection services)
    {
        services.AddSingleton(ResolveOptions);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        return services;
    }

    private static JwtOptions ResolveOptions(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Configure Jwt:SigningKey (at least 32 characters) from a secret store. It is only generated automatically in Development.");
            }

            options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("OneGov.Jwt").LogWarning(
                "Jwt:SigningKey is not set. Using a temporary key for this process, so sign-ins end when the API restarts.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException($"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyBytes} bytes.");
        }

        return options;
    }
}
