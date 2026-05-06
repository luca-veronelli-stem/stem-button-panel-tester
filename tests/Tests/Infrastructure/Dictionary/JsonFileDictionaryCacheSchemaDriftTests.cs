using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheSchemaDriftTests
{
    [Fact]
    public async Task FetchAsync_SchemaVersion2_ReturnsCacheUnreadable_FileLeftInPlace()
    {
        using var harness = new CacheHarness();
        File.WriteAllText(harness.CachePath, """
        {
          "schema_version": 2,
          "fetched_at": "2026-05-06T11:23:45Z",
          "dictionary": {
            "schema_version": 1,
            "generated_at": "2026-05-06T11:23:45Z",
            "panel_types": []
          }
        }
        """);

        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.CacheUnreadable, ((DictionaryFetchResult.Failed)result).Reason);
        Assert.True(File.Exists(harness.CachePath), "schema-drift file must be left in place per FR-010.");
    }
}
