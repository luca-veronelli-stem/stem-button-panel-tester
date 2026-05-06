using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientTimeoutTests
{
    [Fact]
    public async Task FetchAsync_ResponseDelayedPastTimeout_ReturnsFailedTimeout()
    {
        // Use a tight timeout so the test stays fast.
        using var harness = new HttpDictionaryClientHarness(timeout: TimeSpan.FromMilliseconds(200));
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(2))
                .WithBody(DictionaryFixtures.SuccessBody));

        var sw = Stopwatch.StartNew();
        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsFailed, $"expected Failed(Timeout, _), got {result}");
        Assert.Equal(FetchFailureReason.Timeout, ((DictionaryFetchResult.Failed)result).Reason);
        // Sanity: the timeout fires well before the WireMock 2s delay would.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1.5), $"client did not enforce timeout (elapsed={sw.Elapsed}).");
    }
}
