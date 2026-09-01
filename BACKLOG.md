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

## Known Defects

No known defects are currently queued in this repository.

---

## Notes

A private game is the first demonstrated consumer that needs the Godot logger
sink composed with a second destination; `WhispersOfTheEarth` records the same
need for its diagnostics recent-issue buffer.

A shared sink-composition helper is judged on the capability, not on a headcount
of consumers: it becomes extractable when its seam is small and coherent, its API
stays domain-facing, and independent evidence says the reuse is real. A second
materially equivalent consumer is one form of that evidence, not a precondition.
Nothing is scheduled here yet — the seam has not been settled, and a broader
logging-architecture revisit would still supersede it.
