using DotaAiCoach.Gsi.Replay;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class FixtureSequenceLoaderTests
{
    [Fact]
    public void Load_orders_files_by_numeric_prefix()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", "laning-sequence");

        var sequence = FixtureSequenceLoader.Load(dir);

        sequence.Select(f => f.FileName).Should().ContainInOrder(
            "01-pregame.json", "02-game-start.json", "03-farm-tick.json",
            "04-before-level-up.json", "05-level-up.json", "06-item-purchased.json");
    }

    [Fact]
    public void Load_throws_a_clear_message_for_an_empty_directory()
    {
        var empty = Path.Combine(Path.GetTempPath(), "dota-ai-coach-empty-fixtures-" + Guid.NewGuid());
        Directory.CreateDirectory(empty);
        try
        {
            var act = () => FixtureSequenceLoader.Load(empty);

            act.Should().Throw<InvalidOperationException>().WithMessage("*no .json files*");
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public void Load_throws_for_a_missing_directory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "dota-ai-coach-missing-" + Guid.NewGuid());

        var act = () => FixtureSequenceLoader.Load(missing);

        act.Should().Throw<DirectoryNotFoundException>();
    }
}
