# B44.Godot — Backlog

Agreed work that has not started or is not yet complete. Settled architecture
and operational guidance live in [`CLAUDE.md`](CLAUDE.md); completed work is
removed from this file after release.

Status values: **Planned**, **In progress**, **Blocked**, and **Deferred**.

Cross-repository programs live once in
[`B44.Common`'s backlog](https://github.com/DA-Sandman-Jr/B44.Common/blob/main/BACKLOG.md).

---

## Planned Work

### Reusable reproducible game export CI


**Status:** **Deferred** until the B44 games are closer to release.

Add reusable clean-checkout Godot export automation for B44 games. The shared
capability should prove that a consuming game's existing export configuration
can produce its declared distributable artifacts in CI, complementing the
existing engine-free build/test and Godot composition-smoke layers.

B44.Godot owns the reusable Godot-specific export mechanics. Each game remains
authoritative for its export presets, supported platforms, signing, store and
publishing policy, and other product-specific release configuration.

---

### Enable the UID sidecar check in each Godot game

**Status:** **Planned** since 2026-08-26, once someone can run the Godot editor.

`reusable-godot-uid-check.yml` ships and is verified, but no game calls it yet.
Enabling it is one job block per repository — and three of the five would go
red immediately, because they are already missing sidecars:

| Repository | Tracked C# scripts | Missing `.uid` |
|---|---|---|
| TicTacHoe | 178 | 0 |
| WhispersOfTheEarth | 691 | 0 |
| NowhereToNest | 65 | **9** |
| EthicsAcademy | 6 | **3** |
| TimeMachineClicker | 77 | **1** |

The missing thirteen include `NowhereToNest/Scripts/Presentation/Painters/
TerrainContourPainter.cs`, a real presentation script whose references break on
a fresh clone. Turning them green requires opening each project in the Godot
editor once and committing what it generates. **Do not hand-write a UID**: a
fabricated value looks authoritative and resolves to nothing, which is worse
than the missing file it replaces.

Sequence per game: open the editor, commit the generated sidecars, then add the
job. Adding the job first only produces a red build nobody can fix from a
terminal.

## Known Defects

No known defects are currently queued in this repository.

---

## Notes

A private game is the first demonstrated consumer that needs the Godot logger
sink composed with a second destination. Reconsider a shared sink-composition
helper only when a second materially equivalent consumer appears or the
logging architecture is explicitly revisited.
