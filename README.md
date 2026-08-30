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

The workflow asserts on a standardized marker line and the process exit code.
That contract is owned here — games conform to it rather than inventing their
own, because three games with three protocols is the duplication this package
exists to prevent.

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
