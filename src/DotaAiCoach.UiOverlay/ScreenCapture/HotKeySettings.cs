using System.IO;
using System.Text.Json;
using DotaAiCoach.UiOverlay.Interop;

namespace DotaAiCoach.UiOverlay.ScreenCapture;

/// <summary>
/// Persists the player's configured capture hotkey across app restarts,
/// mirroring CaptureRegionSettings' pattern. Stored as a raw Win32
/// virtual-key code + modifier flags (not a WPF Key) so the file has no
/// WPF-type coupling and deserializes stably across .NET/WPF versions.
///
/// Defaults to Ctrl+Tab, deliberately NOT bare Tab: Dota's own default
/// keybind for opening the scoreboard is bare Tab, and a globally
/// registered hotkey is intercepted by Windows before the foreground app
/// (Dota) ever sees the keystroke — registering bare Tab here would break
/// the game's own scoreboard shortcut while this app is running. Ctrl+Tab
/// keeps the player's existing Tab-opens-scoreboard muscle memory intact;
/// they just press Ctrl+Tab right after to also trigger the capture.
/// </summary>
public sealed class HotKeySettings
{
    private const int DefaultVirtualKeyCode = 0x09; // VK_TAB
    private const uint DefaultModifiers = (uint)HotKeyModifiers.Control;

    public int VirtualKeyCode { get; init; } = DefaultVirtualKeyCode;
    public uint Modifiers { get; init; } = DefaultModifiers;

    public static HotKeySettings Load() => Load(overrideDirectory: null);

    internal static HotKeySettings Load(string? overrideDirectory)
    {
        try
        {
            var path = SettingsPath(overrideDirectory);
            if (!File.Exists(path))
            {
                return new HotKeySettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HotKeySettings>(json) ?? new HotKeySettings();
        }
        catch
        {
            return new HotKeySettings();
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
            // Best-effort — losing the saved hotkey means it reverts to
            // the default (Ctrl+Tab) next launch, never a reason to crash.
        }
    }

    private static string SettingsPath(string? overrideDirectory)
    {
        var directory = overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach");
        return Path.Combine(directory, "hotkey.json");
    }
}
