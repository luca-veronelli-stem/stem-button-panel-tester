namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System

/// Outcome of a single fetch attempt by an `IDictionaryProvider`. Both the
/// HTTP (live) and JSON-file (cache) providers return this DU.
///
/// The `Detail` field on `Failed` is human-readable elaboration for logs only;
/// runtime branching keys off `Reason`.
type DictionaryFetchResult =
    | Success of Dictionary: ButtonPanelDictionary * FetchedAt: DateTimeOffset
    | Failed of Reason: FetchFailureReason * Detail: string option
