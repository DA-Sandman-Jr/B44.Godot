# B44.Godot

B44.Godot is the small engine-facing layer shared by B44 games. It provides
Godot adapters and a reusable headless composition smoke test without pulling
engine dependencies into otherwise engine-free libraries.

This is the engine-coupled counterpart to
[`B44.Common`](https://github.com/DA-Sandman-Jr/B44.Common). It exists as its
own repository so every other B44 repository can keep its engine-free MSBuild
guard literally true, with no carve-outs — and so Godot-side code can churn on
the engine's release cadence without dragging the engine-free packages with it.

Unity integration: [`B44.Unity`](https://github.com/DA-Sandman-Jr/B44.Unity).

Planned work and known defects are tracked in [`BACKLOG.md`](BACKLOG.md).

## What's in it

| Namespace | Types | Purpose |
|---|---|---|
| `B44.Godot.Smoke` | `IB44StartupProbe`, `B44StartupState` | The game-side surface: expose whether startup succeeded, and why not |
| `B44.Godot.Smoke` | `SmokeEvaluation`, `SmokeObservation`, `SmokeResult` | Godot-free pass/fail rules, and the marker + exit-code contract |
| `B44.Godot.Smoke` | `B44SmokeRunner` | Thin `Node` shell that gathers observations and quits with a verdict |
| `B44.Godot.Display` | `WindowPlacement`, `WindowRect`, `WindowSize` | Godot-free rules for where a window returns from fullscreen and how a startup window fits its screen |
| `B44.Godot.Display` | `WindowModeSwitcher` | Thin shell that switches a window between fullscreen and its remembered windowed mode, and fits a startup window to its screen |
| `tools/B44.Godot.Validation.psm1` | PowerShell 7 module | Bounded engine launches, the proof verdict, isolated user directories, and the validation ledger and exit codes games' `validate.ps1` scripts share |

## Window mode

`WindowModeSwitcher` switches one window between fullscreen and the mode it had
before: entering fullscreen remembers whether it was windowed or maximized and,
when windowed, where; leaving puts it back, clamped by `WindowPlacement` into the
usable screen and never below the minimum size the game passes. A window that was
never windowed this session returns at the game's fallback size, centred. Headless
runs record the requested mode without touching a window. The game keeps the
policy: its default, persisting the preference, and the control that toggles it.

`FitWindowed(design)` refits a plain window that is too large for its screen, at
startup, to the largest rectangle of the design's shape that fits 90% (by default)
of the usable area, never larger than the design, centred (`WindowPlacement.Fit`,
`FitIfOversized`). A window that already fits, such as one a launch argument or a
test harness sized, is kept. A 1080x1920 portrait
layout opened on a 1080p desktop keeps its shape instead of being cut into a
near-square window taller than the screen. Call it before applying a saved
fullscreen preference; it leaves fullscreen and maximized windows alone.

## Composition smoke testing

A game implements `IB44StartupProbe`, adds `B44SmokeRunner` to a smoke scene,
and calls the reusable workflow:

```yaml
jobs:
  smoke:
    uses: DA-Sandman-Jr/B44.Godot/.github/workflows/reusable-godot-smoke.yml@<sha>
    with:
      godot-version: '4.7.0'      # required; no default
      project-path: '.'
      smoke-scene: 'res://tests/Smoke.tscn'
```

`godot-version` is deliberately required and unset by default so each game owns
the version it tests against, and this repository never needs editing when Godot
releases.

The workflow builds with `B44AgentSyncVerifyOnly=true`, so stale generated
guidance fails the consumer checkpoint instead of being rewritten.

The workflow asserts on a standardized marker line and the process exit code.
That contract is owned here — games conform to it rather than inventing their
own, because three games with three protocols is the duplication this package
exists to prevent.

## Local validation module

The package carries `tools/B44.Godot.Validation.psm1`, a PowerShell 7 module that
NuGet never adds to a build. A game imports it from its restored package, so every
game runs proofs and reports its verdict the same way:

- **Proof runner.** `Invoke-BoundedProcess` passes arguments without shell quoting,
  drains both pipes and kills the process tree at a wall-clock limit.
  `Assert-GodotProof` passes a run only with exit code zero, its completion marker,
  and no `ERROR:`, `WARNING:`, `SCRIPT ERROR:`, `USER ERROR:` or `USER WARNING:`
  line beyond the ones the proof declares, each with an exact count.
  `Write-ProofOverride`, `Get-GodotUserRoot` and `Remove-ProofDirectory` isolate and
  clean up a run's Godot user directory; `Invoke-GodotImport` imports once.
- **Validation ledger.** `Start-Validation`, `Invoke-ValidationStep`,
  `Invoke-ValidationProof`, `Add-ValidationResult` and `Complete-Validation` record
  one run's steps and write `summary.md` and `summary.json`. Each verdict has its
  own exit code and only 0 proves the selection: 0 PASSED, 1 FAILED, 2 UNAVAILABLE
  (it could not start here) and 3 INCOMPLETE (nothing failed, but a required step
  cannot run on this platform). `Test-GodotEngineVersion` checks the engine against
  the host's `Godot.NET.Sdk`, as the reusable smoke workflow does.

The game keeps its scopes, steps, proof scripts, fixtures and expected diagnostics.
It locates the module through the host's restore assets, for example:

```powershell
$assets = Get-Content '.godot/mono/temp/obj/project.assets.json' -Raw | ConvertFrom-Json -AsHashtable
$library = @($assets['libraries'].Keys | Where-Object { $_ -like 'B44.Godot/*' })[0]
$folder = @($assets['packageFolders'].Keys)[0]
Import-Module (Join-Path $folder $assets['libraries'][$library]['path'] 'tools/B44.Godot.Validation.psm1')
```

Because the module arrives with the package, a validator restores the host first
and writes its own summary if that bootstrap fails.

## UID sidecars

Godot writes a `.uid` beside a C# script and uses it as that script's stable
identifier. Every one Godot generates is committed, and `*.uid` never enters
`.gitignore` — without the committed sidecar, references break on the next
fresh clone.

A sidecar Godot has not written yet is not a defect and is not checked here. A
UID is the editor's to allocate: a CI job that demanded one could only produce a
red build whose single remedy is opening the editor, and hand-writing a value is
worse than the missing file because it looks authoritative and resolves to
nothing. This repository shipped `reusable-godot-uid-check.yml` for that and
removed it on 2026-08-29; no game ever called it.

What is checked needs no engine knowledge and lives in `B44.Standards`
repository hygiene: a tracked `.uid` or `.import` whose principal file is gone
is an orphan, and fails the build.

## Versioning

`B44.Godot`, `B44.Common`, and `B44.Standards` each version independently from
their own public repositories. Consumers float `B44.Godot` as `0.<minor>.*`
while it is pre-1.0. Any change to the smoke marker or exit codes is
contract-expanding and bumps the minor version.

## Building

```bash
dotnet test
```

No Godot installation is required to build or test: `GodotSharp` is a
compile-time-only package reference, and every pass/fail rule is deliberately
free of Godot types. The `Node` shell and the workflow are covered by a
consuming game running the workflow in CI.

## Availability and license

The source is publicly visible for review and portfolio evaluation. No license
for reuse is granted, and the package is maintained for B44-owned projects
rather than offered as a supported public dependency. See [`LICENSE`](LICENSE)
and [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
