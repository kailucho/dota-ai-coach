using DotaAiCoach.Domain.Events;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Gsi.Wire;

namespace DotaAiCoach.Gsi.Normalization;

public sealed record NormalizationResult(
    MatchState State,
    IReadOnlyList<DomainEvent> Events,
    FeedHealth Health);

public interface IGsiNormalizer
{
    NormalizationResult Merge(MatchState state, GsiPayload payload, DateTimeOffset receivedAt);
}

/// <summary>
/// Merges a partial GSI payload into a persistent <see cref="MatchState"/>.
/// GSI payloads only ever contain the sections that changed (per
/// dota-gsi's state-diffing guidance), so merging is field-wise coalesce —
/// a present section with an absent field must retain the prior field
/// value, never overwrite it with a default. Events are derived by
/// comparing the pre-merge snapshot against the post-merge snapshot (our
/// own accumulated state is authoritative — not the payload's own
/// "previously" block, which reflects only what the client considered the
/// prior value and isn't reliably delivered).
/// </summary>
public sealed class GsiNormalizer : IGsiNormalizer
{
    private readonly GsiOptions _options;

    public GsiNormalizer(GsiOptions options)
    {
        _options = options;
    }

    public NormalizationResult Merge(MatchState state, GsiPayload payload, DateTimeOffset receivedAt)
    {
        var events = new List<DomainEvent>();
        int? clockTimeForEvents = payload.Map?.ClockTime ?? state.Match.ClockTime;

        DetectDiscontinuity(state, receivedAt, clockTimeForEvents, events);

        var prevHero = state.Hero;
        var prevMatch = state.Match;
        var prevItems = state.Items;

        state.Match = MergeMatch(state.Match, payload.Map);
        state.Player = MergePlayer(state.Player, payload.Player);
        state.Hero = MergeHero(state.Hero, payload.Hero);
        state.Items = MergeItems(state.Items, payload.Items);
        state.Abilities = MergeAbilities(state.Abilities, payload.Abilities);

        DeriveEvents(prevMatch, state.Match, prevHero, state.Hero, clockTimeForEvents, receivedAt, events);
        DeriveItemPurchaseEvents(payload.Items, key => ResolvePriorSlot(prevItems, key), clockTimeForEvents, receivedAt, events);

        state.LastUpdatedAt = receivedAt;
        state.UpdateCount++;

        var health = new FeedHealth
        {
            Status = FeedStatus.Live,
            LastPayloadReceivedAt = receivedAt,
            SecondsSinceLastUpdate = 0
        };

        return new NormalizationResult(state, events, health);
    }

    /// <summary>
    /// Evaluated on a timer by the host, independent of payload arrival, so
    /// staleness is detected even when the defining symptom is that
    /// nothing is arriving at all — per dota-gsi's stale-state guidance.
    /// </summary>
    public FeedHealth EvaluateHealth(MatchState state, DateTimeOffset now)
    {
        if (state.LastUpdatedAt is not { } lastUpdate)
        {
            return FeedHealth.NeverConnectedDefault;
        }

        var elapsed = now - lastUpdate;
        return new FeedHealth
        {
            Status = elapsed > _options.StaleThreshold ? FeedStatus.Stale : FeedStatus.Live,
            LastPayloadReceivedAt = lastUpdate,
            SecondsSinceLastUpdate = elapsed.TotalSeconds
        };
    }

    private void DetectDiscontinuity(
        MatchState state, DateTimeOffset receivedAt, int? clockTime, List<DomainEvent> events)
    {
        if (state.LastUpdatedAt is not { } lastUpdate)
        {
            return;
        }

        var gap = receivedAt - lastUpdate;
        if (gap > _options.DiscontinuityThreshold)
        {
            state.HasStateDiscontinuity = true;
            events.Add(new FeedDiscontinuityDetected(gap.TotalSeconds, clockTime, receivedAt));
        }
    }

    private static MatchSnapshot MergeMatch(MatchSnapshot prev, GsiMap? incoming)
    {
        if (incoming is null)
        {
            return prev;
        }

        var rawState = incoming.GameState ?? prev.RawGameState;
        var gameState = incoming.GameState is not null
            ? GameStateParser.Parse(incoming.GameState)
            : prev.GameState;

        return prev with
        {
            MatchId = incoming.MatchId ?? prev.MatchId,
            ClockTime = incoming.ClockTime ?? prev.ClockTime,
            GameTime = incoming.GameTime ?? prev.GameTime,
            GameState = gameState,
            RawGameState = rawState,
            DaytimeIsDay = incoming.Daytime ?? prev.DaytimeIsDay,
            Paused = incoming.Paused ?? prev.Paused,
            RadiantScore = incoming.RadiantScore ?? prev.RadiantScore,
            DireScore = incoming.DireScore ?? prev.DireScore,
            WinTeam = incoming.WinTeam ?? prev.WinTeam
        };
    }

    private static PlayerSnapshot MergePlayer(PlayerSnapshot prev, GsiPlayer? incoming)
    {
        if (incoming is null)
        {
            return prev;
        }

        return prev with
        {
            SteamId = incoming.SteamId ?? prev.SteamId,
            Name = incoming.Name ?? prev.Name,
            Team = incoming.TeamName ?? prev.Team,
            Kills = incoming.Kills ?? prev.Kills,
            Deaths = incoming.Deaths ?? prev.Deaths,
            Assists = incoming.Assists ?? prev.Assists,
            LastHits = incoming.LastHits ?? prev.LastHits,
            Denies = incoming.Denies ?? prev.Denies,
            Gold = incoming.Gold ?? prev.Gold,
            GoldReliable = incoming.GoldReliable ?? prev.GoldReliable,
            GoldUnreliable = incoming.GoldUnreliable ?? prev.GoldUnreliable,
            Gpm = incoming.Gpm ?? prev.Gpm,
            Xpm = incoming.Xpm ?? prev.Xpm,
            NetWorth = incoming.NetWorth ?? prev.NetWorth
        };
    }

    private static HeroSnapshot MergeHero(HeroSnapshot prev, GsiHero? incoming)
    {
        if (incoming is null)
        {
            return prev;
        }

        return prev with
        {
            Name = incoming.Name ?? prev.Name,
            Id = incoming.Id ?? prev.Id,
            Level = incoming.Level ?? prev.Level,
            Xp = incoming.Xp ?? prev.Xp,
            Alive = incoming.Alive ?? prev.Alive,
            RespawnSeconds = incoming.RespawnSeconds ?? prev.RespawnSeconds,
            BuybackCost = incoming.BuybackCost ?? prev.BuybackCost,
            BuybackCooldown = incoming.BuybackCooldown ?? prev.BuybackCooldown,
            Health = incoming.Health ?? prev.Health,
            MaxHealth = incoming.MaxHealth ?? prev.MaxHealth,
            Mana = incoming.Mana ?? prev.Mana,
            MaxMana = incoming.MaxMana ?? prev.MaxMana
        };
    }

    private static ItemsSnapshot MergeItems(ItemsSnapshot prev, GsiItems? incoming)
    {
        if (incoming is null)
        {
            return prev;
        }

        var inventory = prev.Inventory.ToArray();
        var stash = prev.Stash.ToArray();
        var teleport = prev.Teleport;
        var neutral = prev.Neutral.ToArray();
        var preservedNeutral = prev.PreservedNeutral.ToArray();

        foreach (var (key, wireItem) in incoming.Slots)
        {
            var slot = ToItemSlot(wireItem);

            // "preserved_neutral" must be checked before the plain
            // "neutral" prefix check below, since it also starts with
            // characters that would otherwise partially match once
            // reordered — keep this ordering intentional, not accidental.
            if (TryParseIndexedSlot(key, "preserved_neutral", out var preservedIndex) && preservedIndex is >= 6 and < 11)
            {
                preservedNeutral[preservedIndex - 6] = slot;
            }
            else if (TryParseIndexedSlot(key, "slot", out var invIndex) && invIndex >= 0 && invIndex < inventory.Length)
            {
                inventory[invIndex] = slot;
            }
            else if (TryParseIndexedSlot(key, "stash", out var stashIndex) && stashIndex >= 0 && stashIndex < stash.Length)
            {
                stash[stashIndex] = slot;
            }
            else if (key.StartsWith("teleport", StringComparison.Ordinal))
            {
                teleport = slot;
            }
            else if (TryParseIndexedSlot(key, "neutral", out var neutralIndex) && neutralIndex >= 0 && neutralIndex < neutral.Length)
            {
                neutral[neutralIndex] = slot;
            }
            // Unrecognized slot keys are ignored rather than throwing —
            // consistent with never inventing/guessing GSI field meaning.
        }

        return prev with
        {
            Inventory = inventory,
            Stash = stash,
            Teleport = teleport,
            Neutral = neutral,
            PreservedNeutral = preservedNeutral
        };
    }

    private static bool TryParseIndexedSlot(string key, string prefix, out int index)
    {
        index = -1;
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(key.AsSpan(prefix.Length), out index);
    }

    private static ItemSlot ToItemSlot(GsiItem wireItem) => new()
    {
        Name = wireItem.Name,
        Cooldown = wireItem.Cooldown,
        Charges = wireItem.EffectiveCharges,
        Passive = wireItem.Passive,
        CanCast = wireItem.CanCast
    };

    private static AbilitiesSnapshot MergeAbilities(AbilitiesSnapshot prev, GsiAbilities? incoming)
    {
        if (incoming is null)
        {
            return prev;
        }

        var bySlotKey = prev.Abilities
            .Select((ability, index) => (Key: $"ability{index}", Ability: ability))
            .ToDictionary(x => x.Key, x => x.Ability);

        foreach (var (key, wireAbility) in incoming.Slots)
        {
            bySlotKey[key] = new AbilitySlot
            {
                Name = wireAbility.Name,
                Level = wireAbility.Level,
                Cooldown = wireAbility.Cooldown,
                CanCast = wireAbility.CanCast,
                Passive = wireAbility.Passive,
                Ultimate = wireAbility.Ultimate
            };
        }

        var ordered = bySlotKey
            .OrderBy(kv => ExtractAbilityIndex(kv.Key))
            .Select(kv => kv.Value)
            .ToArray();

        return prev with { Abilities = ordered };
    }

    private static int ExtractAbilityIndex(string key) =>
        int.TryParse(key.AsSpan("ability".Length), out var index) ? index : int.MaxValue;

    private static void DeriveEvents(
        MatchSnapshot prevMatch, MatchSnapshot nextMatch,
        HeroSnapshot prevHero, HeroSnapshot nextHero,
        int? clockTime, DateTimeOffset occurredAt, List<DomainEvent> events)
    {
        if (prevMatch.GameState != nextMatch.GameState)
        {
            events.Add(new GameStateChanged(prevMatch.GameState, nextMatch.GameState, nextMatch.RawGameState, clockTime, occurredAt));
        }

        if (prevHero.Level is { } fromLevel && nextHero.Level is { } toLevel && toLevel > fromLevel)
        {
            events.Add(new HeroLeveledUp(fromLevel, toLevel, clockTime, occurredAt));
        }

        if (prevHero.Alive is { } wasAlive && nextHero.Alive is { } isAlive && wasAlive != isAlive)
        {
            events.Add(new HeroDiedOrRespawned(isAlive, nextHero.RespawnSeconds, clockTime, occurredAt));
        }

        var prevCanBuyback = ComputeCanBuyback(prevHero);
        var nextCanBuyback = ComputeCanBuyback(nextHero);
        if (prevCanBuyback is { } prevValue && nextCanBuyback is { } nextValue && prevValue != nextValue)
        {
            events.Add(new BuybackStatusChanged(nextValue, nextHero.BuybackCost, nextHero.BuybackCooldown, clockTime, occurredAt));
        }
    }

    /// <summary>
    /// Tracks only whether buyback is off cooldown (hero-level state).
    /// Full affordability also needs player gold, which the FeatureEngine
    /// computes by combining this with PlayerSnapshot — see
    /// LiveFeatureSet.HasBuybackAvailable. Null-propagating: if
    /// BuybackCost is unknown (the population-reliability caveat on
    /// HeroSnapshot), the transition is undecidable and must not be
    /// treated as false.
    /// </summary>
    private static bool? ComputeCanBuyback(HeroSnapshot hero)
    {
        if (hero.BuybackCost is null)
        {
            return null;
        }

        return (hero.BuybackCooldown ?? 0) <= 0;
    }

    private static void DeriveItemPurchaseEvents(
        GsiItems? incoming,
        Func<string, ItemSlot?> getPriorSlot,
        int? clockTime,
        DateTimeOffset occurredAt,
        List<DomainEvent> events)
    {
        if (incoming is null)
        {
            return;
        }

        foreach (var (key, wireItem) in incoming.Slots)
        {
            var location = ClassifyLocation(key);
            if (location is null)
            {
                continue;
            }

            var prior = getPriorSlot(key);
            var wasEmpty = prior is null || prior.IsEmpty;
            var isNowReal = wireItem.Name is not (null or ItemSlot.EmptyName);

            if (wasEmpty && isNowReal)
            {
                events.Add(new ItemPurchased(wireItem.Name!, location.Value, clockTime, occurredAt));
            }
        }
    }

    private static ItemLocation? ClassifyLocation(string key) => key switch
    {
        _ when key.StartsWith("slot", StringComparison.Ordinal) => ItemLocation.Inventory,
        _ when key.StartsWith("stash", StringComparison.Ordinal) => ItemLocation.Stash,
        _ when key.StartsWith("teleport", StringComparison.Ordinal) => ItemLocation.Teleport,
        _ when key.StartsWith("neutral", StringComparison.Ordinal) => ItemLocation.Neutral,
        _ => null
    };

    private static ItemSlot? ResolvePriorSlot(ItemsSnapshot prevItems, string key)
    {
        if (TryParseIndexedSlot(key, "slot", out var invIndex) && invIndex >= 0 && invIndex < prevItems.Inventory.Count)
        {
            return prevItems.Inventory[invIndex];
        }

        if (TryParseIndexedSlot(key, "stash", out var stashIndex) && stashIndex >= 0 && stashIndex < prevItems.Stash.Count)
        {
            return prevItems.Stash[stashIndex];
        }

        if (key.StartsWith("teleport", StringComparison.Ordinal))
        {
            return prevItems.Teleport;
        }

        if (TryParseIndexedSlot(key, "neutral", out var neutralIndex) && neutralIndex >= 0 && neutralIndex < prevItems.Neutral.Count)
        {
            return prevItems.Neutral[neutralIndex];
        }

        return null;
    }
}
