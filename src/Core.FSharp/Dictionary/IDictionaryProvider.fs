namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System.Threading
open System.Threading.Tasks

/// Implemented by both the live HTTP client and the on-disk JSON cache.
/// Both implementations honour cancellation per the CANCELLATION standard.
type IDictionaryProvider =
    abstract FetchAsync: ct: CancellationToken -> Task<DictionaryFetchResult>
