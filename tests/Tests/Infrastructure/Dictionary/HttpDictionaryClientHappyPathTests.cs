using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientHappyPathTests
{
    [Fact]
    public async Task FetchAsync_With200Body_ReturnsSuccessWithDeserializedDictionary()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath("/v1/dictionary").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(DictionaryFixtures.SuccessBody));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, $"expected Success, got {result}");
        var success = (DictionaryFetchResult.Success)result;
        Assert.Equal(1, success.Dictionary.SchemaVersion);
        Assert.Single(success.Dictionary.PanelTypes);

        PanelType panel = success.Dictionary.PanelTypes.Head;
        Assert.Equal("BP-12-A", panel.Id);
        Assert.Equal("Button Panel 12 (variant A)", panel.DisplayName);
        Assert.Single(panel.Variables);

        Variable variable = panel.Variables.Head;
        Assert.Equal("voltage_input", variable.Name);
        Assert.Equal("uint16", variable.Type);
        Assert.Equal(4097, variable.Address);
        Assert.Equal(0.01, variable.Scaling);
        Assert.Equal("V", variable.Unit);
    }

    [Fact]
    public async Task FetchAsync_With200Body_SendsBearerAuthorizationHeader()
    {
        // Use WireMock's request matcher to verify the Authorization header arrived.
        // If the header is wrong, the stub doesn't match and the client gets a 404 → MalformedPayload.
        using var harness = new HttpDictionaryClientHarness();
        harness.Credentials.ApiKey = Microsoft.FSharp.Core.FSharpValueOption<string>.NewValueSome("token-xyz");
        harness.Server
            .Given(Request.Create()
                .WithPath("/v1/dictionary")
                .UsingGet()
                .WithHeader("Authorization", "Bearer token-xyz"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(DictionaryFixtures.SuccessBody));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, "stub did not match — Authorization header likely wrong.");
    }
}
