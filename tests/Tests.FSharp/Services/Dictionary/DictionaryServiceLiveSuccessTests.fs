module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.DictionaryServiceLiveSuccessTests

open System
open System.Threading
open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary
open Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

[<Fact>]
let ``Live success transitions to Live and writes the cache`` () =
    let fetchedAt = DateTimeOffset.UtcNow
    let live = QueueingProvider([ Success(sampleDictionary, fetchedAt) ])
    let cache = QueueingProvider([])
    let writer = RecordingCacheWriter()
    let logger = NullLogger<DictionaryService>.Instance
    let svc = DictionaryService(live, cache, writer, logger)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | Updated(dict, source) ->
        Assert.Equal(sampleDictionary.SchemaVersion, dict.SchemaVersion)
        match source with
        | Live t -> Assert.Equal(fetchedAt, t)
        | Cached _ -> Assert.Fail("expected Live")
    | NoDictionaryAvailable _ -> Assert.Fail("expected Updated")

    let writes = writer.Writes |> Seq.toList
    Assert.Single writes |> ignore

[<Fact>]
let ``Live success when cache writer throws still updates in-memory state`` () =
    let live = QueueingProvider([ Success(sampleDictionary, DateTimeOffset.UtcNow) ])
    let cache = QueueingProvider([])
    let writer = ThrowingCacheWriter(System.IO.IOException "disk full")
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | Updated _ -> ()
    | NoDictionaryAvailable _ -> Assert.Fail("cache write failure must not block in-memory state.")
