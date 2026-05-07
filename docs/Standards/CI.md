# Standard: CI

> **Stability:** v1.0.0
> **Platform:** GitHub Actions is CI of record. Bitbucket Pipelines runs a build-only stub for visibility.

## Workflows shipped per repo

| Workflow | File | Trigger | Job |
| --- | --- | --- | --- |
| **CI** | `.github/workflows/ci.yml` | push, PR, manual dispatch, weekly schedule | format check + build + test on `ubuntu-latest` and `windows-latest` |
| **Mirror to Bitbucket** | `.github/workflows/mirror-bitbucket.yml` | push to `main` | fast-forward `bitbucket/main` |
| **Release** (archetype A) | `.github/workflows/release.yml` | tag `v*.*.*` | publish self-contained `win-x64` zip + create GitHub Release |
| **Release** (archetype B) | `.github/workflows/release.yml` | tag `v*.*.*` | `dotnet pack` + publish to GitHub Packages |

Templates live under `shared/templates/.github/workflows/` and are copied into each repo by the rollout script (see REPO_STRUCTURE).

## ci.yml — invariants

- **Triggers:** `push` to any branch, `pull_request` against `main`, `workflow_dispatch`, weekly `schedule` cron (catches dependency drift on idle repos).
- **Concurrency:** `concurrency.group = ci-${{ github.ref }}`, `cancel-in-progress: true` — newer pushes cancel older runs on the same branch.
- **Matrix:** `os: [ubuntu-latest, windows-latest]`. The Linux leg enforces portability; the Windows leg validates Windows-only drivers and any legacy `GUI.Windows` projects.
- **Caching:** `~/.nuget/packages/` keyed on `Directory.Packages.props`; Lean `~/.elan/` keyed on `lean-toolchain` (only when `specs/` exists).
- **Steps:** checkout → setup-dotnet (from `global.json`) → restore → format check → build (Release) → test (Release).

## Format check is a hard gate (whitespace-only in CI)

```yaml
- name: Verify formatting (whitespace)
  run: dotnet format whitespace --verify-no-changes --no-restore
```

Whitespace-only on purpose. The full `dotnet format --verify-no-changes` fails on the GitHub-hosted runners with `CS0246` when C# files reference types from F# projects, even after a successful `dotnet build` — Roslyn's `MSBuildWorkspace` doesn't fully resolve cross-language refs during the analyzer phase on those images. The same command passes locally on the same SDK (10.0.203). Analyzer/style enforcement still happens via the build's `TreatWarningsAsErrors` (BUILD_CONFIG), so the whitespace check is sufficient at the CI formatting gate.

Pure-C# or pure-F# repos don't strictly need the workaround, but the template uses `whitespace` everywhere for uniformity — analyzer enforcement still happens via build.

Husky.NET pre-commit (BUILD_CONFIG) keeps running the full `dotnet format --verify-no-changes` locally, where the cross-language gap doesn't manifest. The CI step is the whitespace backstop.

Revisit when .NET SDK 11 / Roslyn ship: if `MSBuildWorkspace` gains full cross-language resolution on the hosted runners, restore the full check here.

## TFM-conditional matrix legs

The Linux leg builds only `net10.0`. The Windows leg builds both `net10.0` and `net10.0-windows`. Driver projects' `net10.0-windows` TFM is therefore covered exactly once, on the right runner.

```yaml
- name: Build (Linux)
  if: runner.os == 'Linux'
  run: dotnet build --framework net10.0 --configuration Release

- name: Build (Windows)
  if: runner.os == 'Windows'
  run: dotnet build --configuration Release   # picks up both TFMs
```

## Test reporting

`dorny/test-reporter@v1` consumes the TRX output from `dotnet test --logger trx` and surfaces failed tests in the PR check. Step:

```yaml
- name: Test
  run: dotnet test --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"

- name: Test report
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Tests (${{ matrix.os }})
    path: '**/test-results.trx'
    reporter: dotnet-trx
```

`if: always()` so failed tests still produce a report.

## Release workflow — archetype A

Triggered on `v*.*.*` tag push. Steps:

1. Checkout, setup-dotnet from `global.json`.
2. `dotnet publish src/<App>.GUI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
3. `Compress-Archive` to `<app>-<version>-win-x64.zip`.
4. `softprops/action-gh-release@v2` — creates a GitHub Release with the zip attached and the matching CHANGELOG entry as body.

Why archetype A needs a release workflow: the desktop app's distributable is a self-contained zip. Without it, "release" means "open the IDE and copy bin/Release somewhere" — fragile and unreproducible.

## Release workflow — archetype B

Triggered on `v*.*.*` tag push. Steps:

1. Checkout, setup-dotnet.
2. `dotnet pack -c Release -o ./packages -p:Version=$VERSION`.
3. `dotnet nuget push` to GitHub Packages.
4. `softprops/action-gh-release@v2` — GitHub Release with the matching CHANGELOG entry.

## Release workflow — archetype C

`llm-settings` itself. No release workflow — versioning is the git tag (see CHANGELOG.md at this repo's root). Tag is created manually after merging the relevant PR.

## Mirror workflow

Defined in `dual-remote.md` rule. Same content; ships unchanged via the rollout script. Skip for `llm-settings` itself (GitHub-only).

## Bitbucket Pipelines stub

`bitbucket-pipelines.yml` runs a minimal build to keep the team's Bitbucket green. Single step, `dotnet build`, no tests. Sync the .NET SDK version whenever `global.json` changes.

```yaml
image: mcr.microsoft.com/dotnet/sdk:10.0
pipelines:
  default:
    - step:
        name: Build
        script:
          - dotnet build --configuration Release
```

## Branch protection (set once per repo)

After the first green CI run on `main`:

- Require status check `CI / build (ubuntu-latest)` and `CI / build (windows-latest)` before merge.
- Require linear history (no merge commits).
- Require branches to be up-to-date with `main` before merging.
- Allow squash merges only.
- `delete_branch_on_merge: true` (also set via `gh api -X PATCH /repos/{owner}/{repo}`).

## Dependabot

`dependabot.yml` shipped per repo: weekly updates for `nuget` and `github-actions`. Major bumps open separate PRs.

## What CI does NOT do

- **Coverage reporting** — not enforced in v1 (see TESTING).
- **Lint of markdown / YAML** — left to local tooling for now.
- **Deploy** — STEM apps don't auto-deploy. The release workflow's artifact is the deploy bundle.
