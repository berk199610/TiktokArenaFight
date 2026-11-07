using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Arena
{
    /// <summary>Procedural effect textures (no image files needed).</summary>
    public static class FxTex
    {
        static Texture2D soft, ring, star, smoke, stripes, spark, flame;

        public static Texture2D Soft => soft != null ? soft : (soft = Make("fxSoft", 64, (u, v) => { float d = Dist(u, v); return Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d); }));
        public static Texture2D Ring => ring != null ? ring : (ring = Make("fxRing", 128, (u, v) => { float d = Dist(u, v); return Mathf.Clamp01(1 - Mathf.Abs(d - 0.8f) / 0.18f); }));
        public static Texture2D Star => star != null ? star : (star = Make("fxStar", 64, (u, v) =>
        {
            float x = u * 2 - 1, y = v * 2 - 1;
            float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
            float edge = 0.45f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 6f);
            return Mathf.Clamp01((edge - r) * 8f);
        }));
        public static Texture2D Smoke => smoke != null ? smoke : (smoke = Make("fxSmoke", 64, (u, v) =>
        {
            float d = Dist(u, v);
            float n = Mathf.PerlinNoise(u * 5f, v * 5f) * 0.35f;
            return Mathf.Clamp01((1 - d - n) * 2.2f);
        }));
        public static Texture2D Spark => spark != null ? spark : (spark = Make("fxSpark", 32, (u, v) =>
        {
            float x = Mathf.Abs(u * 2 - 1), y = Mathf.Abs(v * 2 - 1);
            return Mathf.Clamp01(1 - x * 4f) * Mathf.Clamp01(1 - y) + Mathf.Clamp01(1 - y * 4f) * Mathf.Clamp01(1 - x) * 0.6f;
        }));
        /// <summary>Diagonal stripes, tileable, for the tornado cone and wind rings.</summary>
        public static Texture2D Stripes => stripes != null ? stripes : (stripes = Make("fxStripes", 64, (u, v) =>
        {
            float s = Mathf.Repeat(u * 3f + v * 1.5f, 1f);
            float band = Mathf.Clamp01(1 - Mathf.Abs(s - 0.5f) * 3.2f);
            float fadeV = Mathf.Sin(v * Mathf.PI);
            return band * fadeV;
        }, TextureWrapMode.Repeat));
        /// <summary>Soft vertical gradient for pillars / fire walls (bright at bottom).</summary>
        public static Texture2D Flame => flame != null ? flame : (flame = Make("fxFlame", 64, (u, v) =>
        {
            float n = Mathf.PerlinNoise(u * 8f, v * 3f);
            return Mathf.Clamp01((1 - v) * (0.6f + n * 0.8f) - v * 0.2f);
        }, TextureWrapMode.Repeat));

        static float Dist(float u, float v) { float x = u * 2 - 1, y = v * 2 - 1; return Mathf.Sqrt(x * x + y * y); }

        static Texture2D Make(string name, int size, Func<float, float, float> alpha, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }

    public enum ImpactStyle { Punch, Kick, Bat, Shovel, Power, Wind, Thunder, KO }

    /// <summary>
    /// All effects of DESIGN §6.4 built from code: one shared particle system per effect type (Emit at a position,
    /// no per-effect allocations), pooled ground rings, light pillars, lightning bolts, wind rings and tornado cones.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx I { get; private set; }

        ParticleSystem stars, smoke, dust, sparkle, confetti, fire, heal, sweat, streaks, flash, debris, embers;

        class Anim
        {
            public Transform t; public Material m; public Renderer r; public float start, dur;
            public Action<Anim, float> update; public bool releaseToPool; public Stack<Anim> pool;
            public Color baseColor; public float a, b;
        }

        readonly List<Anim> running = new List<Anim>();
        readonly Stack<Anim> ringPool = new Stack<Anim>();
        readonly Stack<Anim> pillarPool = new Stack<Anim>();
        readonly Stack<Anim> boltPool = new Stack<Anim>();
        Light flashLight;

        public void Init()
        {
            I = this;
            stars = MakePS("Stars", FxTex.Star, true, 0.35f, 120);
            smoke = MakePS("Smoke", FxTex.Smoke, false, 1.1f, 200);
            dust = MakePS("Dust", FxTex.Smoke, false, 0.9f, 200);
            sparkle = MakePS("Sparkle", FxTex.Spark, true, 0.8f, 300);
            confetti = MakePS("Confetti", FxTex.Soft, false, 3.5f, 400, true);
            fire = MakePS("Fire", FxTex.Soft, true, 0.7f, 1200);
            heal = MakePS("Heal", FxTex.Spark, true, 0.9f, 200);
            sweat = MakePS("Sweat", FxTex.Soft, false, 0.6f, 100, true);
            streaks = MakePS("Streaks", FxTex.Soft, true, 0.2f, 400, false, true);
            flash = MakePS("Flash", FxTex.Soft, true, 0.12f, 120);
            debris = MakePS("Debris", FxTex.Smoke, false, 0.9f, 600);
            embers = MakePS("Embers", FxTex.Spark, true, 1.2f, 600);
            var lgo = new GameObject("FlashLight");
            lgo.transform.SetParent(transform, false);
            flashLight = lgo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 9f;
            flashLight.intensity = 0f;
            flashLight.color = new Color(0.75f, 0.85f, 1f);
        }

        ParticleSystem MakePS(string name, Texture tex, bool additive, float life, int max, bool gravity = false, bool stretch = false)
        {
            var go = new GameObject("PS_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.gravityModifier = gravity ? 1.2f : 0f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, name == "Smoke" || name == "Dust" ? AnimationCurve.EaseInOut(0, 0.6f, 1, 1.6f) : AnimationCurve.EaseInOut(0, 1f, 1, 0.3f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = name == "Stars" || name == "Confetti";
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.FxNew(additive, tex, Color.white);
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.06f; r.lengthScale = 1.2f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.None;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color c, float life)
        {
            var p = new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = size, startColor = c, startLifetime = life, applyShapeToPosition = false };
            ps.Emit(p, 1);
        }

        static Vector3 RandDir(float up = 0.3f)
        {
            var d = UnityEngine.Random.insideUnitSphere;
            d.y = Mathf.Abs(d.y) * up + up * 0.5f;
            return d.normalized;
        }

        // ------------------------------------------------------------------ simple bursts
        /// <summary>"Vuruş: küçük yıldız patlaması".</summary>
        public void HitBurst(Vector3 p, bool strong)
        {
            int n = strong ? 9 : 4;
            for (int i = 0; i < n; i++)
            {
                var c = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0.2f), UnityEngine.Random.value);
                Emit(stars, p, RandDir(0.6f) * (strong ? 4.5f : 2.5f), strong ? 0.42f : 0.3f, c, strong ? 0.35f : 0.25f);
            }
        }

        static readonly string[] kickWords = { "BAM!", "WHAM!", "POW!" };

        /// <summary>Hit effect, different for every kind of hit (user request: not only stars).</summary>
        public void Impact(Vector3 p, ImpactStyle style, bool allowWord = true)
        {
            switch (style)
            {
                case ImpactStyle.Punch:
                    Flash(p, new Color(1f, 1f, 0.95f, 0.9f), 0.7f);
                    Streak(p, new Color(1f, 0.95f, 0.8f), 5, 5f);
                    if (allowWord && Random.value < 0.1f) Word(p + Vector3.up * 0.5f, "POW!", new Color(1f, 0.9f, 0.3f), 0.9f);
                    break;
                case ImpactStyle.Kick:
                    Flash(p, new Color(1f, 0.7f, 0.3f, 0.95f), 1.1f);
                    Streak(p, new Color(1f, 0.8f, 0.4f), 8, 7f);
                    HitBurst(p, false);
                    if (allowWord && Random.value < 0.22f) Word(p + Vector3.up * 0.5f, kickWords[Random.Range(0, kickWords.Length)], new Color(1f, 0.6f, 0.2f), 1.1f);
                    break;
                case ImpactStyle.Bat:
                    Flash(p, new Color(1f, 0.85f, 0.4f, 0.9f), 1f);
                    HitBurst(p, true);
                    if (allowWord && Random.value < 0.5f) Word(p + Vector3.up * 0.5f, "BONK!", new Color(1f, 0.8f, 0.35f), 1.1f);
                    break;
                case ImpactStyle.Shovel:
                    Flash(p, new Color(0.8f, 0.9f, 1f, 0.95f), 1.1f);
                    Streak(p, new Color(0.7f, 0.85f, 1f), 12, 8f);
                    if (allowWord && Random.value < 0.5f) Word(p + Vector3.up * 0.5f, "CLANG!", new Color(0.7f, 0.9f, 1f), 1.1f);
                    break;
                case ImpactStyle.Power:
                    Flash(p, new Color(1f, 0.55f, 0.15f, 1f), 2f);
                    Streak(p, new Color(1f, 0.7f, 0.3f), 14, 10f);
                    HitBurst(p, true);
                    GroundRing(p, 0.2f, 1.6f, 0.35f, new Color(1f, 0.6f, 0.2f, 1f));
                    if (allowWord) Word(p + Vector3.up * 0.6f, "SMASH!", new Color(1f, 0.45f, 0.15f), 1.35f);
                    break;
                case ImpactStyle.Wind:
                    Streak(p, new Color(0.85f, 0.95f, 1f), 6, 6f);
                    if (allowWord && Random.value < 0.08f) Word(p + Vector3.up * 0.5f, "WHOOSH!", new Color(0.8f, 0.95f, 1f), 0.9f);
                    break;
                case ImpactStyle.Thunder:
                    Flash(p, new Color(0.6f, 0.8f, 1f, 1f), 2.2f);
                    Streak(p, new Color(0.6f, 0.85f, 1f), 10, 9f);
                    if (allowWord && Random.value < 0.5f) Word(p + Vector3.up * 0.6f, "ZAP!", new Color(0.6f, 0.9f, 1f), 1.2f);
                    break;
                case ImpactStyle.KO:
                    Flash(p, new Color(1f, 0.3f, 0.3f, 1f), 1.6f);
                    for (int i = 0; i < 8; i++) Emit(stars, p, RandDir(0.8f) * 3.5f, 0.4f, new Color(1f, 0.85f, 0.3f), 0.5f);
                    if (allowWord) Word(p + Vector3.up * 0.8f, "K.O.!", new Color(1f, 0.3f, 0.25f), 1.4f);
                    break;
            }
        }

        void Flash(Vector3 p, Color c, float size) => Emit(flash, p, Vector3.zero, size, c, 0.12f);

        void Streak(Vector3 p, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                var d = Random.onUnitSphere;
                d.y = d.y * 0.6f + 0.15f;
                Emit(streaks, p, d.normalized * speed * Random.Range(0.6f, 1.2f), Random.Range(0.06f, 0.11f), c, Random.Range(0.12f, 0.22f));
            }
        }

        /// <summary>Comic word popping out of a hit ("POW!", "BAM!", "K.O.!"), drawn by the name layer.</summary>
        public void Word(Vector3 p, string text, Color c, float scale)
        {
            if (GameManager.I != null && GameManager.I.Names != null) GameManager.I.Names.Word(p, text, c, scale);
        }

        /// <summary>Dust and debris thrown around a tornado funnel.</summary>
        public void TornadoDebris(Vector3 center, float radius, float now)
        {
            for (int i = 0; i < 2; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = radius * Random.Range(0.7f, 1.5f);
                float h = Random.Range(0.1f, 4.2f);
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var tangent = Vector3.Cross(Vector3.up, radial);
                var pos = center + radial * r + Vector3.up * h;
                var vel = tangent * Random.Range(6f, 11f) + Vector3.up * Random.Range(1f, 3f) + radial * 0.8f;
                bool dark = Random.value < 0.4f;
                var col = dark ? new Color(0.35f, 0.28f, 0.22f, 0.9f) : new Color(0.78f, 0.7f, 0.58f, 0.75f);
                Emit(debris, pos, vel, dark ? Random.Range(0.12f, 0.25f) : Random.Range(0.5f, 1.0f), col, Random.Range(0.4f, 0.8f));
            }
        }

        /// <summary>Rising sparks of the fire ring.</summary>
        public void Ember(Vector3 p)
        {
            Emit(embers, p, new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 3.5f), Random.Range(-0.4f, 0.4f)), Random.Range(0.06f, 0.14f), new Color(1f, Random.Range(0.4f, 0.8f), 0.15f), Random.Range(0.8f, 1.4f));
        }

        public void Dust(Vector3 p, int n = 8, float spread = 1.5f)
        {
            for (int i = 0; i < n; i++)
            {
                var d = UnityEngine.Random.insideUnitCircle;
                Emit(dust, p + new Vector3(d.x, 0.1f, d.y) * 0.3f, new Vector3(d.x, 0.25f, d.y) * spread, UnityEngine.Random.Range(0.5f, 0.9f), new Color(0.75f, 0.65f, 0.5f, 0.75f), UnityEngine.Random.Range(0.6f, 1.0f));
            }
        }

        /// <summary>"Ölüm: çizgi film tarzı duman bulutu".</summary>
        public void SmokePuff(Vector3 p)
        {
            for (int i = 0; i < 16; i++)
            {
                var d = UnityEngine.Random.insideUnitSphere;
                Emit(smoke, p + Vector3.up * 0.7f + d * 0.4f, new Vector3(d.x, Mathf.Abs(d.y) + 0.3f, d.z) * 1.2f, UnityEngine.Random.Range(0.8f, 1.4f), new Color(0.92f, 0.92f, 0.95f, 0.95f), UnityEngine.Random.Range(0.7f, 1.1f));
            }
            for (int i = 0; i < 6; i++) Emit(stars, p + Vector3.up * 0.8f, RandDir(0.8f) * 3f, 0.35f, new Color(1f, 0.9f, 0.4f), 0.4f);
        }

        public void Sparkle(Vector3 p, Color c, int n = 10, float radius = 0.6f, float height = 1.6f)
        {
            for (int i = 0; i < n; i++)
            {
                var d = UnityEngine.Random.insideUnitCircle * radius;
                Emit(sparkle, p + new Vector3(d.x, UnityEngine.Random.Range(0.1f, height), d.y), Vector3.up * UnityEngine.Random.Range(0.4f, 1.4f), UnityEngine.Random.Range(0.12f, 0.25f), c, UnityEngine.Random.Range(0.5f, 0.9f));
            }
        }

        public void HealSparks(Vector3 p, int n = 4)
        {
            for (int i = 0; i < n; i++)
            {
                var d = UnityEngine.Random.insideUnitCircle * 0.4f;
                Emit(heal, p + new Vector3(d.x, UnityEngine.Random.Range(0.3f, 1.4f), d.y), Vector3.up * 1.2f, 0.18f, new Color(0.3f, 1f, 0.4f), 0.7f);
            }
        }

        public void Sweat(Vector3 headPos)
        {
            var d = UnityEngine.Random.insideUnitCircle.normalized;
            Emit(sweat, headPos + new Vector3(d.x * 0.15f, 0.1f, d.y * 0.15f), new Vector3(d.x, 1.5f, d.y) * 0.8f, 0.08f, new Color(0.6f, 0.85f, 1f, 0.9f), 0.5f);
        }

        public void Confetti(Vector3 p, int n = 140)
        {
            Color[] cs = { new Color(1f, 0.3f, 0.4f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 1f), new Color(0.5f, 1f, 0.4f), new Color(0.9f, 0.4f, 1f) };
            for (int i = 0; i < n; i++)
            {
                var d = UnityEngine.Random.insideUnitCircle;
                Emit(confetti, p + new Vector3(d.x * 2f, 4.5f + UnityEngine.Random.value * 2f, d.y * 2f), new Vector3(d.x * 2.5f, UnityEngine.Random.Range(1f, 4f), d.y * 2.5f), UnityEngine.Random.Range(0.12f, 0.2f), cs[i % cs.Length], UnityEngine.Random.Range(2.5f, 3.8f));
            }
        }

        /// <summary>Fire ring flames along a circle (final battle).</summary>
        public void FireRing(float radius, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0.1f, Mathf.Sin(a) * radius);
                var c = Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.8f, 0.2f), UnityEngine.Random.value);
                Emit(fire, p, new Vector3(0, UnityEngine.Random.Range(1.5f, 3f), 0), UnityEngine.Random.Range(0.35f, 0.7f), c, UnityEngine.Random.Range(0.4f, 0.7f));
            }
        }

        public void FireBurst(Vector3 p)
        {
            for (int i = 0; i < 3; i++)
                Emit(fire, p + UnityEngine.Random.insideUnitSphere * 0.25f + Vector3.up * 0.6f, Vector3.up * 1.5f, 0.35f, new Color(1f, 0.5f, 0.1f), 0.4f);
        }

        // ------------------------------------------------------------------ animated objects
        Anim Get(Stack<Anim> pool, Func<Anim> make)
        {
            var a = pool.Count > 0 ? pool.Pop() : make();
            a.pool = pool;
            a.t.gameObject.SetActive(true);
            a.start = Time.time;
            running.Add(a);
            return a;
        }

        Anim MakeMeshAnim(string name, Mesh mesh, bool additive, Texture tex)
        {
            var go = MeshKit.Part(name, mesh, Mats.FxNew(additive, tex, Color.white), transform, Vector3.zero, Vector3.zero, Vector3.one);
            var r = go.GetComponent<Renderer>();
            return new Anim { t = go.transform, r = r, m = r.sharedMaterial };
        }

        /// <summary>Expanding ground ring: shock wave, gate glow, heal ring, spin-kick wind.</summary>
        public void GroundRing(Vector3 center, float fromR, float toR, float dur, Color c, float y = 0.14f)
        {
            var a = Get(ringPool, () => MakeMeshAnim("Ring", MeshKit.FlatRing(0.82f, 1f, 64), true, FxTex.Soft));
            a.t.position = new Vector3(center.x, y, center.z);
            a.t.rotation = Quaternion.identity;
            a.baseColor = c; a.a = fromR; a.b = toR; a.dur = dur;
            a.update = (x, k) =>
            {
                float r = Mathf.Lerp(x.a, x.b, 1 - (1 - k) * (1 - k));
                x.t.localScale = new Vector3(r, 1, r);
                var col = x.baseColor; col.a *= 1 - k;
                x.m.SetColor("_Color", col);
            };
            a.update(a, 0);
        }

        /// <summary>"Güç alma: üstten inen ışık sütunu".</summary>
        public void LightPillar(Vector3 p, Color c, float dur = 0.9f)
        {
            var a = Get(pillarPool, () => MakeMeshAnim("Pillar", MeshKit.Tube(1f, 1f, 24), true, FxTex.Flame));
            a.t.position = new Vector3(p.x, 0, p.z);
            a.baseColor = c; a.dur = dur;
            a.update = (x, k) =>
            {
                float drop = Mathf.Clamp01(k * 4f);       // falls from the sky in the first quarter
                float h = Mathf.Lerp(14f, 9f, drop);
                float w = Mathf.Lerp(0.15f, 0.75f, drop) * (1 - k * 0.5f);
                x.t.localScale = new Vector3(w, h, w);
                x.t.position = new Vector3(x.t.position.x, Mathf.Lerp(10f, 0f, drop), x.t.position.z);
                var col = x.baseColor; col.a *= k < 0.75f ? 1 : (1 - k) * 4f;
                x.m.SetColor("_Color", col);
            };
            a.update(a, 0);
            GroundRing(p, 0.3f, 1.8f, dur, c);
        }

        /// <summary>"Kapıdan giriş: gökten kapının önüne şimşek iner, yerde parlayan halka ve toz".</summary>
        public void Lightning(Vector3 p)
        {
            var a = Get(boltPool, () =>
            {
                var go = new GameObject("Bolt");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 12;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.35f;
                lr.numCapVertices = 2;
                lr.sharedMaterial = Mats.FxNew(true, FxTex.Soft, Color.white);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return new Anim { t = go.transform, r = lr, m = lr.sharedMaterial };
            });
            var line = (LineRenderer)a.r;
            var top = p + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 16f, UnityEngine.Random.Range(-1.5f, 1.5f));
            for (int i = 0; i < 12; i++)
            {
                float k = i / 11f;
                var q = Vector3.Lerp(top, p, k);
                if (i > 0 && i < 11) q += new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0, UnityEngine.Random.Range(-0.6f, 0.6f));
                line.SetPosition(i, q);
            }
            a.dur = 0.35f;
            a.update = (x, k) =>
            {
                float flick = (Mathf.Sin(k * 60f) > 0 ? 1f : 0.45f) * (1 - k);
                x.m.SetColor("_Color", new Color(0.7f, 0.85f, 1f, 1f) * (2.5f * flick));
                ((LineRenderer)x.r).widthMultiplier = 0.45f * (1 - k * 0.6f);
            };
            a.update(a, 0);
            flashLight.transform.position = p + Vector3.up * 2f;
            flashLight.intensity = 25f;
            GroundRing(p, 0.2f, 2.4f, 0.7f, new Color(0.6f, 0.8f, 1f, 1f));
            Dust(p, 12, 2.5f);
        }

        void Update()
        {
            float now = Time.time;
            for (int i = running.Count - 1; i >= 0; i--)
            {
                var a = running[i];
                float k = a.dur > 0 ? Mathf.Clamp01((now - a.start) / a.dur) : 1f;
                a.update?.Invoke(a, k);
                if (k >= 1f)
                {
                    a.t.gameObject.SetActive(false);
                    running.RemoveAt(i);
                    a.pool?.Push(a);
                }
            }
            if (flashLight.intensity > 0) flashLight.intensity = Mathf.Max(0, flashLight.intensity - Time.deltaTime * 90f);
        }

        // ------------------------------------------------------------------ attached effects (owned by special moves)
        /// <summary>Spinning stripy cone around a fighter (tornado), or a wind ring at foot height (spin kick).</summary>
        public static Transform AttachedSpin(Transform parent, bool tornado)
        {
            var root = new GameObject(tornado ? "TornadoFx" : "WindFx").transform;
            root.SetParent(parent, false);
            if (tornado)
            {
                // three funnels (outer, swirl, dark core), a dark cloud on top and a dust skirt at the bottom
                var m1 = Mats.FxNew(false, FxTex.Stripes, new Color(0.92f, 0.9f, 0.85f, 0.95f));
                m1.SetVector("_Scroll", new Vector4(-3.2f, 0.9f, 0, 0));
                m1.SetTextureScale("_MainTex", new Vector2(2f, 1f));
                MeshKit.Part("Funnel", MeshKit.Tube(0.45f, 2.5f, 36), m1, root, new Vector3(0, 0.05f, 0), Vector3.zero, new Vector3(1f, 5.2f, 1f));
                var m2 = Mats.FxNew(true, FxTex.Stripes, new Color(0.7f, 0.82f, 1f, 0.9f));
                m2.SetVector("_Scroll", new Vector4(-4.5f, 1.4f, 0, 0));
                m2.SetTextureScale("_MainTex", new Vector2(3f, 1f));
                MeshKit.Part("Swirl", MeshKit.Tube(0.65f, 3.1f, 36), m2, root, new Vector3(0, 0.05f, 0), new Vector3(0, 90, 0), new Vector3(1f, 4.6f, 1f));
                var m3 = Mats.FxNew(false, FxTex.Stripes, new Color(0.35f, 0.33f, 0.4f, 0.85f));
                m3.SetVector("_Scroll", new Vector4(-5f, 2f, 0, 0));
                MeshKit.Part("Core", MeshKit.Tube(0.25f, 1.5f, 24), m3, root, new Vector3(0, 0.05f, 0), new Vector3(0, 45, 0), new Vector3(1f, 5.4f, 1f));
                var m4 = Mats.FxNew(false, FxTex.Smoke, new Color(0.3f, 0.3f, 0.36f, 0.85f));
                MeshKit.Part("Cloud", MeshKit.Quad, m4, root, new Vector3(0, 5.3f, 0), new Vector3(90, 0, 0), new Vector3(7.5f, 7.5f, 1f));
                var m5 = Mats.FxNew(false, FxTex.Soft, new Color(0.75f, 0.66f, 0.52f, 0.7f));
                MeshKit.Part("DustSkirt", MeshKit.Quad, m5, root, new Vector3(0, 0.15f, 0), new Vector3(90, 0, 0), new Vector3(6f, 6f, 1f));
            }
            else
            {
                var m = Mats.FxNew(true, FxTex.Stripes, new Color(0.8f, 0.95f, 1f, 1f) * 1.5f);
                m.SetVector("_Scroll", new Vector4(-4f, 0, 0, 0));
                m.SetTextureScale("_MainTex", new Vector2(3f, 1f));
                MeshKit.Part("Wind", MeshKit.Tube(1.5f, 2.0f, 32), m, root, new Vector3(0, 0.3f, 0), Vector3.zero, new Vector3(1f, 0.8f, 1f));
                MeshKit.Part("Wind2", MeshKit.Tube(1.2f, 1.7f, 32), m, root, new Vector3(0, 0.9f, 0), new Vector3(0, 60, 0), new Vector3(1f, 0.5f, 1f));
            }
            return root;
        }

        /// <summary>Soft glow disc under a fighter (gifter aura / giant colour glow).</summary>
        public static Renderer AttachedGlow(Transform parent, Color c, float radius)
        {
            var go = MeshKit.Part("Glow", MeshKit.Quad, Mats.FxNew(true, FxTex.Soft, c), parent, new Vector3(0, 0.1f, 0), new Vector3(90, 0, 0), new Vector3(radius * 2, radius * 2, 1));
            return go.GetComponent<Renderer>();
        }
    }
}
