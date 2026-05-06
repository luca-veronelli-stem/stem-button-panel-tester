using System;
using System.Net.Http;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using WireMock.Server;

namespace Tests.Infrastructure.Dictionary;

/// <summary>
/// Boilerplate for spinning up a <see cref="WireMockServer"/> + a real
/// <see cref="HttpDictionaryClient"/> bound to it. Disposed via xUnit
/// <see cref="IDisposable"/> contract on each test class.
/// </summary>
internal sealed class HttpDictionaryClientHarness : IDisposable
{
    public WireMockServer Server { get; }
    public HttpClient HttpClient { get; }
    public FakeCredentialStore Credentials { get; }
    public DictionaryApiOptions ApiOptions { get; }
    public TimeProvider Clock { get; }
    public HttpDictionaryClient Client { get; }

    public HttpDictionaryClientHarness(TimeSpan? timeout = null)
    {
        Server = WireMockServer.Start();
        HttpClient = new HttpClient();
        Credentials = new FakeCredentialStore();
        ApiOptions = new DictionaryApiOptions
        {
            BaseUrl = new Uri(Server.Url ?? throw new InvalidOperationException("WireMock did not start.")),
            MajorVersion = "v1",
            // Default test timeout is generous; tests that exercise the 5s production
            // budget pass an explicit short timeout.
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
        Clock = TimeProvider.System;
        Client = new HttpDictionaryClient(
            HttpClient,
            Credentials,
            Options.Create(ApiOptions),
            NullLogger<HttpDictionaryClient>.Instance,
            Clock);
    }

    public void Dispose()
    {
        Server.Dispose();
        HttpClient.Dispose();
    }
}
