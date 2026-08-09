# Fair Play

Core principle (from `docs/MASTER_PLAN.md` and `.claude/skills/dota-fairplay`):

> Si un jugador humano mirando legítimamente su propia pantalla podría
> conocer el dato, podemos analizarlo. Si Dota deliberadamente lo oculta,
> nuestro sistema también debe ocultarlo.

## The six classifications

Implemented in `DotaAiCoach.FairPlay.FairPlayClassification`:

- **LiveAllowed** — visible to the player right now via their own GSI feed.
- **VisibleScreen** — rendered on the player's own screen but not exposed
  via GSI; must be captured from the actual rendered frame (relevant to
  the deferred Computer Vision phase, not used in this slice).
- **PublicPreflight** — public information independent of the current
  live match (deferred to the OpenDota/STRATZ phase).
- **PostgameOnly** — only legitimately available after the match ends
  (deferred to the Postgame phase).
- **SpectatorOnly** — available only through spectator/observer mode;
  never usable for a live feature.
- **Forbidden** — not legitimately available to a player through any
  sanctioned means.

`FairPlayPolicy.IsAllowedLive(classification)` returns true only for
`LiveAllowed`, `VisibleScreen`, `PublicPreflight`.

## Classification of every live data group in this slice

Everything in this vertical slice is sourced exclusively from GSI while
the local player is actively playing (never spectating) — every group
below is `LiveAllowed`.

| Type | source | availability | visibility | fairPlayClassification | justification |
|---|---|---|---|---|---|
| `MatchSnapshot` | Gsi | GSI feed connected, match loaded | GSI `map` section — clock, match id, phase, score; all on the player's own HUD | LiveAllowed | GSI (playing mode) only exposes match-level state visible to that player |
| `PlayerSnapshot` | Gsi | GSI feed connected, player in active match | GSI `player` section — own KDA/gold/GPM/XPM, shown in own scoreboard | LiveAllowed | GSI (playing mode) only exposes the local player's own state |
| `HeroSnapshot` | Gsi | GSI feed connected, hero loaded | GSI `hero` section — own hero level/HP/mana/buyback, shown in own HUD | LiveAllowed | GSI (playing mode) only exposes the local player's own hero |
| `ItemsSnapshot` | Gsi | GSI feed connected, hero loaded | GSI `items` section — own inventory/stash/teleport/neutral, shown in own HUD | LiveAllowed | GSI (playing mode) only exposes the local player's own inventory |
| `AbilitiesSnapshot` | Gsi | GSI feed connected, hero loaded | GSI `abilities` section — own ability levels/cooldowns, shown in own HUD | LiveAllowed | GSI (playing mode) only exposes the local player's own abilities |
| `LiveFeatureSet` | Gsi | GSI feed live, match in progress | Derived solely from the above (all player-owned GSI data) | LiveAllowed | Every field traces to the player's own GSI feed only |

This documentation is enforced mechanically, not just written down: every
type above carries a `[FairPlaySource(...)]` attribute in code (see
`DotaAiCoach.Domain` and `DotaAiCoach.FeatureEngine`), and
`DotaAiCoach.FairPlay.Tests.FairPlayClassificationTests` asserts every
classified type has a non-empty `Visibility`/`Justification` and is never
`Forbidden`/`SpectatorOnly`.

## The structural test

Per `dota-fairplay`'s testing guidance, the strongest test isn't "does
this look right" — it's "is it structurally impossible for the wrong data
to reach a live path." `DotaAiCoach.FairPlay.Tests.LiveRecommendationInputTests`
does exactly this:

1. Confirms `RuleRecommendationEngine.Evaluate`'s parameter type
   (`LiveFeatureSet`) implements `ILiveRecommendationInput` and is
   attributed `LiveAllowed`.
2. Walks the full type graph reachable from `LiveFeatureSet` via
   reflection, and fails if any reachable `DotaAiCoach.*` type is either
   unclassified or classified as anything other than
   `LiveAllowed`/`VisibleScreen`/`PublicPreflight`.
3. Confirms `DotaAiCoach.RecommendationEngines` doesn't reference
   `DotaAiCoach.Gsi` at the assembly level at all — so a wire type could
   never be smuggled into a rule's input even indirectly.

If a future phase (Computer Vision, OpenDota, replay analysis) adds a
type sourced from `Replay`/`ExternalApi`/`ComputerVision` and it somehow
becomes reachable from a live rule's input, this test fails the build —
it does not rely on a reviewer noticing.

## Explicitly prohibited (per `docs/MASTER_PLAN.md`)

Not implemented anywhere in this project, and never to be added:
process memory reading, DLL injection, packet manipulation, fog-of-war
access, hidden enemy position/wards/items/cooldowns, automated
movement/skill/purchase inputs, or any mechanism designed to reveal
information Dota deliberately hides from the player.

## Fase 2 — Player Profile (OpenDota, PublicPreflight)

Implemented in `DotaAiCoach.PlayerProfile` and `DotaAiCoach.OpenDota`.
Every type carries the same five-field documentation discipline:

| Type | source | availability | visibility | fairPlayClassification | justification |
|---|---|---|---|---|---|
| `PlayerProfileSnapshot` | ExternalApi | Steam profile has "Expose Public Match Data" enabled and OpenDota has indexed the account | OpenDota's public player/match-history endpoints — the same data any player could look up via opendota.com | PublicPreflight | Only republishes publicly-exposed Dota match data; nothing hidden |
| `HeroReference` | StaticReference | OpenDota `/heroes` reference cache populated | Static hero metadata (name, attributes, roles) | PublicPreflight | Publicly documented game data, independent of any live match |

**Profile data does not currently feed any live recommendation.** This
scope decision (see `docs/adr/0012`) is enforced structurally by
`DotaAiCoach.FairPlay.Tests.ProfileFairPlayTests.No_PlayerProfile_type_is_reachable_from_LiveFeatureSet_...`,
which walks the same type graph `LiveRecommendationInputTests` walks and
asserts no `DotaAiCoach.PlayerProfile` type is reachable from
`LiveFeatureSet`. `PublicPreflight` remains in
`FairPlayPolicy.LiveAllowedSet`, so a future phase *could* feed profile
data into a live or draft-time recommendation without a classification
change — only a deliberate decision to make that type reachable, which
would then need the same review this document already calls for.

`ParsedMatchDetail` (per `docs/adr/0016`, six additional Dota DNA
dimensions unlocked via OpenDota's parsed match detail) is the same
`PublicPreflight`/`ExternalApi` OpenDota data, just richer — it flows only
into `DotaDnaProfile`, never `LiveFeatureSet`, and
`ProfileFairPlayTests.ParsedMatchDetail_is_not_reachable_from_LiveFeatureSet_...`
enforces the same guarantee for it specifically.

## What's still deferred

Computer Vision (`VisibleScreen`) and postgame replay analysis
(`PostgameOnly`) are not implemented — see `docs/ROADMAP.md`. When each
phase begins, the same five-field documentation and structural-test
discipline applies to their new data sources before any code merges.
