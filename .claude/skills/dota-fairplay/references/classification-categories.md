# FairPlay Classification Categories

Every live data point gets exactly one of these six classifications. When
a data point seems to straddle two categories, classify it by its *most
restrictive* legitimate access path — don't round up to convenience.

## LIVE_ALLOWED

Visible to the player right now through Dota's own Game State Integration
(GSI) feed, or equivalent officially-sanctioned live data (e.g. values the
game client itself exposes to the player, like their own gold, own
cooldowns, own visible minimap state).

**Use for:** any live recommendation logic, MatchState fields, real-time
coaching.

**Examples:**
- player's own hero HP/mana/level/gold — `LIVE_ALLOWED`
- player's own item cooldowns — `LIVE_ALLOWED`
- enemy hero HP when that hero is currently visible to the player (GSI
  exposes visible-enemy state, mirroring what's on screen) — `LIVE_ALLOWED`

## VISIBLE_SCREEN

Rendered on the player's own screen but not exposed via GSI, so it must be
captured via computer vision (or similar) reading the actual pixels the
player sees — never inferred, reconstructed, or substituted from a source
the player doesn't have.

**Use for:** CV features that read HUD elements, killfeed text, visible
enemy item icons in a teamfight, minimap dots for currently-visible units.

**Critical constraint:** the CV pipeline must only process the frame the
player's own client actually rendered. If a "visible screen" feature is
implemented by pulling the same-looking data from a backend/API/replay
feed instead of the actual screen capture, it's no longer `VISIBLE_SCREEN`
— it has silently become a different, likely `FORBIDDEN` or
`SPECTATOR_ONLY`, data path, even though the output looks identical.

**Examples:**
- reading an enemy's item icons from the HUD when their hero portrait
  currently shows on your screen during a fight — `VISIBLE_SCREEN`
  (captured via CV from your actual render)
- reading the killfeed text after a kill — `VISIBLE_SCREEN`

## PUBLIC_PREFLIGHT

Public information that exists independent of any specific live match —
patch notes, hero base stats, item mechanics, publicly-available prior
match history for a player/hero (e.g. from a public stats site), draft
picks once locked in (visible to both teams by game design).

**Use for:** pre-game prep, draft-time suggestions, static reference data,
matchup/counter reasoning grounded in publicly known mechanics.

**Constraint:** this category is for information that doesn't depend on
peeking into the *current* game's hidden state. "This hero's public win
rate on this patch" is `PUBLIC_PREFLIGHT`. "This specific enemy's item
build in the game happening right now, before it's visible" is not — that
would require an illegitimate live source and is likely `FORBIDDEN`.

**Examples:**
- hero/item mechanical data for the current patch — `PUBLIC_PREFLIGHT`
- an enemy player's historical hero pool from public match history,
  used only in pre-game draft suggestions — `PUBLIC_PREFLIGHT`

## POSTGAME_ONLY

Becomes legitimately available only after the match has concluded —
primarily full replay data, which reveals information (enemy positions,
items, wards) that was hidden by fog-of-war during the live game but is
unlocked for review once the match is over, matching what a player could
legitimately review afterward.

**Use for:** post-game analysis, review features, coaching feedback about
what *actually* happened after the fact.

**Constraint:** POSTGAME_ONLY data must never leak into a live
recommendation path, even indirectly (e.g. via a cached model trained per-
match on the current game's own replay data mid-game — that's a
disguised live use). It's fine to use POSTGAME_ONLY data in aggregate,
patch-level ways that feed `PUBLIC_PREFLIGHT`-classified static data (e.g.
"item X is timed at minute Y across many past games" learned offline from
replays), but a specific live match's own replay must stay walled off from
that same match's live recommendations.

**Examples:**
- full fog-of-war-revealing replay parse for a completed match, used to
  generate a post-game report — `POSTGAME_ONLY`
- ward placement timeline shown after the game ends — `POSTGAME_ONLY`

## SPECTATOR_ONLY

Available through spectator/observer mode, which by design sees more than
any single player does (e.g. all wards, all fog-of-war state, delayed or
live all-vision). This is legitimate for *spectating*, but a spectator's
vantage point is not a player's vantage point — a live coaching tool acts
on behalf of a player, so spectator-level omniscience is never legitimate
input for it.

**Use for:** nothing in this project's live path. If a broadcast/spectator
integration is ever built as a genuinely separate spectator-facing
product, it would need its own explicit scope decision — don't assume
this skill's live-feature rules extend to it.

**Examples:**
- observer-mode fog-of-war-free minimap — `SPECTATOR_ONLY`, never wire
  into live player recommendations
- delayed broadcast data feeds — `SPECTATOR_ONLY`

## FORBIDDEN

Not legitimately available to a player through any sanctioned means, at
any point, full stop. This includes anything obtainable only via the
explicitly forbidden techniques (memory reading, DLL injection, packet
manipulation) or anything Dota's design permanently hides from the player
regardless of game phase (e.g. real-time unwarded enemy jungle position
during the live game, before any legitimate reveal).

**Use for:** nothing, ever. If a data point is `FORBIDDEN`, do not
capture, store, cache, or pass it through the system in any form — not
even "for later, unused" — because unused-but-present forbidden data is
still a fair-play and trust liability if it's ever wired up, logged, or
leaked.

**Examples:**
- enemy hero's exact position while in fog-of-war, obtained via memory
  reading — `FORBIDDEN`
- enemy inventory read via packet inspection before it's ever shown on any
  player's screen — `FORBIDDEN`
