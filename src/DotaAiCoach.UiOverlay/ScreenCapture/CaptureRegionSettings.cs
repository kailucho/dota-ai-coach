using System.IO;
using System.Text.Json;

namespace DotaAiCoach.UiOverlay.ScreenCapture;

/// <summary>
/// Persists the player's configured scoreboard capture region across app
/// restarts, mirroring DotaAiCoach.Host.LocalPlayerSettings' pattern
/// (best-effort disk I/O — a corrupt/missing file degrades to "not
/// configured yet" rather than throwing).
/// </summary>
public sealed class CaptureRegionSettings
{
    public CaptureRegion? Region { get; init; }

    public static CaptureRegionSettings Load() => Load(overrideDirectory: null);

    internal static CaptureRegionSettings Load(string? overrideDirectory)
    {
        try
        {
            var path = SettingsPath(overrideDirectory);
            if (!File.Exists(path))
            {
                return new CaptureRegionSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CaptureRegionSettings>(json) ?? new CaptureRegionSettings();
        }
        catch
        {
            return new CaptureRegionSettings();
        }
    }

    public void Save() => Save(overrideDirectory: null);

    internal void Save(string? overrideDirectory)
    {
        try
        {
            var path = SettingsPath(overrideDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Best-effort — losing the saved region means re-picking it
            // next time, never a reason to crash the capture flow.
        }
    }

    private static string SettingsPath(string? overrideDirectory)
    {
        var directory = overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach");
        return Path.Combine(directory, "capture-region.json");
    }
}
