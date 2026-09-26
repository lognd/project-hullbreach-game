using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.Ship.Behaviours;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // The game-side IWorldSink: routes block-behaviour requests onto
    // the actual scene. See the reference page.
    // frob:doc docs/reference/hullbreach-game.md#worldsink
    [DefaultExecutionOrder(-150)]
    public sealed class WorldSink : MonoBehaviour, IWorldSink
    {
        [SerializeField] ProjectileSpawner spawner;

        // Null before Awake has run / in a scene with no WorldSink at all.
        // frob:doc docs/reference/hullbreach-game.md#worldsink
        public static WorldSink Instance { get; private set; }

        readonly List<ShipController> _controllers = new List<ShipController>();
        readonly List<ShipBody> _ships = new List<ShipBody>();

        // Refreshed on Start and whenever Refresh() is called.
        // frob:doc docs/reference/hullbreach-game.md#worldsink
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

        // Call after a ship is spawned or destroyed at runtime;
        // Start alone is enough for the demo today.
        // frob:doc docs/reference/hullbreach-game.md#worldsink
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

        // frob:doc docs/reference/hullbreach-game.md#worldsink
        public void SpawnProjectile(in ShotRequest shot)
        {
            if (spawner != null) spawner.SpawnFromSink(shot);
        }

        // frob:doc docs/reference/hullbreach-game.md#worldsink
        public void AddTemporaryGravity(GravityBody body, float seconds)
        {
            GravityWorld.Field?.AddTemporary(body, seconds);
        }

        // False when no other ship is known.
        // frob:doc docs/reference/hullbreach-game.md#worldsink
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
