namespace Stem.ButtonPanel.Tester.Core.Dictionary

/// Per FR-011e. Server-side and operational vocabulary for credential state;
/// the client at runtime only branches on Provisioned vs Active vs
/// "needs re-provisioning" (Rotated/Revoked/Expired collapse to a 401 surface
/// per FR-011f).
type CredentialLifecycleState =
    /// Just unwrapped from the installer-bundle (R-1) or returned by /register.
    | Provisioned
    /// Post-first-successful-API-call validation.
    | Active
    /// Server-side replaced (with grace window) — runtime-equivalent to Revoked.
    | Rotated
    /// Server-side invalidated, immediate.
    | Revoked
    /// Validity period exceeded — only meaningful if server policy uses
    /// time-bounded keys.
    | Expired
