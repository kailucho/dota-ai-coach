using DotaAiCoach.Gsi.Parsing;
using FluentAssertions;

namespace DotaAiCoach.Gsi.Tests;

public class ItemSlotParsingTests
{
    private readonly GsiPayloadParser _parser = new();

    private static string ReadFixture(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gsi", relativePath));

    [Fact]
    public void Empty_slots_are_never_omitted_and_are_never_null()
    {
        var json = ReadFixture("single/empty-item-slots.json");

        var result = _parser.Parse(json);

        result.Success.Should().BeTrue();
        var slots = result.Payload!.Items!.Slots;

        slots.Should().ContainKey("slot0");
        slots["slot0"].Name.Should().Be("empty");
        slots["stash5"].Name.Should().Be("empty");
        slots["teleport0"].Name.Should().Be("empty");
        slots["neutral0"].Name.Should().Be("empty");
    }
}
