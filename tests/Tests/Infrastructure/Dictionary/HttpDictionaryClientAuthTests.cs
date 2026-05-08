using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientAuthTests
{
    [Fact]
    public async Task FetchAsync_With401_ReturnsFailedUnauthorized()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/api/dictionaries/2/resolved").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody("""{"error":"unauthorized"}"""));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed, $"expected Failed, got {result}");
        var failed = (DictionaryFetchResult.Failed)result;
        Assert.Equal(FetchFailureReason.Unauthorized, failed.Reason);
    }

    [Fact]
    public async Task FetchAsync_With401_DoesNotRetryWithinSameCall()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/api/dictionaries/2/resolved").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        await harness.Client.FetchAsync(CancellationToken.None);

        Assert.Single(harness.Server.LogEntries);
    }

    [Fact]
    public async Task FetchAsync_With401_SentXApiKeyOnFirstAttempt()
    {
        // Same matcher trick as the happy-path test: if the header is wrong, the
        // stub doesn't match and we never get the 401 we're asking for.
        using var harness = new HttpDictionaryClientHarness();
        harness.Credentials.ApiKey = Microsoft.FSharp.Core.FSharpValueOption<string>.NewValueSome("rotated-key");
        harness.Server
            .Given(Request.Create()
                .WithPath("/api/dictionaries/2/resolved")
                .UsingGet()
                .WithHeader("X-Api-Key", "rotated-key"))
            .RespondWith(Response.Create().WithStatusCode(401));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed, "stub did not match — X-Api-Key header likely wrong.");
        Assert.Equal(FetchFailureReason.Unauthorized, ((DictionaryFetchResult.Failed)result).Reason);
        Assert.Single(harness.Server.LogEntries);
    }

    [Fact]
    public async Task FetchAsync_With403_AlsoMapsToUnauthorized()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/api/dictionaries/2/resolved").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(403));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.Unauthorized, ((DictionaryFetchResult.Failed)result).Reason);
    }

    [Fact]
    public async Task FetchAsync_WithNoCredential_ReturnsSetupIncomplete()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Credentials.ApiKey = Microsoft.FSharp.Core.FSharpValueOption<string>.ValueNone;

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(FetchFailureReason.SetupIncomplete, ((DictionaryFetchResult.Failed)result).Reason);
        // No HTTP call should have been made.
        Assert.Empty(harness.Server.LogEntries);
    }
}
