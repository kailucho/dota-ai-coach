# New Module Checklist

Work through this before writing code for a new project/module, and
again before considering it done. Most architecture drift happens at
module creation time — a project reference added because it was
convenient, a type placed one layer too early — so catching it here is
much cheaper than catching it in review later.

## Before writing code

- [ ] **Which layer does this belong to?** Match it against
  `layers.md`'s "owns" descriptions. If it doesn't fit cleanly into one
  layer, consider whether it should be split rather than forcing a home
  for the whole thing.
- [ ] **What does this layer's row in the dependency graph say it may
  reference?** (`dependency-rules.md`) Write down the expected reference
  list before creating the `.csproj` — if the module you're building
  seems to need a reference not on that list, stop and reconsider the
  design rather than adding the reference and rationalizing it after.
- [ ] **Does this module need to be testable headless?** (No WPF/WinUI,
  no live GSI connection, no live screen.) If yes — and most non-UI
  modules should be — confirm the planned dependencies don't quietly pull
  in a UI or live-connection requirement.
- [ ] **Does this module touch data that needs FairPlay classification?**
  (New GSI field, new CV feature, new ML feature, anything touching
  opponent information or replay data.) If yes, consult `dota-fairplay`
  before finalizing the design — classification affects which layer data
  is allowed to reach.

## While building it

- [ ] **Domain types stay infrastructure-free.** If this module produces
  or consumes Domain types (`MatchState`, features, recommendations),
  confirm those types don't leak infrastructure-specific types (GSI
  payload types, CV library types, WPF types, ONNX tensor types) into
  their public shape. Map at the boundary instead.
- [ ] **No skipped layers.** If this is a Recommendation Engine, confirm
  it consumes Feature Engine output, not `MatchState` directly (rule 6).
  If this is UI, confirm it doesn't reach past the Recommendation
  Orchestrator into earlier layers, and doesn't call external APIs
  directly (rule 5).
- [ ] **Strategy logic stays out of GSI/CV.** If this module lives in the
  GSI or Computer Vision layer, confirm it only produces observations/
  normalized state — no judgments about whether something is good, no
  Dota strategy reasoning (rules 2 and 3).

## Before considering it done

- [ ] **Project references match the plan.** Diff the actual `.csproj`
  references against what you wrote down in the "before writing code"
  step — any drift is worth understanding, not just accepting.
- [ ] **An architecture test covers the new project**, if one doesn't
  already generically cover the whole solution — see
  `dependency-rules.md`'s "Enforcement" section. At minimum, confirm the
  existing architecture test (if present) still passes with the new
  project added; if there isn't one yet, this is a reasonable point to
  add it rather than deferring indefinitely.
- [ ] **Tests exist and run headless** — this module's own test project
  should not require a live GSI connection, live screen, or a running
  WPF app to execute, unless the module genuinely *is* the UI layer (in
  which case its logic-bearing parts should still be extracted and
  tested separately from the view/rendering code where practical).
- [ ] **If GSI/CV/ML/data-related:** fixtures and FairPlay tests exist per
  `dota-gsi`'s and `dota-fairplay`'s testing requirements, and `dotnet
  test` passes including them.

## Red flags worth stopping for

These are the specific shapes architecture drift tends to take in this
project — if you notice one mid-implementation, pause and reconsider
rather than pushing through:

- A Domain-layer file with a `using` statement pulling in a GSI, CV,
  ONNX, or WPF/WinUI namespace.
- A Recommendation Engine constructor parameter typed as `MatchState`
  instead of a Feature Engine output type.
- Any `HttpClient`/API-client usage inside `Ui.Overlay`.
- A CV or GSI class with a method name like `IsGoodItem`, `ShouldBuy`,
  `RecommendPlay` — a strategic verb in the wrong layer's vocabulary.
- A `Training` or `DataPlatform` project reference to
  `PresentationFramework`/`Microsoft.WinUI` or any other UI assembly.
