module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.DictionaryServiceRefreshTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary
open Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

let private liveAt t = Live t
let private cachedAt (t, reason) = Cached(t, reason)

[<Fact>]
let ``T080 Cached -> Live transition on RefreshAsync success fires SourceChanged with new Live`` () =
    let cachedTimestamp = DateTimeOffset.UtcNow.AddHours(-1.0)
    let live = QueueingProvider([
        Failed(NetworkUnreachable, None)        // initial: cache fallback
        Success(sampleDictionary, DateTimeOffset.UtcNow)   // refresh: live succeeds
    ])
    let cache = QueueingProvider([
        Success(sampleDictionary, cachedTimestamp)
    ])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    let observed = ResizeArray<DictionarySource>()
    svc.SourceChanged.Add observed.Add

    svc.InitializeAsync(CancellationToken.None).Wait()
    let firstSnapshot = svc.Snapshot
    match firstSnapshot with
    | ValueSome (_, Cached(t, _)) -> Assert.Equal(cachedTimestamp, t)
    | _ -> Assert.Fail("expected Cached after init")

    svc.RefreshAsync(CancellationToken.None).Wait()

    let snapshotAfter = svc.Snapshot
    match snapshotAfter with
    | ValueSome (_, Live _) -> ()
    | other -> Assert.Fail(sprintf "expected Live after refresh, got %A" other)

    // Two transitions observed: Cached, then Live.
    Assert.Equal(2, observed.Count)
    Assert.True(match observed[0] with Cached _ -> true | _ -> false)
    Assert.True(match observed[1] with Live _ -> true | _ -> false)
    // Refresh success writes the cache.
    Assert.Single(writer.Writes |> Seq.toList) |> ignore

[<Fact>]
let ``T081 Live -> Live updates timestamp on RefreshAsync success`` () =
    let t1 = DateTimeOffset.UtcNow
    let t2 = t1.AddSeconds(10.0)
    let live = QueueingProvider([
        Success(sampleDictionary, t1)
        Success(sampleDictionary, t2)
    ])
    let cache = QueueingProvider([])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    svc.InitializeAsync(CancellationToken.None).Wait()
    svc.RefreshAsync(CancellationToken.None).Wait()

    match svc.Snapshot with
    | ValueSome (_, Live t) -> Assert.Equal(t2, t)
    | other -> Assert.Fail(sprintf "expected Live(%A), got %A" t2 other)

[<Fact>]
let ``T082 RefreshAsync failure while Live retains the prior Live state and does NOT fire SourceChanged`` () =
    let t1 = DateTimeOffset.UtcNow
    let live = QueueingProvider([
        Success(sampleDictionary, t1)
        Failed(Timeout, None)
    ])
    let cache = QueueingProvider([])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(live, cache, writer, NullLogger<DictionaryService>.Instance)

    svc.InitializeAsync(CancellationToken.None).Wait()

    let observed = ResizeArray<DictionarySource>()
    svc.SourceChanged.Add observed.Add

    svc.RefreshAsync(CancellationToken.None).Wait()

    // State retained.
    match svc.Snapshot with
    | ValueSome (_, Live t) -> Assert.Equal(t1, t)
    | other -> Assert.Fail(sprintf "expected Live retained, got %A" other)

    // No SourceChanged event for a refresh-failure-while-Live.
    Assert.Empty(observed)

[<Fact>]
let ``T083 concurrent RefreshAsync calls coalesce to a single in-flight request`` () =
    // Use a TaskCompletionSource-backed provider so the test controls when the
    // live call returns; both concurrent RefreshAsync callers must observe the
    // same task and the provider must be invoked exactly once.
    let tcs = TaskCompletionSource<DictionaryFetchResult>()
    let mutable callCount = 0
    let provider =
        { new IDictionaryProvider with
            member _.FetchAsync(_ct) =
                Interlocked.Increment(&callCount) |> ignore
                tcs.Task }

    let cache = QueueingProvider([])
    let writer = RecordingCacheWriter()
    let svc = DictionaryService(provider, cache, writer, NullLogger<DictionaryService>.Instance)

    let task1 = svc.RefreshAsync(CancellationToken.None)
    let task2 = svc.RefreshAsync(CancellationToken.None)

    // Both callers receive the SAME task reference — coalesce contract.
    Assert.Same(task1, task2)

    // Resolve and let the work run.
    tcs.SetResult(Success(sampleDictionary, DateTimeOffset.UtcNow))
    Task.WaitAll([| task1 :> Task; task2 :> Task |], TimeSpan.FromSeconds 5.0) |> ignore

    // After completion, the underlying provider was hit exactly once
    // (the second caller did not start a parallel fetch).
    Assert.Equal(1, callCount)
