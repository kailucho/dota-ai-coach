# ADR 0010: Inventory/neutral slot counts corrected from a real GSI capture

## Status

Accepted

## Context

`ItemsSnapshot` (ADR-0004) originally modeled the main inventory as
`slot0`-`slot5` (6 slots) and a single `neutral0`, based on the
community-sourced field research in `docs/RESEARCH.md`. Capturing ~1,600
real payloads from a live Dota 2 client (via `DotaAiCoach.GsiCapture`,
see `tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/README.md`) showed the real
payload sends `items.slot0` through `items.slot8` (9 slots),
`items.neutral0`/`items.neutral1` (2 active neutral slots), and a
previously-undocumented `items.preserved_neutral6`-`preserved_neutral10`
group (5 entries; exact gameplay semantics not yet investigated).

The 6-slot model was silently truncating any item placed in inventory
slots 6, 7, or 8 — `GsiNormalizer.MergeItems` simply skipped indices
outside the array bounds. No hand-built fixture caught this, since all
prior fixtures were built from the same (wrong) assumption the code used.

## Decision

`ItemsSnapshot.Inventory` is now 9 slots, `ItemsSnapshot.Neutral` is now a
2-element list (was a single nullable slot), and a new
`ItemsSnapshot.PreservedNeutral` (5 slots) captures the previously-unknown
`preserved_neutral*` keys. `ItemsSnapshot.Stash` stays at 6 slots
(confirmed correct by the same capture). `GsiNormalizer.MergeItems` checks
the `preserved_neutral` prefix before the plain `neutral` prefix, since
the former is a superstring match of the latter.

## Consequences

- Any code reading `ItemsSnapshot.Neutral` as a single value must be
  updated to index into the list — this is a breaking shape change,
  applied throughout this codebase in the same change.
- `PreservedNeutral` is modeled but not yet consumed by any rule/feature;
  its gameplay meaning (banked neutral-tier items vs. something else)
  should be confirmed before building logic on top of it.
- Verified by `MatchStateTests.ItemsSnapshot_default_slot_counts_match_a_live_captured_payload`,
  `MatchStateTests.ItemsSnapshot_OwnedItemNames_includes_neutral_slots`,
  `GsiNormalizerMergeTests.Item_in_slot8_is_correctly_merged_not_truncated`,
  and `GsiNormalizerMergeTests.Second_neutral_slot_is_correctly_merged`.
- This is the direct payoff of replacing hand-built fixtures with real
  captures (see `docs/ROADMAP.md`'s "Known limitations" — previously
  item #1, now resolved): a bug only a real client payload could reveal.
