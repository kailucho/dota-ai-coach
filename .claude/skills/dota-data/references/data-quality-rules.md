# Data Quality Rules

These eight rules exist because each one maps to a specific, plausible
way this pipeline could produce a model that looks fine and is actually
wrong. None of these are stylistic preferences — treat a violation as a
correctness bug in the dataset, not a nitpick.

## 1. Never mix Turbo with normal Ranked training data by default

**Why:** Turbo games have different mechanics (faster gold/XP, different
map dynamics, generally lower-effort play) that produce systematically
different patterns than Ranked All Pick. A model trained on a blend
learns an average of two different games, which is a worse fit for
either one — it'll underperform for both Turbo-specific and
Ranked-specific coaching.

**How to apply:** classify every match's category at NORMALIZED/FEATURES
time using both `game_mode` and `lobby_type` (see
`apis-and-identifiers.md` — Turbo is a `game_mode` value, not a
`lobby_type`). Any DATASET build should select a category explicitly. If
a genuine cross-mode use case comes up, that's a deliberate, documented
exception — not a default behavior.

## 2. Exclude abandoned/incomplete games when inappropriate

**Why:** An abandoned game reflects one player's decision to leave, not
representative play from either team for the rest of the match — item
builds, timings, and outcomes after an abandon don't reflect normal
strategic play. Training on them teaches the model patterns that are
artifacts of someone quitting, not good Dota.

**How to apply:** "when inappropriate" is deliberately not "always" —
some tasks (e.g. studying abandon behavior itself, or filtering it out
downstream instead of upstream) may need abandons included. Default to
excluding them for anything training a "good play" model, and always tag
matches with their completion status explicitly in NORMALIZED so a
FEATURES/DATASET consumer can make an informed, deliberate choice rather
than inheriting whatever the last person happened to filter.

## 3. Always attach patch and timestamp

**Why:** Dota's balance changes meaningfully between patches — an item
build that's correct on one patch can be wrong two patches later. A
record without patch/timestamp can't be correctly filtered into a
patch-appropriate training set, and worse, silently mixing patches
produces a model that's an unlabeled blend of multiple different games'
worth of balance.

**How to apply:** enforce this structurally at NORMALIZED — patch and
timestamp should be non-nullable fields on every match-level record, not
optional metadata that's easy to skip. See `apis-and-identifiers.md` for
how to obtain patch when it isn't directly present on the source record.

## 4. Avoid duplicate matches

**Why:** A match counted twice (re-fetched at two different times, or
present in both an OpenDota and a STRATZ ingestion run) silently doubles
its influence on any aggregate statistic or training signal, which biases
results without any visible error — nothing crashes, the numbers are just
quietly wrong.

**How to apply:** deduplicate at NORMALIZED using `match_id` as the
dedup key (see `pipeline-stages.md`). Document and apply a consistent
tie-breaking rule when the same match appears from multiple sources or
multiple fetches (e.g. prefer the most complete/most recently parsed
version), and record which source/fetch won so it's auditable.

## 5. Track data provenance

**Why:** When a trained model does something surprising, the fastest
diagnosis path is tracing a specific training example back to its
original source data. Without provenance, that's not possible — you're
stuck guessing whether it was a data bug, a feature bug, or a genuinely
hard case.

**How to apply:** every record at every stage (BRONZE, NORMALIZED,
FEATURES) should carry a traceable link back through the pipeline to its
RAW origin (source, endpoint, fetch timestamp — see `pipeline-stages.md`'s
RAW section). Don't treat provenance as something to bolt on later; it's
cheapest to carry forward from the start and expensive to reconstruct
after the fact once RAW records have been discarded or overwritten.

## 6. Handle private/missing profiles

**Why:** A private Steam profile or a missing/deleted account doesn't
return normal data — it returns a distinct (often near-empty or
error-shaped) response. Silently treating that as "just another player
record" can inject nonsensical default values (e.g. zeroed stats that
look like a real low-performing player) into downstream data.

**How to apply:** detect and explicitly tag private/missing profile cases
at BRONZE (see `pipeline-stages.md`). Downstream stages should be able to
tell "this player's data is genuinely unavailable" apart from "this
player had zero of something," and FEATURES/DATASET logic should decide
explicitly whether such matches are usable at all (e.g. a match may still
be usable in aggregate even if one player's account-level profile is
private, since match-level data often doesn't require the profile).

## 7. Prefer temporal dataset splits

**Why:** Dota matches are temporally correlated — metas shift, patches
change balance, and even individual players' behavior drifts over time.
A random split can place near-identical-context matches (same patch,
same week, sometimes same tournament or same player's session) on both
sides of the train/test boundary, which inflates apparent test
performance without proving the model generalizes to genuinely new
situations (like a future patch).

**How to apply:** split by date — train on data before a cutoff, validate/
test on data after it — as the default DATASET construction method (see
`pipeline-stages.md`). A random split should require explicit
justification for the specific task at hand, not be the default choice
made out of convenience.

## 8. Never cause train/test leakage

**Why:** Leakage — any way information from the test set influences
training, directly or indirectly — produces evaluation numbers that look
good but don't predict real-world performance, which is worse than not
evaluating at all because it creates false confidence.

**How to apply:** beyond the temporal split itself (rule 7), actively
check for:
- near-duplicate matches split across train and test (not just exact
  `match_id` duplicates — rule 4 covers those; this is about
  substantively similar matches, e.g. the same scrim/lobby appearing
  twice under different match IDs if that's possible for this data
  source);
- the same player/account contributing heavily to both train and test in
  a way that could leak player-specific signal for a task meant to
  generalize across players;
- any feature that could indirectly encode future information relative
  to when a given match happened (e.g. a "current patch balance"
  reference-data join that should be pinned to the match's own patch, not
  the latest patch as of dataset-build time).

Treat a discovered leakage source as a DATASET-stage bug requiring a
rebuild, not something to patch around in the training code — leakage
fixed downstream of dataset construction is easy to reintroduce the next
time the dataset is rebuilt.
