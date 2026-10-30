using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Синтезирует звуки и музыку в WAV-файлы (Assets/Audio), чтобы не тянуть сторонние ассеты.
// Запуск: Tools → Generate Sounds. Любой файл можно заменить своим с тем же именем
public static class SoundGenerator
{
    const int Rate = 44100;
    const string Dir = "Assets/Audio";

    // Tools → Generate Sounds: синтезировать и сохранить все звуки и музыку
    [MenuItem("Tools/Generate Sounds")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);
        Save("MusicMenu", MenuMusic(), music: true);
        Save("MusicGame", GameMusic(), music: true);
        Save("HitRobot", HitRobot(), music: false);
        Save("HitBumper", HitBumper(), music: false);
        Save("RobotShatter", Shatter(), music: false);
        Save("PlayerDeath", PlayerDeath(), music: false);
        AssetDatabase.Refresh();
        Debug.Log("SoundGenerator: звуки созданы");
    }

    // ---------- Музыка: спокойная гармония Cmaj7 → Am7 → Fmaj7 → G, по 2 с на аккорд, петля 32 с ----------

    static readonly int[] Bass = { 36, 33, 29, 31 };
    static readonly int[][] Chords =
    {
        new[] { 60, 64, 67, 71 },
        new[] { 57, 60, 64, 69 },
        new[] { 57, 60, 65, 69 },
        new[] { 59, 62, 67, 71 },
    };
    const float Beat = 0.5f;          // 120 BPM
    const float ChordLen = 4 * Beat;  // такт на аккорд
    const int Loops = 4;              // 4 круга по 4 аккорда = 32 с

    // Музыка меню: пэд-аккорды, колокольчики четвертями и мягкий бас
    static float[] MenuMusic()
    {
        var buf = new float[(int)(Loops * 4 * ChordLen * Rate)];
        var rnd = new System.Random(7);
        int[] pattern = { 0, 2, 1, 3, 2, 1, 3, 0 };
        for (int loop = 0; loop < Loops; loop++)
        for (int c = 0; c < 4; c++)
        {
            float t0 = (loop * 4 + c) * ChordLen;
            foreach (int n in Chords[c]) Pad(buf, t0, ChordLen, Midi(n), 0.05f);
            // Колокольчики четвертями, на 2-м и 4-м круге на октаву выше
            int octave = loop % 2 == 1 ? 24 : 12;
            for (int i = 0; i < 4; i++)
            {
                int n = Chords[c][pattern[(loop * 4 + i) % pattern.Length]] + octave;
                Bell(buf, t0 + i * Beat, Midi(n), 0.07f + (float)rnd.NextDouble() * 0.02f);
            }
            Pluck(buf, t0, Midi(Bass[c] + 12), 0.12f, 2.5f);
        }
        return Normalize(buf, 0.7f);
    }

    // Игровая музыка: те же аккорды тише, арпеджио восьмыми, бочка на каждую долю, хэт и бас
    static float[] GameMusic()
    {
        var buf = new float[(int)(Loops * 4 * ChordLen * Rate)];
        var rnd = new System.Random(11);
        int[] arp = { 0, 1, 2, 3, 2, 1, 2, 3 };
        for (int loop = 0; loop < Loops; loop++)
        for (int c = 0; c < 4; c++)
        {
            float t0 = (loop * 4 + c) * ChordLen;
            foreach (int n in Chords[c]) Pad(buf, t0, ChordLen, Midi(n), 0.035f);
            for (int i = 0; i < 8; i++) // восьмые
            {
                float t = t0 + i * Beat * 0.5f;
                int n = Chords[c][arp[i]] + (loop >= 2 && i % 4 == 3 ? 24 : 12);
                Bell(buf, t, Midi(n), 0.045f);
                if (i % 2 == 1) Hat(buf, t, 0.03f, rnd);
            }
            for (int b = 0; b < 4; b++)
            {
                float t = t0 + b * Beat;
                Kick(buf, t, 0.3f);
                Pluck(buf, t, Midi(Bass[c] + (b == 2 ? 19 : 12)), 0.16f, 6f);
            }
        }
        return Normalize(buf, 0.7f);
    }

    // Мягкий пэд: две слегка расстроенные синусоиды с плавной атакой и затуханием
    static void Pad(float[] buf, float start, float len, float f, float amp)
    {
        int s0 = (int)(start * Rate), n = (int)((len + 0.8f) * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.5f) * (t < len ? 1f : Mathf.Clamp01(1f - (t - len) / 0.8f));
            float v = Mathf.Sin(2 * Mathf.PI * f * 0.999f * t) + Mathf.Sin(2 * Mathf.PI * f * 1.001f * t)
                    + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2f * t);
            Add(buf, s0 + i, v * env * amp);
        }
    }

    // Колокольчик: синус с быстрым затуханием и обертоном на октаву выше
    static void Bell(float[] buf, float start, float f, float amp)
    {
        int s0 = (int)(start * Rate), n = (int)(1.4f * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 3.5f)
                    + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 2f * t) * Mathf.Exp(-t * 7f);
            Add(buf, s0 + i, v * amp * Mathf.Clamp01(t / 0.004f));
        }
    }

    // Щипок (бас): синус с третьей гармоникой и экспоненциальным затуханием decay
    static void Pluck(float[] buf, float start, float f, float amp, float decay)
    {
        int s0 = (int)(start * Rate), n = (int)(1.2f * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Sin(2 * Mathf.PI * f * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * f * 3f * t);
            Add(buf, s0 + i, v * amp * Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.005f));
        }
    }

    // Бочка: синус, высота которого быстро падает со 120 до 45 Гц
    static void Kick(float[] buf, float start, float amp)
    {
        int s0 = (int)(start * Rate), n = (int)(0.3f * Rate);
        float phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            phase += 2 * Mathf.PI * Mathf.Lerp(45f, 120f, Mathf.Exp(-t * 30f)) / Rate;
            Add(buf, s0 + i, Mathf.Sin(phase) * amp * Mathf.Exp(-t * 14f));
        }
    }

    // Хэт: короткий щелчок высокочастотного шума
    static void Hat(float[] buf, float start, float amp, System.Random rnd)
    {
        int s0 = (int)(start * Rate), n = (int)(0.08f * Rate);
        float prev = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rnd.NextDouble() * 2 - 1;
            Add(buf, s0 + i, (noise - prev) * 0.5f * amp * Mathf.Exp(-t * 45f)); // разность — грубый ФВЧ
            prev = noise;
        }
    }

    // ---------- Эффекты ----------

    // Глухой металлический удар: низкий «бум» + звонкие обертоны корпуса + щелчок шума
    static float[] HitRobot()
    {
        var buf = new float[(int)(0.45f * Rate)];
        var rnd = new System.Random(3);
        float phase = 0, lp = 0;
        float[] partials = { 410f, 987f, 1530f, 2210f };
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            phase += 2 * Mathf.PI * Mathf.Lerp(55f, 150f, Mathf.Exp(-t * 25f)) / Rate;
            float v = Mathf.Sin(phase) * 0.8f * Mathf.Exp(-t * 12f);
            for (int p = 0; p < partials.Length; p++)
                v += Mathf.Sin(2 * Mathf.PI * partials[p] * t) * 0.12f * Mathf.Exp(-t * (14f + p * 6f));
            lp += (((float)rnd.NextDouble() * 2 - 1) - lp) * 0.3f;
            v += lp * 0.6f * Mathf.Exp(-t * 60f);
            buf[i] = v;
        }
        return Normalize(buf, 0.9f);
    }

    // Пружинящий «боинг»: тон с падающим и дрожащим питчем
    static float[] HitBumper()
    {
        var buf = new float[(int)(0.6f * Rate)];
        var rnd = new System.Random(5);
        float phase = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 180f * (1f + 0.7f * Mathf.Exp(-t * 14f)) * (1f + 0.05f * Mathf.Sin(2 * Mathf.PI * 11f * t) * Mathf.Exp(-t * 3f));
            phase += 2 * Mathf.PI * f / Rate;
            float v = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * Mathf.Exp(-t * 6f);
            v += ((float)rnd.NextDouble() * 2 - 1) * 0.4f * Mathf.Exp(-t * 120f);
            buf[i] = v;
        }
        return Normalize(buf, 0.9f);
    }

    // Робот разбивается: удар, треск шума и россыпь коротких звонких «осколков»
    static float[] Shatter()
    {
        var buf = new float[(int)(1.1f * Rate)];
        var rnd = new System.Random(9);
        float prev = 0, phase = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rnd.NextDouble() * 2 - 1;
            float crackle = (noise - prev) * Mathf.Exp(-t * 5f) * (0.6f + 0.4f * Mathf.Sin(t * 90f));
            prev = noise;
            phase += 2 * Mathf.PI * Mathf.Lerp(40f, 110f, Mathf.Exp(-t * 20f)) / Rate;
            buf[i] = crackle * 0.35f + Mathf.Sin(phase) * 0.7f * Mathf.Exp(-t * 9f);
        }
        for (int s = 0; s < 30; s++)
        {
            float start = (float)Math.Pow(rnd.NextDouble(), 2) * 0.45f;
            float f = 1400f + (float)rnd.NextDouble() * 4600f;
            float decay = 25f + (float)rnd.NextDouble() * 30f;
            float amp = 0.08f + (float)rnd.NextDouble() * 0.1f;
            int s0 = (int)(start * Rate), n = (int)(0.25f * Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                Add(buf, s0 + i, Mathf.Sin(2 * Mathf.PI * f * t) * amp * Mathf.Exp(-t * decay), wrap: false);
            }
        }
        return Normalize(buf, 0.9f);
    }

    // Игрок сорвался в пропасть: свист падения вниз, затем глухой удар где-то внизу
    static float[] PlayerDeath()
    {
        var buf = new float[(int)(1.3f * Rate)];
        var rnd = new System.Random(13);
        float phase = 0, lp = 0, boomPhase = 0;
        const float fallLen = 0.85f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            if (t < fallLen)
            {
                // Тон уходит вниз на две октавы с лёгким вибрато
                float f = 640f * Mathf.Pow(0.25f, t / fallLen) * (1f + 0.03f * Mathf.Sin(2 * Mathf.PI * 7f * t));
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Clamp01(t / 0.02f) * (1f - t / fallLen);
                v += (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f) + 0.12f * Mathf.Sin(phase * 3f)) * 0.5f * env;
                // Свист воздуха: шум, который становится всё глуше
                lp += (((float)rnd.NextDouble() * 2 - 1) - lp) * Mathf.Lerp(0.25f, 0.03f, t / fallLen);
                v += lp * 0.35f * env;
            }
            else
            {
                float tb = t - fallLen;
                boomPhase += 2 * Mathf.PI * Mathf.Lerp(40f, 90f, Mathf.Exp(-tb * 25f)) / Rate;
                v += Mathf.Sin(boomPhase) * 0.6f * Mathf.Exp(-tb * 9f) * Mathf.Clamp01(tb / 0.005f);
            }
            buf[i] = v;
        }
        return Normalize(buf, 0.85f);
    }

    // ---------- Утилиты ----------

    // Номер ноты MIDI → частота в герцах (69 = ля первой октавы, 440 Гц)
    static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

    // По умолчанию хвосты нот заворачиваются в начало буфера — музыкальная петля склеивается без щелчка
    static void Add(float[] buf, int index, float v, bool wrap = true)
    {
        if (wrap) buf[index % buf.Length] += v;
        else if (index < buf.Length) buf[index] += v;
    }

    // Подогнать громкость так, чтобы самый громкий сэмпл был равен peak
    static float[] Normalize(float[] buf, float peak)
    {
        float max = 0;
        foreach (float v in buf) max = Mathf.Max(max, Mathf.Abs(v));
        if (max > 0) for (int i = 0; i < buf.Length; i++) buf[i] *= peak / max;
        return buf;
    }

    // Записать 16-битный моно WAV в Assets/Audio и выставить импорт: музыка — сжатие Vorbis, эффекты — без сжатия
    static void Save(string name, float[] samples, bool music)
    {
        string path = $"{Dir}/{name}.wav";
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataSize = samples.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + dataSize); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(dataSize);
            foreach (float v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
        }

        AssetDatabase.ImportAsset(path);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        // Музыку сжимаем (длинная), короткие эффекты держим несжатыми для мгновенного старта
        settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }
}
