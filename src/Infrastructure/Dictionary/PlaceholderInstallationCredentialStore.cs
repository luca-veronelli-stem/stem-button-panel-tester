using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Infrastructure.Dictionary;

/// <summary>
/// US1-only stand-in for <see cref="IInstallationCredentialStore"/>. Returns a
/// constant API key sourced from configuration so the live <c>HttpDictionaryClient</c>
/// can be wired before the DPAPI store lands in US2 (T072).
/// </summary>
/// <remarks>
/// Replaced by <c>DpapiCredentialStore</c> in PR #3 (US2). Do not extend.
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
