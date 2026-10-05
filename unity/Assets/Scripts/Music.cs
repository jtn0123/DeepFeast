using UnityEngine;

namespace DeepFeast
{
    /// <summary>Music, synthesised at startup like the rest of the sound. A pad for each habitat
    /// crossfades as the camera goes deeper, an arpeggio grows with the hero, a pulse plays under
    /// shark encounters and the finale, and a sting marks the victory. Every loop shares one
    /// four-chord progression and one length, so they stay in time and in key.</summary>
    public sealed class Music
    {
        const int SR = 22050;
        const float LOOP = 16, SLOT = LOOP / 4, Level = 0.16f;
        readonly AudioSource reef, kelp, abyss, arp, pulse, sting;
        float level = 1, duck, tense, stingT = 99;

        // Am7, Fmaj7, Cmaj7 and G6, one per four-second slot, voiced for each habitat.
        static readonly int[][] ReefChords = { new[] { 57, 60, 64, 67 }, new[] { 53, 57, 60, 64 }, new[] { 48, 55, 59, 64 }, new[] { 55, 59, 62, 64 } };
        static readonly int[][] KelpChords = { new[] { 45, 52, 59, 60 }, new[] { 41, 48, 55, 57 }, new[] { 48, 55, 62, 64 }, new[] { 43, 50, 57, 59 } };
        static readonly int[][] AbyssChords = { new[] { 33, 45, 52 }, new[] { 29, 41, 48 }, new[] { 36, 43, 48 }, new[] { 31, 43, 50 } };
        static readonly int[][] ArpNotes =
        {
            new[] { 69, 72, 76, 79, 76, 72, 74, 72 }, new[] { 65, 69, 72, 76, 72, 69, 67, 69 },
            new[] { 64, 67, 72, 76, 79, 76, 72, 74 }, new[] { 74, 79, 71, 74, 76, 74, 71, 67 },
        };

        public Music(GameObject host)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            System.Func<float[]>[] stems = { () => Pad(ReefChords, 3), () => Pad(KelpChords, 1.5f), Abyss, Arpeggio, Pulse, Sting };
            var d = new float[stems.Length][];
#if UNITY_WEBGL
            for (int i = 0; i < stems.Length; i++) d[i] = stems[i]();
#else
            System.Threading.Tasks.Parallel.For(0, stems.Length, i => d[i] = stems[i]());
#endif
            reef = Source(host, "reef", d[0], 0.5f);
            kelp = Source(host, "kelp", d[1], 0.5f);
            abyss = Source(host, "abyss", d[2], 0.55f);
            arp = Source(host, "arp", d[3], 0.32f);
            pulse = Source(host, "pulse", d[4], 0.42f);
            sting = Source(host, "sting", d[5], 0.6f, false);
            // Scheduled together on the audio clock, the loops start on the same sample and stay locked.
            double start = AudioSettings.dspTime + 0.2;
            foreach (var s in new[] { reef, kelp, abyss, arp, pulse }) { s.volume = 0; s.PlayScheduled(start); }
            Debug.Log($"[DeepFeast] music synthesised in {watch.ElapsedMilliseconds} ms");
        }

        public void SetLevel(float l) => level = l;

        // Big moments (a bite taken out of you, a shark, a tier-up) push the music back for a moment.
        public void Duck(float amount) => duck = Mathf.Max(duck, amount);

        public void Victory()
        {
            sting.volume = level * Level;
            sting.Play();
            stingT = 0;
        }

        /// look: the habitat around the camera; growth: 0 for a Fry to 1 for a Legend; danger: a shark
        /// encounter or the finale is under way.
        public void Update(float rdt, Habitat.Look look, float growth, bool danger)
        {
            duck = Mathf.Max(0, duck - rdt * 0.7f);
            stingT += rdt;
            tense = Mathf.MoveTowards(tense, danger ? 1 : 0, rdt / (danger ? 1.5f : 3));
            // The loops make room while the victory sting rings.
            float room = stingT < 4 ? 0.25f : Mathf.Lerp(0.25f, 1, (stingT - 4) / 2);
            float bed = level * Level * (1 - duck) * room, calm = 1 - 0.45f * tense;
            float reefW = Mathf.Max(0, 1 - look.kelp - look.abyss);
            reef.volume = bed * reefW * calm;
            kelp.volume = bed * look.kelp * calm;
            abyss.volume = bed * look.abyss * calm;
            arp.volume = bed * Mathf.Lerp(0.3f, 1, growth) * (1 - 0.85f * look.abyss) * (1 - 0.7f * tense);
            pulse.volume = bed * tense;
        }

        // ------------------------------------------------------------------ synthesis
        static AudioSource Source(GameObject host, string name, float[] d, float peak, bool loop = true)
        {
            float max = 1e-6f;
            foreach (float x in d) max = Mathf.Max(max, Mathf.Abs(x));
            for (int i = 0; i < d.Length; i++) d[i] *= peak / max;
            var clip = AudioClip.Create("music_" + name, d.Length, 1, SR, false);
            clip.SetData(d, 0);
            var s = host.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.playOnAwake = false;
            return s;
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2, (midi - 69) / 12f);

        // Adds a note at time t0 for dur seconds; writing round the end of the buffer means a
        // note that rings past the loop's end carries on at its start, so the loop has no seam.
        delegate float Shape(float t);
        static void Note(float[] buf, float t0, float dur, float hz, Shape env, float bright, float detune = 0)
        {
            int n = buf.Length, len = Mathf.RoundToInt(dur * SR), j = (Mathf.RoundToInt(t0 * SR) % n + n) % n;
            // Two slightly detuned oscillators, each a point turned round a circle sample by sample:
            // far cheaper than a sine per sample. Their overtones come from the double- and
            // triple-angle formulas.
            double w1 = U.TAU * hz * (1 + detune) / SR, w2 = U.TAU * hz * (1 - detune) / SR;
            double c1 = System.Math.Cos(w1), s1 = System.Math.Sin(w1), c2 = System.Math.Cos(w2), s2 = System.Math.Sin(w2);
            double x1 = 1, y1 = 0, x2 = 1, y2 = 0;
            for (int i = 0; i < len; i++)
            {
                float a = env(i / (float)SR);
                if (a > 0) buf[j] += a * (float)(y1 + y2 + bright * (2 * y1 * x1 * 0.3 + y2 * (3 - 4 * y2 * y2) * 0.12));
                double t = x1 * c1 - y1 * s1; y1 = y1 * c1 + x1 * s1; x1 = t;
                t = x2 * c2 - y2 * s2; y2 = y2 * c2 + x2 * s2; x2 = t;
                if (++j == n) j = 0;
            }
        }

        // Chords swell in and out across their slot and overlap the next by a second; bright adds
        // overtones, from a soft kelp-forest hum to the reef's clearer shimmer.
        static float[] Pad(int[][] chords, float bright)
        {
            var buf = new float[(int)(LOOP * SR)];
            const float swell = 1.4f, dur = SLOT + swell;
            for (int c = 0; c < chords.Length; c++)
                foreach (int m in chords[c])
                    Note(buf, c * SLOT - swell / 2, dur, Hz(m), t => Swell(t, dur, swell), bright * 0.25f, 0.0025f);
            Breathe(buf, 0.04f);
            return buf;
        }

        // The abyss: open fifths low down over an A that never stops, with a far-off sonar ping.
        static float[] Abyss()
        {
            var buf = Pad(AbyssChords, 0);
            for (int k = 0; k < 4; k++) Note(buf, 0.5f + k * SLOT, 3.5f, Hz(81 - (k % 2) * 5), t => Pluck(t, 0.9f) * 0.12f, 0.2f);
            return buf;
        }

        // Bell-like eighth notes that follow the chords.
        static float[] Arpeggio()
        {
            var buf = new float[(int)(LOOP * SR)];
            for (int c = 0; c < ArpNotes.Length; c++)
                for (int k = 0; k < 8; k++)
                    Note(buf, c * SLOT + k * SLOT / 8, 1.6f, Hz(ArpNotes[c][k]), t => Pluck(t, 0.45f) * (k % 2 == 0 ? 1 : 0.7f), 1.2f);
            return buf;
        }

        // A low pulse on A with a half-step lean to B flat at the end of each bar, and a soft thump on every beat.
        static float[] Pulse()
        {
            var buf = new float[(int)(LOOP * SR)];
            for (int k = 0; k < LOOP / 0.25f; k++)
            {
                int midi = k % 16 == 14 ? 34 : k % 2 == 0 ? 33 : 45;
                Note(buf, k * 0.25f, 0.35f, Hz(midi), t => Pluck(t, 0.09f) * (k % 4 == 0 ? 1 : 0.7f), 2.5f);
                if (k % 2 == 0) Note(buf, k * 0.25f, 0.3f, Hz(33), t => Pluck(t, 0.07f) * 0.9f, 0);
            }
            return buf;
        }

        // A rising C major fanfare that settles on a held chord.
        static float[] Sting()
        {
            var buf = new float[(int)(4.5f * SR)];
            int[] rise = { 60, 64, 67, 72, 76, 79 };
            for (int i = 0; i < rise.Length; i++) Note(buf, i * 0.11f, 1.4f, Hz(rise[i]), t => Pluck(t, 0.35f), 1.2f);
            foreach (int m in new[] { 48, 55, 60, 64, 67, 84 })
                Note(buf, 0.7f, 3.6f, Hz(m), t => Swell(t, 3.6f, 0.25f) * Mathf.Exp(-t * 0.5f) * 0.6f, m > 80 ? 0 : 1.5f, 0.002f);
            return buf;
        }

        // Rises and falls smoothly over `edge` seconds at each end.
        static float Swell(float t, float dur, float edge)
        {
            if (t < 0 || t > dur) return 0;
            float k = Mathf.Min(t, dur - t) / edge;
            return k >= 1 ? 1 : U.Smooth(k);
        }

        static float Pluck(float t, float decay) => t < 0.006f ? t / 0.006f : Mathf.Exp(-(t - 0.006f) / decay);

        // A slow swell in loudness, four breaths to the loop so the seam stays invisible.
        static void Breathe(float[] buf, float depth)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] *= 1 + depth * Mathf.Sin(U.TAU * 4 * i / buf.Length);
        }
    }
}
