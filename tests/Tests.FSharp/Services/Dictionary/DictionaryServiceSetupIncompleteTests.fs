module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.DictionaryServiceSetupIncompleteTests

open System.Threading
open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary
open Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

[<Fact>]
let ``Live SetupIncomplete with cache absent surfaces NoDictionaryAvailable carrying SetupIncomplete`` () =
    // FR-011d distinct path: live fails because the credential isn't provisioned,
    // and there's no cache to fall back to. The orchestrator carries SetupIncomplete
    // through so the GUI can surface FR-011d's setup-failure message instead of
    // FR-008's generic "no dictionary" message.
    let live = QueueingProvider([ Failed(SetupIncomplete, Some "no credential") ])
    let cache = QueueingProvider([ Failed(CacheAbsent, None) ])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let update = svc.InitializeAsync(CancellationToken.None).Result

    match update with
    | NoDictionaryAvailable reason -> Assert.Equal(SetupIncomplete, reason)
    | Updated _ -> Assert.Fail("expected NoDictionaryAvailable")
