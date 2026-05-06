using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

[Trait("Category", "WindowsOnly")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class DpapiCredentialStoreInstallationMismatchTests
{
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("Stem.ButtonPanel.Tester:dict-v1");

    [Fact]
    public async Task GetApiKey_StoredOnDifferentMachineName_ReturnsValueNoneAndClears()
    {
        using var harness = new DpapiHarness();

        // Manually craft a credential file with a foreign MachineName.
        var foreignInstallation = new Installation(
            machineName: "FOREIGN-WORKSTATION",
            userSid: "S-1-5-21-FOREIGN",
            installationId: Guid.NewGuid());

        string json = JsonSerializer.Serialize(new
        {
            api_key = "stale-key",
            installation = foreignInstallation,
        });
        byte[] cleartext = Encoding.UTF8.GetBytes(json);
#pragma warning disable CA1416 // platform-bound — the test trait is WindowsOnly.
        byte[] ciphertext = ProtectedData.Protect(cleartext, Entropy, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        File.WriteAllBytes(harness.FilePath, ciphertext);

        FSharpValueOption<string> got = await harness.Store.GetApiKeyAsync(CancellationToken.None);

        Assert.True(got.IsValueNone, "expected ValueNone — the credential's MachineName does not match the live process.");
        Assert.False(File.Exists(harness.FilePath), "auto-clear must remove the mismatched credential.");
    }
}
