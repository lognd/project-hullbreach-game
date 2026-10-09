# Ship design file format

Version 1. Written and read by `ShipDesignFile`
(`Assets/Scripts/Hullbreach.Builder`). This is the format a player's saved
design uses locally and the one the platform's design storage (S35) should
carry as an opaque text blob; treat it as stable, and only ever change it
by bumping the version.

```
hullbreach-design 1
name Brick
block 0 0 0 0 0
block 0 1 1 0 0
```

- Line 1 is the magic word and the integer format version, separated by one
  space. Readers refuse a version newer than they know and never guess.
- `name <text>` is optional, at most once; the rest of the line is the name
  (newlines are replaced by spaces on write).
- `block <x> <y> <type> <modifiers> <damage>` is one block: signed integers,
  separated by single spaces, in the order of the net wire's
  `SnapshotBlock` (x, y, type id, modifiers, damage). `x` and `y` are
  BlockKey coordinates (-128..127), `type` is a `BlockTypes` id, `modifiers`
  carries the facing, and `damage` must be 0 in a saved design.
- Blank lines and lines starting with `#` are ignored. `\r\n` is accepted;
  files are written with `\n`, UTF-8, integers in the invariant culture.
- Block order carries no meaning. `ShipDesign.FromGrid` writes sorted-key
  order so equal designs produce identical files.

## Validation on load

`ShipDesignFile.Load` parses, then replays the blocks through
`PlacementRules` (see
[the validator](reference/hullbreach-builder.md#shipdesignvalidator)). A
file that is not syntactically a design returns an error with the line
number. A parsed design that breaks the CURRENT rules comes back unchanged
together with a list of problems (cell and reason each); nothing is
dropped, snapped or repaired. Rules can change between releases, so a
design saved yesterday may be reported today: that is the point of
validating on load rather than on save.
