using System.IO;

namespace HunchesAndHerrings.Audio;

/// <summary>Tiny offline synthesiser: plucked strings, bells and filtered noise, rendered to float buffers.</summary>
internal static class Synth
{
    public const int SampleRate = 22050;

    public static double MidiToHz(int note) => 440.0 * Math.Pow(2, (note - 69) / 12.0);

    /// <summary>Karplus–Strong plucked string: a harpsichord-ish tone.</summary>
    public static float[] Pluck(double frequency, double seconds, double decay, Random rng)
    {
        int n = (int)(seconds * SampleRate);
        var output = new float[n];
        int period = Math.Max(2, (int)(SampleRate / frequency));
        var buffer = new float[period];
        for (int i = 0; i < period; i++)
        {
            buffer[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        int index = 0;
        for (int i = 0; i < n; i++)
        {
            float current = buffer[index];
            int next = (index + 1) % period;
            buffer[index] = (float)(decay * 0.5 * (current + buffer[next]));
            output[i] = current;
            index = next;
        }

        return output;
    }

    /// <summary>An inharmonic struck bell.</summary>
    public static float[] Bell(double frequency, double seconds, double gain = 1.0)
    {
        int n = (int)(seconds * SampleRate);
        var output = new float[n];
        double[] partials = { 1.0, 2.0, 2.76, 5.4 };
        double[] amps = { 1.0, 0.5, 0.35, 0.15 };
        double[] decays = { 2.2, 3.0, 4.5, 7.0 };
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SampleRate;
            double v = 0;
            for (int p = 0; p < partials.Length; p++)
            {
                v += amps[p] * Math.Exp(-decays[p] * t / seconds * 3) * Math.Sin(2 * Math.PI * frequency * partials[p] * t);
            }

            output[i] = (float)(v * gain * 0.4);
        }

        return output;
    }

    /// <summary>
    /// Shaped noise. <paramref name="smoothing"/> near 1 gives low rumbles (footsteps), near 0 gives hiss (paper).
    /// </summary>
    public static float[] Noise(double seconds, double smoothing, Func<double, double> envelope, Random rng)
    {
        int n = (int)(seconds * SampleRate);
        var output = new float[n];
        double y = 0;
        for (int i = 0; i < n; i++)
        {
            double x = rng.NextDouble() * 2 - 1;
            y = smoothing * y + (1 - smoothing) * x;
            output[i] = (float)(y * envelope(i / (double)n));
        }

        return output;
    }

    public static float[] Silence(double seconds) => new float[(int)(seconds * SampleRate)];

    /// <summary>Adds <paramref name="source"/> into <paramref name="target"/> at a time offset.</summary>
    public static void Mix(float[] target, float[] source, double atSeconds, double gain)
    {
        int offset = (int)(atSeconds * SampleRate);
        for (int i = 0; i < source.Length && offset + i < target.Length; i++)
        {
            if (offset + i >= 0)
            {
                target[offset + i] += (float)(source[i] * gain);
            }
        }
    }

    public static void Normalize(float[] samples, double peak)
    {
        float max = 0;
        foreach (float s in samples)
        {
            max = Math.Max(max, Math.Abs(s));
        }

        if (max <= 0)
        {
            return;
        }

        float scale = (float)(peak / max);
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    /// <summary>Short linear fades at both ends to avoid clicks.</summary>
    public static void Fade(float[] samples, double seconds)
    {
        int n = Math.Min(samples.Length / 2, (int)(seconds * SampleRate));
        for (int i = 0; i < n; i++)
        {
            float g = i / (float)n;
            samples[i] *= g;
            samples[samples.Length - 1 - i] *= g;
        }
    }

    /// <summary>Encodes mono samples as a 16-bit PCM WAV file.</summary>
    public static byte[] ToWav(float[] samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        int dataBytes = samples.Length * 2;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        foreach (float s in samples)
        {
            writer.Write((short)Math.Clamp((int)(s * short.MaxValue), short.MinValue, short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
