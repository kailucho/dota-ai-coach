# ADR 0004: Empty item slots as a real ItemSlot, never null

## Status

Accepted

## Context

GSI represents an empty inventory/stash/teleport/neutral slot as an
object with `name: "empty"` — it is never omitted from the payload. A
naive model treating "no name" as `null` would conflate two genuinely
different states: "this slot has never been reported" vs. "this slot was
explicitly reported as empty."

## Decision

`ItemSlot` is a record with `IsEmpty => Name is null or "empty"`.
`ItemsSnapshot.Inventory`/`Stash` are always exactly nine/six entries (see
ADR-0010 for the slot-count correction), with
unpopulated slots represented as `ItemSlot.Empty` (`Name = "empty"`), not
`null` entries in the list. The GSI normalizer treats a slot transitioning
to `name: "empty"` as an explicit clear (producing a real
`ItemSlot.Empty`), not a no-op.

## Consequences

- `ItemsSnapshot.OwnedItemNames()`/`Owns(...)` correctly exclude empty
  slots without needing null-checks at every call site.
- `GsiNormalizer`'s item-purchase event derivation
  (`wasEmpty && isNowReal`) is expressed directly in terms of `IsEmpty`,
  matching GSI's own never-omit-a-slot behavior rather than working
  around it.
- Verified by `ItemSlotParsingTests.Empty_slots_are_never_omitted_and_are_never_null`
  and `GsiNormalizerMergeTests.A_slot_transitioning_to_empty_clears_it`.
