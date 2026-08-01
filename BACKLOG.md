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

**Status: Done, 2026-08-01.** Two games run it, and deliberately in two different
shapes — which is what makes it proven rather than merely working once.

- **Time Machine Clicker** probes `GameRoot`, a composition-root autoload. The
  harness observes something else doing the composing.
- **TicTacHoe** has no composition root — its two autoloads are a theme applier
  and an input-map augmenter — so the runner performs the composition itself and
  acts as its own probe, instantiating the main menu and validating it against
  the node paths `MainMenuPaths` declares.

Both pass on every push and pull request. Whispers is the third consumer and is
blocked on a real defect the harness found in it; that work is owned by Whispers'
backlog, not this one.

The durable rules learned here are recorded in `CLAUDE.md`. What follows is the
evidence, kept because the failures were silent and would otherwise be rediscovered.

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
was wrong. The run reached a verdict and reported that `SessionCoordinator` could
not resolve `QuestLog`, warning that the facade would return plausible defaults
able to reach a save file.

**Root cause, and a correction.** This backlog first attributed it to autoload
declaration order. That was wrong. Godot binds one class per script file, matching
the filename, and `QuestLog` was declared inside `QuestState.cs` — so
`/root/QuestLog` was a `QuestState` node wearing the name, and every
`GetNodeOrNull<QuestLog>` in that game returned null permanently. Fixed by giving
the type its own file; the rule is now recorded in `CLAUDE.md` as a B44 standard.

Composition now passes in Whispers. The job stays red on a separate teardown
signal 11, owned by
[Whispers' backlog](https://github.com/DA-Sandman-Jr/WhispersOfTheEarth/blob/main/BACKLOG.md),
and its workflow stays `workflow_dispatch` until that is fixed.

**Worth keeping:** the harness earned its existence here. It found a silent,
long-standing defect that no test caught, in a game that builds clean and passes
CI — which is exactly the class of bug a composition smoke test exists for.

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

### RESOLVED 2026-08-01 — `SmokeObservation.EngineErrors` now populated

Fixed in 0.3.0 via `OS.AddLogger` and a `Godot.Logger` subclass, after confirming
against the GodotSharp 4.7 metadata that the API exists rather than assuming it.

It only works because 0.2.1 made the source generators run on this package: a
`Logger` subclass declared here needs generated dispatch for `_LogError`, exactly
like the `Node` callbacks did.

**Scope is deliberately narrower than the field name suggests, and is documented
in the type.** A logger sees only what is emitted after registration, and the
harness registers from its own `_Ready` — after every autoload has initialised.
So it covers composition the harness performs and anything later, not the autoload
phase. Autoload failures remain the game probe's job, which is what
`IB44StartupProbe` is for. Warnings are excluded: games legitimately push warnings
during startup and failing on those would make the gate unusable.

**Verified live, not merely compiling.** A passing smoke run is indistinguishable
from a collector that silently does nothing — the failure mode this package hit
twice — so a throwaway branch injected a deliberate `GD.PushError` into
TicTacHoe's runner and the run failed with:

```
Engine errors during startup:
  Error: B44 verification: deliberate engine error (res://tests/Smoke/TicTacHoeSmokeRunner.cs:41 in void TicTacHoe.Smoke.TicTacHoeSmokeRunner._Ready())
B44_SMOKE_FAIL outcome=EngineError
```

File, line, and function all came through. The branch was closed unmerged. Do the
same for any future change to this channel: "the build is green" proves nothing
about a code path whose only job is to fire on failure.

**Shipped as a minor, not a patch.** Feeding a previously-dead failure channel can
turn a passing run red, which is enforcement-expanding by B44's own rule — the
same rule Meziantou 0.8.6 broke by shipping an analyzer bump as a patch into
consumers' floats.

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
