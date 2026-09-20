using System;

namespace Hullbreach.Net
{
    /// <summary>
    /// Wire formats.
    ///
    /// THE GOVERNING RULE: send CAUSES, never EFFECTS.
    ///
    /// Integer flood fill is bit-deterministic on every platform; the float FE
    /// solve is not (SIMD width, FMA contraction, iteration counts all
    /// diverge). So the server sends "block (x,y) died" and BOTH sides
    /// independently derive which components detached. A hit that splits a
    /// 10,000-block ship in half puts 4 bytes on the wire, not 5,000 blocks.
    ///
    /// The FE result never goes on the wire at all. Clients compute their own
    /// purely for the S37 tint, where divergence is cosmetic and invisible.
    /// </summary>
    public static class NetMessages
    {
        // TODO: Full snapshot, reliable, sent once on join or respawn.
        //       5 bytes per block raw; block grids deflate ~10:1, so a
        //       10k-block ship is a few KB. Fine as a one-off.
        //
        //   ShipSnapshot { u16 count; { i8 x, i8 y, u8 typeId, u8 mods, u8 damage }[count] }

        // TODO: Per-tick state, unreliable, ~30 Hz. 14 bytes per ship, so two
        //       ships cost under 1 KB/s. Quantize: position at 1/256 m, angle
        //       as a 16-bit turn.
        //
        //   ShipState { u16 netId; i16 px,py; u16 rot; i16 vx,vy; u16 av }

        // TODO: Events, reliable and ORDERED -- destruction must apply in the
        //       same order on both sides or the derived split diverges. Carry a
        //       sequence number.
        //
        //   BlockPlaced     { u16 netId; i8 x,y; u8 typeId; u8 mods }   6 B
        //   BlockDestroyed  { u16 netId; i8 x,y }                       4 B
        //   FragmentSpawned { u16 parentId, newId; i16 px,py; u16 rot;
        //                     i16 vx,vy; u16 av }
        //
        //       FragmentSpawned carries NO block list. Both sides already know
        //       which blocks left, because they ran the same flood fill.
    }
}
