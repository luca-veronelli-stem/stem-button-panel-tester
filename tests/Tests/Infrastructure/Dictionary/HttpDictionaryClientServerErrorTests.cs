using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientServerErrorTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task FetchAsync_With5xx_ReturnsFailedServerError(int statusCode)
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed, $"expected Failed for {statusCode}, got {result}");
        Assert.Equal(FetchFailureReason.ServerError, ((DictionaryFetchResult.Failed)result).Reason);
    }
}
