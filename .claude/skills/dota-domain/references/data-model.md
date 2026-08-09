# Suggested Domain Data Model

No domain-layer code exists yet in this repo (as of this skill's creation).
This file sketches structured representations for the concepts in this
skill, so future implementation stays consistent instead of each feature
inventing its own hero/role/item vocabulary. Treat this as a starting
proposal to adapt, not a locked schema — update this file when the real
implementation diverges from it.

## Relationship to the GSI pipeline

Per the `dota-gsi` skill, raw state flows:

```
Raw GSI JSON → GsiReceiver → GsiNormalizer → MatchState → DomainEvents
```

The domain concepts below live *above* `MatchState`/`DomainEvents`, in a
strategy/domain layer that:
- consumes `MatchState` and `DomainEvents` as input
- consumes static hero/item reference data (mechanics — see below)
- produces strategic reasoning/recommendations as output

It must not reach back into GSI ingestion/normalization, and GSI code must
not contain strategic logic — keep the mechanics/strategy separation from
`SKILL.md` structurally enforced by this layering, not just by convention.

## Static (mechanical) reference data

Represent hero and item mechanics as structured, versioned-by-patch data,
not hardcoded constants scattered through strategic logic:

- `Hero`: id, name, base roles/positions typically played, damage type(s)
  dealt by kit, ability list (with CC type/duration/pierces-spell-immunity
  flags where applicable), available facets, available talents.
- `Item`: id, name, cost, mechanical effects (stats granted, active/passive
  behavior), typical timing/role fit (e.g. "early lane sustain",
  "core damage item", "support utility item") — kept descriptive, not
  prescriptive, since fit is contextual.
- `Position`: enum 1-5, not a free-text role string.
- `DamageType`: enum { Physical, Magical, Pure }.
- `CrowdControlType`: enum { HardDisable, SoftDisable, Positional } with a
  sub-kind (Stun, Hex, Fear, Sleep, Root, Silence, Slow, Knockback, Pull...)
  and a `PiercesSpellImmunity: bool` flag.

## Situational (per-match) state

Represent the *inputs* a recommendation needs as an explicit structure,
so it's obvious when one is missing rather than silently defaulting:

- current `MatchState` (from GSI) — game minute, gold, items owned,
  observed enemy heroes/items where visible
- `Draft` — both teams' picks (and bans, if available)
- `Patch` — active game version, since mechanics/balance are patch-bound
- `Bracket` — skill level context, if known
- `PlayerProfile` — comfort/experience signal for the hero being played,
  if available

A recommendation function/service should take this situational bundle as
input rather than reasoning from a hero or item id alone — that's the
structural way to enforce "never recommend from global win rate alone."

## Recommendation output shape

Prefer recommendation outputs that carry their own justification as
structured fields (e.g. `reasoning: string[]` citing which inputs drove the
result, `confidence`, `alternatives considered`) over a bare hero/item id.
This keeps the "why" auditable and testable, and makes it possible to
write tests asserting that a recommendation actually used contextual
inputs rather than a shortcut like global win rate.
