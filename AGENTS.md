> **Auto-generated from `CLAUDE.md`** — edit the sibling `CLAUDE.md` instead. Direct changes are overwritten by B44.Standards on the next synchronized build.

# B44.Godot — Engine-Coupled Adapters and Composition Smoke Testing

<!-- B44 ORGANIZATION GUIDANCE: START -->
## B44 Organization Guidance

- `AGENTS.md` files are auto-generated on build; see the generated header for the source file to edit.
- Before editing or reviewing a file, read and follow every applicable `AGENTS.md` from the repository root through that file's directory. Nearer instructions override broader instructions.
- Analyzer severities live in the `B44.Standards` packaged globalconfig, never in a repository `.editorconfig`. Repository editorconfigs own style and whitespace only; tune analyzer policy upstream in the package.
- Public server/function and endpoint-owning projects set `<B44SecuritySensitive>true</B44SecuritySensitive>` in `Directory.Build.props`; B44.Standards then enables the complete SDK Security category at a target-level-pinned rule set.
- Fix shared behavior in the B44 package that owns it; do not fork or paste a local copy into a consumer repository.
- Use compatibility-bounded floating versions for internal B44 packages in every consumer, including production: pre-1.0 packages use `0.<minor>.*`, while stable packages use `<major>.*`. Package owners bump the excluded boundary for breaking changes, and consumers cross that boundary manually. Never use an unbounded `*`. Enforcement-expanding Standards changes bump the minor version and never enter an existing patch float.
- Treat roughly 350 physical lines as a review warning for production source files. New production files should normally stay at or below 500 lines; files above 650 lines require a clear cohesion-based reason.
- Existing oversized files must not grow unless the same change performs a real extraction and leaves the file smaller. Coordinators coordinate; do not evade the limit with cosmetic partial classes, one-method services, generic utility dumping grounds, or needless factories.
- Before automated analyzer fixes, baseline measurement, scripted bulk text rewrites, or consuming a freshly published package, read `.b44/B44.Tooling.md`.
- Godot writes a `.uid` file beside every script as a stable identifier. Commit all of them and never add `*.uid` to `.gitignore`: the project still works locally without them, but references break as soon as it is cloned onto another machine, including a CI runner doing a fresh checkout. Godot generates them for every C# script under the project directory, including engine-free `Core` and test projects it never loads; that is expected and those files are committed too.
- Each repository keeps a root `BACKLOG.md` for agreed-but-not-started work and known defects, with defects in their own section so they stay distinct from planned work. It is authored by hand, never generated and never gated by the build — an empty file written to satisfy a check is worse than no file. Cross-repository programs live once in `B44.Common`'s backlog; a consumer's backlog links to the program and holds only its own share of the work, never a restatement that can drift.
- Isolation is by repository, not by folder. Engine- or framework-coupled adapters live in their own repository and package so engine-free build guards remain literal and release cadences stay independent.
- Keep licensing boundaries explicit. Source governed by terms different from a repository's `LICENSE` belongs behind a separately documented repository/package boundary with its provenance and required notices intact.
<!-- B44 ORGANIZATION GUIDANCE: END -->

The one B44 repository allowed to reference Godot. It exists so every other B44
repository can keep its engine-free guard literally true with no carve-outs.

Published as the `B44.Godot` package on nuget.org, consumed by B44 games.

## Hard Rules

- **This is the only place Godot may appear.** Anything engine-free belongs in
  `B44.Common` instead. If a type here has no `using Godot` and no reason to
  live beside one, it is in the wrong repository.
- **Thin adapters only.** This is a bridge over primitives that already exist in
  `B44.Common`, not a second home for game logic. No game rules, no game state,
  no payload schemas, no content catalogs, no scene-flow authority, no global
  service location.
- **The second-occurrence rule applies here exactly as it does to
  `B44.Common`.** A helper enters only when at least two games demonstrably need
  materially equivalent behavior.
- **Pure logic stays testable without the engine.** Pass/fail rules, parsing,
  and formatting go in plain classes with no Godot types; `Node` subclasses stay
  thin shells over them. There is no Godot binary on a typical dev machine or on
  a CI runner before the install step, so anything that needs one to be tested
  effectively is not tested.
- **This repository owns the smoke marker and exit-code contract.** Games
  conform to it. Three games inventing three protocols is the duplication this
  package exists to prevent.

## Versioning & Publish — Decision Record

`B44.Common`, `B44.Standards`, and `B44.Godot` each ship from their own public
repository and version **independently**. `B44.Godot` publishes from its own
`v*` tags; its number does not track either package and should not be expected
to.

Consumers float it `0.<minor>.*` while pre-1.0, like every other internal B44
package. Enforcement- or contract-expanding changes — notably any change to the
smoke marker or exit codes — bump the minor version and never enter an existing
patch float.

## Godot Version Coupling — Decision Record

The library compiles against `GodotSharp` with `PrivateAssets="all"`: the
reference is compile-time only and does not flow to consumers, so a game's
`Godot.NET.Sdk` supplies the runtime assemblies and the two cannot fight.

Pinned to **4.7.0**, matching all three games' `Godot.NET.Sdk/4.7.0` as of
2026-07-30. Godot-side code churns on the engine's release cadence rather than
ours, which is a stated reason this repository is separate — so raising the pin
is a deliberate act, not a routine dependency bump.

The reusable workflow takes `godot-version` as a **required input with no
default**, so each consuming game owns the version it tests against and this
repository never needs editing when Godot releases.

## The Smoke Contract

Four steps, deliberately four abstractions and not two:

1. The **game** exposes `Initializing` / `Ready` / `Failed` plus diagnostics, by
   implementing `IB44StartupProbe`. It owns its own lifecycle vocabulary.
2. The **harness** (`B44SmokeRunner`) observes that and checks required
   autoloads, declared node paths, and engine errors emitted after its logger is
   registered in `_Ready`. Autoload-phase failures happen earlier and remain the
   game probe's responsibility.
3. The harness emits one standardized **marker line** and a human-readable
   report, then quits with a deterministic exit code.
4. The **workflow** asserts on the marker and the exit code.

Collapsing steps 1 and 3 into "the same signal" is the mistake to avoid: the
game's state is its own concern, and the marker is a CI artifact. They must
agree, not be identical.

## Shipping a Godot `Node` in a NuGet Package — Decision Record

This is not the ordinary case, and Godot's tooling assumes the ordinary case in
two ways that both fail silently. Both cost a full debugging cycle in 0.1.x, and
both are load-bearing — changing either re-breaks the harness with no error
message anywhere.

1. **The source generators ship inside `Godot.NET.Sdk`, not `GodotSharp`.** This
   package builds on `Microsoft.NET.Sdk`, so it must reference
   `Godot.SourceGenerators` explicitly, as an analyzer. Without it the build
   emits zero generated files and a `Node` declared here gets no
   `InvokeGodotClassMethod` override — which is how the engine dispatches
   `_Ready` and `_Process` to C#. Overridden callbacks are simply never called.
   A consuming game's generator does not cover the gap; it only sees the game's
   own declarations. `ScriptPathAttribute` is disabled because nothing here lives
   under `res://` and it hard-fails without `GodotProjectDir`.

2. **Never configure through `[Export]` on a type consumers inherit.** Inherited
   exports are not marshalled across an assembly boundary: the game subclass's
   generated `ScriptProperties` has no entry for them, and a scene that sets them
   fails to instantiate with no error printed. Configuration is expressed as
   `protected virtual` members a game overrides. That costs a game a few lines
   instead of one, and it works.

**The general rule this leaves behind: read the generated sources.** Both
findings came from building with `EmitCompilerGeneratedFiles` and looking —
locally, in one build, no Godot install required. Several rounds of CI iteration
beforehand produced three confident hypotheses and all three were wrong. When
Godot behaviour is inexplicable, check what was generated before theorising about
the engine.

## One Godot Type Per Script File — B44 Standard

**A Godot type registered as an autoload or attached to a `.tscn` must be the only
Godot type in its file, and the file must be named after it.**

Godot binds exactly one class per script file: the one whose name matches the
file. Every other Godot type sharing that file receives no `ScriptPath` and
becomes unbindable — with no error and no warning, at build time or run time. The
node still gets created and still carries the name you gave it; it is simply the
wrong type. `GetNodeOrNull<T>` then returns null forever, and typical defensive
null-handling turns that into silent degradation rather than a crash.

Found in Whispers 2026-08-01: `QuestLog` was declared inside `QuestState.cs`, so
`/root/QuestLog` was a `QuestState` node named `QuestLog`. Every
`GetNodeOrNull<QuestLog>` in the game returned null, which silently disabled
quest-driven autosave. It had been that way undetected.

This does **not** overturn B44's multi-type file style — `MA0048` stays off and
sanctioned multi-type files remain fine. Engine-bound types are the exception,
because the engine cannot bind them otherwise.

Verify with the generated `*_ScriptPath.generated.cs` when in doubt. A type that
should be bindable and has no `ScriptPath` is the bug, and that check takes one
build.

**Note what did NOT cause this**, since it was the first and wrong diagnosis:
autoload declaration order. All autoloads are added to `/root` before any `_Ready`
runs, so an autoload can resolve one declared after it. Order only matters when
one autoload calls *methods* on another during `_Ready` and depends on that
other's `_Ready` having completed. Confirmed by testing the real fix with the
declaration order untouched.

## Layout

- `B44.Godot/Smoke/` — the composition smoke harness. `SmokeContract.cs` and
  `SmokeEvaluation.cs` are Godot-free and unit-tested; `B44SmokeRunner.cs` is
  the thin `Node` shell.
- `B44.Godot.Tests/` — xunit.v3, engine-free. `<TestingPlatformDotnetTestSupport>true`
  is required for `dotnet test` to discover xunit.v3 on current SDKs.
- `.github/workflows/reusable-godot-smoke.yml` — the shared workflow games call.

## Tests

```bash
dotnet test
```

Note what this does **not** cover: the `Node` shell and the workflow itself
cannot be exercised without a Godot binary. Their real test is a consuming game
running the workflow in CI, which is why adopting at least one game is part of
finishing this package rather than a follow-up.
