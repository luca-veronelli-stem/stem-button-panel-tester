namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System.Threading
open System.Threading.Tasks

/// DPAPI-backed in production (`DpapiCredentialStore`), manual fakes in tests.
/// `ValueOption` returns express "no credential / no installation provisioned
/// yet" without the cost of allocating an `Option` reference cell.
type IInstallationCredentialStore =
    /// `ValueNone` means no credential has been provisioned yet (FR-011d
    /// `SetupIncomplete` path). Otherwise the cleartext API key for one
    /// outbound HTTP call.
    abstract GetApiKeyAsync: ct: CancellationToken -> Task<string voption>

    /// Persists the API key alongside the current `Installation` identity.
    abstract SetApiKeyAsync: apiKey: string * ct: CancellationToken -> Task

    /// FR-011f re-provisioning hook. Overwrites the credential file with
    /// random bytes before deleting it (defence-in-depth against forensic
    /// recovery of the prior cleartext).
    abstract ClearAsync: ct: CancellationToken -> Task

    /// `ValueNone` if no credential file exists; otherwise the `Installation`
    /// stamped at provisioning time. `DpapiCredentialStore` re-checks this on
    /// every call to detect `(MachineName, UserSid)` drift per CHK007.
    abstract GetInstallationAsync: ct: CancellationToken -> Task<Installation voption>
