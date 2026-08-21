using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace Polaris.AI;

internal static class AISettings
{
    static ConfigEntry<bool>? enabled;

    internal static bool Enabled => enabled?.Value ?? true;

    internal static void Resolve()
    {
        try
        {
            string directory = Path.Combine(Paths.ConfigPath, "Polaris", "AI");
            Directory.CreateDirectory(directory);
            var file = new ConfigFile(Path.Combine(directory, "PolarisAI.cfg"), true);
            enabled = file.Bind("Runtime", "Enabled", true,
                "Global PolarisAI kill switch. Turning it off immediately restores native AI decisions.");
        }
        catch (Exception ex)
        {
            enabled = null;
            Polaris.PolarisAPI.Errors.Report(ex, "Loading PolarisAI settings");
        }
    }
}
