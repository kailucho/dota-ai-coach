using FluentAssertions;

namespace DotaAiCoach.Host.Tests;

public class LocalPlayerSettingsTests
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "DotaAiCoach.Host.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_returns_a_settings_with_null_SteamIdInput_when_no_file_exists_yet()
    {
        var settings = LocalPlayerSettings.Load(_tempDir);

        settings.SteamIdInput.Should().BeNull();
    }

    [Fact]
    public void Save_then_Load_round_trips_the_SteamIdInput()
    {
        new LocalPlayerSettings { SteamIdInput = "76561197960287930" }.Save(_tempDir);

        var loaded = LocalPlayerSettings.Load(_tempDir);

        loaded.SteamIdInput.Should().Be("76561197960287930");
    }

    [Fact]
    public void Load_degrades_to_empty_settings_rather_than_throwing_on_a_corrupt_file()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "player-settings.json"), "{ not valid json");

        var act = () => LocalPlayerSettings.Load(_tempDir);

        act.Should().NotThrow();
        act().SteamIdInput.Should().BeNull();
    }
}
