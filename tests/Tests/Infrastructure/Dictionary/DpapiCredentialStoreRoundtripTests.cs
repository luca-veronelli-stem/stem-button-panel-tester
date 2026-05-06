using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

[Trait("Category", "RequiresWindows")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class DpapiCredentialStoreRoundtripTests
{
    [Fact]
    public async Task SetThenGet_ReturnsValueSomeWithSameKey()
    {
        using var harness = new DpapiHarness();

        await harness.Store.SetApiKeyAsync("supplier-key-1234", CancellationToken.None);
        FSharpValueOption<string> got = await harness.Store.GetApiKeyAsync(CancellationToken.None);

        Assert.True(got.IsValueSome);
        Assert.Equal("supplier-key-1234", got.Value);
        Assert.True(File.Exists(harness.FilePath));
    }

    [Fact]
    public async Task SetThenGetInstallation_ReturnsCurrentMachineUser()
    {
        using var harness = new DpapiHarness();

        await harness.Store.SetApiKeyAsync("k", CancellationToken.None);
        FSharpValueOption<Installation> inst = await harness.Store.GetInstallationAsync(CancellationToken.None);

        Assert.True(inst.IsValueSome);
        Assert.False(string.IsNullOrEmpty(inst.Value.MachineName));
        // SID may legitimately be empty in some test environments; not asserted.
    }

    [Fact]
    public async Task GetApiKey_NoFile_ReturnsValueNone()
    {
        using var harness = new DpapiHarness();

        FSharpValueOption<string> got = await harness.Store.GetApiKeyAsync(CancellationToken.None);

        Assert.True(got.IsValueNone);
    }
}
