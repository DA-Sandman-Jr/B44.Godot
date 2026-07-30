# Third-Party Notices

This file exists because B44 policy requires it in any repository that may
carry vendored, ported, or converted third-party code. It is the single place
that surface is tracked.

## Currently: none

No third-party source is vendored, ported, or converted into this repository.
Every file here is B44's own work.

`GodotSharp` is referenced as a NuGet package with `PrivateAssets="all"` — a
compile-time dependency that is neither redistributed nor copied into this
source tree, so it is not a vendoring event and creates no obligation here.
Godot Engine is MIT-licensed; a consuming game that ships Godot carries that
notice, as it would with or without this package.

## If that changes

Adding vendored, ported, or converted third-party code means adding an entry
below with the upstream name, version or commit, licence, and what was taken.
Note that converting or hand-porting does **not** shed the upstream licence — a
port is a derivative work and the attribution obligation follows it. See the
isolation and clean-room rules in the B44 organization guidance before starting.
