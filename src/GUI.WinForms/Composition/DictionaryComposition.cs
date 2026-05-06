using System;
using System.Net.Http;
using System.Runtime.InteropServices;
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

/// <summary>
/// Wires the <c>Dictionary</c> namespace into the GUI's composition root.
/// In US1 the credential store is a placeholder bound to <c>Dictionary:ApiKey</c>;
/// US2 replaces it with the DPAPI-backed store.
/// </summary>
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

/// <summary>
/// On first run, if no credential is provisioned and one is supplied via
/// configuration (developer or installer-bundle path per R-1), unwrap into
/// the credential store. Idempotent — no-op once a credential exists.
/// </summary>
internal sealed class DictionaryCredentialBootstrap
{
    private readonly IInstallationCredentialStore _store;
    private readonly string? _bootstrapKey;
    private readonly ILogger<DictionaryCredentialBootstrap> _logger;

    public DictionaryCredentialBootstrap(
        IInstallationCredentialStore store,
        string? bootstrapKey,
        ILogger<DictionaryCredentialBootstrap> logger)
    {
        _store = store;
        _bootstrapKey = string.IsNullOrEmpty(bootstrapKey) ? null : bootstrapKey;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken ct)
    {
        Microsoft.FSharp.Core.FSharpValueOption<string> existing = await _store.GetApiKeyAsync(ct).ConfigureAwait(false);
        if (existing.IsValueSome)
        {
            return;
        }

        if (_bootstrapKey is null)
        {
            _logger.LogWarning(
                "No DPAPI credential present and no bootstrap key supplied; live fetches will surface SetupIncomplete.");
            return;
        }

        await _store.SetApiKeyAsync(_bootstrapKey, ct).ConfigureAwait(false);
        _logger.LogInformation("Credential bootstrapped from configuration into DPAPI store.");
    }
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

        // Credential store: real DPAPI on Windows, placeholder elsewhere
        // (Linux CI compiles but never reaches the credential path because the
        // GUI is Windows-only).
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddOptions<DpapiCredentialStoreOptions>()
                .Bind(configuration.GetSection(DpapiCredentialStoreOptions.SectionName));
            services.AddSingleton<IInstallationCredentialStore>(sp =>
                ActivatorUtilities.CreateInstance<DpapiCredentialStore>(sp));

            // First-run bundle unwrap: if no credential is stored yet AND the
            // configuration carries a bootstrap key, prime the DPAPI store.
            // R-1 documents the per-supplier installer-bundle path; this hook
            // is the unwrap step. The bootstrap key is read from the
            // (developer-only) Dictionary:ApiKey config until the bundle
            // tooling lands.
            services.AddSingleton<DictionaryCredentialBootstrap>(sp =>
                new DictionaryCredentialBootstrap(
                    sp.GetRequiredService<IInstallationCredentialStore>(),
                    configuration["Dictionary:ApiKey"],
                    sp.GetRequiredService<ILogger<DictionaryCredentialBootstrap>>()));
        }
        else
        {
            services.AddSingleton<IInstallationCredentialStore>(_ =>
                new PlaceholderInstallationCredentialStore(configuration["Dictionary:ApiKey"]));
        }

        // Two IDictionaryProvider impls — keyed for clarity; the orchestrator
        // takes both via concrete types (no key-aware DI required).
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
