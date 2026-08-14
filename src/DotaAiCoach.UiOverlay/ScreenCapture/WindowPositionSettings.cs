using System.IO;
using System.Text.Json;

namespace DotaAiCoach.UiOverlay.ScreenCapture;

/// <summary>
/// Persists the overlay window's last dragged position across app
/// restarts, mirroring HotKeySettings/CaptureRegionSettings' pattern.
/// Null Left/Top means "no saved position yet" — MainWindow falls back
/// to a right-side default in that case rather than the old fixed
/// top-left corner.
/// </summary>
public sealed class WindowPositionSettings
{
    public double? Left { get; init; }
    public double? Top { get; init; }

    public static WindowPositionSettings Load() => Load(overrideDirectory: null);

    internal static WindowPositionSettings Load(string? overrideDirectory)
    {
        try
        {
            var path = SettingsPath(overrideDirectory);
            if (!File.Exists(path))
            {
                return new WindowPositionSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WindowPositionSettings>(json) ?? new WindowPositionSettings();
        }
        catch
        {
            return new WindowPositionSettings();
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
            // Best-effort — losing the saved position means it reverts to
            // the default right-side placement next launch, never a
            // reason to crash.
        }
    }

    private static string SettingsPath(string? overrideDirectory)
    {
        var directory = overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach");
        return Path.Combine(directory, "window-position.json");
    }
}
