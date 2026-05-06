using System;
using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class JsonFileDictionaryCacheRoundtripTests
{
    [Fact]
    public async Task WriteThenFetch_ReturnsSameDictionary()
    {
        using var harness = new CacheHarness();
        ButtonPanelDictionary input = DictionaryFixtures.Sample();
        DateTimeOffset fetchedAt = DateTimeOffset.UtcNow;

        await harness.Cache.WriteAsync(input, fetchedAt, CancellationToken.None);
        DictionaryFetchResult result = await harness.Cache.FetchAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var success = (DictionaryFetchResult.Success)result;
        Assert.Equal(input.SchemaVersion, success.Dictionary.SchemaVersion);
        Assert.Equal(input.PanelTypes.Length, success.Dictionary.PanelTypes.Length);
        Assert.Equal(input.PanelTypes.Head.Id, success.Dictionary.PanelTypes.Head.Id);
        Assert.Equal(input.PanelTypes.Head.Variables.Head.Address, success.Dictionary.PanelTypes.Head.Variables.Head.Address);
        // FetchedAt is the original fetch timestamp, preserved through the envelope.
        Assert.Equal(fetchedAt, success.FetchedAt);
    }
}
