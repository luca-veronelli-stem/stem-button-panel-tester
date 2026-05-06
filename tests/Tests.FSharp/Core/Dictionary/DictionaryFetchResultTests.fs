module Stem.ButtonPanel.Tester.Tests.Core.Dictionary.DictionaryFetchResultTests

open System
open Xunit
open Stem.ButtonPanel.Tester.Core.Dictionary

let private emptyDictionary : ButtonPanelDictionary =
    {
        SchemaVersion = 1
        GeneratedAt = DateTimeOffset.UnixEpoch
        PanelTypes = []
    }

/// Exhaustive match over every `FetchFailureReason` variant. Adding a new
/// variant without updating this match triggers FS0025 at compile time —
/// which is the test: this file failing to compile is the regression signal.
let private label (r: FetchFailureReason) : string =
    match r with
    | NetworkUnreachable -> "network"
    | Timeout -> "timeout"
    | Unauthorized -> "unauthorized"
    | MalformedPayload -> "malformed"
    | ServerError -> "server"
    | CacheAbsent -> "cache-absent"
    | CacheUnreadable -> "cache-unreadable"
    | SetupIncomplete -> "setup-incomplete"

[<Fact>]
let ``label covers every FetchFailureReason variant`` () =
    let reasons : FetchFailureReason list = [
        NetworkUnreachable
        Timeout
        Unauthorized
        MalformedPayload
        ServerError
        CacheAbsent
        CacheUnreadable
        SetupIncomplete
    ]
    let labels = reasons |> List.map label |> List.distinct
    Assert.Equal(8, labels.Length)

[<Fact>]
let ``Success carries the dictionary and fetch timestamp`` () =
    let now = DateTimeOffset.UtcNow
    let result = Success(emptyDictionary, now)
    match result with
    | Success(dict, fetchedAt) ->
        Assert.Same(box emptyDictionary, box dict)
        Assert.Equal(now, fetchedAt)
    | Failed _ -> Assert.Fail("expected Success")

[<Fact>]
let ``Failed carries the reason and optional detail`` () =
    let result = Failed(Timeout, Some "deadline 5s exceeded")
    match result with
    | Success _ -> Assert.Fail("expected Failed")
    | Failed(reason, detail) ->
        Assert.Equal(Timeout, reason)
        Assert.Equal(Some "deadline 5s exceeded", detail)

[<Fact>]
let ``Failed without detail uses None`` () =
    let result = Failed(NetworkUnreachable, None)
    match result with
    | Success _ -> Assert.Fail("expected Failed")
    | Failed(_, detail) -> Assert.Equal(None, detail)
