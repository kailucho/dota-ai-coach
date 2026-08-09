# GSI Fixtures — Provenance

Per `dota-gsi/references/payload-structure.md`: a hand-built fixture only
proves the code handles the shape we *assumed*, not the shape Dota
actually sends. As of this update, most fixture groups are **real
captures** from a live Dota 2 client (bot match), recorded via the
`DotaAiCoach.GsiCapture` development tool (`tools/DotaAiCoach.GsiCapture`).

| Fixture group | Provenance | Notes |
|---|---|---|
| `single/*.json` | hand-built | field-level shape (nullability, structure) matches the real capture; specific edge cases (malformed JSON, unknown game_state) are deliberately synthetic and can't come from a real client by definition |
| `laning-sequence/*.json` | **real capture** | pregame → game start → farm tick → level 1→2 → real multi-item purchase (2x Branches + Magic Wand recipe), trimmed from a longer session |
| `item-purchase/*.json` | **real capture** | a real Boots purchase (gold 600→100, slot0 empty→item_boots) |
| `hero-death/*.json` | **real capture** | a real death → still-dead tick → respawn sequence |
| `buyback/*.json` | **real capture** | a real death → buyback-used-on-respawn sequence (buyback_cooldown 0→919) |
| `reconnection-gap/*.json` | hand-built | the real capture's largest observed gap between payloads was 3 seconds — no genuine multi-minute disconnect occurred during capture, so this scenario remains synthetic until one is observed |

## What the real capture confirmed and corrected

Capturing ~1,600 real payloads from a live client (see
`docs/RESEARCH.md` for the full writeup) surfaced one real bug this
project's earlier hand-built fixtures never would have caught: **GSI's
actual inventory has 9 slots (`slot0`-`slot8`)**, not the 6 this project
originally assumed — the earlier model was silently truncating any item
placed in slots 6-8. Also corrected: `neutral0`/`neutral1` (2 active
neutral slots, not 1) and a previously-undocumented
`preserved_neutral6`-`preserved_neutral10` group. See `ItemsSnapshot` in
`DotaAiCoach.Domain` and ADR-0010 for the fix.

## Remaining synthetic scenario

`reconnection-gap/` is the one fixture group still hand-built, since a
genuine multi-minute GSI disconnect wasn't observed during the capture
session used for this update. Replace it with a real capture the next
time one is naturally observed (e.g. during a client crash, alt-tab
stall, or network interruption) rather than continuing to rely on the
synthetic version indefinitely.
