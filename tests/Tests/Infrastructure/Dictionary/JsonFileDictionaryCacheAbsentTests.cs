using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheAbsentTests
{
    [Fact]
    public async Task FetchAsync_NoFile_ReturnsCacheAbsent()
    {
        using var harness = new CacheHarness();
        // Harness directory is empty.

        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.CacheAbsent, ((DictionaryFetchResult.Failed)result).Reason);
    }
}
