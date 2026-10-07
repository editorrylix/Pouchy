using System.IO;
using System.Runtime.InteropServices;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    public enum SoundEvent
    {
        /// <summary>The pouch opens.</summary>
        Open,
        /// <summary>Something was dropped or pasted in.</summary>
        Add,
        /// <summary>Items were dropped somewhere else.</summary>
        DragOut,
        /// <summary>Items were removed or cleared.</summary>
        Remove,
    }

    /// <summary>
    /// Short interface sounds. The built-in packs are synthesised at runtime (no audio files to ship);
    /// the Custom pack plays open.wav, add.wav, out.wav and remove.wav from the sounds folder.
    /// </summary>
    public sealed class SoundService : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly string _customFolder;
        // PlaySound reads asynchronously from this memory, so it lives until the cache is cleared.
        private readonly Dictionary<(SoundPack, SoundEvent, int), IntPtr> _cache = new();
        private readonly object _lock = new();

        public SoundService(SettingsService settings, string customFolder)
        {
            _settings = settings;
            _customFolder = customFolder;
        }

        public void Play(SoundEvent sound) => Play(_settings.Current.SoundPack, sound, _settings.Current.SoundVolume);

        public void Play(SoundPack pack, SoundEvent sound, double volume)
        {
            if (pack == SoundPack.Off) return;
            try
            {
                IntPtr wav = Get(pack, sound, volume);
                if (wav != IntPtr.Zero)
                {
                    NativeMethods.PlaySound(wav, IntPtr.Zero, NativeMethods.SND_ASYNC | NativeMethods.SND_MEMORY | NativeMethods.SND_NODEFAULT);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Could not play a sound: " + ex.Message);
            }
        }

        private IntPtr Get(SoundPack pack, SoundEvent sound, double volume)
        {
            int volumeKey = (int)Math.Round(Math.Clamp(volume, 0, 1) * 20);
            lock (_lock)
            {
                if (_cache.TryGetValue((pack, sound, volumeKey), out var cached)) return cached;

                byte[]? bytes = pack == SoundPack.Custom ? LoadCustom(sound) : SoundSynth.Create(pack, sound, volumeKey / 20.0);
                IntPtr memory = IntPtr.Zero;
                if (bytes != null)
                {
                    memory = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, memory, bytes.Length);
                }
                _cache[(pack, sound, volumeKey)] = memory;
                return memory;
            }
        }

        private byte[]? LoadCustom(SoundEvent sound)
        {
            string name = sound switch
            {
                SoundEvent.Open => "open",
                SoundEvent.Add => "add",
                SoundEvent.DragOut => "out",
                _ => "remove",
            };
            string path = Path.Combine(_customFolder, name + ".wav");
            if (!File.Exists(path)) return null;
            var info = new FileInfo(path);
            if (info.Length > 2_000_000) return null; // Interface sounds are short; don't hold big files in memory.
            return File.ReadAllBytes(path);
        }

        /// <summary>Forget loaded sounds, e.g. after the custom WAV files changed.</summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                NativeMethods.PlaySound(IntPtr.Zero, IntPtr.Zero, NativeMethods.SND_PURGE);
                foreach (var memory in _cache.Values.Where(m => m != IntPtr.Zero)) Marshal.FreeHGlobal(memory);
                _cache.Clear();
            }
        }

        public void Dispose() => ClearCache();
    }

    /// <summary>Builds the built-in sounds as 16-bit mono WAV files.</summary>
    public static class SoundSynth
    {
        private const int SampleRate = 44100;

        /// <summary>A tone sliding from one frequency to another, with a quick attack and exponential decay.</summary>
        private readonly record struct Note(double StartMs, double LengthMs, double FromHz, double ToHz, double Gain = 1, Wave Wave = Wave.Sine);

        private enum Wave
        {
            Sine,
            Triangle,
            Noise,
        }

        public static byte[] Create(SoundPack pack, SoundEvent sound, double volume)
        {
            var notes = pack switch
            {
                SoundPack.Bubbly => Bubbly(sound),
                SoundPack.Clicky => Clicky(sound),
                _ => Soft(sound),
            };
            return ToWav(Render(notes, volume));
        }

        private static Note[] Soft(SoundEvent sound) => sound switch
        {
            SoundEvent.Open => new[] { new Note(0, 70, 784, 784, 0.7), new Note(55, 110, 1175, 1175, 0.6) },
            SoundEvent.Add => new[] { new Note(0, 90, 880, 520, 0.9) },
            SoundEvent.DragOut => new[] { new Note(0, 120, 520, 900, 0.6) },
            _ => new[] { new Note(0, 80, 330, 220, 0.8) },
        };

        private static Note[] Bubbly(SoundEvent sound) => sound switch
        {
            SoundEvent.Open => new[] { new Note(0, 70, 380, 900, 0.8), new Note(70, 90, 520, 1300, 0.7) },
            SoundEvent.Add => new[] { new Note(0, 110, 300, 1100, 0.9) },
            SoundEvent.DragOut => new[] { new Note(0, 120, 1100, 380, 0.7) },
            _ => new[] { new Note(0, 90, 420, 160, 0.8) },
        };

        private static Note[] Clicky(SoundEvent sound) => sound switch
        {
            SoundEvent.Open => new[] { new Note(0, 12, 0, 0, 0.5, Wave.Noise), new Note(0, 45, 2100, 2100, 0.35, Wave.Triangle), new Note(50, 12, 0, 0, 0.4, Wave.Noise) },
            SoundEvent.Add => new[] { new Note(0, 10, 0, 0, 0.6, Wave.Noise), new Note(0, 40, 1600, 1500, 0.4, Wave.Triangle) },
            SoundEvent.DragOut => new[] { new Note(0, 10, 0, 0, 0.5, Wave.Noise), new Note(0, 35, 2400, 2300, 0.3, Wave.Triangle) },
            _ => new[] { new Note(0, 14, 0, 0, 0.5, Wave.Noise), new Note(0, 40, 700, 650, 0.35, Wave.Triangle) },
        };

        private static float[] Render(Note[] notes, double volume)
        {
            double totalMs = notes.Max(n => n.StartMs + n.LengthMs) + 10;
            var samples = new float[(int)(totalMs * SampleRate / 1000)];
            var random = new Random(7); // Same noise every time.

            foreach (var note in notes)
            {
                int start = (int)(note.StartMs * SampleRate / 1000);
                int length = (int)(note.LengthMs * SampleRate / 1000);
                double phase = 0;
                for (int i = 0; i < length && start + i < samples.Length; i++)
                {
                    double t = (double)i / length;
                    double frequency = note.FromHz * Math.Pow(note.ToHz / Math.Max(note.FromHz, 1), t);
                    phase += 2 * Math.PI * frequency / SampleRate;

                    double value = note.Wave switch
                    {
                        Wave.Triangle => 2 / Math.PI * Math.Asin(Math.Sin(phase)),
                        Wave.Noise => random.NextDouble() * 2 - 1,
                        _ => Math.Sin(phase),
                    };
                    // 4 ms attack, then an exponential fade to silence.
                    double attack = Math.Min(1, i / (0.004 * SampleRate));
                    double envelope = attack * Math.Exp(-5 * t) * (1 - t);
                    samples[start + i] += (float)(value * envelope * note.Gain);
                }
            }

            double peak = Math.Max(samples.Max(Math.Abs), 1e-6);
            double scale = 0.8 * Math.Clamp(volume, 0, 1) / Math.Max(peak, 1);
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)(samples[i] * scale);
            return samples;
        }

        internal static byte[] ToWav(float[] samples)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);              // fmt chunk size
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);  // bytes per second
            writer.Write((short)2);        // block align
            writer.Write((short)16);       // bits per sample
            writer.Write("data"u8);
            writer.Write(dataBytes);
            foreach (float sample in samples) writer.Write((short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
