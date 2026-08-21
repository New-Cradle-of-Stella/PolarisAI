using System;
using System.Collections.Generic;
using System.IO;

namespace Polaris.AI;

internal sealed class PaiHotReload
{
    readonly string directory;
    readonly Dictionary<string, DateTime> stamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    double elapsed;

    internal PaiHotReload(string directory) { this.directory = directory; }

    internal void Initialize()
    {
        Directory.CreateDirectory(directory);
        Scan(true);
    }

    internal void Tick(float deltaTime)
    {
        elapsed += Math.Max(0, deltaTime);
        if (elapsed < 0.5) return;
        elapsed = 0;
        Scan(false);
    }

    void Scan(bool initial)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "*.pai", SearchOption.AllDirectories))
        {
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (stamps.TryGetValue(path, out DateTime old) && old == stamp) continue;
            stamps[path] = stamp;
            if (initial) BehaviorRepository.LoadFile(path);
            else BehaviorRepository.LoadFile(path); // failed reload keeps the previous compiled behavior
        }
    }
}
