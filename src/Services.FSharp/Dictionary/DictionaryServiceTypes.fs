namespace Stem.ButtonPanel.Tester.Services.Dictionary

open Stem.ButtonPanel.Tester.Core.Dictionary

/// Outcome of `DictionaryService.Initialize` / `RefreshAsync`. Distinct from
/// `DictionaryFetchResult` because the service composes both providers and
/// can also report `NoDictionaryAvailable` when both fail (FR-008).
type DictionaryStateUpdate =
    /// New active dictionary in memory. `Source` distinguishes Live vs Cached.
    | Updated of Dictionary: ButtonPanelDictionary * Source: DictionarySource
    /// Both live fetch and cache failed; the GUI must surface the modal error
    /// per FR-008 / FR-011d. The `LiveReason` is the live-side failure for
    /// log-level differentiation per FR-014.
    | NoDictionaryAvailable of LiveReason: FetchFailureReason
