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

**Status:** In progress. A shared CI workflow that nothing calls is not done, and
the harness's `Node` shell plus the workflow itself have **no other test**:
neither can be exercised without a Godot binary, and there is none on a dev
machine or on a CI runner before the install step.

Adopted in Time Machine Clicker, which is the right first consumer — `GameRoot`
is a real composition root, while TicTacHoe's autoloads are only theme and input
initialisers. Whispers is second, and separately blocked (see below).

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

#### Whispers is blocked on its own headless crash

With all six workflow defects fixed, Whispers segfaults (signal 11) in
`libcoreclr` during autoload initialisation, before the main scene is
instantiated. `FlowCoordinator` eagerly preloads `Title.tscn`, `Dungeon.tscn`,
and `TurnManager.cs` during its own startup, which is the obvious suspect.

That crash predates the `[Export]` finding and is not explained by it, so it
stands as a genuine finding: **Whispers does not survive headless startup**,
which is exactly what a composition smoke test exists to detect. Its workflow is
parked as `workflow_dispatch` so a known-failing job does not sit red on every
push. Investigating wants someone who can run Godot headless locally.

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
