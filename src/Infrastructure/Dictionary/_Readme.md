# Infrastructure/Dictionary

C# implementations for dictionary fetch + persistence:

- `HttpDictionaryClient` (live API, `IDictionaryProvider`) — lands in US1.
- `JsonFileDictionaryCache` (cache fallback, `IDictionaryProvider`) — lands in US2.
- `DpapiCredentialStore` (`IInstallationCredentialStore`, Windows-only) — lands in US2.
- `DictionaryApiOptions` (config) — lands in PR #1 (Foundational).
- `DictionaryCacheEnvelope` (on-disk JSON schema) — lands in US2.
- `Dtos/DictionaryResponseDto` (wire DTO + mapper to F# domain) — lands in US1.

F# domain types and interfaces consumed here live in `src/Core.FSharp/Dictionary/`.
