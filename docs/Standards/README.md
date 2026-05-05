# STEM standards (Standard version: v1.1.0)

These are inline copies pinned to `v1.1.0`. Upstream source of truth is [llm-settings/shared/standards/](https://github.com/luca-veronelli-stem/llm-settings/tree/v1.1.0/shared/standards) (private repo).

| Standard | Purpose |
| --- | --- |
| [REPO_STRUCTURE.md](./REPO_STRUCTURE.md) | Root layout, archetype trees, naming rules. |
| [LANGUAGE.md](./LANGUAGE.md) | F# default; layer-default table; deviation policy. |
| [MODULE_SEPARATION.md](./MODULE_SEPARATION.md) | Onion (A) and hexagonal (B) layering; banned APIs. |
| [PORTABILITY.md](./PORTABILITY.md) | net10.0 default; TFM-conditional drivers; cross-platform replacements. |
| [BUILD_CONFIG.md](./BUILD_CONFIG.md) | Directory.Build.props, Directory.Packages.props, global.json, .editorconfig. |
| [TESTING.md](./TESTING.md) | xUnit + FsCheck + Avalonia.Headless; single F# tests project default. |
| [CI.md](./CI.md) | GitHub Actions: ci.yml, mirror-bitbucket.yml, release.yml; matrix legs. |
| [MIGRATION.md](./MIGRATION.md) | Per-repo adoption phases; major/minor/patch bump procedures. |

## Bumping the standard version

Re-run the rollout from `<llm-settings>/eng/apply-repo-standard.ps1` with `-StandardVersion vX.Y.Z`. The script reads `.stem-standard.json` at the repo root, so only the new tag needs to be passed.
