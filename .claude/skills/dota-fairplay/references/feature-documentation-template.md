# Live Feature Documentation Template

Every live-facing feature (data source, MatchState field, CV feature, ML
model feature, or recommendation) needs these five fields documented
somewhere durable — a code comment near the field/feature definition, a
design doc, or the PR description, matching however this project already
documents things nearby. The point is that the classification decision is
written down and reviewable, not that it lives in one specific file format.

## Fields

```
source:                  Where the data physically comes from.
availability:             When this data exists / is populated.
visibility:               What a legitimate player can concretely see
                           that corresponds to this data.
fairPlayClassification:   One of LIVE_ALLOWED, VISIBLE_SCREEN,
                           PUBLIC_PREFLIGHT, POSTGAME_ONLY,
                           SPECTATOR_ONLY, FORBIDDEN.
justification:            Why this classification is correct here,
                           especially for non-obvious cases.
```

`visibility` is the field most worth scrutinizing during review — if it
can't be stated concretely (a specific GSI field name, a specific screen
region, a specific public data source), that's a sign the classification
might be optimistic rather than correct.

## Worked examples

### Example 1 — MatchState field

```
Field: MatchState.Player.Gold
source: GSI payload, player.gold
availability: always, while in an active game
visibility: player's own gold total, shown in the HUD
fairPlayClassification: LIVE_ALLOWED
justification: Directly sourced from the player's own GSI feed, which
  only exposes information about the player's own state. No fog-of-war
  or opponent-visibility concerns apply to a player's own resources.
```

### Example 2 — Computer vision feature

```
Feature: EnemyItemIconReader (CV)
source: Screen capture of the enemy hero portrait's item icon slots,
  processed via CV when the enemy hero is on-screen
availability: only while the enemy hero (and their portrait/item icons)
  is actually rendered and visible to the player
visibility: exactly what's rendered — item icons the player can see with
  their own eyes on their own screen
fairPlayClassification: VISIBLE_SCREEN
justification: This reads pixels from the player's own render, not a
  backend data source, and only when the enemy hero's UI is actually
  displayed. It stops updating the moment the enemy leaves vision,
  matching real player visibility exactly. If this were instead pulled
  from a live match data API that always knows enemy items regardless of
  vision, it would be FORBIDDEN instead — the distinguishing factor is
  strictly "did this come from the pixels this player's client rendered."
```

### Example 3 — ML model feature (rejected)

```
Feature (proposed): EnemyJunglePositionEstimate
source: Statistical inference from aggregate replay data correlated with
  minute/hero to predict likely current enemy jungle position
availability: continuously during the live game
visibility: none — the player has no way to see this in real time
fairPlayClassification: FORBIDDEN
justification: Even though the underlying training data (past replays)
  is legitimately POSTGAME_ONLY, using it to predict a *current* hidden
  position and surface that prediction live gives the player fog-of-war-
  piercing information they could not legitimately have. The statistical
  indirection doesn't change the outcome: it reveals hidden information.
  This feature should not be built for live use. (A postgame version —
  "here's where similar opponents historically ward/jungle, for prep
  purposes" — would be a different, PUBLIC_PREFLIGHT-classified feature,
  clearly separated from any live game.)
```

### Example 4 — Postgame replay feature

```
Feature: PostGameWardTimeline
source: Full replay parse after match completion
availability: only after the match has ended and replay is processed
visibility: not visible live; matches what a player could review via
  official replay tools after the game
fairPlayClassification: POSTGAME_ONLY
justification: Replay data legitimately reveals fog-of-war-hidden info
  once the match is over — this mirrors what a player could already do
  by reviewing their own replay. Must not be fed into any live
  recommendation path for this or a future match's live state.
```

## When documentation is missing or vague

If you encounter an existing live feature without this documentation, or
with a `visibility` field that's vague/unfalsifiable ("the player probably
knows this"), treat that as a fair-play gap to raise, not something to
silently backfill with a favorable-looking justification. Ask for
clarification or flag it rather than guessing.
