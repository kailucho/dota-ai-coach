# Roadmap

## This iteration: Fase 0 + Fase 1 + vertical slice — DELIVERED

Per the user's explicit scope for this iteration:

- [x] Research: current GSI field names/config, sourced and cited (`docs/RESEARCH.md`)
- [x] `docs/RESEARCH.md`, `docs/ARCHITECTURE.md`, `docs/FAIR_PLAY.md`, `docs/ROADMAP.md`
- [x] ADRs for the major architectural decisions (`docs/adr/`)
- [x] .NET 8 solution with the `dota-architecture`-aligned project layout
- [x] Real GSI HTTP listener (Kestrel, loopback) + parser + normalizer with partial-payload merge
- [x] `MatchState` domain model + domain events + `FeedHealth` (staleness)
- [x] GSI replay simulator (in-process + HTTP modes) — fixtures now real captures from a live client (see below)
- [x] `FeatureEngine` (mandatory seam between `MatchState` and recommendation logic)
- [x] `RuleRecommendationEngine` with 4 real rules: next-item, affordability/farm-until-affordable,
      buyback warning, power spike
- [x] `LiveMessageBudget` (anti-spam: cooldowns, change-threshold buckets, 1-3 message cap)
- [x] Minimal WPF overlay: transparent/topmost/non-activating window, hero/clock/KDA/gold/GPM/XPM/LH-DN/items/buyback display
- [x] FairPlay module: classification system, attribute, structural reachability test
- [x] Full test suite (unit + integration + FairPlay + architecture) — all green
- [x] `dotnet build` and `dotnet test` both green

## Known limitations of this iteration

1. ~~**All GSI fixtures are hand-built, not captured from a live Dota 2
   client.**~~ **Resolved.** `laning-sequence/`, `item-purchase/`,
   `hero-death/`, and `buyback/` are now real payloads captured from a
   live bot match via `DotaAiCoach.GsiCapture`. This directly surfaced a
   real data-loss bug no hand-built fixture had caught: the inventory
   model only had 6 slots (`slot0`-`slot5`) when the real payload uses 9
   (`slot0`-`slot8`), silently truncating items placed in slots 6-8; fixed
   in `ItemsSnapshot`/`GsiNormalizer`. See
   `tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/README.md`. `reconnection-gap/`
   remains hand-built — no genuine multi-minute disconnect occurred during
   the capture session; replace it the next time one is naturally
   observed.
2. ~~**Four fields are flagged "verify via fixture"**~~ **Resolved for
   two of the four** (see `docs/RESEARCH.md`): `buyback_cost`/
   `buyback_cooldown` populate reliably (confirmed via a real
   death→buyback-used sequence); `neutral0` is corrected to
   `neutral0`/`neutral1` (2 slots). Item charge key selection and
   `hero.xp` semantics remain unverified — neither was exercised in the
   captured match.
3. **`ItemCatalog` and `NextItemSelector` are a deliberately tiny,
   hardcoded placeholder** (~15 items, one generic progression order),
   not the hero/context-aware candidate-generation/scoring/ranking
   pipeline `dota-item-recommender` describes. Explicitly labeled as such
   in code comments. Concretely observed during live-client validation
   (see below): the selector re-recommends an item the player
   deliberately sold (e.g. an early Bracer sold later for inventory
   space) because it only checks "is this item currently owned," with no
   concept of purchase history, timing, or deliberate build changes.
   Fixing this properly needs the real candidate-generation/scoring
   pipeline, not a quick patch — see `dota-item-recommender`'s skill for
   the intended design (hero/role/patch/bracket/minute/current-items/
   comfort-aware scoring, never a bare "owned or not" check).
4. **No player profile, Steam auth, OpenDota/STRATZ integration, ML
   models, Computer Vision, draft coaching, or postgame analysis** — all
   out of scope for this iteration by explicit instruction.
5. **`LiveMessageBudget` lives inside `RecommendationEngines`**, not a
   standalone `RecommendationOrchestrator` project, since only one engine
   exists so far. Written to be self-contained so the eventual move is
   mechanical.
6. **Overlay is functionally minimal**: no compact/expanded mode toggle,
   no drag-to-reposition, no multi-monitor/DPI-aware repositioning logic,
   no hotkeys. Window behavior (transparent/topmost/non-activating) and
   the click-through Win32 interop hook are real and tested by
   construction, but click-through is off by default and the safe-zone/
   draggable-configuration features `dota-overlay` describes are not yet
   built.

## Fase 2 — Player Profile — DELIVERED

- [x] `DotaAiCoach.PlayerProfile` (pure scoring/domain, no HTTP) +
      `DotaAiCoach.OpenDota` (HTTP client, RAW JSON cache, rate limiter,
      DTO→domain mapper) as new projects, wired through `DotaAiCoach.Host`'s
      `IProfileSession`/`ProfileSessionFactory` (mirrors the GSI firewall —
      see `docs/adr/0012`).
- [x] **Manual Steam ID entry, not Steam OpenID** (`docs/adr/0013`) — GSI
      already reports the local player's Steam64, so the profile window
      pre-fills from it when a feed is active; nothing is authenticated,
      since only public OpenDota data is read.
- [x] **JSON RAW cache, not SQLite** (`docs/adr/0011`) — deliberate
      deviation from `docs/MASTER_PLAN.md`'s "SQLite" line; SQLite is
      deferred to Fase 3's bulk ingestion pipeline, where this cache
      becomes the RAW layer rather than being discarded.
- [x] Hero Comfort Score (0-100) with Bayesian shrinkage toward the
      player's own prior *and* a hard volume-based cap, so a small lucky
      sample cannot reach 100 (`docs/adr/0015`) — verified against the
      MASTER_PLAN's literal "2 games / 2 wins ≠ 100" requirement in tests,
      and against the user's real 127-hero OpenDota profile in production.
- [x] Dota DNA v1: 4 of 11 dimensions actually computed (Farm, Fighting,
      Survival, Late Game), the other 7 explicitly marked with a truthful
      unavailability reason rather than a fabricated score
      (`docs/adr/0014`). **Extended same-phase**: 6 more dimensions
      (Laning, Objectives, Vision, Itemization, BuybackDiscipline, Tempo)
      unlocked via an explicit, user-triggered "Deep DNA Refresh" that
      parses up to 20 recent matches on OpenDota — 10 of 11 dimensions now
      computed; only Positioning stays deferred, since parsed data there
      is heatmap-shaped and judged insufficient for an honest score
      (`docs/adr/0016`). Verified live: the Deep DNA Refresh button was
      exercised against the user's real account and produced 20 cached
      parsed matches and real scores across 10 dimensions.
- [x] Profile shown in a separate WPF window (not overlay navigation),
      offline-first (loads from cache on open, never auto-fetches).
- [x] Private/missing OpenDota profiles handled as a first-class state
      (`ProfileAvailability.MatchHistoryPrivate`/`PlayerNotFound`), per
      `dota-data` rule 6 — verified against the user's own account, which
      was genuinely private (`fh_unavailable` + empty match history) at
      the start of this phase and correctly showed the private-profile
      message before OpenDota re-indexed the account.
- [x] FairPlay: `PlayerProfileSnapshot`/`HeroReference` classified
      `PublicPreflight`/`ExternalApi`; a dedicated structural test
      (`ProfileFairPlayTests`) asserts no `PlayerProfile` type is
      reachable from `LiveFeatureSet`, enforcing the "profile does not
      feed live recommendations yet" scope decision at build time.
- [x] `dotnet build`/`dotnet test` green across all 8 projects (189 tests
      total after this phase, including the parsed-match extension).

**Known gap carried forward**: `MatchupFamiliarity` (a Hero Comfort Score
component) is modeled but left `null` — OpenDota's `/heroes` endpoint only
exposes matchup data at the player level, not per played-hero, and a
future phase should find a proper per-hero data source rather than
approximate it (see `docs/adr/0015`). `Positioning` (a DNA dimension) is
left `NotYetImplemented` — parsed match data exists (`lane_pos`/
`position_est`) but is heatmap-shaped, not a real trajectory signal (see
`docs/adr/0016`).

## Fase 3 (scoped) — Win Probability baseline — DELIVERED

Scoped by explicit user decision to one model: `P(win | player-visible
state at timestamp T)`, no ONNX export, no live wiring, no other models
(draft/item ranker deferred). Full detail in `docs/adr/0017`.

- [x] `tools/DotaAiCoach.Training/` — Python pipeline outside the .NET
      solution, own venv, `dota-architecture`'s allowed separate toolchain.
- [x] **Critical correction caught before training**: `game_mode==22` is
      Ranked All Draft, `23` is Turbo — the opposite of this project's
      prior assumption. Verified live against
      `/constants/game_mode`; would have silently mixed Turbo into the
      "ranked" dataset if uncaught (`dota-data` rule 1). Guarded by a
      regression test.
- [x] Data acquisition: a verified 3-step funnel (`/parsedMatches` →
      `/explorer` filter → `/matches/{id}`) landed **1863 real ranked
      matches** (1912 fetched, 49 excluded as abandoned) spanning ~8
      months of real calendar time, at zero incremental parse cost
      (matches were already parsed on OpenDota).
- [x] RAW→BRONZE→NORMALIZED→FEATURES→DATASET pipeline (Parquet, no
      SQLite — see `docs/adr/0011`'s updated status), all 8 `dota-data`
      quality rules enforced as automated tests, not manual review.
- [x] 18-feature timestamp-bound feature set + `features_spec.yaml` (the
      Python-side parallel to C#'s `[FairPlaySource]`), with an automated
      leakage re-derivation test verified against the real captured match
      fixture from Fase 2.
- [x] Four-way temporal split (train/val/calib/test), calibration fit on
      a dedicated third split, **test touched exactly once**.
- [x] Logistic Regression vs. LightGBM: **LightGBM did not beat the
      simpler model** on either run — the pipeline correctly kept
      Logistic Regression, the "permission to stop at baseline" outcome
      `dota-ml` treats as a good result, not a shortfall.
- [x] Final test metrics (touched once, after the regularization fix
      below): **AUC-ROC 0.839, log loss 0.506, Brier 0.162** — beats the
      constant-0.5 baseline decisively and comfortably beats the
      single-feature baseline too. Segmented evaluation confirms the
      expected early-game-weaker (AUC 0.762) / late-game-stronger
      (AUC 0.900) pattern; all domain sanity checks (monotonicity,
      near-even-at-zero, symmetry) pass.
- [x] **Regression investigated and fixed** (`docs/adr/0018`): the first
      trained model scored val AUC 0.741 — *worse* than a single-feature
      baseline (0.793) — traced to severe multicollinearity among the
      economic-advantage features (`gold_adv_at_T`/`xp_adv_at_T`/their
      slope variants correlate 0.63-0.91) combined with too-weak default
      L2 regularization. A val-selected `C` grid search fixed it (val AUC
      0.741→0.792) with **no features removed** — confirmed by a control
      that pruning alone barely moved AUC (0.740), isolating
      regularization strength as the actual cause.
- [x] 38 automated tests (leakage, features, split ordering, data
      quality, patch-pinning) — all green, all against real captured
      match data, not synthetic-only fixtures.
- [x] Full reproducibility record (`metadata.json`, including the full
      `C` search results) and evaluation record (`evaluation.json`)
      committed alongside the model.

**Known limitations, reported honestly rather than hidden** (full detail
in `docs/adr/0017`/`docs/adr/0018`'s Consequences): all 1863 matches
landed on a single patch, so by-patch segmentation and patch-robustness
are untested; the gold-advantage monotonicity sanity check now shows a
much flatter response to any single feature swept in isolation (an
expected side effect of strong regularization, not a new bug — the
model relies on the combination of correlated signals together); this
model mixes `POSTGAME_ONLY` features (enemy exact gold/items) by explicit
user choice for baseline signal strength and is **research-only** — not
live-eligible without a retrain restricted to `features_spec.yaml`'s
`LIVE_ALLOWED` subset.

## Fase 3 (scoped) — Draft baseline — DELIVERED

Scoped to `dota-ml`'s own baseline list for Draft: hero global winrate +
matchup matrix + logistic regression, reusing the win-probability
model's dataset/split infrastructure. Full detail in `docs/adr/0019`.

- [x] `models/draft_baseline.py` — one row per match (no timestamp
      dimension), Bayesian-shrunk hero winrate table + shrunk matchup
      matrix, Logistic Regression over both.
- [x] **Real bug found and fixed**: the first version's hero winrate
      table had no shrinkage at all (raw empirical rates, e.g. 100% on 71
      games) — exactly `dota-ml`'s "8/10 games, noisy and overconfident"
      failure mode. Caught by the domain sanity check (top-5-winrate
      heroes should beat bottom-5) actually **failing** — the unshrunk
      model predicted the opposite direction. Fixed with the same
      shrinkage discipline already used for the matchup matrix; the
      sanity check now passes decisively (79% vs 33%). Permanent
      regression tests added.
- [x] **Honest result**: draft alone barely predicts outcome in this
      dataset — final test AUC **0.531**, only marginally above chance.
      Reported as a genuine finding, not tuned or hidden: at pub-bracket
      ranked skill levels, draft quality matters far less than in-game
      execution, which is consistent with the Win Probability model
      (which observes live state) scoring AUC 0.839 on the same match
      population.
- [x] Investigated an implausibly strong train-only feature correlation
      (0.87) before trusting or dismissing it, per `dota-ml`'s ablation-
      sanity guidance — confirmed it was overfitting to a sparse matchup
      table (held-out val correlation ~0.00), not a leakage bug.
- [x] 43 automated tests total (5 new for this model) — all green.

**Known limitation**: this baseline's predictive power is too weak to
use for any real recommendation as-is. It exists as the required
baseline and as a concrete scoping data point — a future draft model
needs more training data, richer features, or both (see
`docs/MASTER_PLAN.md`'s fuller Draft Coach representation for what those
richer features would look like).

## Fase 3 (scoped) — Item Recommendation ranker — DELIVERED

Third and final model of `docs/MASTER_PLAN.md`'s ML Baselines module, a
ranking task (LightGBM LambdaMART), evaluated with NDCG/MRR rather than
AUC. Full detail in `docs/adr/0020`.

- [x] Two real data-quality bugs found and fixed before training: (1) a
      hand-blacklist of "meaningful items" let ~350k noise rows through
      (cheap components like `ogre_axe`/`gauntlets` dominating by raw
      frequency); replaced with a data-driven whitelist from OpenDota's
      own `/constants/items` `qual` field (129 real equipment-tier items).
      (2) ~2.3% of rows were repurchases of an already-owned item,
      structurally unscoreable against a candidate pool that excludes
      owned items; filtered at extraction time.
- [x] Two baselines per `dota-ml`'s progression: global item popularity
      ("rule" tier) and per-hero Bayesian-shrunk popularity
      (frequency-conditioned tier) — hero-conditioning alone more than
      doubled NDCG@3 (0.191 → 0.409), confirming item choice is strongly
      hero-dependent.
- [x] **LightGBM LambdaMART ranker beat both baselines decisively**: test
      NDCG@3 **0.544**, NDCG@5 **0.587**, MRR **0.557** (touched once).
      This is the strongest of the three Fase 3 models — unlike the draft
      baseline's weak signal, item choice given (hero, minute, items
      owned) is genuinely learnable at this dataset's scale.
- [x] Segmented by minute bucket: early-game NDCG@3 0.715 (most
      standardized builds), late-game 0.351 (most situational) — expected
      pattern, confirmed rather than assumed.
- [x] Domain sanity check: ranker agrees with the per-hero baseline's
      top pick 47.5% of the time, vastly above the 0.9% chance rate.
- [x] Zero `POSTGAME_ONLY` features — unlike the win-probability model,
      every feature here is legitimately live-knowable.
- [x] A real performance bug found and fixed: the initial NDCG/MRR
      implementation called sklearn's `ndcg_score` in a per-query Python
      loop and effectively hung on ~9M candidate rows; rewritten as a
      vectorized closed-form computation (valid for this task's
      single-relevant-document-per-query structure), cutting evaluation
      time from an unbounded hang to ~1.5s per 2M rows.
- [x] 49 automated tests total (6 new for this model) — all green.

**Known limitation**: the candidate pool is not gold-affordability-filtered
in this v1.0 (would need a new join with the win-probability model's
minute-indexed gold data) — so test NDCG numbers are a lower bound
relative to a filtered v1.1. `Expected Win Uplift(item|state)`
(MASTER_PLAN's stretch goal) is explicitly deferred: computing it naively
would be exactly the survivor-bias trap the module's own Divine Rapier
example warns against.

## Fase 3 — ML Baselines module — COMPLETE

All three baselines `docs/MASTER_PLAN.md`'s ML Baselines module calls for
now exist, each honestly evaluated: Win Probability (AUC 0.839, strong),
Item Recommendation (NDCG@3 0.544, strong), Draft (AUC 0.531, weak — a
real, reported finding, not a shortfall to hide). See `docs/adr/0017`
through `0020` for full detail on each.

## Recommended next phase

> **Note (2026-08-10): this section is stale.** Everything it
> recommends — the multi-patch RAW→BRONZE→NORMALIZED→FEATURES→DATASET
> rebuild, ONNX export, and live wiring of all three ML models into the
> overlay — was actually completed in sessions between this document's
> last commit (`8bae0b1`) and today. This file was accidentally
> overwritten mid-session (a `Write` call replaced the whole document
> instead of appending), and the sections that documented that work are
> not recoverable — they were never committed to git, so there is no
> history to restore them from. Left as-is below rather than rewritten
> from memory, since guessing at lost content risks introducing
> inaccuracies into a document whose purpose is to be an accurate
> record. The two dated sections at the end of this file (Ask LLM panel,
> Item Ranker rank-tier retrain) are current and accurate — they were
> written directly from this session's verified work, not reconstructed.

A genuinely production-scale RAW→BRONZE→NORMALIZED→FEATURES→DATASET
rebuild (the ~1863-match dataset built across all three Fase 3 models
becomes a seed, not a discard, for that rebuild) is the natural next step
before ONNX export and live wiring are considered for any of these three
models.

Do **not** skip ahead to Computer Vision or advanced personalization
before this — per `docs/MASTER_PLAN.md`'s explicit sequencing and
`dota-ml`'s baseline-first principle.

## Deferred phases (from `docs/MASTER_PLAN.md`, unchanged)

Fase 3 remainder (draft/item-ranker baselines, ONNX, live ML wiring) ·
Fase 5 (Draft CV) · Fase 6 (Postgame) ·
Fase 7 (Advanced personalization) · Fase 8 (Macro coach: objectives/
Roshan/high ground/death cost) · Fase 9 (Advanced CV) · Fase 10
(Refinement: ONNX optimization, installer, auto-updater, UX polish).

## Added: manual "Ask LLM" panel (LLM-refinement path, not GSI-based)

Given the `draft`-block closure above, the user's actual prior workflow
for enemy-hero-aware recommendations was manual: screenshot their own
scoreboard (which legitimately shows enemy heroes + KDA — visible on
their own screen, `VISIBLE_SCREEN` per `dota-fairplay`, same basis as
the deferred Fase 9 CV path) and paste a description into ChatGPT at
specific match minutes. Rather than building full CV first, this phase
ports that manual workflow directly into the app as a standalone,
immediately-useful feature, and lays the groundwork to later use its
output to refine the trained ML Item Ranker.

**Provider: OpenAI** (not Claude) — chosen explicitly by the user, who
had a reference implementation for the vision-extraction piece in a
sibling project, `D:\KAI\proyectos\dota-plus-free`
(`packages/server/src/routes/tickExtract.ts`): OpenAI Responses API +
vision + a forced function-call with a strict JSON Schema. This
project's implementation ports the same prompt/schema shape onto the
official OpenAI .NET SDK's Chat Completions + Structured Outputs
(`ChatResponseFormat.CreateJsonSchemaFormat`) — same guarantee (valid,
schema-conformant JSON, no free text), different SDK surface. An earlier
draft of this feature used Claude/Anthropic for the whole flow; it was
replaced end-to-end (both the vision extraction and the build-
recommendation text) once the user chose OpenAI as the single provider,
to avoid needing two API keys.

- [x] `DotaAiCoach.LlmCoach` project — OpenAI C# SDK client, two pieces:
      - `OpenAiScoreboardExtractor` (`IScoreboardExtractor`) — vision +
        Structured Outputs, extracts enemy hero level/KDA from a
        player-captured scoreboard screenshot. Ports
        `tickExtract.ts`'s prompt and schema shape
        (`hero`/`level`/`kda.{k,d,a}`), restricted/normalized against an
        optional expected-enemies hint, same as the reference project.
      - `OpenAiSituationAdvisor` (`ISituationAdvisor`) — the final build
        recommendation call, given the extracted scoreboard + typed own
        hero/KDA.
      Both wired through Host only (`ILlmCoachSession`/`LlmCoachSession`)
      per the same UiOverlay-never-sees-infrastructure firewall as
      OpenDota/MlInference (see the architecture test
      `UiOverlay_references_no_LlmCoach_or_OpenAI_directly_or_transitively`).
- [x] `ManualSituationInput`/`ScoreboardExtractionResult`/
      `LlmSituationAdvice` domain types (`DotaAiCoach.Domain.LlmCoach`).
      `EnemyScoreboard` (extracted, structured) replaced the original
      free-text `EnemyKdaSummary` field — the player now uploads/drags a
      screenshot instead of typing enemy KDA by hand, closer to their
      actual prior manual workflow. Own hero/KDA stay as typed text
      (trivially known, no need to screenshot). All fields are either
      player-typed or extracted from a player-captured image of their
      own screen — `fairPlayClassification: VISIBLE_SCREEN`, `source`:
      manual player input / player-captured screenshot (not GSI/CV
      automation), `availability`: any time during a live match,
      `justification`: identical basis to the deferred CV path, just
      without the always-on automated pixel-recognition step.
- [x] `AskLlmWindow` — a separate window (mirrors `ProfileWindow`'s
      precedent), reachable via a new "Ask LLM" button next to "Profile"
      on the main overlay. Drag-and-drop or file-picker screenshot
      upload, auto-extracts on selection (mirrors `TickForm.tsx`'s
      auto-extract-on-drop UX), shows a live preview + extraction
      summary before the player asks for a recommendation. Not part of
      the live `ItemPlan`/message feed — user-triggered, no
      `LiveMessageBudget` cooldown gating applies (`dota-live-coach`
      governs the automatic live feed, not an explicit on-demand query).
- [x] `SituationLogWriter` — appends every (input, LLM response) pair as
      JSONL to `%LocalAppData%\DotaAiCoach\llm-situations\situations.jsonl`.
      This is the durable dataset for the user's stated long-term goal
      ("me gustaria que a futuro nuestro modelo ya este bien pulido") —
      using accumulated LLM responses to eventually refine the trained
      Item Ranker. No automatic training pipeline consumes this file yet;
      that's future work once enough situations have accumulated.
- [ ] **Not yet done**: an actual retraining step that consumes
      `situations.jsonl` to refine the Item Ranker. Deliberately deferred
      until there's enough logged data to be useful — revisit once the
      user has used the panel across a meaningful number of real
      matches/minutes.
- [ ] **Not yet done**: `OPENAI_API_KEY` was not yet configured on the
      user's machine as of this writing — `LlmCoachSession.IsAvailable`
      degrades gracefully (panel shows "no API key configured" instead of
      failing) until it is. End-to-end verification with a real API call
      (both the vision extraction and the recommendation) is still
      pending.

## Item Ranker retrained with `avg_rank_tier` feature (2026-08-10)

Closes the pending "retrain once bracket acquisition finishes" work
queued earlier. The bracket-4-6 (Archon-Ancient) data acquisition
process (`acquire_by_bracket.py`, target 3000 matches) stalled at
2799/3000 (93%) on persistent OpenDota 429 rate-limit errors and the
background process died without completing — confirmed via `ps aux`
(no longer running) and the absence of a "bracket acquisition complete"
log line. Per explicit user decision, proceeded with the partial data
rather than restarting the acquisition.

**Pipeline rebuilt end-to-end, each stage verified before the next:**

1. `bronze.parquet` rebuilt from all of `data/raw/` (5674 well-formed
   matches, up from the pre-bracket-run baseline) — `avg_rank_tier`
   populated for 5669/5674 rows (range 12-75) via the existing sidecar.
2. `normalized.parquet` rebuilt (5674 rows, 0 dropped).
3. `item_events.parquet` rebuilt (207,326 (player, purchase) rows, 236
   abandoned matches excluded). Verified the bracket-run's contribution
   directly: 1202 of 1413 bracket-4-6 matches produced qualifying
   purchase rows (the rest either had no meaningful purchases or were
   abandons).
4. New dataset `item-events-2780-20260810` built (train 1668 / val 417 /
   calib 278 / test 417 matches). Verified bracket 4-6 coverage went
   from 209 matches (the original scarcity problem) to **664 matches** —
   a real improvement despite the acquisition stopping short.
5. `avg_rank_tier` added as a 6th Item Ranker feature
   (`tools/DotaAiCoach.Training/src/dota_training/models/item_ranker.py`):
   joined from `bronze.parquet` by `match_id`, missing values filled with
   the TRAIN split's median (71.0, computed fresh — not hardcoded — and
   persisted in both `model.pkl` and `metadata.json` so C# can read the
   same fallback the model was actually trained with, never an
   arbitrary default).
6. Retrained (`item-ranker-v1.0-20260810T203622Z`). **Metrics held, no
   regression**: NDCG@3 (val) 0.5237 vs. the 0.524 baseline (~0.0003
   difference, within noise); test NDCG@3 0.5234, MRR 0.5388 (touched
   once). Domain sanity check passed. Feature importance: `avg_rank_tier`
   contributes real but modest signal (importance 11, weakest of the 6
   features) — independently confirmed on the Python side that ~16% of
   randomly sampled feature combinations are genuinely rank-tier-
   sensitive (score changes between Herald and Immortal), the rest are
   not, which is expected at this still-modest bracket-4-6 sample size.
7. Exported to ONNX (parity validated, max abs diff 1.19e-6, well under
   the 1e-3 tolerance). Bundled model updated in
   `src/DotaAiCoach.MlInference/Models/item-ranker/` — `model.onnx`,
   `onnx_export_metadata.json`, `hero_item_popularity.json` (regenerated
   via `scripts/export_popularity_table.py`), and (new) `metadata.json`
   bundled for the first time so `ItemRankPredictor` can read
   `avg_rank_tier_fill` directly from the model that produced it.
8. C# updated to match the new 6-feature shape:
   - `ItemRankInput.AverageRankTier` (Domain) — new required field.
   - `IItemRankPredictor.AverageRankTierFill` — new interface member,
     giving callers the model's own training-time fallback instead of
     an arbitrary default when the player's rank tier isn't known yet.
   - `ItemRankPredictor` (MlInference) — reads `metadata.json` at
     construction, feeds `AverageRankTier` into the ONNX tensor at the
     correct position.
   - `NextItemSelector`/`NextItemRule` (RecommendationEngines) — thread
     `RuleContext.AverageRankTier` through to `ItemRankInput`, falling
     back to `itemRankPredictor.AverageRankTierFill` when unknown.
9. Full test suite green: **336/336 C# tests** (`dotnet test
   DotaAiCoach.sln`), **59/59 Python tests** (`pytest`). Fixed 3
   pre-existing `HeroItemPopularityTableTests` hardcoded expected values
   (stale from the old bundled popularity table — legitimately different
   numbers now, independently re-verified against the new JSON file
   directly, not just read back from the C# loader). Regenerated the
   `item_ranker_parity.json` ONNX-parity fixture for the new model/shape.
10. Live verification: `LiveItemPlanSmokeTests` (new,
    `tests/DotaAiCoach.Host.Tests`) exercises the **actual bundled ONNX
    model** (not a fake) end-to-end — constructs a real `ItemRankPredictor`
    + `HeroItemPopularityTable.LoadFromBundledModels()`, confirms
    `NextItemSelector.SelectByPhase` still produces a real multi-phase
    item plan for a known hero, and confirms `AverageRankTier` genuinely
    changes the ONNX model's output for a Python-verified rank-tier-
    sensitive input (not a flaky arbitrary point — an initial attempt
    with an insensitive input combination correctly failed and was
    replaced with one independently confirmed sensitive on the Python
    side first).

**Known gap, not addressed by this pass:** `docs/ROADMAP.md` itself was
found to have been overwritten (only this file's most recent two
sections survived) rather than appended to in an earlier turn — the
original Fase 0-9 history, known-limitations list, and prior "Recommended
next phase" notes are not recoverable from this file anymore. Flagged to
the user; not reconstructed here since doing so from memory risks
introducing inaccurate history into a document whose whole purpose is to
be an accurate record.
