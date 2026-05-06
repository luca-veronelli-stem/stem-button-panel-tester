using System;
using System.IO;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Dictionary;

internal sealed class DpapiHarness : IDisposable
{
    public string Directory { get; }
    public DpapiCredentialStoreOptions Options { get; }
#pragma warning disable CA1416 // Validate platform compatibility
    public DpapiCredentialStore Store { get; }

    public DpapiHarness()
    {
        Directory = Path.Combine(Path.GetTempPath(), "stem-dpapi-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Options = new DpapiCredentialStoreOptions { Directory = Directory };
        Store = new DpapiCredentialStore(
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<DpapiCredentialStore>.Instance);
    }
#pragma warning restore CA1416

    public string FilePath => Path.Combine(Directory, Options.FileName);

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { /* best-effort */ }
    }
}
