using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Manual fake (no Moq, per Constitution IV) for tests that need a credential
/// store but don't want to touch DPAPI.
/// </summary>
internal sealed class FakeCredentialStore : IInstallationCredentialStore
{
    public FSharpValueOption<string> ApiKey { get; set; } =
        FSharpValueOption<string>.NewValueSome("fake-api-key");

    public FSharpValueOption<Installation> Installation { get; set; } =
        FSharpValueOption<Installation>.ValueNone;

    public int GetApiKeyCallCount { get; private set; }

    public Task<FSharpValueOption<string>> GetApiKeyAsync(CancellationToken ct)
    {
        GetApiKeyCallCount++;
        return Task.FromResult(ApiKey);
    }

    public Task SetApiKeyAsync(string apiKey, CancellationToken ct)
    {
        ApiKey = FSharpValueOption<string>.NewValueSome(apiKey);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        ApiKey = FSharpValueOption<string>.ValueNone;
        Installation = FSharpValueOption<Installation>.ValueNone;
        return Task.CompletedTask;
    }

    public Task<FSharpValueOption<Installation>> GetInstallationAsync(CancellationToken ct)
        => Task.FromResult(Installation);
}
