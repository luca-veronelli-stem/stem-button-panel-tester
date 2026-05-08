using System.Threading;
using System.Threading.Tasks;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientHappyPathTests
{
    private const string ResolvedPath = "/api/dictionaries/2/resolved";

    [Fact]
    public async Task FetchAsync_With200Body_ReturnsSuccessWithDeserializedDictionary()
    {
        using var harness = new HttpDictionaryClientHarness();
        harness.Server
            .Given(Request.Create().WithPath(ResolvedPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(DictionaryFixtures.SuccessBody));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, $"expected Success, got {result}");
        var success = (DictionaryFetchResult.Success)result;
        Assert.Equal(1, success.Dictionary.SchemaVersion);
        Assert.Single(success.Dictionary.PanelTypes);

        // Stopgap (see docs/STOPGAP_API_KEY.md): one PanelType per server-side
        // dictionary; PanelType.Id = dictionary.id stringified, DisplayName = name.
        PanelType panel = success.Dictionary.PanelTypes.Head;
        Assert.Equal("2", panel.Id);
        Assert.Equal("Pulsantiere", panel.DisplayName);
        Assert.Single(panel.Variables);

        Variable variable = panel.Variables.Head;
        Assert.Equal("Foto Tasti", variable.Name);
        Assert.Equal("UInt8", variable.Type);
        Assert.Equal((128 << 8) | 0, variable.Address);
        Assert.Equal(1.0, variable.Scaling);
        Assert.Equal(string.Empty, variable.Unit);
    }

    [Fact]
    public async Task FetchAsync_With200Body_SendsXApiKeyHeader()
    {
        // Stopgap (see docs/STOPGAP_API_KEY.md): wire-level credential is X-Api-Key,
        // not the spec'd `Authorization: Bearer`. WireMock's request matcher verifies
        // the header arrived; a wrong header → no match → 404 → MalformedPayload.
        using var harness = new HttpDictionaryClientHarness();
        harness.Credentials.ApiKey = Microsoft.FSharp.Core.FSharpValueOption<string>.NewValueSome("token-xyz");
        harness.Server
            .Given(Request.Create()
                .WithPath(ResolvedPath)
                .UsingGet()
                .WithHeader("X-Api-Key", "token-xyz"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(DictionaryFixtures.SuccessBody));

        DictionaryFetchResult result = await harness.Client.FetchAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, "stub did not match — X-Api-Key header likely wrong.");
    }
}
