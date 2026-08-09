# Pipeline: Candidate Generation → Scoring → Ranking → Explanation

## Why these stay structurally separate

It's tempting to collapse this into one function: "given context, return
the best item." Resist that — the four stages have different jobs,
different failure modes, and different testing needs, and merging them
makes each of those harder to get right independently:

- A candidate-generation bug (missing a relevant item) is invisible to
  scoring/ranking tests that only exercise items already in the candidate
  set.
- A scoring bug is easiest to catch with unit tests against fixed
  contexts and expected relative orderings — that's much harder to write
  and interpret if scoring is entangled with ranking's tie-breaking and
  diversity logic.
- Explanation quality issues (a confusing or inaccurate reason) should
  never require touching the actual decision logic to fix, and a bug in
  the decision logic should never be masked by an explanation that
  happens to sound reasonable regardless of what was actually decided.

Per `dota-architecture`, this maps onto the Recommendation Engines /
Recommendation Orchestrator layers — candidate generation, scoring, and
ranking are Recommendation Engine responsibilities (consuming Feature
Engine output, never raw `MatchState`); explanation may sit at the
Orchestrator level or beyond, since it's the stage where an LLM boundary
applies.

## 1. Candidate generation

**Job:** produce the set of items worth considering at all, given
hero/role/inventory/gold-trajectory constraints. Optimize for recall —
don't miss a genuinely relevant item — not precision. A candidate that
turns out to score poorly is cheap (scoring will rank it low); a
candidate that's never generated can never be recommended no matter how
good the rest of the pipeline is.

**What it should exclude:** items already owned or functionally
redundant with owned items (see `context-inputs.md`'s inventory section),
items mechanically unavailable to this hero (if any such restriction
exists), and items wildly unaffordable within any reasonable near-future
horizon (distinguish this from "not affordable right now but a
reasonable target soon," which should remain a candidate).

**What it should not do:** apply strategic judgment about whether an item
is *good* for the situation — that's scoring's job. Candidate generation
is closer to a filter for relevance/legality than a quality judgment.

## 2. Scoring

**Job:** assign each candidate a score given the full context
(`context-inputs.md`). This is where the actual "is this good, right
now, for this situation" reasoning happens — combining `dota-domain`
strategic rules with any model-based scoring per `dota-ml`.

**Composition of scoring logic:** scoring doesn't have to be a single
model — it's reasonable (and often clearer) to combine multiple signals:
a rule-based component encoding clear `dota-domain` logic (e.g. "this
item's damage type isn't mitigated by what the enemy has itemized" is a
deterministic, explainable check), and a model-based component (per
`dota-ml`) for signal that's harder to hand-encode. Whatever the
composition, the output must be traceable to `reasonCodes` (see
`output-schema.md`) — a score with no traceable justification is not
useful for the explanation stage and not debuggable when it looks wrong.

**Guard against the core-rule violation here specifically:** this is the
stage where "just use win rate" is most tempting, because raw
purchase-outcome data is the easiest signal to reach for. See
`confidence-and-bias.md` for why naive win-rate/purchase-correlation
fitting is biased, and use context-conditioned, bias-aware signals
instead — anything trained on historical data here is subject to
`dota-ml`'s full discipline (baseline-first, leakage tests, temporal
validation), not exempt from it because it's "just item scoring."

## 3. Ranking

**Job:** order scored candidates and decide what to actually surface (top
pick, ranked alternatives list). Ranking is the natural place to apply
constraints that shouldn't live in scoring itself:

- **Diversity/deduplication** — if two candidates are near-equivalent
  (e.g. two items serving essentially the same purpose), avoid surfacing
  both as if they were meaningfully different top choices; pick the
  stronger one for the primary slot and note the other as a close
  alternative rather than a separate independent recommendation.
- **Confidence-aware demotion** — a high-scoring but low-confidence
  candidate (per `confidence-and-bias.md`) might be ranked below a
  slightly-lower-scoring but well-supported one, depending on how the
  product wants to handle that tradeoff — keep this logic in ranking, not
  buried inside the score itself, so the tradeoff is visible and tunable
  independently of the underlying scoring model.

**What it should not do:** introduce new strategic judgments not already
reflected in scoring — if ranking needs to know something scoring didn't
account for, that's a sign the missing signal belongs in scoring's
context, not a ranking-stage patch.

## 4. Explanation

**Job:** turn the structured output (`itemId`, `score`, `confidence`,
`reasonCodes`) into human-readable reasoning for the player. See
`SKILL.md`'s "LLM boundary" — this is the one stage where an LLM may
participate, and even then, only to narrate, not to decide.

**Constraint:** the explanation must be traceable to `reasonCodes` — an
LLM generating an explanation should be constrained to actually justify
the recommendation using the structured reasons already computed, not
free-associate a plausible-sounding rationale that may not match what
scoring/ranking actually did. If an explanation and a `reasonCodes` list
diverge, that's a bug to fix in the explanation stage's prompt/logic, not
a sign the explanation "found more context" — the explanation stage
doesn't have access to more truth than the structured decision it's
explaining.

**Testing implication:** explanation-stage output quality is evaluated
separately from decision-stage correctness (per `dota-ml`'s "ML decides,
LLM explains" evaluation guidance) — a good explanation of a wrong
recommendation is not a passing test result for the pipeline as a whole.
