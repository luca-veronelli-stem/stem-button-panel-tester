namespace Stem.ButtonPanel.Tester.Services.Dictionary

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Stem.ButtonPanel.Tester.Core.Dictionary

/// Optional callback invoked by `DictionaryService` after a successful live
/// fetch so the cache can persist the new dictionary. Lives in
/// `Services.FSharp` rather than `Core.FSharp` because it's an
/// orchestration concern, not a domain concept.
type IDictionaryCacheWriter =
    abstract WriteAsync:
        dictionary: ButtonPanelDictionary
        * fetchedAt: DateTimeOffset
        * ct: CancellationToken
        -> Task

/// Orchestrates the live-then-cache decision and the in-flight coalesce
/// behaviour (CHK021). Single in-flight `RefreshAsync` at a time; concurrent
/// callers receive the in-flight task.
///
/// State machine driven by `DictionarySource`; transitions are documented in
/// `data-model.md`. The service holds the active `ButtonPanelDictionary` and
/// the current `DictionarySource` together so callers (the GUI) can render
/// the indicator with one snapshot.
type DictionaryService(
    liveProvider: IDictionaryProvider,
    cacheProvider: IDictionaryProvider,
    cacheWriter: IDictionaryCacheWriter,
    logger: ILogger<DictionaryService>) =

    let stateLock = obj ()
    let mutable currentDictionary : ButtonPanelDictionary voption = ValueNone
    let mutable currentSource : DictionarySource voption = ValueNone
    let mutable inFlight : Task<DictionaryStateUpdate> voption = ValueNone

    let sourceChanged = Event<DictionarySource>()

    let updateState (dict: ButtonPanelDictionary) (source: DictionarySource) =
        lock stateLock (fun () ->
            currentDictionary <- ValueSome dict
            currentSource <- ValueSome source)
        sourceChanged.Trigger source

    /// Live -> success path (Phase 4 + 5: also write the cache).
    let onLiveSuccess (dict: ButtonPanelDictionary) (fetchedAt: DateTimeOffset) (ct: CancellationToken) =
        task {
            try
                do! cacheWriter.WriteAsync(dict, fetchedAt, ct)
            with ex ->
                logger.LogWarning(ex, "Cache write after live success failed; in-memory state still updated.")
            let source = Live fetchedAt
            updateState dict source
            return Updated(dict, source)
        }

    /// Live -> failure path: fall through to cache (or surface NoDictionaryAvailable).
    let onLiveFailure (liveReason: FetchFailureReason) (ct: CancellationToken) =
        task {
            let! cacheResult = cacheProvider.FetchAsync ct
            match cacheResult with
            | Success(dict, cachedAt) ->
                let source = Cached(cachedAt, liveReason)
                updateState dict source
                return Updated(dict, source)
            | Failed(_, _) ->
                logger.LogError(
                    "No dictionary available: live fetch failed with {LiveReason}, cache unavailable.",
                    liveReason)
                return NoDictionaryAvailable liveReason
        }

    let runRefresh (ct: CancellationToken) =
        task {
            let! liveResult = liveProvider.FetchAsync ct
            match liveResult with
            | Success(dict, fetchedAt) ->
                return! onLiveSuccess dict fetchedAt ct
            | Failed(reason, detail) ->
                logger.LogInformation(
                    "Live fetch failed ({Reason}, {Detail}); attempting cache fallback.",
                    reason,
                    Option.toObj detail)
                // While Live: a refresh failure must NOT regress to Cached
                // mid-session per data-model.md. Keep the existing in-memory
                // dictionary; surface the failure to the caller as Updated
                // with the prior source (i.e. event does not fire again).
                let snapshot =
                    lock stateLock (fun () -> currentDictionary, currentSource)
                match snapshot with
                | ValueSome dict, ValueSome (Live _ as priorSource) ->
                    logger.LogWarning(
                        "Refresh failed while Live ({Reason}); retaining existing Live dictionary.",
                        reason)
                    return Updated(dict, priorSource)
                | _ ->
                    return! onLiveFailure reason ct
        }

    /// Snapshot of the active dictionary + source. `ValueNone` until the
    /// first successful fetch (live or cached) lands.
    member _.Snapshot : (ButtonPanelDictionary * DictionarySource) voption =
        lock stateLock (fun () ->
            match currentDictionary, currentSource with
            | ValueSome d, ValueSome s -> ValueSome(d, s)
            | _ -> ValueNone)

    /// Fired whenever the `DictionarySource` changes (state event for FR-005's
    /// indicator). Does NOT fire for refresh failures while Live (per
    /// data-model.md: existing Live dictionary retained).
    [<CLIEvent>]
    member _.SourceChanged = sourceChanged.Publish

    /// Initial fetch — call once at startup. Equivalent to `RefreshAsync`
    /// but the name reads better at the composition root.
    member this.InitializeAsync(ct: CancellationToken) : Task<DictionaryStateUpdate> =
        this.RefreshAsync ct

    /// Manual refresh (FR-006). Concurrent callers coalesce: the second
    /// caller awaits the same task as the first.
    member _.RefreshAsync(ct: CancellationToken) : Task<DictionaryStateUpdate> =
        let task =
            lock stateLock (fun () ->
                match inFlight with
                | ValueSome t -> t
                | ValueNone ->
                    let t = runRefresh ct
                    inFlight <- ValueSome t
                    let _ = t.ContinueWith(
                                Action<Task<DictionaryStateUpdate>>(fun _ ->
                                    lock stateLock (fun () -> inFlight <- ValueNone)),
                                TaskScheduler.Default)
                    t)
        task
