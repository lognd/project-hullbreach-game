# Adding a block behaviour

Two recipes: a new **variant** of an existing block type (a new weapon or
thruster mode -- most powerups and combat additions are this), and a new
block **type** from scratch (a new row in `BlockTypes`).

Read `docs/architecture.md`'s "modifier byte" and "ShipBody.Step" sections
first; this doc assumes you already know what the facing/ramp/variant bits
are and where `ShipBody.Step` calls into behaviours.

## Recipe: a new variant (worked example -- Scatter Shot)

Goal: a fourth Cannon variant that fires three projectiles in a small
spread instead of one, as a stretch powerup alongside the gravity gun and
anti-gravity gun.

### 1. Pick the variant id

Cannon already uses variant 0 (`CannonBehaviour`), 1 (`GravityGunBehaviour`),
2 (`AntiGravityGunBehaviour`). Scatter Shot is variant 3. Variant ids are
per-`TypeId`, 0..15 (4 bits, `Hullbreach.Core.BlockVariants`), so this does
not collide with Thruster's variant 1 (`SeekingThrusterBehaviour`).

### 2. Write the behaviour class

`CannonBehaviourBase` (`Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviourBase.cs`)
handles cooldown and a *single* shot; Scatter Shot needs three shots per
trigger pull, so it implements `IBlockBehaviour` directly instead of
subclassing that base, reusing the same `ctx.TickCooldown()` /
`ctx.ResetCooldown()` / `ctx.Fire()` primitives `CannonBehaviourBase` uses
internally.

New file `Assets/Scripts/Hullbreach.Ship/Behaviours/ScatterCannonBehaviour.cs`:

```csharp
using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>Cannon variant 3: fires three of the ship's own Projectile
    /// spec in a small fan (center, +SpreadDegrees, -SpreadDegrees) on one
    /// trigger pull and cooldown, instead of CannonBehaviourBase's single
    /// shot. Written against IBlockBehaviour directly (not
    /// CannonBehaviourBase) because the base type only ever records one
    /// ShotRequest per Step.</summary>
    public sealed class ScatterCannonBehaviour : IBlockBehaviour
    {
        /// <summary>Half-angle, in degrees, of the two side shots either
        /// side of straight ahead.</summary>
        public float SpreadDegrees = 12f;

        /// <summary>Ticks cooldown and, if the fire button was pressed this
        /// Step and the cooldown has elapsed, fires three shots and applies
        /// one combined recoil impulse along the center direction.</summary>
        public void Step(ref BlockContext ctx)
        {
            float remaining = ctx.TickCooldown();
            if (!ctx.Input.FirePressed || remaining > 0f) return;

            float2 facing = ctx.Facing;
            float2 muzzleLocal = ctx.LocalCenter + facing * 0.6f;
            float2 worldOrigin = ctx.Ship.LocalToWorld(muzzleLocal);
            var spec = ctx.Ship.Projectile;

            FireOne(ref ctx, worldOrigin, facing, 0f, spec);
            FireOne(ref ctx, worldOrigin, facing, SpreadDegrees, spec);
            FireOne(ref ctx, worldOrigin, facing, -SpreadDegrees, spec);

            float2 worldDirection = ctx.Ship.RotateLocalToWorld(facing);
            ctx.Ship.ApplyImpulseAtWorldPoint(ctx.WorldCenter, -worldDirection * spec.Impulse);
            ctx.ResetCooldown(ctx.Ship.CannonCooldown);
        }

        /// <summary>Fires one shot along `localFacing` rotated by
        /// `degrees`, converted to world space.</summary>
        static void FireOne(ref BlockContext ctx, float2 worldOrigin, float2 localFacing,
                             float degrees, in ProjectileSpec spec)
        {
            float radians = math.radians(degrees);
            float2 rotated = RotateLocal(localFacing, radians);
            float2 worldDirection = ctx.Ship.RotateLocalToWorld(rotated);
            ctx.Fire(new ShotRequest(ctx.Key, worldOrigin, worldDirection, spec));
        }

        static float2 RotateLocal(float2 v, float radians)
        {
            float s = math.sin(radians);
            float c = math.cos(radians);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
```

### 3. Register it

`Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs`,
`RegisterDefaults()`:

```csharp
Register(Hullbreach.Core.BlockTypes.Cannon, 3, new ScatterCannonBehaviour());
```

`BehaviourRegistry.Resolve` needs no changes; it already looks up
`(TypeId, Variant)` from the table.

### 4. Expose it as a powerup in the scene

`PowerupSpawner` on the `Powerups` object in `DemoScene.unity` carries an
array of `PowerupPreset` (`Assets/Scripts/Hullbreach.Game/PowerupSpawner.cs`)
authored in the Inspector -- to add a fourth pickup, add one more
`PowerupPreset` element with `baseTypeId = BlockTypes.Cannon`,
`variant = 3`, a `seconds` duration, a `color`, and a `displayName`, the
same shape as the existing gravity-gun/anti-gravity-gun/seeking-thruster
entries. Since `DemoScene.unity` is hand-authored YAML that no one has
opened in the actual Unity editor yet (see `docs/demo-scene.md`), adding
an array element by hand means adding one more `PowerupPreset` struct
block to the `presets` array in the scene YAML, matching the existing
entries' field layout exactly -- or, once someone has the editor open,
doing it from the Inspector instead and letting Unity write the YAML.

At runtime a ship picks it up through `Powerup.OnTriggerEnter2D` ->
`ShipBody.ApplyPowerup(variant, baseTypeId, point, seconds)`, which finds
the nearest block of `baseTypeId` on that ship, swaps its `Modifiers`'
variant bits to 3 for `seconds`, and reverts to 0 when `TickPowerups`
expires it -- no scatter-shot-specific code needed there at all, since the
whole mechanism reads the variant bits generically.

### 5. Give it a renderer tint

`ShipRenderer` (`Assets/Scripts/Hullbreach.Game/ShipRenderer.cs`) tints by
`TypeId` for the base color and by `Modifiers`/overlay for everything
else; it does not currently special-case variants for a distinct color the
way `Powerup`'s "slow white pulse" reads on the *ship* while a powerup is
active (see the pulse logic near where `visual.TypeId`/`visual.Modifiers`
are compared). If Scatter Shot should visually stand out beyond that pulse,
add a small tint nudge alongside the existing `BlockVariants.Get`
check in `ShipRenderer`'s per-block color logic, following the same
pattern the gravity gun/anti-gravity gun already use there.

### 6. Test it with `NullWorldSink`

Every `ShipBody` defaults `World` to `NullWorldSink.Instance`
(`Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs`), so a plain
NUnit test never needs a scene. Follow the pattern in
`Assets/Tests/EditMode/Hullbreach.Ship.Tests/BlockBehaviourTests.cs`:

```csharp
[Test]
public void ScatterCannon_Shot_FiresThreeProjectiles()
{
    BehaviourRegistry.RegisterDefaults();

    var ship = new ShipBody();
    ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
    ship.Grid.TryAdd(BlockKey.Pack(0, 1),
        new Block(BlockTypes.Cannon, BlockVariants.With(0, 3)));

    ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);

    Assert.AreEqual(3, ship.PendingShots.Count,
        "one trigger pull on a Scatter Shot cannon should record three shots");
}
```

Add this to `Assets/Tests/EditMode/Hullbreach.Ship.Tests/BlockBehaviourTests.cs`
(or a new file in that same folder) -- it compiles and runs under
`tools/plaincs/run_tests.sh` with zero Unity involvement, since
`Hullbreach.Ship` and its tests are both engine-free. See
`docs/testing.md` for how to run it and how the asmdef references work.

## Recipe: a brand-new block type

Adding a whole new `BlockTypes` entry (not just a variant) touches more
files, all in `Core` and `Builder` plus optionally `Ship`/`Game`:

1. **`Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs`**: add a `public
   const byte NewType = 7;` to `BlockTypes` (next free id after
   `RetroThruster = 6`) and one row to the `Table` array: `name, mass,
   youngsModulus, poissonClass, yieldStress, spallStress,
   compressiveStress`, all normalized against Hull's `YieldStress = 1.0`
   the same way every existing row is (see the big comment above `Table`
   for how Core/Hull/Armor/etc. were balanced relative to each other).
2. **`Assets/Scripts/Hullbreach.Builder/BlockPalette.cs`**: add one more
   entry to the `Cost` array (same index as the new `BlockTypes` id) and,
   if the new type has no facing, nothing else -- `IsSymmetric` already
   defaults to true for everything except Cannon and Fin. If the new type
   *is* directional, add it to the `!=` chain in `IsSymmetric` so the
   two-click facing flow (S30) applies to it.
3. **`Assets/Scripts/Hullbreach.Builder/Clearance.cs`**: add a `case
   BlockTypes.NewType:` to `TryReservedCells` if the new block needs
   reserved empty cells (an exhaust, a muzzle, side nozzles -- anything
   that must stay clear), and/or a branch in `RequiredAnchor` if it must
   sit on a specific existing block the way Fin requires hull behind it.
   A plain block (like Hull/Armor) needs neither -- `TryReservedCells`
   already returns an empty list and `RequiredAnchor` already returns
   false for anything not explicitly matched.
4. **`Assets/Scripts/Hullbreach.Game/ShipRenderer.cs`**: add a `case
   BlockTypes.NewType => new Color(r, g, b),` to the type-color switch
   (see the existing table for Core/Hull/Armor/etc.) so it renders as
   something other than the switch's fallback color.
5. **A behaviour, only if the block does anything per-tick.** Follow the
   variant recipe above with `variant = 0` on the new `TypeId` (variant 0
   is always a type's base behaviour), and if it should be one of
   `ShipBody`'s cached key groups (like `ThrusterKeys`/`WeaponKeys`), add
   it to `RebuildDerivedViews` in `ShipBody.cs` so `Step` actually calls
   into it every tick. A structural-only block (Hull/Armor-like) needs no
   behaviour at all -- `BehaviourRegistry.Resolve` already returns `null`
   for a type with nothing registered, and `ShipBody.Step` only calls
   behaviours for the key groups it iterates.
6. **Tests**: extend `Assets/Tests/EditMode/Hullbreach.Core.Tests/BlockTypesTests.cs`
   for the table entry, `Hullbreach.Builder.Tests/PlacementRulesTests.cs`
   and/or `Hullbreach.Builder.Tests/BlockPaletteTests.cs` for clearance and
   palette, and a new `Hullbreach.Ship.Tests` case if you added a
   behaviour, mirroring the Scatter Shot test above.
