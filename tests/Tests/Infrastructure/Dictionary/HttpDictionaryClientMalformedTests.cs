using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientMalformedTests
{
    private const string ResolvedPath = "/api/dictionaries/2/resolved";

    [Fact]
    public async Task FetchAsync_TruncatedJson_ReturnsFailedMalformed()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath(ResolvedPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id": 2, "name": "Pulsantiere", "variables": ["""));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed, $"expected Failed, got {result}");
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_MissingRequiredField_ReturnsFailedMalformed()
    {
        // The DTO marks `name`, `id`, `variables`, and per-variable
        // `name`/`addressHigh`/`addressLow`/`dataType` as JsonRequired —
        // omitting any of them should fail JSON binding.
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath(ResolvedPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "id": 2,
                  "name": "Pulsantiere",
                  "variables": [
                    { "name": "x", "addressHigh": 0 }
                  ]
                }
                """));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_EmptyVariables_ReturnsFailedMalformed()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath(ResolvedPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "id": 2,
                  "name": "Pulsantiere",
                  "variables": []
                }
                """));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_404_MapsToMalformedPayload()
    {
        // Per contract: 404 (e.g. wrong DictionaryId, server-side route change)
        // is a malformed-fallback for control flow; logs distinguish.
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath(ResolvedPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }
}
