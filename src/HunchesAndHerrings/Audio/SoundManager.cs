using System.IO;
using System.Windows.Media;

namespace HunchesAndHerrings.Audio;

/// <summary>
/// Plays the synthesised music loop and sound effects. Audio is generated once at startup into a
/// temporary folder and played with <see cref="MediaPlayer"/> so effects can overlap the music.
/// Audio is optional: any failure leaves the game silent rather than interrupting play.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private const int MaxConcurrentEffects = 6;

    private readonly Dictionary<Sfx, Uri> _effects = new();
    private readonly List<MediaPlayer> _active = new();
    private Uri? _musicUri;
    private MediaPlayer? _music;
    private string? _folder;
    private bool _musicEnabled = true;

    public bool IsReady { get; private set; }

    public bool SfxEnabled { get; set; } = true;

    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            _musicEnabled = value;
            if (value)
            {
                StartMusic();
            }
            else
            {
                _music?.Pause();
            }
        }
    }

    /// <summary>Synthesises and writes all audio on a background thread. Call from the UI thread.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            string folder = Path.Combine(Path.GetTempPath(), "HunchesAndHerrings", Environment.ProcessId.ToString());
            var files = await Task.Run(() =>
            {
                CleanOldFolders();
                Directory.CreateDirectory(folder);
                var written = new Dictionary<Sfx, string>();
                foreach (Sfx sfx in Enum.GetValues<Sfx>())
                {
                    string path = Path.Combine(folder, $"{sfx}.wav");
                    File.WriteAllBytes(path, AudioLibrary.Effect(sfx));
                    written[sfx] = path;
                }

                string musicPath = Path.Combine(folder, "music.wav");
                File.WriteAllBytes(musicPath, AudioLibrary.Music());
                return (Effects: written, Music: musicPath);
            });

            _folder = folder;
            foreach (var (sfx, path) in files.Effects)
            {
                _effects[sfx] = new Uri(path);
            }

            _musicUri = new Uri(files.Music);
            IsReady = true;
        }
        catch (IOException)
        {
            IsReady = false;
        }
        catch (UnauthorizedAccessException)
        {
            IsReady = false;
        }
    }

    public void StartMusic()
    {
        if (!IsReady || !_musicEnabled || _musicUri is null)
        {
            return;
        }

        try
        {
            if (_music is null)
            {
                _music = new MediaPlayer { Volume = 0.28 };
                _music.MediaEnded += (_, _) =>
                {
                    _music.Position = TimeSpan.Zero;
                    _music.Play();
                };
                _music.Open(_musicUri);
            }

            _music.Play();
        }
        catch (InvalidOperationException)
        {
            // Media components unavailable (e.g. Windows N without the Media Feature Pack).
        }
    }

    public void Play(Sfx sfx, double volume = 0.6)
    {
        if (!IsReady || !SfxEnabled || !_effects.TryGetValue(sfx, out Uri? uri))
        {
            return;
        }

        try
        {
            if (_active.Count >= MaxConcurrentEffects)
            {
                MediaPlayer oldest = _active[0];
                _active.RemoveAt(0);
                oldest.Close();
            }

            var player = new MediaPlayer { Volume = volume };
            player.MediaEnded += (_, _) =>
            {
                player.Close();
                _active.Remove(player);
            };
            player.Open(uri);
            player.Play();
            _active.Add(player);
        }
        catch (InvalidOperationException)
        {
            // Ignore: sound is a nicety.
        }
    }

    public void Dispose()
    {
        _music?.Close();
        foreach (MediaPlayer player in _active)
        {
            player.Close();
        }

        _active.Clear();
        try
        {
            if (_folder is not null && Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Files may still be locked by the media pipeline; the next run cleans them up.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Removes audio folders left behind by earlier runs.</summary>
    private static void CleanOldFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "HunchesAndHerrings");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string dir in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(dir) == Environment.ProcessId.ToString())
            {
                continue;
            }

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
