# State Diffing: Partial Payloads, `previously`, `added`

## Payloads are partial by design

A GSI POST is not a full snapshot of the match. It typically contains only
the sections/fields that changed (or newly became relevant) since the last
POST, plus whatever `heartbeat` forces through even without changes. This
has a direct consequence for parsing and normalization: **never treat an
incoming payload as the complete current state.** Always merge it into an
accumulated `MatchState`, field by field, rather than replacing
`MatchState` wholesale with whatever arrived.

A normalizer that does `MatchState = ParsePayload(json)` instead of
`MatchState = Merge(MatchState, ParsePayload(json))` will silently lose
any field not present in the latest payload — including fields that are
very much still true, just not re-sent because they didn't change.

## The `previously` block

Alongside the main state, GSI payloads commonly include a sibling
`previously` object mirroring the shape of the fields that changed,
holding their **prior** value. E.g. if gold changed, you'd see both the
new value at `player.gold` and the old value at `previously.player.gold`.

Use this for:
- deriving `DomainEvents` that need a "from → to" transition (e.g. "hero
  leveled up" needs both old and new level, not just the new one);
- validating that your own accumulated `MatchState` agrees with what the
  game client thinks the prior value was, as a sanity check / drift
  detector during development.

Don't use `previously` as your only source of "what changed" — prefer
comparing against your own accumulated `MatchState` before this payload,
since that's authoritative for your system's view of history, whereas
`previously` only reflects what the *client* considered the prior value
(and payload delivery isn't perfectly reliable — see
`payload-structure.md`'s reliability notes).

## The `added` block

A sibling `added` object marks fields that are newly present in this
payload that weren't present before (e.g. a new item appearing in an
inventory slot, a newly available ability). Useful for distinguishing
"this field's value changed" from "this field just started existing" —
relevant for domain events like "item purchased" vs. "item's charges
changed," which need different handling even though both touch the same
`items` section.

## Merge algorithm shape

A reasonable normalization approach:

1. Parse the incoming JSON into a typed partial-payload structure (only
   fields actually present get populated; everything else stays
   unset/absent — don't default absent fields to zero/null values that
   look like real data).
2. For each section/field present in the payload, write it into the
   accumulated `MatchState`, overwriting the prior value.
3. Use `previously`/`added` (where present and trustworthy) to help derive
   `DomainEvents` for this update, rather than to drive the merge itself.
4. Fields absent from the payload are left untouched in `MatchState` —
   they retain whatever value they had before this update.

Write this merge logic to be idempotent and safe against payloads arriving
out of order or duplicated (see `stale-state-and-reconnection.md`) — don't
assume strict, gap-free ordering.

## Testing implication

Because correctness here depends on *sequences* of partial payloads, not
just a single payload in isolation, tests for the normalizer should feed
in a short sequence of fixture payloads and assert on the resulting
`MatchState` after each step — not just test each payload independently.
A parser can pass every single-payload test and still merge incorrectly
across a sequence. See `fixtures-and-testing.md` for fixture sequencing
and the replay simulator, which exists specifically to exercise this.
