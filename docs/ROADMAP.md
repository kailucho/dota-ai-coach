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
      (`docs/adr/0014`).
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
- [x] `dotnet build`/`dotnet test` green across all 8 projects (169 tests
      total after this phase).

**Known gap carried forward**: `MatchupFamiliarity` (a Hero Comfort Score
component) is modeled but left `null` — OpenDota's `/heroes` endpoint only
exposes matchup data at the player level, not per played-hero, and a
future phase should find a proper per-hero data source rather than
approximate it (see `docs/adr/0015`). `Tempo` (a DNA dimension) is
similarly left `NotYetImplemented` for the same reason (see
`docs/adr/0014`).

## Recommended next phase

1. **Capture real OpenDota parsed-match fixtures** to unlock the 7
   deferred Dota DNA dimensions (Laning, Objectives, Vision, Itemization,
   Buyback Discipline need `POST /request/{match_id}` parsed data;
   Positioning needs raw replay parsing) — the natural continuation of
   Fase 2, not a new phase.
2. **Fase 3 — ML offline baselines**: this is also when the JSON RAW cache
   built for Fase 2 should feed into `dota-data`'s full
   RAW→BRONZE→NORMALIZED→FEATURES→DATASET pipeline (DuckDB/Parquet),
   superseding the "SQLite deferred" note in `docs/adr/0011`.

Do **not** skip ahead to Computer Vision or advanced personalization
before these — per `docs/MASTER_PLAN.md`'s explicit sequencing and
`dota-ml`'s baseline-first principle.

## Deferred phases (from `docs/MASTER_PLAN.md`, unchanged)

Fase 3 (ML offline baselines) · Fase 5 (Draft CV) · Fase 6 (Postgame) ·
Fase 7 (Advanced personalization) · Fase 8 (Macro coach: objectives/
Roshan/high ground/death cost) · Fase 9 (Advanced CV) · Fase 10
(Refinement: ONNX optimization, installer, auto-updater, UX polish).
