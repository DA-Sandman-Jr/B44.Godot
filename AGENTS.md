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
- Each repository keeps a root `BACKLOG.md` for agreed-but-not-started work and known defects, with defects in their own section so they stay distinct from planned work. It is authored by hand, never generated and never gated by the build — an empty file written to satisfy a check is worse than no file. Cross-repository programs live once in `B44.Common`'s backlog; a consumer's backlog links to the program and holds only its own share of the work, never a restatement that can drift.
- Isolation is by repository, not by folder. Engine- or framework-coupled code (`B44.Godot`, any future adapter) and third-party code we vendor, port, or convert each live in their own repository publishing their own package, never inside a normal B44 repository. Engine coupling keeps the engine-free MSBuild guard literally true with no carve-outs and decouples our cadence from the engine's. Third-party code is a licensing boundary: B44 packages are all rights reserved — source public for reference, not licensed for reuse — so an obligation-bearing file inside one contradicts its own terms. Converting or hand-porting does NOT shed the upstream license; a port is a derivative work and the attribution obligation follows it. Each such repository carries its own `LICENSE` and `THIRD-PARTY-NOTICES.md`. A separate project in-tree is not a substitute: it would require weakening the guard, or dual-licensing within one tree.

### Clean-Room Reimplementation — Protocol

When we need behavior an outside codebase has, but its code must not enter a B44 repository, it may be reimplemented clean-room. Output of a correctly executed clean room is B44's own expression — it is not third-party code under the isolation rule and needs no isolated repository. A clean room that skips any step below is not a clean room; its output is a port, and a port does.

1. **Two sides, two vendors.** The describing side may read the source. The implementing side must be a *different model from a different vendor* (Anthropic ↔ ChatGPT/Codex), in a separate session, with no shared context, no shared memory store, and no file access to the source. The implementer never sees the original, at any point, in any form.
2. **Only behavior crosses the wall.** The spec states inputs, outputs, invariants, edge cases, error conditions, and observable ordering guarantees. It must NOT contain source excerpts, pseudocode, function or type decomposition, identifier names, or internal step sequence. If someone could reconstruct the original's structure from the spec, the spec is itself a derivative work — rewrite it before it crosses.
3. **Review the spec before it crosses.** A human reads it for leaked structure. This is the control that actually matters; steps 1 and 5 are worthless if the spec is a transcription.
4. **Check the result for memorized expression.** Different vendors decorrelate training data but do not eliminate the risk — popular OSS sits in every corpus, so the implementer may reproduce upstream expression from weights rather than from the spec. Diff the implementation against upstream for verbatim runs before accepting it, and treat a hit as contamination rather than coincidence.
5. **Keep the record.** Retain the spec as it crossed the wall, which model/vendor produced each side, and the date. Without records the work was done but cannot be shown, and being able to show it is the point.

**Scope limits.** This addresses copyright only. It is no defense to patents (independent creation is irrelevant there) and does not cure license, EULA, or trade-secret obligations accepted in order to obtain the source. At step 1, do not paste confidential or non-public source into a third-party vendor's product; the describing side is only safe for source we are already entitled to read.

**Not worth it for permissive licenses.** MIT/BSD/Apache cost an attribution notice. Clean-rooming to avoid that is high effort and residual risk to dodge a trivial obligation — take the code and add the notice, in its own repository per the isolation rule. Reserve this protocol for copyleft (GPL/AGPL) or unlicensed/proprietary behavior we cannot take on its own terms.
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

`B44.Common` and `B44.Standards` ship in **lockstep from a single `v*` tag** in
their shared repository, because they live together. `B44.Godot` is a separate
repository and therefore versions **independently**, on its own tags. It does
not track `B44.Common`'s number and should not be expected to.

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
2. The **harness** (`B44SmokeRunner`) observes that, checks required autoloads
   and declared node paths, and captures engine errors.
3. The harness emits one standardized **marker line** and a human-readable
   report, then quits with a deterministic exit code.
4. The **workflow** asserts on the marker and the exit code.

Collapsing steps 1 and 3 into "the same signal" is the mistake to avoid: the
game's state is its own concern, and the marker is a CI artifact. They must
agree, not be identical.

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
