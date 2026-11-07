using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Sound (user request: noticeable but never a headache). Everything is synthesised from code at start — no audio
    /// files, no licences. One quiet music loop (soft pad + light beat, 100 BPM) and a handful of short effects. Each
    /// effect has its own cool-down and there is a global cap, so a busy arena never turns into noise: punches are
    /// mostly silent, only every few landed hits make a soft thud; gifts, KOs, level-ups and the winner get a clear cue.
    /// Volumes: config-free on purpose (master 0.8, music 0.16).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        const int Rate = 22050;
        AudioSource music, fx;
        AudioClip hit, heavy, ko, gift, level, win, join;
        readonly float[] nextAt = new float[8];
        float globalNext;

        public static void Create(GameObject host)
        {
            if (I != null) return;
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            I = host.AddComponent<Sfx>();
            I.Build();
        }

        void Build()
        {
            AudioListener.volume = 0.8f;
            fx = gameObject.AddComponent<AudioSource>();
            fx.playOnAwake = false; fx.spatialBlend = 0f; fx.volume = 0.55f;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = true; music.spatialBlend = 0f; music.volume = 0.16f;
            hit = Make("hit", 0.12f, (t, d) => Noise() * Env(t, 0.002f, 0.09f) * 0.5f + Sine(90 - 40 * t / d, t) * Env(t, 0.002f, 0.1f) * 0.6f);
            heavy = Make("heavy", 0.35f, (t, d) => Sine(70 - 35 * t / d, t) * Env(t, 0.003f, 0.3f) * 0.9f + Noise() * Env(t, 0.002f, 0.12f) * 0.35f);
            ko = Make("ko", 0.7f, (t, d) => Sine(Mathf.Lerp(520, 90, t / d), t) * Env(t, 0.01f, 0.6f) * 0.35f + Sine(55, t) * Env(t - 0.25f, 0.005f, 0.4f) * 0.8f);
            gift = Make("gift", 0.75f, (t, d) => Bell(new[] { 784f, 988f, 1175f, 1568f }, 0.085f, t) * 0.45f);
            level = Make("level", 0.6f, (t, d) => Bell(new[] { 523f, 659f, 784f, 1047f }, 0.07f, t) * 0.4f);
            win = Make("win", 1.6f, (t, d) => Bell(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, 0.16f, t) * 0.4f + Sine(262, t) * Env(t, 0.05f, 1.5f) * 0.15f);
            join = Make("join", 0.12f, (t, d) => Sine(Mathf.Lerp(600, 900, t / d), t) * Env(t, 0.004f, 0.1f) * 0.3f);
            music.clip = MusicLoop();
            music.Play();
        }

        // ---- public cues (each one rate-limited) ----
        int landed;
        public void Impact(ImpactStyle s)
        {
            switch (s)
            {
                case ImpactStyle.KO: Play(2, ko, 0.4f, 0.9f); break;
                case ImpactStyle.Power: case ImpactStyle.Thunder: case ImpactStyle.Wind: Play(1, heavy, 0.25f, 0.8f); break;
                default: if (++landed % 3 == 0) Play(0, hit, 0.18f, 0.45f, Random.Range(0.85f, 1.15f)); break;
            }
        }
        public void Gift() => Play(3, gift, 0.35f, 0.7f);
        public void LevelUp() => Play(4, level, 0.5f, 0.6f);
        public void Winner() => Play(5, win, 3f, 0.9f);
        public void Join() => Play(6, join, 0.6f, 0.5f, Random.Range(0.9f, 1.1f));

        void Play(int slot, AudioClip c, float cooldown, float vol, float pitch = 1f)
        {
            float now = Time.unscaledTime;
            if (now < nextAt[slot]) return;
            if (slot == 0 || slot == 6) { if (now < globalNext) return; } // the small sounds give way to anything else
            nextAt[slot] = now + cooldown;
            globalNext = now + 0.07f;
            fx.pitch = pitch;
            fx.PlayOneShot(c, vol);
        }

        // ---- synthesis helpers ----
        delegate float Wave(float t, float dur);
        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Sine(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Env(float t, float a, float r) => t < 0 ? 0 : t < a ? t / a : Mathf.Exp(-(t - a) / (r * 0.35f));
        static float Bell(float[] notes, float step, float t)
        {
            float s = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float u = t - i * step;
                if (u < 0) break;
                s += (Sine(notes[i], u) + 0.3f * Sine(notes[i] * 2, u)) * Env(u, 0.004f, 0.35f) * 0.7f;
            }
            return s;
        }

        static AudioClip Make(string name, float dur, Wave w)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float fade = Mathf.Clamp01((n - i) / (Rate * 0.02f)); // no click at the end
                data[i] = Mathf.Clamp(w(t, dur) * fade, -1f, 1f);
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Calm loop: Am-F-C-G pad, soft bass, gentle kick + shaker. 4 bars x 4 beats at 100 BPM = 9.6 s.</summary>
        static AudioClip MusicLoop()
        {
            const float bpm = 100f; float beat = 60f / bpm; int bars = 4;
            float dur = bars * 4 * beat;
            int n = Mathf.CeilToInt(dur * Rate);
            var data = new float[n];
            float[][] chords = { new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f }, new[] { 261.6f, 329.6f, 392f }, new[] { 196f, 246.9f, 293.7f } };
            float[] bass = { 110f, 87.3f, 130.8f, 98f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = Mathf.Min(bars - 1, (int)(t / (4 * beat)));
                float inBar = t - bar * 4 * beat;
                float inBeat = t % beat;
                float s = 0;
                // pad: soft triangle-ish chord with slow swell per bar (crossfades so the loop is seamless)
                float swell = 0.55f + 0.45f * Mathf.Sin(Mathf.PI * inBar / (4 * beat));
                foreach (var hz in chords[bar]) s += (Sine(hz, t) + 0.25f * Sine(hz * 2.003f, t)) * 0.07f * swell;
                // bass on beats 1 and 3
                float b = inBar % (2 * beat);
                s += Sine(bass[bar], t) * Env(b, 0.01f, 0.5f) * 0.22f;
                // kick on every beat (soft), shaker on off-beats
                s += Sine(Mathf.Lerp(110, 45, Mathf.Clamp01(inBeat / 0.12f)), inBeat) * Env(inBeat, 0.002f, 0.12f) * 0.35f;
                float off = (t + beat * 0.5f) % beat;
                s += Noise() * Env(off, 0.001f, 0.03f) * 0.05f;
                // plucked melody note every half beat (pentatonic, quiet)
                float half = t % (beat * 0.5f);
                int step = (int)(t / (beat * 0.5f));
                float[] mel = { 659.3f, 587.3f, 523.3f, 440f, 523.3f, 587.3f, 659.3f, 784f };
                if (step % 3 != 2) s += Sine(mel[(step * 5) % mel.Length], half) * Env(half, 0.003f, 0.18f) * 0.05f;
                data[i] = Mathf.Clamp(s, -1f, 1f) * 0.9f;
            }
            var c = AudioClip.Create("music", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
