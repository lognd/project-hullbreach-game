using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.Ship.Behaviours;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>
    /// The game-side IWorldSink: routes block-behaviour requests (spawn a
    /// projectile, drop a temporary gravity well, find the nearest enemy)
    /// onto the actual scene: ProjectileSpawner, GravityWorld.Field, and
    /// every ShipController found in the scene. One instance per scene,
    /// exposed as a static Instance (same convention as GravityWorld.Field)
    /// so ShipController.Awake can wire it up without a scene-graph
    /// reference. Caches the ship list at Start and can be refreshed later
    /// (e.g. after a ship is destroyed/spawned) via Refresh().
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class WorldSink : MonoBehaviour, IWorldSink
    {
        [SerializeField] ProjectileSpawner spawner;

        /// <summary>The live sink for this scene, or null before Awake has
        /// run / in a scene with no WorldSink at all.</summary>
        public static WorldSink Instance { get; private set; }

        readonly List<ShipController> _controllers = new List<ShipController>();
        readonly List<ShipBody> _ships = new List<ShipBody>();

        /// <summary>Every ship body known to this sink, refreshed on Start
        /// and whenever Refresh() is called.</summary>
        public IReadOnlyList<ShipBody> Ships => _ships;

        void Awake()
        {
            Instance = this;
            if (spawner == null) spawner = FindFirstObjectByType<ProjectileSpawner>();
        }

        void Start() => Refresh();

        void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>Re-scans the scene for ShipControllers. Call after a ship
        /// is spawned or destroyed at runtime; the demo never does either
        /// today, so Start alone is enough for it, but a later mode
        /// (multiplayer lobby, respawn) will need this.</summary>
        public void Refresh()
        {
            _controllers.Clear();
            _ships.Clear();
            foreach (var controller in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
            {
                _controllers.Add(controller);
                if (controller.Ship != null) _ships.Add(controller.Ship);
            }
        }

        /// <summary>Hands the shot to the scene's ProjectileSpawner, if any.</summary>
        public void SpawnProjectile(in ShotRequest shot)
        {
            if (spawner != null) spawner.SpawnFromSink(shot);
        }

        /// <summary>Drops a temporary gravity well/anti-well into this
        /// scene's GravityWorld.Field, if one exists.</summary>
        public void AddTemporaryGravity(GravityBody body, float seconds)
        {
            GravityWorld.Field?.AddTemporary(body, seconds);
        }

        /// <summary>Finds the nearest ship (by ShipBody.Position) other than
        /// `self`. False when no other ship is known.</summary>
        public bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity)
        {
            position = float2.zero;
            velocity = float2.zero;
            float bestDistSq = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < _ships.Count; i++)
            {
                var candidate = _ships[i];
                if (candidate == null || ReferenceEquals(candidate, self)) continue;
                float distSq = math.distancesq(candidate.Position, from);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    position = candidate.Position;
                    velocity = candidate.Velocity;
                    found = true;
                }
            }

            return found;
        }
    }
}
