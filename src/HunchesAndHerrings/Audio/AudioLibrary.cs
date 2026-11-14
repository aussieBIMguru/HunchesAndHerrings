namespace HunchesAndHerrings.Audio;

/// <summary>Sound effects the game can play.</summary>
public enum Sfx
{
    Step,
    Page,
    Search,
    Quill,
    Chime,
    Hint,
    Report,
    Contradiction,
    Win,
    Lose,
}

/// <summary>Composes the music loop and every sound effect as WAV data.</summary>
internal static class AudioLibrary
{
    /// <summary>A slow, minor-key harpsichord loop in A minor (about 25 seconds).</summary>
    public static byte[] Music()
    {
        var rng = new Random(1888);
        const double beat = 60.0 / 76.0;
        const double eighth = beat / 2;

        // (arpeggio notes, bass note) per bar
        (int[] Arp, int Bass)[] bars =
        {
            (new[] { 57, 60, 64, 69 }, 45), // Am
            (new[] { 53, 57, 60, 65 }, 41), // F
            (new[] { 55, 60, 64, 67 }, 43), // C/G
            (new[] { 56, 59, 64, 68 }, 40), // E
            (new[] { 57, 60, 64, 69 }, 45), // Am
            (new[] { 57, 62, 65, 69 }, 38), // Dm
            (new[] { 56, 59, 64, 68 }, 40), // E
            (new[] { 57, 60, 64, 69 }, 45), // Am
        };
        int[] pattern = { 0, 2, 3, 1, 3, 2, 1, 2 };
        int[] melodyScale = { 69, 71, 72, 74, 76, 77, 79, 81 };

        double total = bars.Length * 4 * beat;
        var mix = new float[(int)(total * Synth.SampleRate) + Synth.SampleRate];

        for (int b = 0; b < bars.Length; b++)
        {
            double barStart = b * 4 * beat;
            var (arp, bass) = bars[b];

            Synth.Mix(mix, Synth.Pluck(Synth.MidiToHz(bass), 3.0, 0.998, rng), barStart, 0.55);
            Synth.Mix(mix, Synth.Pluck(Synth.MidiToHz(bass + 12), 1.5, 0.997, rng), barStart + 2 * beat, 0.3);

            for (int e = 0; e < 8; e++)
            {
                int note = arp[pattern[e]];
                Synth.Mix(mix, Synth.Pluck(Synth.MidiToHz(note), 1.2, 0.996, rng), barStart + e * eighth, 0.28);
            }

            // A sparse melody: a note or two per bar, drawn toward chord tones.
            if (rng.NextDouble() < 0.8)
            {
                int note = rng.NextDouble() < 0.6 ? arp[rng.Next(arp.Length)] + 12 : melodyScale[rng.Next(melodyScale.Length)];
                Synth.Mix(mix, Synth.Pluck(Synth.MidiToHz(note), 2.0, 0.997, rng), barStart + rng.Next(0, 3) * beat, 0.32);
            }
        }

        var loop = mix.Take((int)(total * Synth.SampleRate)).ToArray();
        Synth.Normalize(loop, 0.6);
        Synth.Fade(loop, 0.03);
        return Synth.ToWav(loop);
    }

    public static byte[] Effect(Sfx sfx)
    {
        var rng = new Random(500 + (int)sfx);
        float[] samples = sfx switch
        {
            Sfx.Step => Steps(rng),
            Sfx.Page => Synth.Noise(0.35, 0.35, t => Math.Sin(Math.PI * t) * Math.Sin(Math.PI * t), rng),
            Sfx.Search => Rustle(rng),
            Sfx.Quill => Quill(rng),
            Sfx.Chime => Synth.Bell(523.25, 2.5),
            Sfx.Hint => Synth.Bell(1046.5, 1.2, 0.5),
            Sfx.Report => Notes(rng, 0.9, (74, 0.0), (79, 0.14)),
            Sfx.Contradiction => Notes(rng, 0.9, (62, 0.0), (63, 0.05), (69, 0.1)),
            Sfx.Win => WithBell(Notes(rng, 2.5, (57, 0.0), (61, 0.15), (64, 0.3), (69, 0.45), (73, 0.6)), 880, 0.6),
            Sfx.Lose => WithBell(Notes(rng, 3.0, (45, 0.0), (46, 0.05), (52, 0.4), (51, 0.8)), 110, 0.3),
            _ => Synth.Silence(0.1),
        };

        Synth.Normalize(samples, 0.7);
        Synth.Fade(samples, 0.005);
        return Synth.ToWav(samples);
    }

    private static float[] Steps(Random rng)
    {
        var mix = Synth.Silence(0.75);
        for (int i = 0; i < 3; i++)
        {
            float[] thump = Synth.Noise(0.09, 0.93, t => Math.Exp(-t * 6), rng);
            Synth.Mix(mix, thump, i * 0.24, 1.0);
        }

        return mix;
    }

    private static float[] Rustle(Random rng)
    {
        var mix = Synth.Silence(1.2);
        for (int i = 0; i < 6; i++)
        {
            float[] swish = Synth.Noise(0.2 + rng.NextDouble() * 0.15, 0.3 + rng.NextDouble() * 0.3, t => Math.Sin(Math.PI * t), rng);
            Synth.Mix(mix, swish, rng.NextDouble() * 0.95, 0.6 + rng.NextDouble() * 0.4);
        }

        return mix;
    }

    private static float[] Quill(Random rng)
    {
        var mix = Synth.Silence(0.5);
        for (int i = 0; i < 4; i++)
        {
            float[] scratch = Synth.Noise(0.05 + rng.NextDouble() * 0.04, 0.05, t => Math.Sin(Math.PI * t), rng);
            Synth.Mix(mix, scratch, i * 0.1 + rng.NextDouble() * 0.03, 0.8);
        }

        return mix;
    }

    private static float[] Notes(Random rng, double seconds, params (int Note, double At)[] notes)
    {
        var mix = Synth.Silence(seconds);
        foreach (var (note, at) in notes)
        {
            Synth.Mix(mix, Synth.Pluck(Synth.MidiToHz(note), seconds - at, 0.997, rng), at, 0.5);
        }

        return mix;
    }

    private static float[] WithBell(float[] samples, double frequency, double gain)
    {
        Synth.Mix(samples, Synth.Bell(frequency, samples.Length / (double)Synth.SampleRate), 0, gain);
        return samples;
    }
}
