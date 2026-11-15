using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // The own-ship half of what a bot sees: its pose plus the handling numbers a player learns by
    // flying it (thrust, turn rate, cannon axis). Nothing about the enemy ship lives here.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botobservation
    public struct SelfView
    {
        public float2 Position;
        public float Rotation;
        public float2 Velocity;
        public float AngularVelocity;

        // 0 (pristine) to 1 (breached).
        public float CoreDamage;
        public bool Alive;

        // Handling, from the bot's own block layout.
        public float MaxForwardAccel;
        public float MaxRetroAccel;
        public float MaxAngularAccel;

        // Ship-local unit vector the cannons fire along; HasCannon is false when there are none.
        public float2 AimLocal;
        public bool HasCannon;
        public float CannonCooldownSeconds;
        public ProjectileSpec Projectile;
    }

    // The enemy as any player sees it: where it is, how it moves, how hurt its core looks.
    // frob:doc docs/reference/hullbreach-bots.md#botobservation
    public struct EnemyView
    {
        public bool Visible;
        public float2 Position;
        public float2 Velocity;
        public float Rotation;
        public float AngularVelocity;
        public float2 CorePosition;
        public float CoreDamage;

        // Rough radius of the hull, for hit tolerance.
        public float Radius;
    }

    // A shot in flight. Hostile is true for shots the enemy fired.
    // frob:doc docs/reference/hullbreach-bots.md#botobservation
    public struct ProjectileView
    {
        public float2 Position;
        public float2 Velocity;
        public bool Hostile;
    }

    // Everything a bot may know on one tick. Built by BotPeer from the public match state; there is no
    // field for the enemy's design, input, trust, or anything a spectator could not read off the screen.
    // frob:doc docs/reference/hullbreach-bots.md#botobservation
    public sealed class BotObservation
    {
        public uint Tick;
        public float Dt;
        public SelfView Self;
        public EnemyView Enemy;
        public readonly List<ProjectileView> Projectiles = new List<ProjectileView>();
        public readonly List<GravityBody> Planets = new List<GravityBody>();
        public ArenaBounds Bounds;

        // Storm as the HUD shows it; HasStorm is false when the match has none.
        public bool HasStorm;
        public StormState Storm;
    }

    // The boundary a bot plugs in at: one tick of what the player sees in, the same three controls a
    // human has out. A human's version is the keyboard; ShipInput is the struct the server consumes.
    // frob:doc docs/reference/hullbreach-bots.md#ishipinputsource
    public interface IShipInputSource
    {
        // Thrust and steer are in [-1, 1]; FirePressed is an edge, true for one tick per press.
        ShipInput Next(BotObservation observation);
    }
}
