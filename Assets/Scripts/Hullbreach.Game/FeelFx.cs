using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // The game-feel layer: impact flash + sparks, floating damage numbers, camera shake, shot trails,
    // muzzle flash, core-damage warning, and procedurally synthesised sound. Everything is pooled and
    // cheap, created on first use (no scene wiring), and each family is switched by GameSettings
    // (ScreenShake, HitEffects, SoundEffects; plus the master/effects volumes).
    // frob:doc docs/design/ui-port.md#feel-layer
    public sealed class FeelFx : MonoBehaviour
    {
        const int MaxFx = 96;
        const int AudioVoices = 8;

        static FeelFx _inst;
        static FeelFx I
        {
            get
            {
                if (_inst == null && Application.isPlaying)
                {
                    var go = new GameObject("FeelFx");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<FeelFx>();
                }
                return _inst;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _inst = null; }

        // The local ship's transform (the camera target); used to tell "I was hit" from "I hit them".
        public static Transform PlayerTransform => _inst != null ? _inst._player : null;

        // ---- static facade: every call is a no-op outside Play Mode ----

        public static void Impact(Vector2 pos, Vector2 dir, float damage, bool onPlayer)
        {
            var f = I; if (f != null) f.DoImpact(pos, dir, damage, onPlayer);
        }

        public static void Muzzle(Vector2 pos, Vector2 dir, Vector2 carrierVelocity)
        {
            var f = I; if (f != null) f.DoMuzzle(pos, dir);
        }

        public static void AttachTrail(GameObject projectile, float radius)
        {
            var f = I; if (f != null) f.DoTrail(projectile, radius);
        }

        public static void AddShake(float trauma)
        {
            var f = I; if (f != null && f._s.ScreenShake) f._trauma = FeelModel.AddTrauma(f._trauma, trauma);
        }

        public static void UndoShake(Transform cam)
        {
            if (_inst == null) return;
            cam.position -= (Vector3)_inst._appliedShake;
            _inst._appliedShake = Vector2.zero;
        }

        public static void ApplyShake(Transform cam)
        {
            if (_inst == null || !_inst._s.ScreenShake) return;
            FeelModel.ShakeOffset(_inst._trauma, 0.7f, Time.unscaledTime, out float x, out float y);
            _inst._appliedShake = new Vector2(x, y);
            cam.position += (Vector3)_inst._appliedShake;
        }

        public static void ReportPlayer(Transform target, Rigidbody2D body)
        {
            var f = I; if (f != null) f.DoReportPlayer(target);
        }

        public static void ReportStorm(bool active, float signedDistance, bool band)
        {
            var f = I; if (f != null) { f._stormActive = active; f._stormSigned = signedDistance; f._band = band; }
        }

        // Called by the settings screen after Apply so the changes take hold without a restart.
        public static void ApplySettings(GameSettings settings)
        {
            if (_inst == null || settings == null) return;
            _inst._s = settings;
            if (settings.SoundEffects && _inst._click == null) _inst.BuildAudio();
        }

        // Core-hit alarm (low = the quieter, lower repeat used while the core stays low).
        public static void PlayCoreAlarm(bool low)
        {
            var f = I; if (f != null) f.Play(f._alarm, low ? 0.3f : 0.5f, low ? 0.8f : 1f);
        }

        // Short beep for the planet warning; `urgency` 0..1 raises the pitch.
        public static void PlayWarningBeep(float urgency)
        {
            var f = I; if (f != null) f.Play(f._beep, 0.5f, 1.1f + 0.4f * Mathf.Clamp01(urgency));
        }

        public static void PlayClick()
        {
            var f = I; if (f != null) f.Play(f._click, 0.45f, 1f);
        }

        // ---- state ----

        GameSettings _s = new GameSettings();
        float _trauma;
        Vector2 _appliedShake;

        Transform _player;
        ShipController _playerCtl;

        bool _stormActive, _band; float _stormSigned; float _nextBeep;

        struct Fx
        {
            public Transform T; public SpriteRenderer Sr; public TextMesh Text;
            public Vector2 Vel; public float Age, Life, Scale0, Scale1; public Color Color; public bool Alive; public float Drag; public int NumId;
        }
        readonly List<Fx> _fx = new List<Fx>(MaxFx);
        readonly Stack<Transform> _freeSprites = new Stack<Transform>();
        readonly Stack<Transform> _freeTexts = new Stack<Transform>();
        Font _font;
        readonly DamageNumberAggregator _numbers = new DamageNumberAggregator();

        AudioSource[] _voices; int _nextVoice;
        AudioSource _thrust; float _thrustVol;
        AudioClip _cannon, _hit, _beep, _alarm, _click, _thrustClip;
        float _lastHitSfx, _lastCannonSfx, _lastAlarm;

        Image _flash; float _flashAlpha;

        void Awake()
        {
            try
            {
                var store = new GameSettingsStore(System.IO.Path.Combine(Application.persistentDataPath, "settings.txt"));
                _s = store.Load();
            }
            catch { _s = new GameSettings(); }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _voices = new AudioSource[AudioVoices];
            for (int i = 0; i < AudioVoices; i++)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false; a.spatialBlend = 0f;
                _voices[i] = a;
            }
            if (_s.SoundEffects) BuildAudio();
            BuildFlashOverlay();
        }

        AudioClip Clip(string name, float[] data, bool loop = false)
        {
            var c = AudioClip.Create(name, data.Length, 1, ProceduralAudio.SampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        void BuildAudio()
        {
            _cannon = Clip("fx_cannon", ProceduralAudio.Cannon());
            _hit = Clip("fx_hit", ProceduralAudio.Hit());
            _beep = Clip("fx_storm_beep", ProceduralAudio.StormBeep());
            _alarm = Clip("fx_core_alarm", ProceduralAudio.CoreAlarm());
            _click = Clip("fx_click", ProceduralAudio.Click());
            _thrustClip = Clip("fx_thrust", ProceduralAudio.ThrusterLoop());
            _thrust = gameObject.AddComponent<AudioSource>();
            _thrust.clip = _thrustClip; _thrust.loop = true; _thrust.spatialBlend = 0f; _thrust.volume = 0f; _thrust.playOnAwake = false;
        }

        void BuildFlashOverlay()
        {
            var cgo = new GameObject("FeelFlash", typeof(Canvas));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var igo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            igo.transform.SetParent(cgo.transform, false);
            _flash = igo.GetComponent<Image>();
            _flash.raycastTarget = false;
            var rt = igo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _flash.color = new Color(1f, 0.1f, 0.05f, 0f);
        }

        void Play(AudioClip clip, float gain, float pitch)
        {
            if (!_s.SoundEffects || clip == null) return;
            float v = FeelModel.Volume(_s.MasterVolume, _s.EffectsVolume, gain);
            if (v <= 0f) return;
            var a = _voices[_nextVoice]; _nextVoice = (_nextVoice + 1) % _voices.Length;
            a.pitch = pitch;
            a.PlayOneShot(clip, v);
        }

        void DoReportPlayer(Transform target)
        {
            if (target == _player) return;
            if (_playerCtl != null) _playerCtl.Damaged -= OnPlayerDamaged;
            _player = target;
            _playerCtl = target != null ? target.GetComponent<ShipController>() : null;
            if (_playerCtl != null) _playerCtl.Damaged += OnPlayerDamaged;
            if (_playerCtl != null) FlightHudView.Ensure(_playerCtl);
        }

        void OnDestroy()
        {
            if (_playerCtl != null) _playerCtl.Damaged -= OnPlayerDamaged;
        }

        void OnPlayerDamaged(Vector2 point, byte damage, bool core)
        {
            if (!core) return;
            // Core under attack: red flash, alarm and a hard shake, whatever the hit's size.
            _flashAlpha = _s.HitEffects ? 0.45f : 0f;
            AddShake(0.5f);
            if (Time.unscaledTime - _lastAlarm > 0.8f)
            {
                _lastAlarm = Time.unscaledTime;
                Play(_alarm, 0.5f, 1f);
            }
        }

        // ---- pooled sprite/text effects ----

        Transform GetSprite()
        {
            if (_freeSprites.Count > 0) { var t = _freeSprites.Pop(); t.gameObject.SetActive(true); return t; }
            var go = new GameObject("fx"); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShipRenderer.MakeSprite(); sr.sortingOrder = 50;
            return go.transform;
        }

        Transform GetText()
        {
            if (_freeTexts.Count > 0) { var t = _freeTexts.Pop(); t.gameObject.SetActive(true); return t; }
            var go = new GameObject("fxText"); go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font; tm.fontSize = 64; tm.characterSize = 0.11f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = _font.material; mr.sortingOrder = 60;
            return go.transform;
        }

        bool Spawn(Fx f)
        {
            if (_fx.Count >= MaxFx) return false;
            _fx.Add(f); return true;
        }

        void Spark(Vector2 pos, Vector2 vel, float life, float size, Color c)
        {
            var t = GetSprite();
            t.position = pos; t.localScale = Vector3.one * size;
            var sr = t.GetComponent<SpriteRenderer>(); sr.color = c;
            if (!Spawn(new Fx { T = t, Sr = sr, Vel = vel, Life = life, Scale0 = size, Scale1 = size * 0.2f, Color = c, Alive = true, Drag = 3f }))
            { t.gameObject.SetActive(false); _freeSprites.Push(t); }
        }

        void DoImpact(Vector2 pos, Vector2 dir, float damage, bool onPlayer)
        {
            float trauma = FeelModel.TraumaForDamage(damage) * (onPlayer ? 1f : 0.45f);
            AddShake(trauma);
            Play(_hit, onPlayer ? 0.75f : 0.5f, Random.Range(0.9f, 1.1f));

            if (!_s.HitEffects) return;

            // Flash: a fast expanding white-orange disc.
            var t = GetSprite();
            t.position = pos; t.localScale = Vector3.one * 0.5f; t.rotation = Quaternion.identity;
            var sr = t.GetComponent<SpriteRenderer>(); sr.color = new Color(1f, 0.95f, 0.7f, 1f);
            if (!Spawn(new Fx { T = t, Sr = sr, Life = 0.16f, Scale0 = 0.6f, Scale1 = 2.6f, Color = new Color(1f, 0.85f, 0.45f, 1f), Alive = true }))
            { t.gameObject.SetActive(false); _freeSprites.Push(t); }

            // Sparks thrown back against the shot.
            int n = 6 + (int)Mathf.Min(8f, damage * 0.2f);
            Vector2 back = -dir.normalized;
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(-1.1f, 1.1f);
                Vector2 v = (Vector2)(Quaternion.Euler(0, 0, a * Mathf.Rad2Deg) * back) * Random.Range(3f, 11f);
                Spark(pos, v, Random.Range(0.2f, 0.45f), Random.Range(0.1f, 0.22f), i % 3 == 0 ? new Color(1f, 1f, 0.8f) : new Color(1f, 0.6f, 0.2f));
            }

            // Damage number floats up. Hits landing together at the same spot are summed into one number; the rest
            // fan out sideways by a deterministic offset (DamageNumberAggregator).
            var num = _numbers.Add(pos.x, pos.y, damage, false, Time.unscaledTime);
            if (num.Merged)
            {
                for (int i = 0; i < _fx.Count; i++)
                {
                    if (_fx[i].NumId != num.Id || _fx[i].Text == null) continue;
                    var m = _fx[i];
                    m.Age = 0f;
                    m.Text.text = FeelModel.DamageText(num.Sum, num.Destroyed);
                    _fx[i] = m;
                    return;
                }
            }
            var tt = GetText();
            tt.position = pos + Vector2.up * 0.6f + Vector2.right * num.OffsetX;
            var tm = tt.GetComponent<TextMesh>();
            tm.text = FeelModel.DamageText(num.Sum, num.Destroyed);
            tm.color = onPlayer ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.95f, 0.5f);
            tt.localScale = Vector3.one;
            if (!Spawn(new Fx { T = tt, Text = tm, Vel = new Vector2(0f, 3.2f), Life = 0.8f, Scale0 = 1f, Scale1 = 1f, Color = tm.color, Alive = true, Drag = 2f, NumId = num.Id }))
            { tt.gameObject.SetActive(false); _freeTexts.Push(tt); }
        }

        void DoMuzzle(Vector2 pos, Vector2 dir)
        {
            Play(_cannon, 0.5f, Random.Range(0.95f, 1.05f));
            if (_player != null) AddShake(0.04f); // a tiny kick, felt more than seen
            if (!_s.HitEffects) return;
            var t = GetSprite();
            t.position = pos + dir * 0.4f; t.localScale = Vector3.one * 0.5f;
            var sr = t.GetComponent<SpriteRenderer>(); sr.color = new Color(1f, 0.9f, 0.5f, 1f);
            if (!Spawn(new Fx { T = t, Sr = sr, Life = 0.07f, Scale0 = 0.5f, Scale1 = 1.2f, Color = new Color(1f, 0.9f, 0.5f, 1f), Alive = true }))
            { t.gameObject.SetActive(false); _freeSprites.Push(t); }
        }

        void DoTrail(GameObject projectile, float radius)
        {
            if (!_s.HitEffects) return;
            var tr = projectile.GetComponent<TrailRenderer>();
            if (tr == null) tr = projectile.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.startWidth = Mathf.Max(0.12f, radius * 2f);
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.15f;
            tr.sharedMaterial = TrailMaterial();
            tr.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            tr.endColor = new Color(1f, 0.4f, 0.1f, 0f);
            tr.sortingOrder = 40;
            tr.alignment = LineAlignment.View;
        }

        static Material _trailMat;
        static Material TrailMaterial()
        {
            if (_trailMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                _trailMat = new Material(sh) { name = "FeelTrail" };
            }
            return _trailMat;
        }

        // ---- per-frame ----

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _trauma = FeelModel.DecayTrauma(_trauma, dt);

            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.Age += Time.deltaTime;
                float k = FeelModel.FadeOut(f.Age, f.Life);
                if (f.Age >= f.Life)
                {
                    f.T.gameObject.SetActive(false);
                    (f.Text != null ? _freeTexts : _freeSprites).Push(f.T);
                    _fx.RemoveAt(i);
                    continue;
                }
                f.Vel *= Mathf.Exp(-f.Drag * Time.deltaTime);
                f.T.position += (Vector3)(f.Vel * Time.deltaTime);
                float u = f.Age / f.Life;
                if (f.Sr != null)
                {
                    f.T.localScale = Vector3.one * Mathf.Lerp(f.Scale0, f.Scale1, u);
                    var c = f.Color; c.a = k; f.Sr.color = c;
                }
                else
                {
                    var c = f.Color; c.a = Mathf.Clamp01(k * 1.6f); f.Text.color = c;
                    float s = 1f + 0.5f * (1f - u) * (1f - u);
                    f.T.localScale = Vector3.one * s;
                }
                _fx[i] = f;
            }

            if (_flash != null)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 1.6f);
                var c = _flash.color; c.a = _flashAlpha; _flash.color = c;
            }

            UpdateAudio(dt);
        }

        void UpdateAudio(float dt)
        {
            if (!_s.SoundEffects || _thrust == null) return;

            float mean = 0f;
            if (_playerCtl != null && _playerCtl.Ship != null)
                mean = Mathf.Max(_playerCtl.Ship.ForwardThrottleMean, _playerCtl.Ship.ReverseThrottleMean * 0.8f);
            float target = FeelModel.ThrusterVolume(mean) * 0.3f;
            _thrustVol = Mathf.MoveTowards(_thrustVol, target, dt * 4f);
            _thrust.volume = FeelModel.Volume(_s.MasterVolume, _s.EffectsVolume, _thrustVol);
            _thrust.pitch = FeelModel.ThrusterPitch(mean);
            if (_thrustVol > 0.001f && !_thrust.isPlaying) _thrust.Play();
            else if (_thrustVol <= 0.001f && _thrust.isPlaying) _thrust.Pause();

            float interval = FeelModel.StormBeepInterval(_stormActive, _stormSigned, _band);
            if (interval > 0f && Time.unscaledTime >= _nextBeep)
            {
                _nextBeep = Time.unscaledTime + interval;
                Play(_beep, 0.4f, _stormSigned < 0f ? 1.15f : 1f);
            }
        }
    }
}
