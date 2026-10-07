// Usage: dotnet run --project trailer/tools/AppSounds -- <output folder>
// Writes <pack>-<event>.wav for every built-in sound pack, exactly as Pouchy plays them.
using System.IO;
using Pouchy.Models;
using Pouchy.Services;

string outDir = args.Length > 0 ? args[0] : "sounds";
Directory.CreateDirectory(outDir);
foreach (var pack in new[] { SoundPack.Soft, SoundPack.Bubbly, SoundPack.Clicky })
{
    foreach (var sound in Enum.GetValues<SoundEvent>())
    {
        string path = Path.Combine(outDir, $"{pack}-{sound}.wav".ToLowerInvariant());
        File.WriteAllBytes(path, SoundSynth.Create(pack, sound, 1.0));
        Console.WriteLine(path);
    }
}
