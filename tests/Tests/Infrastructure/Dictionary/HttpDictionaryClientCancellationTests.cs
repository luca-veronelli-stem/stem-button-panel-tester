using System;
using System.Threading;
using System.Threading.Tasks;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientCancellationTests
{
    [Fact]
    public async Task FetchAsync_CallerCancels_ThrowsOperationCancelled()
    {
        using var harness = new HttpDictionaryClientHarness(timeout: TimeSpan.FromSeconds(30));
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(10))
                .WithBody(DictionaryFixtures.SuccessBody));

        using var cts = new CancellationTokenSource();
        Task<Stem.ButtonPanel.Tester.Core.Dictionary.DictionaryFetchResult> fetch = harness.Client.FetchAsync(cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Caller cancellation must surface as OperationCanceledException —
        // distinct from the Failed(Timeout, _) path (T032).
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);
    }
}
