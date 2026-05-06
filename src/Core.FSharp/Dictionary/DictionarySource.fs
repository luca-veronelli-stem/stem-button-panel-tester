namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System

/// Origin of the active in-memory dictionary. Drives FR-005's UI indicator.
///
/// State transitions are owned by `DictionaryService`; see data-model.md for
/// the diagram. The `Live -> Cached` transition does NOT occur mid-session by
/// design — once a session has a Live dictionary, it stays Live until the
/// process exits.
type DictionarySource =
    | Live of FetchedAt: DateTimeOffset
    | Cached of FetchedAt: DateTimeOffset * FallbackReason: FetchFailureReason
