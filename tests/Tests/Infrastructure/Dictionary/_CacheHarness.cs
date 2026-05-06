using System;
using System.IO;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Per-test temp directory + a <see cref="JsonFileDictionaryCache"/> rooted
/// in it. Disposal cleans up.
/// </summary>
internal sealed class CacheHarness : IDisposable
{
    public string Directory { get; }
    public DictionaryCacheOptions Options { get; }
    public JsonFileDictionaryCache Cache { get; }

    public CacheHarness(ICacheWriteFaultInjector? faultInjector = null)
    {
        Directory = Path.Combine(Path.GetTempPath(), "stem-cache-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Options = new DictionaryCacheOptions { Directory = Directory };
        Cache = new JsonFileDictionaryCache(
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<JsonFileDictionaryCache>.Instance,
            faultInjector);
    }

    public string CachePath => Path.Combine(Directory, Options.FileName);

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { /* best-effort */ }
    }
}
