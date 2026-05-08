using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// Plaintext-config-backed <see cref="IInstallationCredentialStore"/>. Returns
/// the API key sourced from <c>Dictionary:ApiKey</c> (or env var
/// <c>Dictionary__ApiKey</c>) so <c>HttpDictionaryClient</c> can authenticate
/// without DPAPI.
/// </summary>
/// <remarks>
/// <para><b>Stopgap.</b> See <c>docs/STOPGAP_API_KEY.md</c>. Originally this
/// class was a US1-only stand-in replaced by <c>DpapiCredentialStore</c> in
/// US2; the DPAPI path was unwired in
/// <c>feat/dictionary-api-key-config-stopgap</c> to ship a same-day
/// API-backed dictionary build, and the placeholder is the runtime store
/// again until the secure path is reinstated.</para>
/// </remarks>
public sealed class PlaceholderInstallationCredentialStore : IInstallationCredentialStore
{
    private readonly string? _apiKey;

    public PlaceholderInstallationCredentialStore(string? apiKey)
    {
        _apiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey;
    }

    public Task<FSharpValueOption<string>> GetApiKeyAsync(CancellationToken ct)
        => Task.FromResult(_apiKey is null
            ? FSharpValueOption<string>.ValueNone
            : FSharpValueOption<string>.NewValueSome(_apiKey));

    public Task SetApiKeyAsync(string apiKey, CancellationToken ct)
        => throw new NotSupportedException("Placeholder credential store does not persist; replaced by DpapiCredentialStore in US2.");

    public Task ClearAsync(CancellationToken ct)
        => Task.CompletedTask;

    public Task<FSharpValueOption<Installation>> GetInstallationAsync(CancellationToken ct)
        => Task.FromResult(FSharpValueOption<Installation>.ValueNone);
}
