using System;
using System.Collections.Generic;

namespace BlindAttack.Game
{
    /// <summary>
    /// Ana müzik ve portal sesi için "akustik" sentez.
    /// Unity'ye bağımlı değildir (sadece float dizisi üretir), bu yüzden dışarıda da test edilip WAV'a yazılabilir.
    ///
    /// Müzik: D Dorian, 6/8 jig ritminde, ortaçağ halk ezgisi tarzında özgün bir beste.
    ///  - Melodi: tahta flüt (nefes sesi + gecikmeli vibrato)
    ///  - Eşlik: ud/arp benzeri tel çekme (Karplus-Strong), bas + kırık akor
    ///  - Ritim: bendir (bodhrán) vuruşları
    ///  - Dem: çok kısık, uzun D-A sesi (gayda / hurdy-gurdy hissi)
    ///  - Notalar insan çalıyormuş gibi hafif zamanlama ve şiddet farkıyla çalınır, sonra küçük bir oda yankısı eklenir.
    /// </summary>
    public static class MusicSynth
    {
        const float TwoPi = (float)(Math.PI * 2.0);

        static float Midi(int m) => 440f * (float)Math.Pow(2.0, (m - 69) / 12.0);

        // ================= ANA MÜZİK =================

        // Melodi: (midi notası, sekizlik cinsinden uzunluk). 0 = es. Her ölçü 6 sekizlik.
        static readonly (int note, int len)[] SectionA =
        {
            (74,2),(69,1),(65,1),(67,1),(69,1),     // 1  Dm
            (71,2),(69,1),(67,2),(64,1),            // 2  G
            (65,1),(67,1),(69,1),(74,2),(72,1),     // 3  Dm
            (69,3),(67,1),(65,1),(64,1),            // 4  Am
            (62,2),(65,1),(69,2),(74,1),            // 5  Dm
            (72,2),(69,1),(67,1),(65,1),(67,1),     // 6  C
            (69,1),(72,1),(69,1),(67,2),(64,1),     // 7  Am
            (62,5),(0,1),                           // 8  Dm
        };

        static readonly (int note, int len)[] SectionB =
        {
            (69,1),(74,1),(76,1),(77,2),(76,1),     // 9  Dm
            (74,2),(72,1),(69,3),                   // 10 F
            (67,1),(72,1),(74,1),(76,2),(74,1),     // 11 C
            (72,2),(69,1),(67,3),                   // 12 C
            (65,1),(69,1),(74,1),(77,2),(79,1),     // 13 Dm
            (77,1),(76,1),(74,1),(72,2),(69,1),     // 14 F
            (71,1),(67,1),(64,1),(69,2),(73,1),     // 15 Em -> A
            (74,5),(0,1),                           // 16 Dm
        };

        // A bölümünün ikinci kez çalınışı: son iki ölçü farklı biter (süslemeli kapanış)
        static readonly (int note, int len)[] SectionA2Ending =
        {
            (69,1),(72,1),(74,1),(72,1),(69,1),(67,1),  // 7'
            (65,1),(64,1),(62,4),                       // 8'
        };

        // Ölçü başına akor (bas kökü ve akor sesleri, midi)
        static readonly int[][] Chords =
        {
            // A (1-8)
            new[]{50, 62,65,69}, new[]{43, 62,67,71}, new[]{50, 62,65,69}, new[]{45, 60,64,69},
            new[]{50, 62,65,69}, new[]{48, 60,64,67}, new[]{45, 60,64,69}, new[]{50, 62,65,69},
            // B (9-16)
            new[]{50, 62,65,69}, new[]{41, 60,65,69}, new[]{48, 60,64,67}, new[]{48, 60,64,67},
            new[]{50, 62,65,69}, new[]{41, 60,65,69}, new[]{45, 61,64,69}, new[]{50, 62,65,69},
            // A' (17-24)
            new[]{50, 62,65,69}, new[]{43, 62,67,71}, new[]{50, 62,65,69}, new[]{45, 60,64,69},
            new[]{50, 62,65,69}, new[]{48, 60,64,67}, new[]{45, 60,64,69}, new[]{50, 62,65,69},
        };

        public static float[] FolkLoop(int sr)
        {
            const float eighth = 0.27f;          // ~74 bpm noktalı dörtlük
            const int bars = 24;
            float barLen = eighth * 6f;
            int n = (int)(barLen * bars * sr);
            int tail = sr * 3;                   // yankı kuyruğu, döngü başına katlanır
            var mix = new float[n + tail];
            var rng = new Random(20261019);

            float Jitter(float ms) => (float)((rng.NextDouble() * 2.0 - 1.0) * ms / 1000.0);
            float Vel(float v, float spread) => v * (1f + (float)((rng.NextDouble() * 2.0 - 1.0) * spread));

            // ---- melodi (flüt). B bölümünde melodi telli çalgıya geçer, flüt ikinci sese çekilir ----
            var melody = new List<(int note, float start, float len, int section)>();
            void AddSection((int note, int len)[] sec, int startBar, int section)
            {
                float t = startBar * barLen;
                foreach (var (note, len) in sec)
                {
                    if (note > 0) melody.Add((note, t, len * eighth, section));
                    t += len * eighth;
                }
            }
            AddSection(SectionA, 0, 0);
            AddSection(SectionB, 8, 1);
            var a2 = new List<(int, int)>(SectionA);
            // A' için son iki ölçüyü değiştir
            int cut = 0, acc = 0;
            for (int i = 0; i < SectionA.Length; i++) { if (acc >= 36) { cut = i; break; } acc += SectionA[i].len; }
            a2.RemoveRange(cut, a2.Count - cut);
            a2.AddRange(SectionA2Ending);
            AddSection(a2.ToArray(), 16, 2);

            foreach (var m in melody)
            {
                float start = m.start + Jitter(10);
                if (m.section == 1)
                {
                    // B: ud melodisi + flüt bir oktav aşağıda, çok kısık, uzun notalarda
                    Pluck(mix, sr, Midi(m.note), start, Math.Min(1.6f, m.len + 0.8f), Vel(0.30f, 0.12f), 0.9965f, true);
                    if (m.len >= eighth * 2f) Flute(mix, sr, Midi(m.note - 12), start, m.len, Vel(0.10f, 0.1f), rng);
                }
                else
                {
                    Flute(mix, sr, Midi(m.note), start, m.len * 0.97f, Vel(m.section == 2 ? 0.21f : 0.19f, 0.10f), rng);
                }
            }

            // ---- eşlik (ud): jig kalıbı: 1 bas, 3 akor, 4 beşli, 6 akor ----
            for (int b = 0; b < bars; b++)
            {
                var ch = Chords[b];
                float t0 = b * barLen;
                bool soft = b < 2; // ilk iki ölçü yavaşça girsin
                float amp = soft ? 0.6f : 1f;
                Pluck(mix, sr, Midi(ch[0]), t0 + Jitter(6), 1.6f, Vel(0.34f, 0.08f) * amp, 0.9975f, false);
                Strum(mix, sr, ch, t0 + 2 * eighth + Jitter(8), Vel(0.13f, 0.12f) * amp, rng);
                Pluck(mix, sr, Midi(ch[0] + 7), t0 + 3 * eighth + Jitter(6), 1.2f, Vel(0.24f, 0.1f) * amp, 0.997f, false);
                Strum(mix, sr, ch, t0 + 5 * eighth + Jitter(8), Vel(0.10f, 0.12f) * amp, rng);
                // B bölümünde ara sekizliklere hafif tek tel
                if (b >= 8 && b < 16)
                    Pluck(mix, sr, Midi(ch[2] + 12), t0 + 4 * eighth + Jitter(6), 0.7f, Vel(0.07f, 0.2f), 0.995f, true);
            }

            // ---- bendir ----
            for (int b = 2; b < bars; b++)
            {
                float t0 = b * barLen;
                Bodhran(mix, sr, t0 + Jitter(5), Vel(0.42f, 0.08f), 1f, rng);
                Bodhran(mix, sr, t0 + 3 * eighth + Jitter(5), Vel(0.26f, 0.1f), 1.12f, rng);
                Bodhran(mix, sr, t0 + 5 * eighth + Jitter(6), Vel(0.12f, 0.2f), 1.35f, rng);
                if (b % 4 == 3) Bodhran(mix, sr, t0 + 4 * eighth + Jitter(6), Vel(0.10f, 0.2f), 1.3f, rng); // dolgu
            }

            // ---- dem (çok kısık, B'de biraz açılır) ----
            Drone(mix, sr, n, barLen, rng);

            // ---- oda yankısı ----
            Reverb(mix, sr, 0.20f, 0.80f);

            // kuyruğu başa katla: kesintisiz döngü
            var outBuf = new float[n];
            Array.Copy(mix, outBuf, n);
            for (int i = 0; i < tail; i++) outBuf[i] += mix[n + i];

            Normalize(outBuf, 0.85f);
            return outBuf;
        }

        // Tahta flüt: yumuşak atak, gecikmeli vibrato, nefes sesi
        static void Flute(float[] buf, int sr, float f, float start, float len, float vol, Random rng)
        {
            int s0 = (int)(start * sr);
            float rel = 0.09f;
            int count = (int)((len + rel) * sr);
            float phase = (float)rng.NextDouble() * TwoPi;
            float noiseLp = 0f, noiseBp = 0f;
            float vibRate = 4.6f + (float)rng.NextDouble() * 0.8f;
            for (int i = 0; i < count; i++)
            {
                int idx = s0 + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = (float)i / sr;
                float att = Math.Min(1f, t / 0.045f);
                att = att * att * (3f - 2f * att);
                float env = t < len ? att : att * Math.Max(0f, 1f - (t - len) / rel);
                // uzun notalarda hafif nefes kabarması
                env *= 0.92f + 0.08f * (float)Math.Sin(Math.Min(t, len) / Math.Max(0.05f, len) * Math.PI);
                float vibDepth = Math.Min(1f, Math.Max(0f, (t - 0.18f) / 0.25f)) * 0.0045f;
                float freq = f * (1f + vibDepth * (float)Math.Sin(TwoPi * vibRate * t));
                phase += TwoPi * freq / sr;
                float tone = (float)Math.Sin(phase) + 0.18f * (float)Math.Sin(2f * phase + 0.4f) + 0.07f * (float)Math.Sin(3f * phase + 1.1f);
                // nefes: bant geçiren gürültü, atakta daha belirgin
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // nefes: tonun etrafında dar bantlı yumuşak hışırtı (1-2.5 kHz civarı)
                noiseLp += 0.30f * (white - noiseLp);
                noiseBp += 0.20f * ((white - noiseLp * 0.6f) - noiseBp);
                float breath = noiseBp * (0.025f + 0.18f * Math.Max(0f, 1f - t / 0.05f));
                buf[idx] += (tone * 0.9f + breath) * env * vol;
            }
        }

        // Karplus-Strong tel çekme (ud / arp)
        static void Pluck(float[] buf, int sr, float f, float start, float dur, float vol, float decay, bool bright)
        {
            int s0 = (int)(start * sr);
            int period = Math.Max(2, (int)(sr / f));
            var line = new float[period];
            var rng = new Random((int)(f * 1000) ^ s0);
            // ilk gürültüyü yumuşat (parmakla çekme), parlak tellerde daha az
            float lp = 0f;
            for (int i = 0; i < period; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (bright ? 0.7f : 0.45f) * (w - lp);
                line[i] = lp;
            }
            int count = (int)(dur * sr);
            int p = 0;
            float prev = 0f;
            float body = 0f;
            for (int i = 0; i < count; i++)
            {
                int idx = s0 + i;
                float cur = line[p];
                int next = (p + 1) % period;
                line[p] = decay * 0.5f * (cur + line[next]);
                p = next;
                // basit gövde rezonansı (tahta kutu hissi)
                body += 0.08f * (cur - body);
                float outS = cur * 0.85f + body * 0.6f;
                float fade = i > count - sr / 20 ? (count - i) / (float)(sr / 20) : 1f;
                if (idx >= 0 && idx < buf.Length) buf[idx] += outS * vol * fade;
                prev = cur;
            }
        }

        // Akoru alttan üste hafif tarayarak çal
        static void Strum(float[] buf, int sr, int[] chord, float start, float vol, Random rng)
        {
            for (int k = 1; k < chord.Length; k++)
            {
                float offs = (k - 1) * (0.012f + (float)rng.NextDouble() * 0.006f);
                Pluck(buf, sr, Midi(chord[k]), start + offs, 0.9f, vol * (1f - (k - 1) * 0.12f), 0.996f, true);
            }
        }

        // Bendir: düşen perdeli gövde + deri tokadı
        static void Bodhran(float[] buf, int sr, float start, float vol, float pitchMul, Random rng)
        {
            int s0 = (int)(start * sr);
            int count = (int)(0.38f * sr);
            float phase = 0f, nlp = 0f;
            for (int i = 0; i < count; i++)
            {
                int idx = s0 + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = (float)i / sr;
                float f = (68f + 70f * (float)Math.Exp(-t * 28f)) * pitchMul;
                phase += TwoPi * f / sr;
                float bodyS = (float)Math.Sin(phase) * (float)Math.Exp(-t * 11f);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                nlp += 0.3f * (w - nlp);
                float slap = nlp * (float)Math.Exp(-t * 60f) * 0.55f;
                buf[idx] += (bodyS + slap) * vol;
            }
        }

        // Dem: D2 + A2, testere dişinin yumuşatılmışı, yavaş nefes alan genlik
        static void Drone(float[] buf, int sr, int n, float barLen, Random rng)
        {
            float p1 = 0f, p2 = 0f, lp = 0f;
            float f1 = Midi(38), f2 = Midi(45);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float bar = t / barLen;
                // A: çok kısık, B (8-16): biraz daha açık; döngü sınırında yumuşak geçiş
                float target = bar >= 8f && bar < 16f ? 1f : 0.55f;
                float edge = Math.Min(1f, Math.Min(Math.Abs(bar - 8f), Math.Abs(bar - 16f)) / 1.5f);
                float level = 0.55f + (target - 0.55f) * edge;
                p1 += f1 / sr; p2 += f2 / sr * 1.0012f;
                p1 -= (float)Math.Floor(p1); p2 -= (float)Math.Floor(p2);
                float saw = (2f * p1 - 1f) + 0.7f * (2f * p2 - 1f);
                lp += 0.035f * (saw - lp);
                float breathe = 0.85f + 0.15f * (float)Math.Sin(TwoPi * t / (barLen * 2f));
                buf[i] += lp * 0.030f * level * breathe;
            }
        }

        // ================= PORTAL =================

        /// <summary>Portala giriş: yükselen girdap, cam çan arpeji, sonunda derin bir "vuup".</summary>
        public static float[] PortalEnter(int sr)
        {
            float dur = 2.2f;
            int n = (int)(dur * sr);
            var s = new float[n + sr];
            var rng = new Random(7031);

            // 1) girdap: rezonanslı bant filtreden geçen gürültü, frekansı yükselir, titreşim hızlanır
            float low = 0f, band = 0f;
            for (int i = 0; i < (int)(1.5f * sr); i++)
            {
                float t = (float)i / sr;
                float k = t / 1.5f;
                float fc = 250f * (float)Math.Pow(14.0, k);              // 250 Hz -> 3.5 kHz
                float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, sr * 0.2f) / sr);
                float q = 0.12f;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += f * band;
                float high = w - low - q * band;
                band += f * high;
                float swirlRate = 6f + 22f * k * k;
                float trem = 0.65f + 0.35f * (float)Math.Sin(TwoPi * swirlRate * t);
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, k * 1.05f)) * (0.4f + 0.6f * k);
                s[i] += band * trem * env * 0.22f;
            }

            // 2) cam çanlar: D minör pentatonik yukarı (inharmonik kısmi seslerle)
            int[] notes = { 74, 77, 79, 81, 84, 86, 89 };
            for (int k = 0; k < notes.Length; k++)
            {
                float start = 0.25f + k * 0.11f + (float)(rng.NextDouble() * 0.015);
                Bell(s, sr, Midi(notes[k]), start, 1.2f, 0.16f + k * 0.012f);
            }
            // son parlak akor
            Bell(s, sr, Midi(86), 1.12f, 1.4f, 0.12f);
            Bell(s, sr, Midi(93), 1.14f, 1.4f, 0.09f);

            // 3) derin "vuup": geçiş anı
            float ph = 0f;
            int w0 = (int)(1.05f * sr);
            for (int i = 0; i < (int)(0.9f * sr); i++)
            {
                int idx = w0 + i;
                float t = (float)i / sr;
                float f = 55f + 120f * (float)Math.Exp(-t * 7f);
                ph += TwoPi * f / sr;
                float env = Math.Min(1f, t / 0.02f) * (float)Math.Exp(-t * 4.5f);
                s[idx] += (float)Math.Sin(ph) * env * 0.5f;
            }

            Reverb(s, sr, 0.32f, 0.84f);
            var outBuf = new float[n + sr / 2];
            Array.Copy(s, outBuf, outBuf.Length);
            // yumuşak bitiş
            int fade = sr / 3;
            for (int i = 0; i < fade; i++) outBuf[outBuf.Length - 1 - i] *= (float)i / fade;
            Normalize(outBuf, 0.9f);
            return outBuf;
        }

        static void Bell(float[] buf, int sr, float f, float start, float dur, float vol)
        {
            int s0 = (int)(start * sr);
            int count = (int)(dur * sr);
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
            float[] amps = { 1f, 0.45f, 0.22f, 0.1f };
            float[] decays = { 3.2f, 5.5f, 9f, 14f };
            for (int i = 0; i < count; i++)
            {
                int idx = s0 + i;
                if (idx < 0 || idx >= buf.Length) continue;
                float t = (float)i / sr;
                float att = Math.Min(1f, t / 0.004f);
                float v = 0f;
                for (int h = 0; h < ratios.Length; h++)
                    v += (float)Math.Sin(TwoPi * f * ratios[h] * t) * amps[h] * (float)Math.Exp(-t * decays[h]);
                buf[idx] += v * att * vol;
            }
        }

        // ================= Ortak =================

        /// <summary>Schroeder tipi küçük oda yankısı (4 comb + 2 allpass).</summary>
        static void Reverb(float[] buf, int sr, float wet, float feedback)
        {
            float scale = sr / 44100f;
            int[] combLen = { (int)(1116 * scale), (int)(1188 * scale), (int)(1277 * scale), (int)(1356 * scale) };
            int[] apLen = { (int)(556 * scale), (int)(441 * scale) };
            var combs = new float[4][];
            var combIdx = new int[4];
            var combLp = new float[4];
            for (int c = 0; c < 4; c++) combs[c] = new float[combLen[c]];
            var aps = new float[2][];
            var apIdx = new int[2];
            for (int a = 0; a < 2; a++) aps[a] = new float[apLen[a]];
            const float damp = 0.35f;

            for (int i = 0; i < buf.Length; i++)
            {
                float input = buf[i] * 0.25f;
                float acc = 0f;
                for (int c = 0; c < 4; c++)
                {
                    float y = combs[c][combIdx[c]];
                    combLp[c] = y * (1f - damp) + combLp[c] * damp;
                    combs[c][combIdx[c]] = input + combLp[c] * feedback;
                    combIdx[c] = (combIdx[c] + 1) % combLen[c];
                    acc += y;
                }
                for (int a = 0; a < 2; a++)
                {
                    float bufOut = aps[a][apIdx[a]];
                    float v = -acc + bufOut;
                    aps[a][apIdx[a]] = acc + bufOut * 0.5f;
                    apIdx[a] = (apIdx[a] + 1) % apLen[a];
                    acc = v;
                }
                buf[i] = buf[i] * (1f - wet * 0.5f) + acc * wet;
            }
        }

        static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            foreach (var v in buf) max = Math.Max(max, Math.Abs(v));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }
    }
}
