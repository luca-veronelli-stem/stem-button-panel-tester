namespace Stem.ButtonPanel.Tester.Core.Dictionary

open System

/// Identity unit per CHK011: one Windows user account on one workstation.
/// Used as the scope key for both the dictionary cache (FR-013) and the
/// API-key credential (FR-011 family). F# record — structural equality is
/// automatic on all three fields.
type Installation = {
    /// `Environment.MachineName` at provisioning time.
    MachineName: string
    /// `WindowsIdentity.GetCurrent().User.Value`.
    UserSid: string
    /// Generated at first run; persisted in DPAPI alongside the credential.
    InstallationId: Guid
}

[<RequireQualifiedAccess>]
module Installation =

    /// Boundary helper used by `DpapiCredentialStore.GetInstallationAsync` to
    /// detect a `(MachineName, UserSid)` mismatch. Per data-model.md, full F#
    /// equality on the record stays available for tests; this helper compares
    /// only the two scope-bearing fields. `InstallationId` is metadata.
    let installationsMatch (a: Installation) (b: Installation) : bool =
        a.MachineName = b.MachineName && a.UserSid = b.UserSid
