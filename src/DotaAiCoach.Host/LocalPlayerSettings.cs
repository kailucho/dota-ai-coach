using System.Text.Json;

namespace DotaAiCoach.Host;

/// <summary>
/// Small local settings file so the user doesn't have to re-type their
/// Steam ID every session (per user request). Deliberately just the Steam
/// ID input string — no auth token, no session data, nothing sensitive
/// beyond what's already visible in the profile window's own text box.
/// Stored under %LocalAppData%\DotaAiCoach, one file, best-effort read/
/// write (a corrupt/missing file behaves exactly like "never saved
/// before", never crashes startup).
/// </summary>
public sealed class LocalPlayerSettings
{
    public string? SteamIdInput { get; init; }

    /// <summary>Overridable only for tests, so they never touch the real user's settings file. Production callers must always use the parameterless overloads.</summary>
    private static string ResolvePath(string? overrideDirectory = null)
    {
        var dir = overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotaAiCoach");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "player-settings.json");
    }

    public static LocalPlayerSettings Load() => Load(overrideDirectory: null);

    internal static LocalPlayerSettings Load(string? overrideDirectory)
    {
        try
        {
            var path = ResolvePath(overrideDirectory);
            if (!File.Exists(path))
            {
                return new LocalPlayerSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LocalPlayerSettings>(json) ?? new LocalPlayerSettings();
        }
        catch
        {
            // Corrupt/unreadable settings file must never block startup —
            // behaves as if no Steam ID had ever been saved.
            return new LocalPlayerSettings();
        }
    }

    public void Save() => Save(overrideDirectory: null);

    internal void Save(string? overrideDirectory)
    {
        try
        {
            var json = JsonSerializer.Serialize(this);
            File.WriteAllText(ResolvePath(overrideDirectory), json);
        }
        catch
        {
            // Best-effort: a failed save (e.g. permissions) should not
            // crash the profile window, just silently not persist.
        }
    }
}
