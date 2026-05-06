using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientMalformedTests
{
    [Fact]
    public async Task FetchAsync_TruncatedJson_ReturnsFailedMalformed()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"schema_version": 1, "panel_types": ["""));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed, $"expected Failed, got {result}");
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_SchemaVersionNot1_ReturnsFailedMalformed()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "schema_version": 99,
                  "generated_at": "2026-05-06T11:23:45.000Z",
                  "panel_types": [{"id":"x","display_name":"x","variables":[]}]
                }
                """));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_EmptyPanelTypes_ReturnsFailedMalformed()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "schema_version": 1,
                  "generated_at": "2026-05-06T11:23:45.000Z",
                  "panel_types": []
                }
                """));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_404_MapsToMalformedPayload()
    {
        // Per contract: 404 (e.g. /v2/ cutover server-side) is a malformed-fallback
        // for control flow; logs distinguish.
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.MalformedPayload, ((DictionaryFetchResult.Failed)result).Reason);
    }
}
