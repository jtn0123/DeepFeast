using UnityEngine;

namespace DeepFeast
{
    /// <summary>All sound is synthesised into AudioClips at startup (no audio assets).</summary>
    public sealed class Sfx
    {
        const int SR = 44100;
        enum Wave { Sine, Triangle, Saw, Square }

        readonly AudioSource[] voices = new AudioSource[14];
        int next;
        readonly AudioSource ambient;
        AudioClip chomp, ding, tierUp, hurt, alert, shark, zap, pearl, dash, bump;
        public bool Muted { get; private set; }
        // The player's volume settings, 0 to 1.
        float master = 1, effects = 1;
        const float Listener = 0.5f, AmbientLevel = 0.22f;

        // A recording keeps its sound whatever the mute: the audio renderer takes the whole mix and the
        // speakers get silence.
        public Sfx(GameObject host, bool forceMute, bool recording = false)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = host.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            ambient = host.AddComponent<AudioSource>();
            ambient.loop = true;
            ambient.playOnAwake = false;
            ambient.volume = AmbientLevel;
            Muted = !recording && (forceMute || PlayerPrefs.GetInt("deepfeast.muted", 0) == 1);
            AudioListener.volume = Muted ? 0 : Listener;
            Build();
            ambient.clip = Ambient();
            ambient.Play();
        }

        public void SetMuted(bool m)
        {
            Muted = m;
            PlayerPrefs.SetInt("deepfeast.muted", m ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = m ? 0 : Listener * master;
        }

        public void SetLevels(float masterLevel, float effectsLevel, float ambienceLevel)
        {
            master = masterLevel; effects = effectsLevel;
            ambient.volume = AmbientLevel * ambienceLevel;
            AudioListener.volume = Muted ? 0 : Listener * master;
        }

        void Play(AudioClip c, float pitch = 1, float vol = 1)
        {
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.pitch = pitch;
            v.volume = vol * effects;
            v.clip = c;
            v.Play();
        }

        public void Chomp(float rel, int combo)
        {
            float p = Mathf.Clamp(560 - rel * 260, 160, 620) * (1 + Mathf.Min(combo, 8) * 0.04f);
            Play(chomp, p / 420f);
            if (combo > 1) Play(ding, Mathf.Pow(1.0595f, combo * 2));
        }
        public void TierUp() => Play(tierUp);
        public void Hurt() => Play(hurt);
        public void Alert() => Play(alert);
        public void Shark() => Play(shark);
        public void Zap() => Play(zap);
        public void Pearl() => Play(pearl);
        public void Dash() => Play(dash);
        public void Bump() => Play(bump);

        // ------------------------------------------------------------------ synthesis
        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

        static AudioClip Clip(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static void Tone(float[] buf, float f0, float f1, float dur, Wave w, float vol, float delay = 0)
        {
            int s0 = (int)(delay * SR), n = (int)(dur * SR);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                float f = f1 > 0 ? f0 * Mathf.Pow(f1 / f0, t / dur) : f0;
                ph += f / SR;
                float x = (float)(ph - System.Math.Floor(ph));
                float o = w switch
                {
                    Wave.Sine => Mathf.Sin(x * U.TAU),
                    Wave.Triangle => 1 - 4 * Mathf.Abs(x - 0.5f),
                    Wave.Saw => 2 * x - 1,
                    _ => x < 0.5f ? 1 : -1,
                };
                buf[s0 + i] += o * Env(t, dur, vol);
            }
        }

        static float Env(float t, float dur, float vol)
        {
            const float A = 0.012f, FLOOR = 0.0001f;
            if (t < A) return FLOOR * Mathf.Pow(vol / FLOOR, t / A);
            return vol * Mathf.Pow(FLOOR / vol, Mathf.Clamp01((t - A) / (dur - A)));
        }

        static readonly System.Random rng = new System.Random(7);
        static float White() => (float)rng.NextDouble() * 2 - 1;

        static void Noise(float[] buf, float dur, float vol, float freq, float q, float delay = 0)
        {
            int s0 = (int)(delay * SR), n = (int)(dur * SR);
            float w0 = U.TAU * freq / SR, alpha = Mathf.Sin(w0) / (2 * q), a0 = 1 + alpha;
            float b0 = alpha / a0, b2 = -alpha / a0, a1 = -2 * Mathf.Cos(w0) / a0, a2 = (1 - alpha) / a0;
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float x = White();
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                float t = i / (float)SR;
                buf[s0 + i] += y * vol * Mathf.Pow(0.0001f / vol, t / dur);
            }
        }

        void Build()
        {
            var b = Buf(0.16f); Tone(b, 420, 420 * 0.42f, 0.11f, Wave.Triangle, 0.22f); Noise(b, 0.07f, 0.2f, 2400, 0.9f); chomp = Clip("chomp", b);
            b = Buf(0.22f); Tone(b, 660, 0, 0.14f, Wave.Sine, 0.07f, 0.05f); ding = Clip("ding", b);
            b = Buf(0.85f); float[] tu = { 523, 659, 784, 1047, 1319 };
            for (int i = 0; i < tu.Length; i++) Tone(b, tu[i], 0, 0.4f, Wave.Triangle, 0.13f, i * 0.085f);
            tierUp = Clip("tierUp", b);
            b = Buf(0.65f); Tone(b, 240, 50, 0.6f, Wave.Saw, 0.16f); Noise(b, 0.35f, 0.25f, 420, 0.7f); hurt = Clip("hurt", b);
            b = Buf(0.12f); Tone(b, 900, 700, 0.1f, Wave.Square, 0.035f); alert = Clip("alert", b);
            b = Buf(1.55f); float[] sd = { 0, 0.42f, 0.75f, 1.0f, 1.2f };
            for (int i = 0; i < sd.Length; i++) Tone(b, i % 2 == 1 ? 87.3f : 82.4f, 0, 0.3f, Wave.Saw, 0.11f, sd[i]);
            shark = Clip("shark", b);
            b = Buf(0.32f); Tone(b, 1600, 260, 0.28f, Wave.Saw, 0.06f); Noise(b, 0.22f, 0.16f, 5200, 2); zap = Clip("zap", b);
            b = Buf(0.55f); float[] pf = { 1047, 1319, 1568, 2093 };
            for (int i = 0; i < pf.Length; i++) Tone(b, pf[i], 0, 0.3f, Wave.Sine, 0.09f, i * 0.06f);
            pearl = Clip("pearl", b);
            b = Buf(0.3f); Noise(b, 0.28f, 0.16f, 800, 0.5f); dash = Clip("dash", b);
            b = Buf(0.1f); Tone(b, 180, 120, 0.08f, Wave.Sine, 0.08f); bump = Clip("bump", b);
        }

        /// Brown noise through a slowly sweeping low-pass; loops seamlessly.
        static AudioClip Ambient()
        {
            const int sr = 22050;
            float len = 1f / 0.07f, fade = 0.6f;
            int n = (int)(len * sr), nf = (int)(fade * sr);
            var raw = new float[n + nf];
            float last = 0, x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            float b0 = 0, b1 = 0, b2 = 0, a1 = 0, a2 = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                if ((i & 63) == 0)
                {
                    float fc = 420 + 160 * Mathf.Sin(U.TAU * 0.07f * i / sr);
                    float w0 = U.TAU * fc / sr, cs = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2 * 0.8f), a0 = 1 + alpha;
                    b0 = (1 - cs) / 2 / a0; b1 = (1 - cs) / a0; b2 = b0; a1 = -2 * cs / a0; a2 = (1 - alpha) / a0;
                }
                last = (last + 0.02f * White()) / 1.02f;
                float x = last * 3.5f;
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                raw[i] = y;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < nf; i++)
            {
                float t = i / (float)nf;
                d[i] = raw[i] * t + raw[n + i] * (1 - t);
            }
            var c = AudioClip.Create("ambient", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
