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

**Status:** Planned — and this is what makes 1A actually finished. A shared CI
workflow that nothing calls is not done, and the harness's `Node` shell plus the
workflow itself have **no other test**: neither can be exercised without a Godot
binary, and there is none on a dev machine or on a CI runner before the install
step.

Prefer Whispers if its startup-readiness work has landed, since it is the game
that needs an explicit `Initializing` / `Ready` / `Failed` state anyway.
Otherwise adopt the simplest game first and migrate Whispers afterwards.

Serial dependency to plan for: this package must be created, packaged, published
to nuget.org, and consumed before a game can adopt the harness — the same
publish-then-migrate cycle the `B44.Standards` 0.8.x work used.

#### First real run against Whispers, 2026-07-31 — four defects found, one open

Every item previously listed here as unproven has now been tested against a real
game. Four were wrong, in ways no amount of local review would have caught:

1. **Download URL 404'd.** Godot tags a `.0` release as `4.7-stable`, not
   `4.7.0-stable` — only patches carry the third component — while NuGet
   versions always carry three. `Godot.NET.Sdk/4.7.0` means engine release
   `4.7`. Note the SDK-compatibility check ran and *passed* immediately before:
   the strings agreed while denoting different things.
2. **Binary name was reconstructed wrongly.** The archive extracts a folder
   using `_x86_64` containing a binary using `.x86_64` — underscore in one, dot
   in the other. Now located by search rather than reconstruction, because
   guessing failed twice.
3. **Building the solution failed with MSB4126.** Godot-generated solutions
   define only `ExportDebug`, `ExportRelease`, and `Release` — no plain `Debug`
   at solution level. The project file does define it, so the workflow builds
   the csproj beside `project.godot`.
4. **The scene argument must be a project-relative file path, not `res://`.** A
   `res://` argument is accepted silently and ignored: the engine printed its
   banner, started no scene, and idled until the job timeout killed it. Ten
   minutes of output was one line.

**Confirmed working:** `GodotSharp` as a `PrivateAssets="all"` compile-time
reference coexists fine with the game's `Godot.NET.Sdk`; cold-checkout
`--headless --import` succeeds; the job timeout correctly kills a hung engine.

#### Diagnosed 2026-07-31 — the CLI scene argument does not override the main scene

With `--verbose` the log is unambiguous, and the earlier hypothesis was wrong:
the hand-written `.tscn` is fine. Godot parses it, loads
`WhispersSmokeRunner.cs`, and resolves all nine autoloads. Committing the
script's `.uid` was correct and necessary, but it was not the cause.

What the log actually shows:

- `res://tests/Smoke/Smoke.tscn` — **loaded**
- `res://Scenes/Title/Title.tscn`, the project's configured main scene — **also
  loaded, immediately afterwards**
- `B44SmokeRunner._Ready` — **never ran** (zero occurrences of its startup line)

So passing a scene path on the command line causes Godot to *load* that scene
as a resource while still running the project's configured main scene. The
harness node is therefore never instantiated, nothing ever calls
`GetTree().Quit()`, and the run only ends because of `--quit-after`. The
subsequent SIGSEGV is in `libcoreclr.so` during teardown, after Godot's
"Resource still in use" reporting — a shutdown-path crash, plausibly incidental
to headless .NET teardown rather than the thing to fix first.

**This needs a design change, not a workflow tweak.** Two candidates:

1. **`--script`** — the documented approach for headless Godot tooling. A
   `SceneTree`-derived script is the entry point, so nothing competes with the
   main scene. This likely means the harness stops being a `Node` in a `.tscn`
   and becomes a script entry point, which also removes the per-game one-line
   subclass and the hand-authored scene file.
2. **Autoload** — the harness registers as an autoload in the game's
   `project.godot`. It then runs regardless of which scene is current, at the
   cost of editing every consumer's project file.

(1) is cleaner and matches how Godot expects headless automation to work. Either
way `B44SmokeRunner`, the marker contract, and `SmokeEvaluation` survive intact —
the pure evaluator and its 11 tests are unaffected, since only the entry point
changes.

**Superseded note, kept for the record — the engine aborts.** With the scene path fixed, Godot reaches the
scene and dies with **exit 134 (SIGABRT)**, and the captured log contains only
the version banner, so the cause is not yet visible. Instrumentation is now in
place for the next attempt: `--verbose`, `stdbuf` to defeat block buffering
(which is why output vanished when the process died rather than exited), and
explicit signal reporting so a crash is not misreported as a missing marker.

**Leading hypothesis, untested:** the hand-written `tests/Smoke/Smoke.tscn` in
Whispers. It was authored by hand rather than saved by the Godot editor, and a
malformed scene or a script that fails to attach is the most likely cause of an
abort this early. Opening the project once in the editor and re-saving the scene
would settle it. Diagnosing this properly wants a local Godot install; blind CI
iteration was stopped deliberately at that point.

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
