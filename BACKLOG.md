# B44.Godot — Backlog

Agreed work that has not started or is not yet complete. Settled architecture
and operational guidance live in [`CLAUDE.md`](CLAUDE.md); completed work is
removed from this file after release.

Status values: **Planned**, **In progress**, **Blocked**, and **Deferred**.

Cross-repository programs live once in
[`B44.Common`'s backlog](https://github.com/DA-Sandman-Jr/B44.Common/blob/main/BACKLOG.md).

---

## Planned Work

No B44.Godot-local planned work is currently queued.

---

## Known Defects

No known defects are currently queued in this repository.

---

## Notes

Whispers of the Earth is the first demonstrated consumer that needs the Godot
logger sink composed with a second destination (`DiagnosticsCapture.Recent`).
Reconsider a shared sink-composition helper only when a second materially
equivalent consumer appears or the logging architecture is explicitly revisited.
