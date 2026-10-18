using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlindAttack.Game
{
    public enum SoundId
    {
        UiClick,
        UiHover,
        UiCancel,
        UiCopy,
        BannerWhoosh,
        BannerGong,
        PlaceUnit,
        PlaceOgre,
        PlaceTrap,
        PlaceCrate,
        Remove,
        Error,
        Step,
        SwordSlash,
        Impact,
        ArrowShoot,
        ArrowHit,
        ShieldBlock,
        BombExplode,
        TrapSnap,
        PotionDrink,
        HeroDie,
        EnemyDie,
        UrgentTick,
        RoundWin,
        RoundLoss,
        VictoryFanfare,
        AmbientMusic
    }

    /// <summary>
    /// Harici ses dosyalarına ihtiyaç duymadan çalışma anında matematiksel sentez ile AudioClip üretir.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 22050;
        static readonly Dictionary<SoundId, AudioClip> cache = new Dictionary<SoundId, AudioClip>();

        public static AudioClip Get(SoundId id)
        {
            if (cache.TryGetValue(id, out var clip) && clip != null) return clip;
            clip = Synthesize(id);
            cache[id] = clip;
            return clip;
        }

        static AudioClip Synthesize(SoundId id)
        {
            float[] samples;
            switch (id)
            {
                case SoundId.UiClick:
                    samples = GenClick();
                    break;
                case SoundId.UiHover:
                    samples = GenHover();
                    break;
                case SoundId.UiCancel:
                    samples = GenToneSweep(420f, 180f, 0.08f, 0.6f);
                    break;
                case SoundId.UiCopy:
                    samples = GenDoubleChime(1046f, 1318f, 0.22f);
                    break;
                case SoundId.BannerWhoosh:
                    samples = GenWhoosh(0.35f);
                    break;
                case SoundId.BannerGong:
                    samples = GenGong(1.2f);
                    break;
                case SoundId.PlaceUnit:
                    samples = GenThud(170f, 55f, 0.12f, 0.9f);
                    break;
                case SoundId.PlaceOgre:
                    samples = GenThud(95f, 32f, 0.25f, 1.0f, true);
                    break;
                case SoundId.PlaceTrap:
                    samples = GenMetalClick(0.1f);
                    break;
                case SoundId.PlaceCrate:
                    samples = GenWoodKnock(0.14f);
                    break;
                case SoundId.Remove:
                    samples = GenToneSweep(110f, 380f, 0.09f, 0.7f);
                    break;
                case SoundId.Error:
                    samples = GenErrorBuzz();
                    break;
                case SoundId.Step:
                    samples = GenStep();
                    break;
                case SoundId.SwordSlash:
                    samples = GenSlash();
                    break;
                case SoundId.Impact:
                    samples = GenImpact();
                    break;
                case SoundId.ArrowShoot:
                    samples = GenBowRelease();
                    break;
                case SoundId.ArrowHit:
                    samples = GenArrowHit();
                    break;
                case SoundId.ShieldBlock:
                    samples = GenShieldClang();
                    break;
                case SoundId.BombExplode:
                    samples = GenExplosion(0.85f);
                    break;
                case SoundId.TrapSnap:
                    samples = GenTrapSnap();
                    break;
                case SoundId.PotionDrink:
                    samples = GenHealChime();
                    break;
                case SoundId.HeroDie:
                    samples = GenHeroFall();
                    break;
                case SoundId.EnemyDie:
                    samples = GenEnemyPoof();
                    break;
                case SoundId.UrgentTick:
                    samples = GenTick();
                    break;
                case SoundId.RoundWin:
                    samples = GenChordFanfare(new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.65f);
                    break;
                case SoundId.RoundLoss:
                    samples = GenChordFanfare(new[] { 622.25f, 587.33f, 523.25f }, 0.7f);
                    break;
                case SoundId.VictoryFanfare:
                    samples = GenVictoryMelody();
                    break;
                case SoundId.AmbientMusic:
                    samples = GenAmbientLoop();
                    break;
                default:
                    samples = GenClick();
                    break;
            }

            SmoothEdges(samples, id == SoundId.AmbientMusic);
            var audioClip = AudioClip.Create(id.ToString(), samples.Length, 1, SampleRate, false);
            audioClip.SetData(samples, 0);
            return audioClip;
        }

        class BrownNoiseGen
        {
            float val = 0f;
            public float Next(System.Random rng)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                val = (val + 0.035f * white) / 1.035f;
                return Mathf.Clamp(val * 4.5f, -1f, 1f);
            }
        }

        static void SmoothEdges(float[] s, bool loopCrossfade = false)
        {
            if (s == null || s.Length < 32) return;
            int n = s.Length;

            if (loopCrossfade)
            {
                int cross = Mathf.Min(n / 4, (int)(SampleRate * 0.12f));
                for (int i = 0; i < cross; i++)
                {
                    float k = (float)i / cross;
                    s[i] = s[i] * k + s[n - cross + i] * (1f - k);
                }
                return;
            }

            int attack = Mathf.Min(n / 6, Mathf.Max(16, (int)(SampleRate * 0.005f)));
            for (int i = 0; i < attack; i++)
            {
                float k = (float)i / attack;
                float w = 0.5f * (1f - Mathf.Cos(k * Mathf.PI));
                s[i] *= w;
            }
            s[0] = 0f;

            int release = Mathf.Min(n / 3, Mathf.Max(32, (int)(SampleRate * 0.016f)));
            for (int i = 0; i < release; i++)
            {
                float k = (float)i / release;
                float w = 0.5f * (1f - Mathf.Cos(k * Mathf.PI));
                s[n - 1 - i] *= w;
            }
            s[n - 1] = 0f;
        }

        // ================= Dalga Formu Üreticileri =================

        static float[] GenClick()
        {
            int n = (int)(SampleRate * 0.032f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.0025f);
                float env = Mathf.Exp(-t * 95f) * att;
                float freq = Mathf.Lerp(680f, 180f, (float)i / n);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.65f;
            }
            return s;
        }

        static float[] GenHover()
        {
            int n = (int)(SampleRate * 0.024f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin((float)i / n * Mathf.PI);
                float freq = Mathf.Lerp(480f, 580f, (float)i / n);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.22f;
            }
            return s;
        }

        static float[] GenToneSweep(float startFreq, float endFreq, float duration, float vol)
        {
            int n = (int)(SampleRate * duration);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float freq = Mathf.Lerp(startFreq, endFreq, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float att = Mathf.Clamp01(k / 0.12f);
                float rel = Mathf.Clamp01((1f - k) / 0.15f);
                float env = (1f - k * 0.85f) * att * rel;
                s[i] = Mathf.Sin(phase) * env * vol;
            }
            return s;
        }

        static float[] GenDoubleChime(float f1, float f2, float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.004f);
                float env = Mathf.Exp(-t * 16f) * att;
                s[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f) * env;
            }
            return s;
        }

        static float[] GenWhoosh(float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            var rng = new System.Random(42);
            var brown = new BrownNoiseGen();
            float filter = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float env = Mathf.Sin(k * Mathf.PI);
                float noise = brown.Next(rng);
                float alpha = Mathf.Lerp(0.08f, 0.22f, env);
                filter += alpha * (noise - filter);
                s[i] = filter * env * 0.75f;
            }
            return s;
        }

        static float[] GenGong(float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.005f);
                float env = Mathf.Exp(-t * 3.8f) * att;
                float wave = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * 274f * t) * 0.3f
                           + Mathf.Sin(2f * Mathf.PI * 415f * t) * 0.15f;
                s[i] = wave * env * 0.9f;
            }
            return s;
        }

        static float[] GenThud(float startF, float endF, float dur, float vol, bool subBass = false)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            float phase = 0f;
            var rng = new System.Random(1337);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float freq = Mathf.Lerp(startF, endF, k * k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float att = Mathf.Clamp01(k / 0.04f);
                float env = Mathf.Exp(-k * (subBass ? 5f : 9f)) * att;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-k * 30f) * 0.2f;
                s[i] = (Mathf.Sin(phase) + noise) * env * vol;
            }
            return s;
        }

        static float[] GenMetalClick(float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.003f);
                float env = Mathf.Exp(-t * 55f) * att;
                float wave = Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.55f
                           + Mathf.Sin(2f * Mathf.PI * 1820f * t) * 0.25f;
                s[i] = wave * env * 0.70f;
            }
            return s;
        }

        static float[] GenWoodKnock(float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.003f);
                float env = Mathf.Exp(-t * 45f) * att;
                float freq = Mathf.Lerp(240f, 95f, (float)i / n);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.75f;
            }
            return s;
        }

        static float[] GenErrorBuzz()
        {
            int n = (int)(SampleRate * 0.17f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float beep1 = (t < 0.065f) ? Mathf.Sin(t / 0.065f * Mathf.PI) : 0f;
                float beep2 = (t > 0.090f && t < 0.155f) ? Mathf.Sin((t - 0.090f) / 0.065f * Mathf.PI) : 0f;
                float env = beep1 + beep2;
                float tone = Mathf.Sin(2f * Mathf.PI * 155f * t) + Mathf.Sin(2f * Mathf.PI * 232f * t) * 0.35f;
                s[i] = Mathf.Clamp(tone * 1.5f, -0.65f, 0.65f) * env * 0.45f;
            }
            return s;
        }

        static float[] GenStep()
        {
            int n = (int)(SampleRate * 0.06f);
            var s = new float[n];
            var rng = new System.Random();
            var brown = new BrownNoiseGen();
            float filter = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float env = Mathf.Sin(k * Mathf.PI);
                float noise = brown.Next(rng);
                filter += 0.15f * (noise - filter);
                float tap = Mathf.Sin(2f * Mathf.PI * 68f * (float)i / SampleRate) * 0.35f;
                s[i] = (filter * 0.65f + tap) * env * 0.28f;
            }
            return s;
        }

        static float[] GenSlash()
        {
            int n = (int)(SampleRate * 0.15f);
            var s = new float[n];
            var rng = new System.Random(99);
            var brown = new BrownNoiseGen();
            float filter = 0f;
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float env = Mathf.Sin(k * Mathf.PI);
                float noise = brown.Next(rng);
                float alpha = Mathf.Lerp(0.20f, 0.05f, k);
                filter += alpha * (noise - filter);
                float freq = Mathf.Lerp(360f, 150f, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float bladeTone = Mathf.Sin(phase) * 0.35f;
                s[i] = (filter * 0.65f + bladeTone) * env * 0.70f;
            }
            return s;
        }

        static float[] GenImpact()
        {
            int n = (int)(SampleRate * 0.16f);
            var s = new float[n];
            var rng = new System.Random(77);
            var brown = new BrownNoiseGen();
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float freq = Mathf.Lerp(190f, 42f, k * k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float att = Mathf.Clamp01(k / 0.025f);
                float env = Mathf.Exp(-k * 8.5f) * att;
                float punchNoise = brown.Next(rng) * Mathf.Exp(-k * 20f);
                s[i] = (Mathf.Sin(phase) * 0.75f + punchNoise * 0.35f) * env * 0.85f;
            }
            return s;
        }

        static float[] GenBowRelease()
        {
            int n = (int)(SampleRate * 0.14f);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float att = Mathf.Clamp01(k / 0.05f);
                float freq = Mathf.Lerp(580f, 240f, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Exp(-k * 11f) * att;
                s[i] = Mathf.Sin(phase) * env * 0.65f;
            }
            return s;
        }

        static float[] GenArrowHit()
        {
            int n = (int)(SampleRate * 0.08f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float att = Mathf.Clamp01(k / 0.06f);
                float freq = Mathf.Lerp(420f, 90f, k);
                float env = Mathf.Exp(-k * 22f) * att;
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * (float)i / SampleRate) * env * 0.75f;
            }
            return s;
        }

        static float[] GenShieldClang()
        {
            int n = (int)(SampleRate * 0.30f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.003f);
                float env = Mathf.Exp(-t * 8.5f) * att;
                float wave = Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.45f
                           + Mathf.Sin(2f * Mathf.PI * 1640f * t) * 0.35f
                           + Mathf.Sin(2f * Mathf.PI * 2380f * t) * 0.12f;
                s[i] = wave * env * 0.75f;
            }
            return s;
        }

        static float[] GenExplosion(float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            var rng = new System.Random(12345);
            var brown = new BrownNoiseGen();
            float filter = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float att = Mathf.Clamp01(k / 0.02f);
                float env = Mathf.Exp(-k * 4.0f) * att;
                float noise = brown.Next(rng);
                filter += 0.06f * (noise - filter);
                float freq = Mathf.Lerp(80f, 24f, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float sub = Mathf.Sin(phase) * 0.65f;
                s[i] = (filter * 0.65f + sub) * env * 0.95f;
            }
            return s;
        }

        static float[] GenTrapSnap()
        {
            int n = (int)(SampleRate * 0.20f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.0025f);
                float env = Mathf.Exp(-t * 16f) * att;
                float metal = Mathf.Sin(2f * Mathf.PI * 1350f * t) * 0.45f
                            + Mathf.Sin(2f * Mathf.PI * 2100f * t) * 0.25f;
                float crunch = Mathf.Lerp(260f, 55f, (float)i / n);
                s[i] = (metal + Mathf.Sin(2f * Mathf.PI * crunch * t) * 0.45f) * env;
            }
            return s;
        }

        static float[] GenHealChime()
        {
            int n = (int)(SampleRate * 0.38f);
            var s = new float[n];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float noteDur = 0.088f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                int idx = Mathf.Clamp(Mathf.FloorToInt(t / noteDur), 0, notes.Length - 1);
                float nt = t - idx * noteDur;
                float noteEnv = Mathf.Sin(Mathf.Clamp01(nt / noteDur) * Mathf.PI);
                s[i] = Mathf.Sin(2f * Mathf.PI * notes[idx] * nt) * noteEnv * 0.42f;
            }
            return s;
        }

        static float[] GenHeroFall()
        {
            int n = (int)(SampleRate * 0.55f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float att = Mathf.Clamp01(k / 0.04f);
                float freq = Mathf.Lerp(220f, 55f, k * k);
                float env = (1f - k) * att;
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * (float)i / SampleRate) * env * 0.65f;
            }
            return s;
        }

        static float[] GenEnemyPoof()
        {
            int n = (int)(SampleRate * 0.18f);
            var s = new float[n];
            var rng = new System.Random(555);
            var brown = new BrownNoiseGen();
            float filter = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float att = Mathf.Clamp01(k / 0.04f);
                float env = Mathf.Exp(-k * 8f) * att;
                float noise = brown.Next(rng);
                filter += 0.12f * (noise - filter);
                float freq = Mathf.Lerp(260f, 65f, k);
                s[i] = (Mathf.Sin(2f * Mathf.PI * freq * (float)i / SampleRate) * 0.45f + filter * 0.55f) * env * 0.7f;
            }
            return s;
        }

        static float[] GenTick()
        {
            int n = (int)(SampleRate * 0.038f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.002f);
                float env = Mathf.Exp(-t * 120f) * att;
                s[i] = Mathf.Sin(2f * Mathf.PI * 740f * t) * env * 0.65f;
            }
            return s;
        }

        static float[] GenChordFanfare(float[] freqs, float dur)
        {
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float att = Mathf.Clamp01(t / 0.006f);
                float env = Mathf.Exp(-t * 3.5f) * att;
                float sum = 0f;
                foreach (var f in freqs) sum += Mathf.Sin(2f * Mathf.PI * f * t);
                s[i] = (sum / freqs.Length) * env * 0.85f;
            }
            return s;
        }

        static float[] GenVictoryMelody()
        {
            float dur = 1.6f;
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            (float f, float start, float len)[] notes =
            {
                (523.25f, 0.0f, 0.18f),
                (659.25f, 0.18f, 0.18f),
                (783.99f, 0.36f, 0.18f),
                (1046.50f, 0.54f, 0.68f)
            };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;
                foreach (var note in notes)
                {
                    if (t >= note.start && t < note.start + note.len)
                    {
                        float nt = t - note.start;
                        float att = Mathf.Clamp01(nt / 0.008f);
                        float rel = Mathf.Clamp01((note.len - nt) / 0.015f);
                        float env = Mathf.Exp(-nt * 2.8f) * att * rel;
                        sample += Mathf.Sin(2f * Mathf.PI * note.f * nt) * env * 0.68f;
                    }
                }
                s[i] = sample;
            }
            return s;
        }

        static float[] GenAmbientLoop()
        {
            float dur = 8.0f;
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            var rng = new System.Random(1337);
            var brown = new BrownNoiseGen();

            float filter = 0f;
            for (int i = 0; i < n; i++)
            {
                float noise = brown.Next(rng);
                filter += 0.04f * (noise - filter);
                s[i] = filter * 0.018f;
            }

            var notes = new (float f, float start, float len, float vol)[]
            {
                // Bar 0: Am
                (110.00f, 0.00f, 1.85f, 0.18f),
                (220.00f, 0.00f, 0.95f, 0.16f),
                (261.63f, 0.45f, 0.90f, 0.14f),
                (329.63f, 0.90f, 0.90f, 0.15f),
                (440.00f, 1.35f, 0.75f, 0.13f),

                // Bar 1: F
                (87.31f,  2.00f, 1.85f, 0.18f),
                (174.61f, 2.00f, 0.95f, 0.15f),
                (220.00f, 2.45f, 0.90f, 0.14f),
                (261.63f, 2.90f, 0.90f, 0.15f),
                (329.63f, 3.35f, 0.75f, 0.13f),

                // Bar 2: C
                (130.81f, 4.00f, 1.85f, 0.18f),
                (196.00f, 4.00f, 0.95f, 0.15f),
                (261.63f, 4.45f, 0.90f, 0.15f),
                (329.63f, 4.90f, 0.90f, 0.15f),
                (392.00f, 5.35f, 0.75f, 0.14f),

                // Bar 3: G
                (98.00f,  6.00f, 1.85f, 0.18f),
                (146.83f, 6.00f, 0.95f, 0.14f),
                (196.00f, 6.45f, 0.90f, 0.14f),
                (246.94f, 6.90f, 0.90f, 0.15f),
                (293.66f, 7.35f, 0.70f, 0.13f)
            };

            foreach (var note in notes)
            {
                int startSample = (int)(note.start * SampleRate);
                int noteSamples = (int)(note.len * SampleRate);
                float phase = 0f;
                for (int i = 0; i < noteSamples; i++)
                {
                    int idx = (startSample + i) % n;
                    float t = (float)i / SampleRate;
                    float att = t < 0.012f ? 0.5f * (1f - Mathf.Cos(t / 0.012f * Mathf.PI)) : 1f;
                    float env = att * Mathf.Exp(-t * 2.2f);
                    phase += 2f * Mathf.PI * note.f / SampleRate;
                    float acoustic = Mathf.Sin(phase) * 0.70f
                                   + Mathf.Sin(phase * 2f) * 0.22f
                                   + Mathf.Sin(phase * 3f) * 0.08f;
                    s[idx] += acoustic * env * note.vol;
                }
            }

            float padPhase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                int bar = (int)(t / 2.0f) % 4;
                float chordRoot = bar == 0 ? 220f : bar == 1 ? 174.61f : bar == 2 ? 130.81f : 196f;
                padPhase += 2f * Mathf.PI * (chordRoot * 0.5f) / SampleRate;
                float barT = (t % 2.0f) / 2.0f;
                float padEnv = Mathf.Sin(barT * Mathf.PI);
                s[i] += Mathf.Sin(padPhase) * padEnv * 0.045f;
            }

            return s;
        }
    }
}
