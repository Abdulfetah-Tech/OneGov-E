using OneGov.Application.Identity;
using OneGov.Infrastructure.Identity;

namespace OneGov.Api.Security;

public static class NationalIdSetup
{
    /// <summary>Chooses the national ID provider from configuration, lazily so overrides are honoured.</summary>
    public static IServiceCollection AddNationalIdSignIn(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            provider.GetRequiredService<IConfiguration>().GetSection("NationalId").Get<NationalIdOptions>()
            ?? new NationalIdOptions());

        services.AddSingleton<INationalIdProvider>(provider =>
        {
            var options = provider.GetRequiredService<NationalIdOptions>();
            var environment = provider.GetRequiredService<IHostEnvironment>();

            switch (options.Provider.Trim().ToLowerInvariant())
            {
                case "":
                case "none":
                    return new DisabledNationalIdProvider();
                case "mock":
                    if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
                    {
                        throw new InvalidOperationException(
                            "NationalId:Provider=mock lets anyone sign in as anyone. It is allowed in Development and Testing only.");
                    }

                    return new MockNationalIdProvider(options);
                default:
                    throw new InvalidOperationException(
                        $"Unknown NationalId:Provider '{options.Provider}'. Use none or mock. The Fayda adapter is not built yet.");
            }
        });

        return services;
    }
}
