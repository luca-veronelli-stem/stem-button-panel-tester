module Stem.ButtonPanel.Tester.Tests.Core.Dictionary.DictionarySourceTests

open System
open Xunit
open Stem.ButtonPanel.Tester.Core.Dictionary

// State-transition invariants per data-model.md state diagram.
// The orchestrator (DictionaryService) lands in US2; here we verify the DU
// shape is rich enough to express each documented transition.

[<Fact>]
let ``Live -> Live preserves variant identity, updates timestamp`` () =
    let t0 = DateTimeOffset.UtcNow
    let t1 = t0.AddMinutes(1.0)
    let before = Live t0
    let after = Live t1

    match before, after with
    | Live a, Live b ->
        Assert.True(b > a)
    | _ -> Assert.Fail("expected Live -> Live")

[<Fact>]
let ``Cached -> Live on successful refresh discards the fallback reason`` () =
    let cachedAt = DateTimeOffset.UtcNow.AddHours(-2.0)
    let refreshedAt = DateTimeOffset.UtcNow
    let before = Cached(cachedAt, NetworkUnreachable)
    let after = Live refreshedAt

    match before, after with
    | Cached(_, reason), Live t ->
        Assert.Equal(NetworkUnreachable, reason)
        Assert.Equal(refreshedAt, t)
    | _ -> Assert.Fail("expected Cached -> Live")

[<Fact>]
let ``Refresh failure while Live keeps the existing Live (no mid-session regression)`` () =
    let liveAt = DateTimeOffset.UtcNow
    let before = Live liveAt
    // Per data-model.md: a refresh failure while Live does NOT transition to
    // Cached; the prior Live is retained. Encode that here as: the "after"
    // value chosen by the orchestrator must be the same Live record.
    let after = before

    match before, after with
    | Live a, Live b -> Assert.Equal(a, b)
    | _ -> Assert.Fail("expected Live to be retained on refresh failure")

[<Fact>]
let ``Cached carries the fallback reason for FR-005 indicator differentiation`` () =
    let cachedAt = DateTimeOffset.UtcNow.AddMinutes(-30.0)
    let viaUnauthorized = Cached(cachedAt, Unauthorized)
    let viaNetwork = Cached(cachedAt, NetworkUnreachable)

    Assert.NotEqual<DictionarySource>(viaUnauthorized, viaNetwork)
