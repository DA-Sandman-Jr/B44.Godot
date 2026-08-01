# B44.Godot — Backlog

Agreed-but-not-started work and known defects. Decisions already settled live in
[`CLAUDE.md`](CLAUDE.md); this file is what is still ahead.

Status values: **Planned** (agreed, not started), **In progress**, **Blocked**,
**Done** (drop the entry once released and record any rule change in
`CLAUDE.md`).

Cross-repository programs live once in
[`B44.Common`'s backlog](https://github.com/DA-Sandman-Jr/B44.Common/blob/main/BACKLOG.md).
This repository was created by its entry 1; entries here hold only this
repository's share.

---

## Planned Work

### 1. Adopt the smoke workflow in a real game

**Status:** Done for the first consumer, 2026-08-01. Time Machine Clicker runs
it on every push and pull request and it passes end to end: `_Ready` fires, the
probe resolves, both autoloads are verified, `B44_SMOKE_PASS outcome=Passed` is
emitted, and the workflow asserts on it. TMC was the right first consumer —
`GameRoot` is a real composition root, while TicTacHoe's autoloads are only theme
and input initialisers.

Remaining: TicTacHoe has not adopted it, and Whispers is blocked on its own
headless crash (below). The entry stays open until at least one more game runs
it, since a harness proven against exactly one project has proven less than it
looks.

Getting here took two package fixes, six workflow fixes, and — the part worth
keeping — a change in method. Everything below the workflow list was found by
reading generated sources locally. Everything above it cost a CI round trip
each.

#### Root cause of the harness never running, 2026-08-01 — cross-assembly `[Export]`

**Godot's source generator does not marshal `[Export]` properties inherited from
a base class in another assembly.** The generated `ScriptProperties` for a game's
subclass of `B44SmokeRunner` contained no entry for `ProbePath`,
`RequiredAutoloads`, or `TimeoutSeconds` — only an empty `PropertyName` class
inheriting the base one. A scene that sets those properties then fails to
instantiate, silently: the scene resource loads, no node is created, `_Ready`
never runs, and the engine waits until something kills it.

That single defect explains the symptom in **both** adopting games, and it
supersedes three earlier hypotheses recorded here — the hand-written `.tscn`, the
CLI scene argument, and a Whispers-specific crash. Two of those were real bugs
worth the fixes they got; none of them was why the runner never ran.

Found by building a consumer with `EmitCompilerGeneratedFiles` and reading the
generated source — locally, in one build, with no Godot install and no CI cycle.
Worth remembering: several rounds of blind CI iteration preceded it and produced
nothing this decisive.

**Fixed in 0.2.0.** Configuration is now plain `protected virtual` members a game
overrides. A few lines per game instead of one, and unlike the previous design it
works. The scene is a bare node carrying only the script.

#### Second root cause, same day — the generators never ran on this package at all

Found by asking whether the `[Export]` finding had a sibling. It did, and it was
worse.

Godot's source generators ship **inside `Godot.NET.Sdk`**. This package
deliberately builds on `Microsoft.NET.Sdk` with a plain `GodotSharp` reference,
so the generators never ran here: the build produced **zero** generated files. A
`Node` type declared in this assembly therefore had no `InvokeGodotClassMethod`
override, which is how the engine dispatches `_Ready` and `_Process` to C#. Both
were overridden and neither could ever be called.

A consuming game's generator does not cover the gap — it only sees the game's own
declarations. Verified by reading the generated `ScriptMethods` on both sides: the
game's subclass had no `_Ready`/`_Process` entry, while a game class declaring its
own `_Ready` had the full dispatch.

So the harness had **two independent fatal defects**, either of which alone
guaranteed silence. Fixing only the exports would have produced another identical
CI failure and no new information.

**Fixed in 0.2.1** by referencing `Godot.SourceGenerators` directly as an
analyzer. `ScriptPathAttribute` is disabled: it maps a type to its `res://` file,
nothing here lives under `res://`, and it hard-fails without `GodotProjectDir`
which only the Godot SDK sets.

Configuration stays on virtual members rather than returning to `[Export]`. With
generators on both sides inherited exports might now marshal, but the virtual
design does not depend on that subtlety being right — and this area has now been
wrong twice.

**The general lesson, worth more than either fix:** shipping a Godot `Node` in a
NuGet package is not the ordinary case, and the SDK carries machinery that is
easy to assume comes with `GodotSharp`. It does not. Read the generated sources.

#### Workflow defects found by running against a real game

Six, all fixed, all invisible to local review:

1. **Download URL 404'd.** Godot tags a `.0` release as `4.7-stable`, not
   `4.7.0-stable` — only patches carry the third component — while NuGet versions
   always carry three. Note the SDK-compatibility check ran and *passed*
   immediately before: the strings agreed while denoting different things.
2. **Binary name reconstructed wrongly.** The archive extracts a folder using
   `_x86_64` containing a binary using `.x86_64`. Now located by search, because
   guessing failed twice.
3. **Building the solution failed with MSB4126.** Godot-generated solutions
   define only `ExportDebug`/`ExportRelease`/`Release` — no plain `Debug`. The
   workflow builds the csproj beside `project.godot`.
4. **The scene argument must be a project-relative path, not `res://`.** A
   `res://` argument is accepted silently and ignored.
5. **A CLI scene argument does not override the main scene at all.** It loads the
   scene as a resource while still running the project's configured one. The
   workflow now rewrites `run/main_scene` before import, so the engine takes its
   completely normal startup path — which is the path this test exists to
   exercise.
6. **`--quit-after` exits before the main scene is instantiated.** Removed; the
   harness quits itself once it has a verdict, and the job timeout is the
   backstop.

**Confirmed working:** `GodotSharp` as a `PrivateAssets="all"` compile-time
reference coexists with the game's `Godot.NET.Sdk`; cold-checkout
`--headless --import` succeeds; the job timeout kills a hung engine.

#### Whispers: the harness found a real bug on its first honest run

Re-tested against 0.2.1. The earlier reading — that Whispers simply does not
survive headless startup — was made while the harness itself was broken, and it
was wrong. The run now reaches a verdict and reports:

> SessionCoordinator could not resolve: QuestLog at `/root/QuestLog`. Calls
> through this facade would return plausible defaults (floor 0, default branch,
> title location) that can reach a save file.

`SessionCoordinator` is autoload #3 and `QuestLog` is #7, and Godot registers
autoloads in declaration order. A silent facade returning defaults that can reach
a save file is precisely what this harness was built to detect, and it found one
on the first run that worked.

Owned by [Whispers' backlog](https://github.com/DA-Sandman-Jr/WhispersOfTheEarth/blob/main/BACKLOG.md),
not this one. Its workflow stays `workflow_dispatch` until the defect is fixed.
The engine does still take signal 11 during teardown, after the marker prints —
real, secondary, recorded there.

### 2. Migrate the shared engine-side adapters (B44.Common backlog entry 1B)

**Status:** Planned, after entry 1 above is working. Deliberately not bundled
into standing the repository up.

- **Godot logger sink factory** — present in all three games, behaviorally
  identical (verified 2026-07-29); the diffs are an `if`-chain vs a `switch`.
- **`NodePathValidator`** — in TicTacHoe (49 lines) and Whispers (48). Take
  **TicTacHoe's**: it throws a descriptive `InvalidOperationException` where
  Whispers throws a bare `ArgumentNullException`. Time Machine Clicker has no copy.
- **The `GD.PushWarning` warning sink** passed to
  `RepositoryFactory.CreateWithFallback`.

Do **not** reintroduce the GD0102 `global using` workaround. Godot 4.7 marshals
cross-assembly enums into `[Export]` properties correctly, verified 2026-07-29
by reading generated `ScriptProperties` source.

Once `NodePathValidator` lives here, the harness can validate declared paths
through it by reflection over a game's `*Paths.cs` types, rather than the
string list `B44SmokeRunner` takes today.

---

## Known Defects

None currently recorded — nothing has run against a real game yet, which is
entry 1 above rather than a defect.

---

## Notes

**Ratchet.** Baseline is at the repository root and currently records nothing;
every file here is well under the tracking threshold. Regenerate only in a
change that performs a real extraction:

```bash
dotnet build B44.Godot/B44.Godot.csproj -t:B44WriteRatchetBaseline
```

Never regenerate to permit growth, never raise an existing entry, and never
grant a ratchet exception without David's approval.

**Why this repository's own CI has no Godot.** It calls B44.Common's engine-free
reusable workflow. That is deliberate: it proves the claim in `CLAUDE.md` that
the pass/fail rules are free of Godot types and testable without an engine.
