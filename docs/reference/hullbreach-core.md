# Hullbreach.Core reference

Per-type reference for `Assets/Scripts/Hullbreach.Core`, linked from the code by
`// frob:doc docs/reference/hullbreach-core.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### Block

Deliberately a small readonly struct: no heap allocation (so no GC
pressure/hitches), stored inline in arrays (cache-friendly iteration), and
blittable (so Burst can compile over it). Everything shared by all blocks
of a kind lives in BlockType instead, looked up by TypeId: that flyweight
split is why this is not an abstract base class with a subclass per block
kind, which would cost a heap object and a virtual call in the innermost
loop of the FE assembly and lock the simulation out of Burst.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.TypeId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.Modifiers -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.Damage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.Block -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.DamageFraction -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.WithDamage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Block.cs::Block.WithModifiers -->

### BlockGrid

The authoritative sparse block map. Everything else in the simulation is a
derived view rebuilt from this, so MUTABLE per-block state (damage,
upgrades) must live here and nowhere else: a derived view that holds state
silently loses it on the next rebuild.

A managed Dictionary on purpose. Get it correct first; swapping to
NativeHashMap for Burst is a later mechanical change, and the tests will
tell you immediately if it broke something.

`_sortedKeys` is a sorted snapshot of `_blocks.Keys`, rebuilt lazily
whenever `_keysVersion` no longer matches `_structureVersion`. TryAdd and
TryRemove bump `_structureVersion` (they change the key set); TrySet does
not (it only rewrites a value in place), so damage accumulation never pays
for a resort.

`CoreKey` is the packed key of the core, or null when this grid is debris.
A fragment that breaks off has NO core, so connectivity has no root and
simply does not run for it. This is why the core is nullable rather than
assumed to sit at (0,0).

`TopologyDirty` is true when a derived view needs rebuilding. At a few
hundred blocks a full rebuild is microseconds; make this per-chunk only
once profiling says to, and keep the accessor so callers never change.

`All`/`KeyCount`/`KeyAt`/`SortedKeys` give allocation-free, deterministic
iteration; the sorted view is rebuilt lazily by `EnsureSortedKeys` when a
structural edit happened since the last rebuild, and `SortedKeys`'s span
is only valid until the next structural edit.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.CoreKey -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.Count -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.Mass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.TopologyDirty -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.All -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.KeyCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.KeyAt -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.SortedKeys -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.TryAdd -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.TryRemove -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.TryGet -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.Contains -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.TrySet -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.ClearDirty -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.CenterOf -->

### BlockGrid.BlockEnumerable

Thin struct wrapper around `Dictionary<int,Block>.Enumerator` so foreach
over `BlockGrid.All` never boxes the enumerator (a plain `IEnumerable<T>`
return type would box it on every foreach, once per Step per grid). The
struct `GetEnumerator` is the foreach pattern-match target the compiler
prefers over the interface methods, which exist only as fallbacks for
LINQ and other `IEnumerable`-typed consumers and box the enumerator same
as before this change.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.BlockEnumerable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.BlockEnumerable.BlockEnumerable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs::BlockGrid.BlockEnumerable.GetEnumerator -->

### BlockKey

Packs a signed 2D grid coordinate into a single int. A packed int (not
(int,int)) because ValueTuple hashing goes through per-field
EqualityComparer<T>.Default and is measurably slower, and the packed int
is exactly what goes on the wire, so the network identity and the
dictionary key end up being the same thing.

GRID CONVENTION: everything downstream depends on this: block (x, y)
occupies the unit square [x, x+1] x [y, y+1], so its center is at
(x + 0.5, y + 0.5).

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Min -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Max -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Pack -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Unpack -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.Neighbors -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs::BlockKey.InRange -->

### BlockType

Immutable, shared, one per kind of block. Looked up by Block.TypeId. This
is the "type object" half of the flyweight.

UNITS: do not use SI. Steel is E = 200e9 Pa with yield 250e6 Pa, and
running CG on numbers spanning 1e9 throws away float precision and wrecks
conditioning. Normalize instead: plain hull has YieldStress 1.0 and
everything else is relative to it. The solver behaves better and the
numbers stay readable to whoever balances the game.

`YoungsModulus` factors straight out of the element stiffness, since
K_e = E * KHat[PoissonClass]. `PoissonClass` is an index into the
precomputed KHat table; Poisson's ratio is quantized into a few classes
because K is not linear in nu, so a continuous nu would cost you the
precomputation entirely. `YieldStress` is the ductile limit, compared
against von Mises of the quasi-static load case. `SpallStress` is the
brittle limit, compared against max TENSILE principal stress of the
impulsive load case. `CompressiveStress` is the brittle limit in
compression, much larger than SpallStress for armor-like materials:
brittle solids are far stronger in compression than in tension.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.Width -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.Height -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.Name -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.Mass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.YoungsModulus -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.PoissonClass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.YieldStress -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.SpallStress -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.CompressiveStress -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockType.BlockType -->

### BlockTypes

The block type table. Index with Block.TypeId. `RetroThruster` pushes the
ship BACKWARD via two small side nozzles that exhaust forward, and fires
on the reverse key.

The table is normalized against Hull.YieldStress = 1.0:
- Core: the heaviest, toughest block: it must survive whatever kills
  everything around it, so both stresses are the highest in the table and
  it is stiffer than hull.
- Hull: the reference. Ductile: ordinary yield, ordinary spall.
- Armor: ~2x hull mass, stiffer (denser lattice), and brittle: high
  SpallStress/CompressiveStress (brittle solids take compression far
  better than tension) but LOWER YieldStress than hull: it is meant to
  shatter rather than bend.
- Thruster: hull-like stiffness/strength, a bit heavier for the machinery
  packed inside.
- Cannon: same idea as Thruster: hull-like structurally, a little heavier
  for its mechanism.
- Fin: light control surface: cheap mass so placement is about leverage,
  not weight, and slightly weaker than hull since it is a thin surface
  rather than a hull plate.

`EffectiveStiffness` folds in damage softening: a yielded block gets less
stiff so it sheds load to its neighbors, which is what makes ductile
failure actually read as ductile, without needing a nonlinear solve. This
one scalar is the entire reason K_e = E * KHat works, so everything that
changes stiffness must go through here. The floor (0.05) is a placeholder
curve that just keeps K non-singular; a later DamageModel may replace it
with something that better matches real ductile softening (TODO D3).

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Core -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Hull -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Armor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Thruster -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Cannon -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Fin -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.RetroThruster -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Get -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.Count -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs::BlockTypes.EffectiveStiffness -->

### BlockVariants

Decodes bits 4-7 of Block.Modifiers into a per-type "variant" id. Variant
0 is always the base behaviour for a type (e.g. plain Cannon); nonzero
variants are alternate behaviours registered against the same TypeId
(e.g. the gravity gun is Cannon variant 1). Lives in Core, alongside
Facing (bits 0-1) and ThrusterUpgrades (bits 2-3), since builder placement
code needs to read/write variant bits without depending on
Hullbreach.Ship.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockVariants.cs::BlockVariants -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockVariants.cs::BlockVariants.Mask -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockVariants.cs::BlockVariants.Get -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/BlockVariants.cs::BlockVariants.With -->

### Facing

Decodes the low 2 bits of Block.Modifiers into a ship-local facing. Lives
in Core (rather than Ship) so builder placement rules (which need to know
what is "ahead of" or "behind" a directional block) can use the same
encoding without depending on Hullbreach.Ship.

Encoding: 0 = +y ("up"), 1 = +x, 2 = -y, 3 = -x, all in ship-local space
before Rotation is applied.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Mask -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Step -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Direction -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Opposite -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Perpendicular -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Ahead -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Grid/Facing.cs::Facing.Behind -->

### MassProperties

Running mass, center of mass and rotational inertia, maintained in O(1) as
blocks come and go. No traversal, ever.

Deliberately NOT delegated to the physics engine. Rigidbody2D's
useAutoMass recomputes from collider geometry on every change, which is
slower, gives no control over the value the netcode must agree on, and,
worth knowing, com.unity.physics is a 3D package that cannot help a 2D
game at all. Unity's 2D physics is Box2D behind Rigidbody2D.

The accumulators (`Total`, `FirstMoment`, `SecondMomentAboutOrigin`) are
kept about the ORIGIN and shifted to the center of mass on read, via the
parallel axis theorem. That is precisely what makes removal O(1): you
cannot incrementally maintain a quantity measured about a center that
itself moves when you edit. `CenterOfMass` is zero (not NaN) for an empty
grid, since dividing by zero mass is meaningless. `Add`/`Remove` take
`center` in ship-local space and `localInertia` about the block's OWN
center; the parallel axis theorem folds the block's own inertia plus its
offset into the origin-frame second moment, all in O(1), and `Remove` is
the exact inverse of `Add`.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.Total -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.FirstMoment -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.SecondMomentAboutOrigin -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.CenterOfMass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.InertiaAboutCenterOfMass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.RectangleInertia -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.Add -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Mass/MassProperties.cs::MassProperties.Remove -->

### Articulation

Cut vertices of the block adjacency graph: blocks whose removal would
disconnect the ship.

This is the optimization that makes removal cheap. Removing a block that
is NOT an articulation point cannot split anything, so the flood fill is
skipped outright. Ships are mostly 2-connected blobs, so most removals
take the fast path.

It doubles as UI: these are exactly the load-bearing blocks, which pairs
naturally with the S37 stress tint.

`Compute` uses Tarjan's algorithm, O(V + E), once per topology change,
with an ITERATIVE DFS: a recursive one would blow the stack on a large
ship. Works with no core (any block can serve as the DFS root, since
articulation points are a property of the adjacency graph alone) and
correctly reports no articulation points for a single block (a root is
only a cut vertex when it has 2+ DFS children).

The DFS also skips only the single edge back to a node's immediate tree
parent (not all edges to it), since 4-connected grid adjacency never has
more than one edge between the same pair of nodes; that is why one
comparison (`child == frame.Parent`) is safe.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Articulation.cs::Articulation -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Articulation.cs::Articulation.Compute -->

### Connectivity

Which blocks are still attached to the core (S38).

This is a plain flood fill and it should stay one. At a few hundred
blocks an O(n) pass is microseconds. Union-Find handles unions but not
deletions, and deletion-capable structures (Euler tour trees, link-cut
trees) are wildly out of proportion to the problem.

DETERMINISM: this is all integer work, so it reproduces bit-exactly on
every platform, unlike the float FE solve, which does not. That asymmetry
is what lets the server send only "block (x,y) died" and have both sides
independently derive the same detached components, instead of ever
putting a block list on the wire.

`ReachableFromCore` is left empty when the grid has no core: a fragment
is debris and has nothing to stay attached to. `FindDetached` is
everything NOT reachable from the core; in combat, apply every
destruction for the tick FIRST and then call this once: one fill for the
whole batch, never one per block. `SplitIntoComponents` splits a detached
set into individual connected components, so a hit that shears off two
separate chunks yields two debris bodies rather than one; it is a BFS
restricted to the given key set only, same shape as ReachableFromCore but
bounded to the given keys instead of the whole grid.

<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Connectivity.cs::Connectivity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Connectivity.cs::Connectivity.ReachableFromCore -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Connectivity.cs::Connectivity.FindDetached -->
<!-- frob:describes Assets/Scripts/Hullbreach.Core/Topology/Connectivity.cs::Connectivity.SplitIntoComponents -->
