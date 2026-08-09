# Pipeline Stages: RAW → BRONZE → NORMALIZED → FEATURES → DATASET

Each stage exists to answer a specific question about the data's
trustworthiness. Skipping a stage (e.g. deriving FEATURES straight from
RAW) tends to reintroduce exactly the bugs the intermediate stages exist
to catch — do the stages in order, even when it's tempting to shortcut
for a one-off analysis.

## RAW

**What it is:** the unmodified response from an external source — full
JSON from OpenDota/STRATZ, or raw `.dem` bytes — captured exactly as
received, alongside capture metadata (source, endpoint/query, fetch
timestamp, HTTP status, request parameters used).

**Rules:**
- Never mutate a RAW record after capture. If a response was wrong or
  needs re-fetching, capture a new RAW record (with its own timestamp),
  don't edit the old one in place. RAW's value is being an honest,
  reproducible record of "what the source actually said, when."
- Store enough request context to know how to re-fetch equivalently
  later (e.g. which endpoint, which match/account ID, any query params).
- RAW format matches the source's natural format — don't force JSON API
  responses into Parquet at this stage; that transformation belongs to
  BRONZE.

**This stage is the foundation of provenance tracking** — see
`data-quality-rules.md` rule 5. If RAW is missing or discarded, later
stages can't be re-derived or audited.

## BRONZE

**What it is:** RAW parsed into typed records close to the source's own
shape, with obviously-broken responses rejected (malformed JSON, HTTP
errors, empty/placeholder responses for private profiles) but no
business/domain rules applied yet.

**Rules:**
- Reject/flag records that are structurally invalid (can't be parsed
  into the expected type at all), but don't yet apply judgment calls like
  "should this match be used for training" — that's FEATURES/DATASET's
  job. BRONZE answers "is this well-formed data," not "is this data
  useful for our purposes."
- Preserve a link back to the RAW record(s) each BRONZE record came from
  (provenance chain).
- Handle private/missing profiles explicitly here (see
  `data-quality-rules.md` rule 6) — a private profile typically returns a
  distinct placeholder/error shape from the API rather than throwing;
  BRONZE should recognize and tag this case rather than let it silently
  parse into a record with nonsensical/default field values.

## NORMALIZED

**What it is:** BRONZE reshaped into this project's own canonical schema
— consistent IDs (one canonical Steam ID form, `match_id` preserved),
consistent units, every record patch- and timestamp-tagged, duplicates
resolved. This is the layer that should align with `dota-domain`'s
suggested data model concepts (Hero, Item, Position, DamageType, etc.) —
NORMALIZED match/player data and `dota-domain`'s structured concepts
should be talking about the same things in the same shapes.

**Rules:**
- Apply ID canonicalization (Steam32 vs Steam64 — see
  `apis-and-identifiers.md`) consistently across every record.
- Attach patch and timestamp to every record — this is where the "always
  attach patch and timestamp" rule (rule 3) gets structurally enforced,
  ideally via a schema that makes those fields non-nullable rather than
  a convention that's easy to forget.
- Deduplicate here (rule 4) — a match re-fetched at two different times,
  or sourced from both OpenDota and STRATZ, should collapse to one
  NORMALIZED record, not two. Dedup key: `match_id`, with a documented
  rule for which source wins on conflict (e.g. prefer the more complete/
  more recently parsed version, and record which source won).
- Preserve provenance links back through BRONZE to RAW.

## FEATURES

**What it is:** NORMALIZED data transformed into model-ready features —
the offline-training-data equivalent of `dota-architecture`'s live
Feature Engine. Business/domain filtering rules apply here (or earlier,
never later):

- Turbo/Ranked separation (rule 1) — matches get tagged with which
  category they belong to; a FEATURES-stage consumer building a training
  set should be selecting a category explicitly, not accidentally
  blending them because the tag wasn't checked.
- Abandoned/incomplete game exclusion (rule 2) — applied based on the
  specific downstream use; don't apply a blanket exclusion so early that
  a legitimately different use case (e.g. studying abandon behavior
  itself) becomes impossible — but for standard "learn good play"
  training data, exclude by default and require an explicit opt-in to
  include abandons.

**Rules:**
- A FEATURES-stage record should be traceable back to its NORMALIZED
  (and further, BRONZE/RAW) origin — don't lose provenance just because
  the shape has changed significantly.
- Feature computation should be deterministic and versioned — if feature
  logic changes, that's a new FEATURES version, not a silent overwrite of
  the old one, since DATASET-stage reproducibility depends on knowing
  exactly which FEATURES version produced a given dataset.

## DATASET

**What it is:** the final train/validation/test split assembled from
FEATURES, ready to hand to a training run.

**Rules:**
- Use a **temporal split** (rule 7) by default — training data from
  before a cutoff, validation/test from after — rather than a random
  split, because random splits on match data tend to leak information
  (players/metas are temporally correlated; a random split can put two
  matches from the same tournament/patch/player streak on both sides of
  the split, which inflates apparent performance without representing
  real generalization).
- Actively check for and prevent train/test leakage (rule 8) — beyond
  just using a temporal split, check for near-duplicate matches, the same
  player/account appearing heavily on both sides in a way that could leak
  player-specific signal the model shouldn't be allowed to use for a
  general task, and any derived feature that could indirectly encode
  future information (e.g. a "final patch balance state" feature applied
  to matches before that patch existed).
- Version and document the exact DATASET build — which FEATURES version,
  which filters applied (Turbo/Ranked selection, abandon exclusion,
  patch range), what the temporal cutoff was — as part of the dataset's
  own metadata, not just in a commit message or someone's memory.

## Cross-stage principle: provenance survives every transformation

At every stage, a record should be traceable back to where it came from
— which RAW capture(s), which BRONZE parse, which NORMALIZED
canonicalization, which FEATURES version. This isn't bureaucracy for its
own sake: when a trained model behaves oddly, the fastest way to find out
why is tracing a suspicious training example back to its original API
response, and that's only possible if provenance wasn't dropped somewhere
along the way. See `data-quality-rules.md` rule 5 and the checklists in
`data-quality-checklists.md`.
