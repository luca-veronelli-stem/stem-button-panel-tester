using System;
using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheConcurrencyTests
{
    [Fact]
    public async Task ConcurrentWrites_ProduceNonTornFile()
    {
        using var harness = new CacheHarness();
        ButtonPanelDictionary dict = DictionaryFixtures.Sample();
        DateTimeOffset t0 = DateTimeOffset.UtcNow;

        // Two threads write the same shape concurrently. NTFS rename-over is
        // atomic, so the file always contains exactly one writer's payload.
        Task[] writers = new[]
        {
            Task.Run(() => harness.Cache.WriteAsync(dict, t0, CancellationToken.None)),
            Task.Run(() => harness.Cache.WriteAsync(dict, t0.AddSeconds(1), CancellationToken.None)),
        };

        await Task.WhenAll(writers);

        // Both completed without exceptions; the file deserialises cleanly.
        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);
        Assert.True(result.IsSuccess, "concurrent writes produced a torn cache file.");
    }
}
