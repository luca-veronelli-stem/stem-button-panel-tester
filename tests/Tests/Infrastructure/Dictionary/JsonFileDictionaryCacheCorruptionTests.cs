using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheCorruptionTests
{
    [Fact]
    public async Task FetchAsync_MalformedJson_ReturnsCacheUnreadable()
    {
        using var harness = new CacheHarness();
        File.WriteAllText(harness.CachePath, "{not really json");

        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.CacheUnreadable, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_TruncatedJson_ReturnsCacheUnreadable()
    {
        using var harness = new CacheHarness();
        File.WriteAllText(harness.CachePath, """{"schema_version": 1, "fetched_at": """);

        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.CacheUnreadable, ((DictionaryFetchResult.Failed)result).Reason);
    }
}
