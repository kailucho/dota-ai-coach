# Research

This document records the research performed before implementing Fase 0 +
Fase 1 + the vertical slice, per the project's `dota-gsi` skill rule:
**never invent GSI fields; verify against current documentation/fixtures.**

Valve publishes no official Dota 2 GSI schema. Everything below is
reverse-engineered from actively-maintained community GSI client
libraries, cross-checked against multiple independent sources. Field
names are high-confidence; some *behaviors* (population reliability of
specific fields) are lower-confidence and explicitly flagged.

## Sources consulted

- [antonpup/Dota2GSI](https://github.com/antonpup/Dota2GSI) (C#) — primary
  source for field names, verified directly against library source files
  (`Nodes/Map.cs`, `PlayerProvider/PlayerDetails.cs`,
  `HeroProvider/HeroDetails.cs`, `ItemsProvider/ItemDetails.cs`,
  `ItemsProvider/Item.cs`, `AbilitiesProvider/Ability.cs`).
- [xzion/dota2-gsi](https://github.com/xzion/dota2-gsi) — cfg file format,
  `auth` block behavior, conventional port.
- [ValveSoftware/Dota-2 GitHub issue #1023](https://github.com/ValveSoftware/Dota-2/issues/1023)
  ("Game State Integration works intermittently") — documented reliability
  issues, long-standing and unresolved.
- [Game State Integration Intro — auo.nu](https://auo.nu/posts/game-state-integration-intro/) —
  launch option requirement, general setup.

Research performed as part of this iteration; re-verify periodically,
especially after major Dota patches, per the project's `dota-gsi` skill.

**Update — real capture verification.** The field table and confidence
caveats below were subsequently checked against ~1,600 real payloads
captured from a live Dota 2 client (bot match, `DotaAiCoach.GsiCapture`
tool). Real-capture-confirmed corrections are called out inline where
they differ from the community-sourced research below; the original
research is left in place rather than rewritten, since it correctly
predicted the shape and was wrong only on specific counts/details.

## GSI configuration

- Config file: `game/dota/cfg/gamestate_integration/gamestate_integration_<name>.cfg`,
  filename must start with `gamestate_integration_`.
- Requires the `-gamestateintegration` Dota 2 launch option — the single
  most common cause of "listener receives nothing."
- KeyValues format: `uri` / `timeout` / `buffer` / `throttle` / `heartbeat`
  (all quoted strings, including numbers) + a `data` block of boolean
  flags per section.
- `auth` block: optional but recommended for an open local listener — the
  only thing distinguishing a real game POST from anything else hitting
  the same loopback port.
- Conventional port: **3000** (default in both antonpup and xzion
  examples) — used as this project's default.

## Verified field paths (playing mode, local player)

All paths below are confirmed present in current community GSI client
library source. Playing mode's payload shape is flat (`player.gold`);
spectating mode nests per-player (`player.team2.player0.gold`) — this
project's vertical slice targets playing mode only.

| Section | Fields |
|---|---|
| `map` | `matchid`, `game_time`, `clock_time`, `daytime`, `game_state`, `paused`, `radiant_score`, `dire_score`, `win_team` |
| `player` | `steamid`, `name`, `team_name`, `kills`, `deaths`, `assists`, `last_hits`, `denies`, `gold`, `gold_reliable`, `gold_unreliable`, `gpm`, `xpm`, `net_worth` |
| `hero` | `id`, `name`, `level`, `xp`, `alive`, `respawn_seconds`, `buyback_cost`, `buyback_cooldown`, `health`, `max_health`, `mana`, `max_mana` |
| `items` | keyed by `slot0`-`slot8` (**9 slots — corrected, see below**), `stash0`-`stash5`, `teleport0`, `neutral0`-`neutral1` (**2 slots — corrected**), plus `preserved_neutral6`-`preserved_neutral10` (**previously undocumented**); each with `name`, `purchaser`, `cooldown`, `passive`, `can_cast`, `item_level`, and charge-related keys (see below) |
| `abilities` | keyed by `ability0`-`abilityN`; each with `name`, `level`, `can_cast`, `passive`, `ability_active`, `cooldown`, `max_cooldown`, `ultimate` |

**Real-capture correction (supersedes the community-sourced row above):**
this project originally modeled the main inventory as `slot0`-`slot5` (6
slots) and a single `neutral0`. A live capture showed the real payload
uses **`slot0`-`slot8` (9 slots)** and **`neutral0`/`neutral1` (2 active
neutral slots)**, plus a previously-undocumented
`preserved_neutral6`-`preserved_neutral10` group (semantics not yet
investigated — likely neutral-tier items banked but not equipped). The
original 6-slot model silently truncated any item placed in slots 6-8.
Fixed in `ItemsSnapshot` (`DotaAiCoach.Domain`); see
`tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/README.md` for the fixtures
that now exercise this, and the corresponding ADR.

**Other previously-undocumented fields observed in the real capture** (not
yet consumed by any rule/feature in this slice — noted here so the next
person doesn't have to rediscover them): `map.name`, `map.customgamename`,
`map.ward_purchase_cooldown`; `player.accountid`, `activity`,
`kill_streak`, `commands_issued`, `kill_list`, `player_slot`, `team_slot`,
`gold_from_hero_kills`/`gold_from_creep_kills`/`gold_from_summon_kills`/`gold_from_income`/`gold_from_shared`;
`hero.xpos`/`ypos`, `silenced`/`stunned`/`disarmed`/`magicimmune`/`hexed`/`muted`/`break`,
`smoked`, `permanent_buffs`, `has_debuff`, `talent_1`-`talent_8`,
`attributes_level`; `items.*.item_level`; `abilities.*.max_cooldown`.

`map.game_state` is a string enum (e.g.
`DOTA_GAMERULES_STATE_GAME_IN_PROGRESS`) — this project's
`GameStateParser` (see `DotaAiCoach.Domain`) parses known values and
degrades unrecognized strings to `Unknown` rather than throwing, since
Valve can add new states at any time.

## Fields flagged "verify via fixture" — confidence caveats

These field *names* are confirmed present in library source, but their
*population behavior* is not independently verified against a live
capture in this iteration (this slice's fixtures are hand-built — see
`tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/README.md`). Treat these as the
highest-priority items to confirm against a real client capture:

1. **`hero.buyback_cost` / `hero.buyback_cooldown`** — ~~field names
   confirmed, but community reports describe inconsistent population~~
   **Resolved by real capture: these populate reliably.** Both fields were
   present and correctly tracked across a real death → buyback-used
   sequence (`buyback_cost` 383→354, `buyback_cooldown` 0→919 after use;
   see `Fixtures/Gsi/buyback/`). The nullable modeling and
   `BuybackWarningRule`'s null-guard are kept regardless (defensive
   against the payload arriving before the hero section populates at
   all), but the "often absent or zero" community concern was not
   observed in practice.
2. **`items.neutral0`** — ~~neutral item mechanics have been reworked
   repeatedly across patches; exact current population behavior not
   independently verified.~~ **Resolved by real capture, and corrected**:
   the real payload has `neutral0` **and** `neutral1` (2 active slots, not
   1) — see the slot-count correction above.
3. **Item charge keys** — observed under three different possible keys
   depending on item type: `charges`, `item_charges`, `ability_charges`.
   This project's `GsiItem.EffectiveCharges` captures all three and uses
   the first non-null value, never assuming a specific one.
4. **`hero.xp`** — whether this is cumulative total XP or XP into the
   current level is unconfirmed. Not currently used by any rule in this
   slice; flagged in `HeroSnapshot`'s XML doc for whoever builds a
   level-progress feature on it next.

## OpenDota / STRATZ / Steam OpenID

Per this iteration's scope (Fase 0 + Fase 1 + vertical slice = GSI-only
live pipeline), OpenDota, STRATZ, and Steam OpenID integration are **not
implemented** in this pass — they belong to later phases (Player Profile,
postgame analysis) per `docs/ROADMAP.md`. No research claims are made
about them here; that research should happen immediately before the
phase that needs them, per the project's `dota-data`/`dota-fairplay`
skills' "verify current API state" guidance, since API surfaces and rate
limits shift over time.
