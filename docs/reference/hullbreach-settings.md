# Hullbreach.Settings reference

Per-type reference for `Assets/Scripts/Hullbreach.Settings`, linked from the
code by `// frob:doc docs/reference/hullbreach-settings.md#<anchor>`. One
heading per public type. The assembly is engine-free and references
nothing; the settings screen (T-0048) and the audio/display code in
`Hullbreach.Game` consume it. Architecture-level context lives in
[architecture.md](../architecture.md).

### GameSettings

The player's preferences as plain mutable data: `MasterVolume`,
`MusicVolume`, `EffectsVolume` (each 0..1, defaults 1.0 / 0.8 / 1.0),
`ResolutionWidth` x `ResolutionHeight` (default 1920x1080, each edge
accepted from a file only within 320..16384) and `Fullscreen` (default
true). `KeyBindings` is a placeholder for T-0049: a sorted action-name to
key-name map that is written and read back but not yet consumed by any
input code. `Normalize` clamps volumes (NaN becomes 0) and resets a
resolution pair with an out-of-range edge to the default. `Clone` gives the
settings screen an editable copy so Cancel is free.

### ReadOutcome

What `ISettingsStorage.Read` found: `Found`, `Missing` (first launch, not an
error) or `Unreadable` (exists but locked or forbidden).

### ISettingsStorage

The seam between settings and the disk. `Read` and `TryWrite` report I/O
trouble as a result, never an exception, so a bad disk cannot crash a
launch. Tests use an in-memory implementation; the game uses
`FileSettingsStorage`.

### FileSettingsStorage

`ISettingsStorage` over one file. The caller supplies the path (Game passes
a file under `Application.persistentDataPath`), which keeps this assembly
free of Unity. A write goes to `<path>.tmp` and is then copied over the
real file, so a crash mid-save leaves the previous file intact; missing
parent directories are created.

### SettingsLoadStatus

`Loaded` (a file was read, possibly with per-value warnings), `Missing`
(no file, defaults) or `Corrupt` (unreadable, empty, wrong header: defaults).

### SettingsLoadResult

`Settings` (never null), `Status` and `Warnings`. A corrupt or missing file
yields fresh defaults and is left on disk untouched; the next `Save`
replaces it.

### SettingsFile

The text format, version 1: first line `hullbreach-settings 1`, then one
`key value` line per setting; blank lines and `#` comments are ignored.

```
hullbreach-settings 1
master_volume 0.35
music_volume 0
effects_volume 0.9
resolution_width 1280
resolution_height 720
fullscreen false
bind.fire Space
```

`bind.<action> <key>` lines carry the key-binding placeholder. Floats and
ints use the invariant culture, so a German locale does not write `0,35`.
Parsing is forgiving per value and strict per file: a bad header means
`Corrupt` + defaults; an unparsable or unknown line is skipped with a
warning and that field keeps its default; out-of-range values are clamped
by `Normalize`; a newer version still reads the keys it knows and warns.

### SettingsSaveResult

`Ok`, or `Error` with the storage's reason (for example a full disk).

### SettingsStore

`Load` always returns usable settings, whatever is on disk. `Save`
normalizes a COPY (a wild slider value never reaches the file and the
caller's object is untouched), writes it through the storage and reports
failure in the result.
