# Correlation, Association, and Causality

## Why the item-purchase example is the canonical mistake

"You bought Black King Bar, then won the game" contains zero causal
information — it's a correlation between two events in one game, and
presenting it as "buying BKB won you the game" is a category error, not
just an oversimplification. This exact mistake — mistaking within-game
temporal sequence for causation — is worth naming explicitly because
it's the most tempting shortcut available: it requires no statistics at
all, just noticing that a purchase preceded a win, which makes it
dangerously easy to generate automatically and present confidently.

## The three tiers, precisely

**Correlation (single-game, temporal):** two things happened in the same
game, one before the other. This is observational noise until it's been
checked against a reference group — a single game provides no basis for
any generalizable claim. The system should essentially never surface a
single-game correlation as an insight on its own; at most, it can be
context supporting an association-level or mechanism-based claim (see
below).

**Association (reference-group statistical relationship):** across a
comparison group (per `comparison-groups.md`), some pattern holds more
often than chance would predict — e.g. "players in your bracket who
finished BKB before minute 18 on this hero won more often than those who
finished later." This is a real, checkable statistical claim, but it's
still not causal — see "Why association isn't causation" below. Present
association-level claims with explicit hedged language ("associated
with," "players who did X tended to," "correlated with") — never "caused"
or "won you the game" phrasing.

**Causality (X actually changed the outcome):** requires isolating X's
effect from the confounding factors that also influence both X and the
outcome. Observational match data (no randomization, no controlled
counterfactual) essentially never supports a strong causal claim on its
own. This project should treat causal language as reserved for cases with
strong, explicit, articulable justification (e.g. a clear, direct
mechanical relationship established via `dota-domain` reasoning — "this
item's active directly countered the enemy's only reliable disable,
which is why the ensuing fight was winnable" is a mechanism-grounded
claim, closer to legitimate causal reasoning than a purely statistical
one) — and even then, phrase it as "likely contributed to" rather than
"caused," since match outcomes are almost always multi-causal.

## Why association isn't causation, concretely

The same selection-bias mechanism `dota-item-recommender`'s
`confidence-and-bias.md` documents for item purchase data applies
directly here: players who bought item X in a given situation typically
already had information/comfort/game-state advantages that led them to
that choice, and that same underlying advantage (not the item itself)
may be what's actually driving the higher observed win rate. A
statistically real association can still be substantially confounded —
acknowledging the association is real doesn't license concluding the
item was the cause.

## Phrasing guidance

| What you actually know | Acceptable phrasing | Not acceptable |
|---|---|---|
| Single-game correlation only | (generally don't surface alone) | "Buying X won you the game" |
| Reference-group association | "Players in your bracket who did X won more often" | "X caused you to win" / "X is why you won" |
| Mechanism-grounded reasoning (via `dota-domain`) | "X likely helped because it directly countered Y" | "X definitely won the fight" |

## What this means for insight generation

Any insight-generation logic (rule-based or model-based, per `dota-ml`'s
governance for the model-based case) must track *which tier* a given
observation falls into as a first-class property, not just compute a
number and rely on downstream phrasing templates to "soften" it
appropriately after the fact — tier should determine which phrasing
templates are even eligible to be used for that observation, so a
correlation-tier finding can't accidentally get routed through a
causal-sounding template. See `dota-ml`'s "ML decides, LLM explains"
principle if an LLM is involved in generating report language: the LLM
must not be permitted to escalate a correlation/association-tier
finding into causal-sounding language for the sake of a punchier
sentence — that's exactly the kind of decision-versus-narration boundary
violation that principle exists to prevent.

## Testing implication

Per `dota-testing`, any post-game insight-generation logic should have
tests asserting that generated language matches its underlying evidence
tier — e.g. a test fixture representing a single-game-only correlation
should never produce output containing causal language, checked via
explicit assertions on phrasing patterns, not just spot-checked
manually.
