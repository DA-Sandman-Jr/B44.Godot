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

**Unverified until this happens.** Everything below is reasoned, not observed:

- That `GodotSharp` as a `PrivateAssets="all"` compile-time reference coexists
  cleanly with a consuming game's `Godot.NET.Sdk` at runtime.
- That the workflow's Godot download URL and archive layout are correct for
  4.7.0 — the mono Linux x86_64 naming has changed between Godot releases before.
- That `--headless --import` on a cold checkout leaves the project in a state
  where the smoke scene loads.
- That `GetTree().Quit(code)` surfaces as the process exit code under
  `--headless`, which the marker check partly compensates for but does not prove.

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
