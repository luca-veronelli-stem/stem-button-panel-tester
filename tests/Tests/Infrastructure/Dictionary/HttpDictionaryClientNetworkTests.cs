using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stem.ButtonPanel.Tester.Core.Dictionary;

namespace Tests.Infrastructure.Dictionary;

public class HttpDictionaryClientNetworkTests
{
    [Fact]
    public async Task FetchAsync_ClosedPort_ReturnsFailedNetworkUnreachable()
    {
        // Bind a socket, immediately close it, point the client at the now-closed port.
        // Connecting attempt yields TCP RST -> HttpRequestException -> NetworkUnreachable.
        int port;
        using (var listener = new TcpListener(System.Net.IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        var apiOptions = new DictionaryApiOptions
        {
            BaseUrl = new Uri($"http://127.0.0.1:{port}"),
            DictionaryId = 2,
            Timeout = TimeSpan.FromSeconds(5),
        };
        using var httpClient = new HttpClient();
        var credentials = new FakeCredentialStore();
        var client = new HttpDictionaryClient(
            httpClient,
            credentials,
            Options.Create(apiOptions),
            NullLogger<HttpDictionaryClient>.Instance);

        var sw = Stopwatch.StartNew();
        DictionaryFetchResult result = await client.FetchAsync(CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsFailed, $"expected Failed, got {result}");
        Assert.Equal(FetchFailureReason.NetworkUnreachable, ((DictionaryFetchResult.Failed)result).Reason);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4),
            $"closed-port refusal should be fast (no timeout fall-through), was {sw.Elapsed}.");
    }
}
