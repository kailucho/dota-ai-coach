# ADR 0003: Field-wise coalesce merge for partial GSI payloads

## Status

Accepted

## Context

GSI payloads are partial by design — a single POST may contain only some
top-level sections, and within a present section, only some fields.
Naively replacing `MatchState` (or any section of it) with the latest
payload would silently erase previously-known data that simply wasn't
re-sent because it hadn't changed.

## Decision

`GsiNormalizer.Merge` performs a **field-wise coalesce**: for each
top-level section present in the incoming payload, build a new snapshot
record where each field is `incoming.Field ?? previous.Field`. A section
entirely absent from the payload leaves the corresponding `MatchState`
section completely untouched. The wire model (`GsiPayload` and its
nested types) makes every field nullable specifically to make "absent"
representable and distinguishable from "zero"/"false".

Domain events are derived by comparing the **pre-merge snapshot** against
the **post-merge snapshot** — never against the payload's own
`previously` block, which reflects only what the game client itself
considered the prior value and isn't guaranteed to arrive reliably (see
`docs/RESEARCH.md`'s reliability notes).

## Consequences

- A player-only payload (no hero/items/map sections) correctly leaves
  previously-known hero/items/map state intact — verified by
  `GsiNormalizerMergeTests.Merging_player_only_payload_leaves_previously_populated_hero_untouched`.
- Merging an identical payload twice is idempotent with respect to event
  derivation — verified by
  `GsiNormalizerMergeTests.Merging_identical_payload_twice_produces_zero_events_the_second_time`.
- This merge strategy requires care in `MatchState` itself: it must
  expose enough surface (internal setters plus a `Clone()` for
  isolated-snapshot use, see ADR-0007) for the normalizer to mutate in
  place without ever fully replacing a section from a partial payload.
