using System.Collections.Generic;
using UnityEngine;

namespace WobbleWaiter
{
    // Sound effects (Kenney CC0 packs in Resources/Audio) and the background song,
    // which is composed in code: drums, walking bass, electric piano and a melody.
    public class Sfx : MonoBehaviour
    {
        const int SR = 22050;
        AudioSource fx, steps, music, jingle;
        float soundVol = 1, musicVol = 0.6f;
        readonly System.Random rng = new System.Random(7);
        readonly Dictionary<string, AudioClip[]> sets = new Dictionary<string, AudioClip[]>();
        int stepIndex;

        public float SoundVolume { get => soundVol; set { soundVol = Mathf.Clamp01(value); steps.volume = 0.22f * soundVol; jingle.volume = 0.7f * soundVol; } }
        public float MusicVolume { get => musicVol; set { musicVol = Mathf.Clamp01(value); music.volume = 0.42f * musicVol; if (musicVol > 0 && !music.isPlaying) music.Play(); if (musicVol <= 0) music.Stop(); } }

        void Awake()
        {
            fx = gameObject.AddComponent<AudioSource>();
            steps = gameObject.AddComponent<AudioSource>();
            jingle = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            foreach (var s in new[] { fx, steps, jingle, music }) s.playOnAwake = false;

            Load("land", "impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002", "impactGeneric_light_000", "impactGeneric_light_001");
            Load("smash", "impactPlate_heavy_000", "impactPlate_heavy_001", "impactPlate_heavy_002");
            Load("glass", "impactGlass_light_000");
            Load("thud", "impactWood_heavy_000", "impactWood_heavy_001", "impactPunch_heavy_000");
            Load("step", "footstep_carpet_000", "footstep_carpet_001", "footstep_carpet_002", "footstep_carpet_003", "footstep_carpet_004");
            Load("coin", "chips-collide-1", "chips-collide-2");
            Load("click", "select_002");
            Load("back", "back_002");
            Load("whoosh", "maximize_004");
            Load("slip", "drop_002");
            Load("miss", "error_004");
            Load("start", "jingles_PIZZI00");
            Load("good", "jingles_PIZZI10");
            Load("over", "jingles_SAX03");

            music.clip = BuildSong();
            SoundVolume = Settings.SoundVolume;
            MusicVolume = Settings.MusicVolume;
        }

        void Load(string key, params string[] files)
        {
            var list = new List<AudioClip>();
            foreach (var f in files) { var c = Resources.Load<AudioClip>("Audio/" + f); if (c != null) list.Add(c); }
            sets[key] = list.ToArray();
        }

        void Play(string key, float vol = 1f, float pitchJitter = 0.06f, AudioSource src = null)
        {
            if (soundVol <= 0 || !sets.TryGetValue(key, out var clips) || clips.Length == 0) return;
            var s = src ?? fx;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clips[Random.Range(0, clips.Length)], vol * soundVol);
        }

        public void Land(float strength) => Play("land", Mathf.Clamp(strength, 0.25f, 1f) * 0.8f, 0.12f);
        public void Smash(bool glass) { Play("smash", 0.9f); if (glass) Play("glass", 0.6f); }
        public void Thud(float strength) => Play("thud", strength);
        public void Coin() => Play("coin", 0.7f, 0.1f);
        public void Click() => Play("click", 0.7f, 0f);
        public void Back() => Play("back", 0.7f, 0f);
        public void Whoosh() => Play("whoosh", 0.5f, 0.1f);
        public void Slip() => Play("slip", 0.8f);
        public void Miss() => Play("miss", 0.5f, 0f);
        public void Milestone() => PlayJingle("good");
        public void RunStart() => PlayJingle("start");
        public void Jingle(bool good) => PlayJingle(good ? "good" : "over");

        void PlayJingle(string key)
        {
            if (soundVol <= 0 || !sets.TryGetValue(key, out var clips) || clips.Length == 0) return;
            jingle.Stop();
            jingle.clip = clips[0];
            jingle.pitch = 1;
            jingle.Play();
        }

        // one footstep each time a foot lands; quieter when walking slowly
        public void Step(float speed)
        {
            if (soundVol <= 0 || !sets.TryGetValue("step", out var clips) || clips.Length == 0) return;
            steps.pitch = 0.95f + Random.Range(0f, 0.1f);
            steps.PlayOneShot(clips[stepIndex++ % clips.Length], Mathf.Clamp(speed / 5f, 0.35f, 1f));
        }

        // ---------- the song ----------
        static float Midi(int n) => 440f * Mathf.Pow(2, (n - 69) / 12f);
        float Noise() => (float)(rng.NextDouble() * 2 - 1);

        AudioClip BuildSong()
        {
            const float bpm = 112f;
            float beat = 60f / bpm;
            // 16 bars: C Am F G | C Am Dm G | F G Em Am | F G C C
            int[] roots = { 48, 45, 41, 43, 48, 45, 38, 43, 41, 43, 40, 45, 41, 43, 48, 48 };
            int[][] chord =
            {
                new[] { 0, 4, 7, 11 }, new[] { 0, 3, 7, 10 }, new[] { 0, 4, 7, 11 }, new[] { 0, 4, 7, 10 },
                new[] { 0, 4, 7, 11 }, new[] { 0, 3, 7, 10 }, new[] { 0, 3, 7, 10 }, new[] { 0, 4, 7, 10 },
                new[] { 0, 4, 7, 11 }, new[] { 0, 4, 7, 10 }, new[] { 0, 3, 7, 10 }, new[] { 0, 3, 7, 10 },
                new[] { 0, 4, 7, 11 }, new[] { 0, 4, 7, 10 }, new[] { 0, 4, 7, 11 }, new[] { 0, 4, 7, 9 }
            };
            // melody: (bar, beat, midi, length in beats); 0 = rest. A playful two-phrase tune, answered in bars 9-16.
            var mel = new List<(int bar, float b, int n, float len)>();
            int[][] phraseA =
            {
                new[] { 72, 74, 76, 79 }, new[] { 76, 74, 72, 69 }, new[] { 72, 69, 65, 69 }, new[] { 71, 74, 79, 77 },
                new[] { 76, 79, 81, 79 }, new[] { 76, 72, 74, 76 }, new[] { 77, 74, 72, 74 }, new[] { 71, 72, 74, 0 }
            };
            int[][] phraseB =
            {
                new[] { 77, 76, 74, 72 }, new[] { 74, 76, 79, 77 }, new[] { 76, 74, 71, 76 }, new[] { 72, 76, 81, 79 },
                new[] { 77, 81, 79, 77 }, new[] { 76, 79, 77, 74 }, new[] { 76, 79, 84, 81 }, new[] { 79, 76, 72, 0 }
            };
            for (int bar = 0; bar < 16; bar++)
            {
                var notes = bar < 8 ? phraseA[bar] : phraseB[bar - 8];
                for (int i = 0; i < 4; i++) if (notes[i] > 0) mel.Add((bar, i, notes[i], i == 3 ? 1.4f : 0.8f));
            }

            float total = 16 * 4 * beat;
            int n = Mathf.CeilToInt(total * SR);
            var d = new float[n];
            for (int bar = 0; bar < 16; bar++)
            {
                float t0 = bar * 4 * beat;
                int root = roots[bar];
                var ch = chord[bar];
                for (int b = 0; b < 4; b++)
                {
                    float tb = t0 + b * beat;
                    if (b == 0 || b == 2) Kick(d, tb);
                    if (b == 1 || b == 3) Snare(d, tb);
                    for (int e = 0; e < 2; e++) Hat(d, tb + e * beat * 0.5f + (e == 1 ? beat * 0.06f : 0), e == 1 ? 0.06f : 0.04f);
                }
                // walking bass
                int[] walk = { 0, 7, 12, 10 };
                for (int b = 0; b < 4; b++) Bass(d, t0 + b * beat, Midi(root - 12 + walk[b]), beat * 0.9f);
                // electric piano: on the beat and pushed onto the "and" of 2
                foreach (float at in new[] { 0f, 1.5f, 3f })
                    for (int k = 1; k < 4; k++) Keys(d, t0 + at * beat, Midi(root + 12 + ch[k]), beat * (at == 1.5f ? 1.4f : 1f));
            }
            foreach (var m in mel) Lead(d, (m.bar * 4 + m.b) * beat, Midi(m.n), m.len * beat);

            // gentle compression so it sits under the sound effects
            for (int i = 0; i < n; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.4f) * 0.8f;
            var clip = AudioClip.Create("diner-song", n, 1, SR, false);
            clip.SetData(d, 0);
            return clip;
        }

        static void Add(float[] d, float start, float len, System.Func<float, float> f)
        {
            int s = Mathf.RoundToInt(start * SR), c = Mathf.RoundToInt(len * SR);
            for (int k = 0; k < c; k++) { int i = (s + k) % d.Length; d[i] += f(k / (float)SR); }
        }

        static void Kick(float[] d, float t) =>
            Add(d, t, 0.22f, x => Mathf.Sin(2 * Mathf.PI * (50 * x + 90 * (1 - Mathf.Exp(-x * 30)) / 30)) * Mathf.Exp(-x * 14) * 0.55f);

        void Snare(float[] d, float t)
        {
            float prev = 0;
            Add(d, t, 0.18f, x => { float nz = Noise(); float hp = nz - prev; prev = nz; return (hp * 0.22f + Mathf.Sin(2 * Mathf.PI * 190 * x) * 0.12f) * Mathf.Exp(-x * 22); });
        }

        void Hat(float[] d, float t, float vol)
        {
            float prev = 0;
            Add(d, t, 0.04f, x => { float nz = Noise(); float hp = nz - prev; prev = nz; return hp * vol * Mathf.Exp(-x * 90); });
        }

        static void Bass(float[] d, float t, float f, float len) =>
            Add(d, t, len, x =>
            {
                float p = f * x % 1f, tri = 4 * Mathf.Abs(p - 0.5f) - 1;
                float env = Mathf.Min(1, x * 200) * Mathf.Exp(-x * 2.2f);
                return (tri * 0.8f + Mathf.Sin(2 * Mathf.PI * f * x) * 0.4f) * env * 0.28f;
            });

        static void Keys(float[] d, float t, float f, float len) =>
            Add(d, t, len, x =>
            {
                float env = Mathf.Min(1, x * 120) * Mathf.Exp(-x * 3.5f);
                float trem = 1 + 0.15f * Mathf.Sin(2 * Mathf.PI * 5.5f * x);
                float tone = Mathf.Sin(2 * Mathf.PI * f * x) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2 * x + 0.5f) + 0.12f * Mathf.Sin(2 * Mathf.PI * f * 3.01f * x);
                return tone * env * trem * 0.05f;
            });

        static void Lead(float[] d, float t, float f, float len) =>
            Add(d, t, len, x =>
            {
                float vib = 1 + 0.004f * Mathf.Sin(2 * Mathf.PI * 5.8f * x) * Mathf.Min(1, x * 3);
                float p = f * vib * x % 1f;
                float sq = (p < 0.5f ? 1 : -1) * 0.35f + Mathf.Sin(2 * Mathf.PI * f * vib * x) * 0.65f;
                float env = Mathf.Min(1, x * 60) * (x < len * 0.7f ? 1 : Mathf.Max(0, 1 - (x - len * 0.7f) / (len * 0.3f)));
                return sq * env * 0.09f;
            });
    }
}
