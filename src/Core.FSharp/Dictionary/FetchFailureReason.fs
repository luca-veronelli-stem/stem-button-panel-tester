namespace Stem.ButtonPanel.Tester.Core.Dictionary

/// Why a single dictionary fetch attempt did not yield a usable dictionary.
/// Closed set per data-model.md; exhaustive matching gates new variants.
type FetchFailureReason =
    | NetworkUnreachable
    | Timeout
    | Unauthorized
    | MalformedPayload
    | ServerError
    | CacheAbsent
    | CacheUnreadable
    | SetupIncomplete
