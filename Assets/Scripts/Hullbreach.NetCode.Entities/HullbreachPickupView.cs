using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Game;
using Hullbreach.Net;
using Hullbreach.World;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Draws the replica's pickups in the world (before this they only showed on the minimap) and answers a
    /// collection with a ring burst, a click and a "+1 Hull" toast for the player who took it. Salvage is a tinted
    /// diamond (the tint is the block it holds); a supply crate is a larger square with a bright rim. A pickup about
    /// to expire blinks. Created once at start-up, idle outside an online match.
    /// </summary>
    public sealed class HullbreachPickupView : MonoBehaviour
    {
        sealed class Visual
        {
            public Transform Root;
            public SpriteRenderer Body, Rim;
            public PickupView Last;
        }

        readonly Dictionary<uint, Visual> _live = new Dictionary<uint, Visual>();
        readonly List<PickupView> _views = new List<PickupView>(32);
        readonly List<uint> _gone = new List<uint>();
        readonly List<(Transform t, SpriteRenderer r, float born, float size, Color color)> _bursts = new List<(Transform, SpriteRenderer, float, float, Color)>();
        Sprite _diamond, _square, _ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PickupView");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachPickupView>();
        }

        void Awake()
        {
            _diamond = MakeSprite(32, (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 1f);
            _square = MakeSprite(32, (x, y) => Mathf.Abs(x) <= 0.85f && Mathf.Abs(y) <= 0.85f);
            _ring = MakeSprite(64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return d <= 1f && d >= 0.82f; });
        }

        // A white sprite, filled where `inside` (x and y run -1..1) is true, so it can be tinted by colour.
        static Sprite MakeSprite(int size, System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                    px[j * size + i] = inside((i + 0.5f) / size * 2f - 1f, (j + 0.5f) / size * 2f - 1f) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Color Tint(PickupView v)
        {
            if (v.Kind == PickupKind.Crate) return new Color(0.3f, 0.85f, 1f, 1f);
            return Color.HSVToRGB((v.Type * 0.17f) % 1f, 0.55f, 1f);
        }

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null || !HullbreachNetworkGameplayController.Active || !HullbreachNetCodeClient.IsConnected)
            {
                Clear();
                return;
            }

            replica.Economy.GetPickups(replica.SimulationTick, _views);
            _gone.Clear();
            foreach (uint id in _live.Keys) _gone.Add(id);
            float t = Time.unscaledTime;
            foreach (PickupView v in _views)
            {
                _gone.Remove(v.Id);
                if (!_live.TryGetValue(v.Id, out Visual vis)) _live[v.Id] = vis = Build(v);
                vis.Last = v;
                bool crate = v.Kind == PickupKind.Crate;
                vis.Root.position = new Vector3(v.Position.x, v.Position.y, -0.1f);
                float pulse = 1f + 0.1f * Mathf.Sin(t * 4f + v.Id);
                float size = (crate ? 2.4f : 1.3f) * pulse;
                vis.Root.localScale = new Vector3(size, size, 1f);
                vis.Root.rotation = Quaternion.Euler(0f, 0f, crate ? 0f : t * 40f + v.Id * 37f);
                float blink = v.SecondsLeft < 3f ? (Mathf.Sin(t * 14f) > 0f ? 1f : 0.25f) : 1f;
                var c = Tint(v); c.a = blink;
                vis.Body.color = c;
                vis.Rim.color = new Color(1f, 1f, 1f, blink * 0.9f);
            }

            // Collected / expired notices: only a collection gets a burst.
            ushort local = HullbreachNetCodeClient.LocalNetworkId;
            while (replica.Economy.TryDequeueCollected(out PickupDespawned msg))
            {
                if (msg.Reason != PickupRemoval.Collected) continue;
                if (!TryLast(msg.Id, out PickupView last)) continue;
                bool mine = msg.Collector == local;
                Burst(new Vector2(last.Position.x, last.Position.y), mine ? 3.2f : 1.6f, mine ? new Color(0.4f, 1f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.6f));
                if (!mine) continue;
                FeelFx.PlayClick();
                string what = last.Kind == PickupKind.Crate ? "Supply crate" : BlockTypes.Get(last.Type).Name;
                HullbreachNetworkGameplayController.Toasts.Push($"+{Mathf.Max(1, (int)last.Count)} {what}", Time.unscaledTime);
            }

            // Visuals leave only after the collection notices above have read their last position.
            foreach (uint id in _gone)
            {
                if (_live.TryGetValue(id, out Visual vis)) { Destroy(vis.Root.gameObject); _live.Remove(id); }
            }

            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                float k = (t - b.born) / 0.45f;
                if (k >= 1f) { Destroy(b.t.gameObject); _bursts.RemoveAt(i); continue; }
                b.t.localScale = Vector3.one * Mathf.Lerp(0.4f, b.size, 1f - (1f - k) * (1f - k));
                var col = b.color; col.a *= 1f - k;
                b.r.color = col;
            }
        }

        // The last view of a pickup that has just left the list, so a collection can still say what and where it was.
        bool TryLast(uint id, out PickupView v)
        {
            if (_live.TryGetValue(id, out Visual vis)) { v = vis.Last; return true; }
            v = default;
            return false;
        }

        Visual Build(PickupView v)
        {
            var root = new GameObject("Pickup" + v.Id).transform;
            var body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            body.transform.SetParent(root, false);
            body.sprite = v.Kind == PickupKind.Crate ? _square : _diamond;
            body.sortingOrder = 5;
            var rim = new GameObject("Rim", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            rim.transform.SetParent(root, false);
            rim.sprite = _ring;
            rim.transform.localScale = Vector3.one * 0.9f;
            rim.sortingOrder = 6;
            return new Visual { Root = root, Body = body, Rim = rim, Last = v };
        }

        void Burst(Vector2 at, float size, Color color)
        {
            var go = new GameObject("PickupBurst", typeof(SpriteRenderer));
            go.transform.position = new Vector3(at.x, at.y, -0.15f);
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = _ring; r.sortingOrder = 7; r.color = color;
            _bursts.Add((go.transform, r, Time.unscaledTime, size, color));
        }

        void Clear()
        {
            foreach (var v in _live.Values) if (v.Root != null) Destroy(v.Root.gameObject);
            _live.Clear();
            foreach (var b in _bursts) if (b.t != null) Destroy(b.t.gameObject);
            _bursts.Clear();
        }
    }
}
