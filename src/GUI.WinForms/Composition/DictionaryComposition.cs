using System;
using System.Net.Http;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace GUI.Windows.Composition;

/// <summary>
/// Wires the <c>Dictionary</c> namespace into the GUI's composition root.
/// In US1 the credential store is a placeholder bound to <c>Dictionary:ApiKey</c>;
/// US2 replaces it with the DPAPI-backed store.
/// </summary>
internal static class DictionaryComposition
{
    public static IServiceCollection AddDictionarySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DictionaryApiOptions>()
            .Bind(configuration.GetSection(DictionaryApiOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DictionaryApiOptions>, DictionaryApiOptionsValidator>();

        // Single long-lived HttpClient per R-3.
        services.AddSingleton(_ => new HttpClient());

        // Placeholder credential store (US1). Replaced by DpapiCredentialStore in US2.
        services.AddSingleton<IInstallationCredentialStore>(sp =>
        {
            string? apiKey = configuration["Dictionary:ApiKey"];
            return new PlaceholderInstallationCredentialStore(apiKey);
        });

        services.AddSingleton<HttpDictionaryClient>();
        services.AddSingleton<IDictionaryProvider>(sp => sp.GetRequiredService<HttpDictionaryClient>());

        return services;
    }
}
