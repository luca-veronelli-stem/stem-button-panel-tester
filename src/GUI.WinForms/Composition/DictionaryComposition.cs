using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using Stem.ButtonPanel.Tester.Services.Dictionary;

namespace GUI.Windows.Composition;

// Stopgap (see docs/STOPGAP_API_KEY.md): the DPAPI-backed credential store and
// the first-run bootstrap step are bypassed; the API key is read directly from
// configuration (Dictionary:ApiKey, override via env var Dictionary__ApiKey).
// DpapiCredentialStore.cs and the F# IInstallationCredentialStore contract are
// retained so the re-secure follow-up can reinstate the secure path with a
// minimal diff.

/// <summary>
/// Bridges the C# <see cref="JsonFileDictionaryCache"/> (which has its own
/// concrete <c>WriteAsync</c>) to the F# <see cref="IDictionaryCacheWriter"/>
/// interface that <see cref="DictionaryService"/> consumes.
/// </summary>
internal sealed class DictionaryCacheWriterAdapter : IDictionaryCacheWriter
{
    private readonly JsonFileDictionaryCache _cache;
    public DictionaryCacheWriterAdapter(JsonFileDictionaryCache cache) => _cache = cache;

    public Task WriteAsync(ButtonPanelDictionary dictionary, DateTimeOffset fetchedAt, CancellationToken ct)
        => _cache.WriteAsync(dictionary, fetchedAt, ct);
}

internal static class DictionaryComposition
{
    public static IServiceCollection AddDictionarySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DictionaryApiOptions>()
            .Bind(configuration.GetSection(DictionaryApiOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DictionaryApiOptions>, DictionaryApiOptionsValidator>();

        services.AddOptions<DictionaryCacheOptions>()
            .Bind(configuration.GetSection(DictionaryCacheOptions.SectionName));

        // Single long-lived HttpClient per R-3.
        services.AddSingleton(_ => new HttpClient());

        // Credential source: plaintext config (stopgap). The DPAPI flow is
        // retained on disk but unwired; reinstate by registering
        // DpapiCredentialStore behind RuntimeInformation.IsOSPlatform(Windows)
        // and resurrecting DictionaryCredentialBootstrap.EnsureAsync.
        services.AddSingleton<IInstallationCredentialStore>(_ =>
            new PlaceholderInstallationCredentialStore(configuration["Dictionary:ApiKey"]));

        services.AddSingleton<HttpDictionaryClient>();
        services.AddSingleton<JsonFileDictionaryCache>();
        services.AddSingleton<IDictionaryCacheWriter>(sp =>
            new DictionaryCacheWriterAdapter(sp.GetRequiredService<JsonFileDictionaryCache>()));

        services.AddSingleton<DictionaryService>(sp => new DictionaryService(
            liveProvider: sp.GetRequiredService<HttpDictionaryClient>(),
            cacheProvider: sp.GetRequiredService<JsonFileDictionaryCache>(),
            cacheWriter: sp.GetRequiredService<IDictionaryCacheWriter>(),
            logger: sp.GetRequiredService<ILogger<DictionaryService>>()));

        return services;
    }
}
