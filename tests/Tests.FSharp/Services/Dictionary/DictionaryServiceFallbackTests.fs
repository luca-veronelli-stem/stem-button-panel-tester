module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.DictionaryServiceFallbackTests

open System
open System.Threading
open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary
open Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

[<Theory>]
[<InlineData(0)>] // NetworkUnreachable
[<InlineData(1)>] // Timeout
[<InlineData(2)>] // Unauthorized
[<InlineData(3)>] // MalformedPayload
[<InlineData(4)>] // ServerError
let ``Live failure with cache present transitions to Cached(reason)`` (reasonIndex: int) =
    let reason =
        match reasonIndex with
        | 0 -> NetworkUnreachable
        | 1 -> Timeout
        | 2 -> Unauthorized
        | 3 -> MalformedPayload
        | 4 -> ServerError
        | _ -> failwithf "Unexpected index %d" reasonIndex

    let cachedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    let live = QueueingProvider([ Failed(reason, None) ])
    let cache = QueueingProvider([ Success(sampleDictionary, cachedAt) ])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | Updated(_, Cached(t, fallbackReason)) ->
        Assert.Equal(cachedAt, t)
        Assert.Equal(reason, fallbackReason)
    | _ -> Assert.Fail(sprintf "expected Cached(%A, _), got %A" reason update)

    // No cache write on a live failure — would just overwrite the cache with itself.
    Assert.Empty(writer.Writes)
