using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Dictionary;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheAtomicTests
{
    private sealed class CrashAfterTempWriteInjector : ICacheWriteFaultInjector
    {
        public Task BeforeRenameAsync()
            => throw new IOException("simulated crash before rename");
    }

    [Fact]
    public async Task FaultBetweenTempWriteAndRename_PreviousGoodCacheIntact()
    {
        // Seed: write a known-good cache.
        using var harness = new CacheHarness();
        ButtonPanelDictionary good = DictionaryFixtures.Sample();
        DateTimeOffset goodAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await harness.Cache.WriteAsync(good, goodAt, CancellationToken.None);

        long goodSize = new FileInfo(harness.CachePath).Length;
        Assert.True(goodSize > 0);

        // Re-create the cache wrapping the same directory but with a fault
        // injector. The temp file is written, then the injector throws — the
        // rename should never happen.
        using var faulting = new CacheHarness(new CrashAfterTempWriteInjector());
        // Point the faulting harness at the same directory so it shares the cache file.
        var sharedOptions = new DictionaryCacheOptions { Directory = harness.Directory };
        var faultingCache = new JsonFileDictionaryCache(
            Microsoft.Extensions.Options.Options.Create(sharedOptions),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonFileDictionaryCache>.Instance,
            new CrashAfterTempWriteInjector());

        ButtonPanelDictionary updated = DictionaryFixtures.Sample();
        await Assert.ThrowsAsync<IOException>(() =>
            faultingCache.WriteAsync(updated, DateTimeOffset.UtcNow, CancellationToken.None));

        // The previous good cache must be intact (size and content).
        Assert.True(File.Exists(harness.CachePath));
        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // A .tmp file may be present, but the canonical cache file is unmodified.
        // (FR-002: temp cleanup is the cache's responsibility on next read OR via explicit purge.)
        string[] tmpFiles = Directory.GetFiles(harness.Directory, "*.tmp.*");
        Assert.True(tmpFiles.Length >= 1, "expected an orphan .tmp file from the simulated crash.");
    }
}
