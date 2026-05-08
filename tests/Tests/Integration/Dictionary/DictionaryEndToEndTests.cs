using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Dictionary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Core;
using Stem.ButtonPanel.Tester.Core.Dictionary;
using Stem.ButtonPanel.Tester.Services.Dictionary;
using Tests.Infrastructure.Dictionary;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Tests.Integration.Dictionary;

/// <summary>
/// End-to-end against WireMock + temp filesystem. Maps to quickstart.md
/// scenarios 1, 2, 4. Host-only on CI (no DPAPI; uses fake credential store).
/// </summary>
public class DictionaryEndToEndTests
{
    /// <summary>Bridges the F# `IDictionaryCacheWriter` to a concrete cache.</summary>
    private sealed class CacheWriterAdapter : IDictionaryCacheWriter
    {
        private readonly JsonFileDictionaryCache _cache;
        public CacheWriterAdapter(JsonFileDictionaryCache cache) => _cache = cache;
        public Task WriteAsync(ButtonPanelDictionary dictionary, DateTimeOffset fetchedAt, CancellationToken ct)
            => _cache.WriteAsync(dictionary, fetchedAt, ct);
    }

    [Fact]
    public async Task ColdStart_NoCacheAndNoCredential_SurfacesNoDictionaryAvailable()
    {
        using var cacheHarness = new CacheHarness();
        using var server = WireMockServer.Start();
        using var http = new HttpClient();
        var apiOptions = new DictionaryApiOptions
        {
            BaseUrl = new Uri(server.Url ?? throw new InvalidOperationException()),
            DictionaryId = 2,
            Timeout = TimeSpan.FromSeconds(30),
        };
        var credentials = new FakeCredentialStore { ApiKey = FSharpValueOption<string>.ValueNone };
        var live = new HttpDictionaryClient(http, credentials, Options.Create(apiOptions),
            NullLogger<HttpDictionaryClient>.Instance);
        var writer = new CacheWriterAdapter(cacheHarness.Cache);
        var svc = new DictionaryService(live, cacheHarness.Cache, writer,
            NullLogger<DictionaryService>.Instance);

        DictionaryStateUpdate update = await svc.InitializeAsync(CancellationToken.None);

        Assert.True(update.IsNoDictionaryAvailable);
        var none = (DictionaryStateUpdate.NoDictionaryAvailable)update;
        Assert.Equal(FetchFailureReason.SetupIncomplete, none.LiveReason);
        Assert.True(svc.Snapshot.IsValueNone);
    }

    [Fact]
    public async Task PrimedCredential_LiveSucceeds_CacheWritten_NextLaunchOfflineUsesCache()
    {
        // Run #1: prime credential, live succeeds, cache written.
        using var cacheHarness = new CacheHarness();
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/dictionaries/2/resolved").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(DictionaryFixtures.SuccessBody));

        using var http1 = new HttpClient();
        var apiOptions = new DictionaryApiOptions
        {
            BaseUrl = new Uri(server.Url ?? throw new InvalidOperationException()),
            DictionaryId = 2,
            Timeout = TimeSpan.FromSeconds(30),
        };
        var credentials = new FakeCredentialStore();
        var live1 = new HttpDictionaryClient(http1, credentials, Options.Create(apiOptions),
            NullLogger<HttpDictionaryClient>.Instance);
        var writer = new CacheWriterAdapter(cacheHarness.Cache);
        var svc1 = new DictionaryService(live1, cacheHarness.Cache, writer,
            NullLogger<DictionaryService>.Instance);

        DictionaryStateUpdate first = await svc1.InitializeAsync(CancellationToken.None);

        Assert.True(first.IsUpdated);
        var firstUpdated = (DictionaryStateUpdate.Updated)first;
        Assert.True(firstUpdated.Source.IsLive);
        Assert.True(File.Exists(cacheHarness.CachePath));

        // Run #2: WireMock down (server disposed), but cache exists from run #1.
        server.Stop();
        using var http2 = new HttpClient();
        var live2 = new HttpDictionaryClient(http2, credentials, Options.Create(apiOptions),
            NullLogger<HttpDictionaryClient>.Instance);
        var svc2 = new DictionaryService(live2, cacheHarness.Cache, writer,
            NullLogger<DictionaryService>.Instance);

        DictionaryStateUpdate second = await svc2.InitializeAsync(CancellationToken.None);

        Assert.True(second.IsUpdated);
        var secondUpdated = (DictionaryStateUpdate.Updated)second;
        Assert.True(secondUpdated.Source.IsCached);
        var cached = (DictionarySource.Cached)secondUpdated.Source;
        // The fallback reason captures *why* live failed — NetworkUnreachable since the server is down.
        Assert.Equal(FetchFailureReason.NetworkUnreachable, cached.FallbackReason);
    }
}
