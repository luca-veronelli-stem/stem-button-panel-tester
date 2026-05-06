module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.DictionaryServiceNoDictionaryTests

open System.Threading
open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary
open Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

[<Fact>]
let ``Live fails and cache absent surfaces NoDictionaryAvailable`` () =
    let live = QueueingProvider([ Failed(NetworkUnreachable, None) ])
    let cache = QueueingProvider([ Failed(CacheAbsent, None) ])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | NoDictionaryAvailable reason -> Assert.Equal(NetworkUnreachable, reason)
    | Updated _ -> Assert.Fail("expected NoDictionaryAvailable when both fail.")

    // Snapshot stays uninitialized.
    Assert.True(svc.Snapshot.IsValueNone)

[<Fact>]
let ``Live fails and cache unreadable surfaces NoDictionaryAvailable`` () =
    let live = QueueingProvider([ Failed(Timeout, None) ])
    let cache = QueueingProvider([ Failed(CacheUnreadable, Some "schema_version=99") ])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | NoDictionaryAvailable reason -> Assert.Equal(Timeout, reason)
    | Updated _ -> Assert.Fail("expected NoDictionaryAvailable when cache is unreadable.")
