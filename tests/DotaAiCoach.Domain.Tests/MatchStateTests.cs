using DotaAiCoach.Domain.Match;
using FluentAssertions;

namespace DotaAiCoach.Domain.Tests;

public class MatchStateTests
{
    [Fact]
    public void Clone_produces_isolated_snapshot_not_affected_by_later_mutation()
    {
        var state = new MatchState { Player = PlayerSnapshot.Empty with { Gold = 600 } };

        var clone = state.Clone();
        state.Player = PlayerSnapshot.Empty with { Gold = 900 };

        clone.Player.Gold.Should().Be(600);
        state.Player.Gold.Should().Be(900);
    }

    [Fact]
    public void ItemsSnapshot_OwnedItemNames_excludes_empty_slots()
    {
        var items = ItemsSnapshot.Empty with
        {
            Inventory = new[]
            {
                new ItemSlot { Name = "item_tango" },
                ItemSlot.Empty,
                new ItemSlot { Name = "item_boots" },
                ItemSlot.Empty,
                ItemSlot.Empty,
                ItemSlot.Empty
            }
        };

        items.OwnedItemNames().Should().BeEquivalentTo(new[] { "item_tango", "item_boots" });
    }

    [Fact]
    public void ItemsSnapshot_Owns_is_case_insensitive_and_false_for_empty()
    {
        var items = ItemsSnapshot.Empty with
        {
            Inventory = new[]
            {
                new ItemSlot { Name = "item_black_king_bar" },
                ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty, ItemSlot.Empty
            }
        };

        items.Owns("item_black_king_bar").Should().BeTrue();
        items.Owns("ITEM_BLACK_KING_BAR").Should().BeTrue();
        items.Owns("empty").Should().BeFalse();
        items.Owns("item_blink").Should().BeFalse();
    }

    [Fact]
    public void HeroSnapshot_HealthFraction_is_null_when_max_health_missing_or_zero()
    {
        var missingMax = HeroSnapshot.Empty with { Health = 100 };
        missingMax.HealthFraction.Should().BeNull();

        var zeroMax = HeroSnapshot.Empty with { Health = 100, MaxHealth = 0 };
        zeroMax.HealthFraction.Should().BeNull();

        var valid = HeroSnapshot.Empty with { Health = 50, MaxHealth = 200 };
        valid.HealthFraction.Should().Be(0.25);
    }

    [Fact]
    public void ItemsSnapshot_default_slot_counts_match_a_live_captured_payload()
    {
        // Regression test: an earlier version of this model assumed 6
        // inventory slots. A real Dota 2 client capture (see
        // docs/RESEARCH.md) showed slot0 through slot8 (9 slots) actually
        // present, plus neutral0/neutral1 (2, not 1) and a separate
        // preserved_neutral6-10 group — the 6-slot assumption was
        // silently truncating real inventory data for any item in
        // slots 6-8.
        var items = ItemsSnapshot.Empty;

        items.Inventory.Should().HaveCount(9);
        items.Stash.Should().HaveCount(6);
        items.Neutral.Should().HaveCount(2);
        items.PreservedNeutral.Should().HaveCount(5);
    }

    [Fact]
    public void ItemsSnapshot_OwnedItemNames_includes_neutral_slots()
    {
        var items = ItemsSnapshot.Empty with
        {
            Neutral = new[]
            {
                new ItemSlot { Name = "item_ogre_seal_totem" },
                ItemSlot.Empty
            }
        };

        items.OwnedItemNames().Should().Contain("item_ogre_seal_totem");
        items.Owns("item_ogre_seal_totem").Should().BeTrue();
    }
}
