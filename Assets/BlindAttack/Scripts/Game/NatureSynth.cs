using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Ortam sesleri (prosedürel, kesintisiz döngü): sabah kuşları, gün batımı esintisi, gece cırcır böcekleri ve baykuş.
    /// Ses dosyası gerekmez; her çağrı aynı sesi üretir (sabit tohum).
    /// </summary>
    public static class NatureSynth
    {
        const float LoopSeconds = 16f, Cross = 1.5f;

        public static float[] Morning(int sr) => Loop(sr, 101, (s, n, rng) =>
        {
            Wind(s, sr, rng, 0.05f, 0.25f);
            // ötleğen benzeri cıvıltılar: hızlı perde kaymalı 3-7 notalık cümleler
            float t = 0.3f;
            while (t < LoopSeconds + Cross)
            {
                int notes = rng.Next(3, 8);
                float baseF = 2600f + (float)rng.NextDouble() * 1800f;
                float pan = 0.6f + (float)rng.NextDouble() * 0.4f;   // uzaklık hissi
                for (int k = 0; k < notes; k++)
                {
                    float f0 = baseF * (0.85f + (float)rng.NextDouble() * 0.4f);
                    float f1 = f0 * (rng.NextDouble() < 0.5 ? 1.35f : 0.7f);
                    Chirp(s, sr, t, 0.06f + (float)rng.NextDouble() * 0.05f, f0, f1, 0.09f * pan);
                    t += 0.09f + (float)rng.NextDouble() * 0.07f;
                }
                t += 0.6f + (float)rng.NextDouble() * 2.2f;
            }
            // daha seyrek, alçak "tüü-ii" ıslıklar (başka bir kuş)
            t = 1.7f;
            while (t < LoopSeconds + Cross)
            {
                Chirp(s, sr, t, 0.35f, 1900f, 2300f, 0.05f);
                Chirp(s, sr, t + 0.42f, 0.28f, 2300f, 1700f, 0.045f);
                t += 4f + (float)rng.NextDouble() * 4f;
            }
        });

        public static float[] Sunset(int sr) => Loop(sr, 202, (s, n, rng) =>
        {
            Wind(s, sr, rng, 0.09f, 0.6f);   // daha belirgin esinti
            // yaprak hışırtısı: rüzgâr dalgasıyla kabaran yüksek frekanslı gürültü
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float gust = Mathf.Pow(Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 0.23f + 1f)), 2f);
                float w = (float)(rng.NextDouble() * 2 - 1);
                hp = 0.92f * (hp + w - prev); prev = w;
                s[i] += hp * 0.018f * gust;
            }
            // uzakta karatavuk: yavaş, melodik ıslıklar
            float tt = 1.2f;
            while (tt < LoopSeconds + Cross)
            {
                float f = 1500f + (float)rng.NextDouble() * 700f;
                Chirp(s, sr, tt, 0.22f, f, f * 1.2f, 0.04f);
                Chirp(s, sr, tt + 0.26f, 0.18f, f * 1.2f, f * 0.95f, 0.035f);
                Chirp(s, sr, tt + 0.5f, 0.3f, f * 0.9f, f * 1.1f, 0.03f);
                tt += 3.5f + (float)rng.NextDouble() * 3.5f;
            }
        });

        public static float[] Night(int sr) => Loop(sr, 303, (s, n, rng) =>
        {
            Wind(s, sr, rng, 0.025f, 0.15f);
            // cırcır böcekleri: farklı perde ve hızda birkaç böcek; her cırlama 3 hızlı darbe
            for (int c = 0; c < 5; c++)
            {
                float carrier = 4200f + c * 350f + (float)rng.NextDouble() * 200f;
                float rate = 1.1f + (float)rng.NextDouble() * 0.9f;    // saniyede cırlama
                float vol = 0.018f + (float)rng.NextDouble() * 0.014f;
                float phase = (float)rng.NextDouble();
                for (float t = phase; t < LoopSeconds + Cross; t += 1f / rate)
                {
                    if (rng.NextDouble() < 0.12) continue; // ara sıra susar
                    for (int p = 0; p < 3; p++) Pulse(s, sr, t + p * 0.045f, 0.03f, carrier, vol);
                }
            }
            // baykuş: "huu ... hu-huu"
            float tt = 3f;
            while (tt < LoopSeconds + Cross - 2f)
            {
                Hoot(s, sr, tt, 0.45f, 420f, 0.05f);
                Hoot(s, sr, tt + 0.9f, 0.18f, 400f, 0.04f);
                Hoot(s, sr, tt + 1.15f, 0.5f, 390f, 0.045f);
                tt += 7f + (float)rng.NextDouble() * 4f;
            }
        });

        // ---------- yardımcılar ----------
        delegate void Fill(float[] s, int n, System.Random rng);

        /// <summary>LoopSeconds + Cross uzunluğunda üretir, sonu başa bindirerek kesintisiz döngü yapar.</summary>
        static float[] Loop(int sr, int seed, Fill fill)
        {
            int n = (int)(sr * (LoopSeconds + Cross)), loop = (int)(sr * LoopSeconds), x = n - loop;
            var s = new float[n];
            fill(s, n, new System.Random(seed));
            var o = new float[loop];
            for (int i = 0; i < loop; i++) o[i] = s[i];
            for (int i = 0; i < x; i++)
            {
                float k = (float)i / x;
                o[i] = s[i] * k + s[loop + i] * (1f - k);
            }
            // yumuşak sınırlama
            for (int i = 0; i < loop; i++) o[i] = (float)System.Math.Tanh(o[i] * 2.6f * 1.5f) / 1.5f;
            return o;
        }

        /// <summary>Alçak geçiren kahverengi gürültü + yavaş dalgalanan şiddet.</summary>
        static void Wind(float[] s, int sr, System.Random rng, float level, float gustiness)
        {
            float b = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / sr;
                b = (b + 0.02f * (float)(rng.NextDouble() * 2 - 1)) / 1.02f;
                lp += 0.06f * (b * 6f - lp);
                float g = 1f - gustiness + gustiness * (0.5f + 0.5f * Mathf.Sin(t * 0.41f) * Mathf.Sin(t * 0.17f + 2f));
                s[i] += lp * level * g;
            }
        }

        /// <summary>Kuş cıvıltısı: perdesi kayan, hızlı açılıp kapanan sinüs (hafif FM ile canlı).</summary>
        static void Chirp(float[] s, int sr, float start, float dur, float f0, float f1, float vol)
        {
            int a = (int)(start * sr), len = (int)(dur * sr);
            float ph = 0f;
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float k = (float)i / len;
                float f = Mathf.Lerp(f0, f1, k * k * (3f - 2f * k)) * (1f + 0.02f * Mathf.Sin(i * 0.02f));
                ph += 2f * Mathf.PI * f / sr;
                float env = Mathf.Sin(k * Mathf.PI); env *= env;
                s[a + i] += Mathf.Sin(ph) * env * vol;
            }
        }

        /// <summary>Cırcır böceği darbesi.</summary>
        static void Pulse(float[] s, int sr, float start, float dur, float f, float vol)
        {
            int a = (int)(start * sr), len = (int)(dur * sr);
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float k = (float)i / len;
                float env = Mathf.Sin(k * Mathf.PI);
                float t = (float)i / sr;
                s[a + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)) * env * vol;
            }
        }

        /// <summary>Baykuş ötüşü: alçak, nefesli, hafif düşen ton.</summary>
        static void Hoot(float[] s, int sr, float start, float dur, float f, float vol)
        {
            int a = (int)(start * sr), len = (int)(dur * sr);
            float ph = 0f, br = 0f;
            var rng = new System.Random(a);
            for (int i = 0; i < len && a + i < s.Length; i++)
            {
                float k = (float)i / len;
                float fr = f * (1f - 0.06f * k);
                ph += 2f * Mathf.PI * fr / sr;
                float env = Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.35f);
                br += 0.04f * ((float)(rng.NextDouble() * 2 - 1) - br);   // boğuk nefes
                float breath = br * 0.6f;
                s[a + i] += (Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 2f) + breath) * env * vol;
            }
        }
    }
}
