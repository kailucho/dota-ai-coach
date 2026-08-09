# ADR 0012: Player profile data reaches the UI through Host, not a direct OpenDota reference

## Status

Accepted

## Context

`dota-architecture` rule 5 states the UI must not call OpenDota directly.
`tests/DotaAiCoach.Architecture.Tests/DependencyGraphTests.cs` enforces a
narrower, textual proxy of this rule for GSI: it asserts
`DotaAiCoach.UiOverlay.csproj`'s raw file text never contains the string
`System.Net.Http`. Fase 2 needed the same guarantee for OpenDota.

## Decision

Mirror the existing GSI firewall (`Host`/`ICoachSession`/
`CoachSessionFactory`) with a parallel one for profile data: `IProfileSession`,
`ProfileSession`, `ProfileSessionFactory`, all in `DotaAiCoach.Host`.
`Host.csproj` references `DotaAiCoach.OpenDota` with
`PrivateAssets="compile"` (compile-time invisible to anything referencing
Host, runtime DLLs still flow through) and `DotaAiCoach.PlayerProfile` as a
normal reference, since `IProfileSession` exposes `PlayerProfileSnapshot`
and friends as its public surface. `DotaAiCoach.UiOverlay.csproj` adds only
`DotaAiCoach.PlayerProfile` — never `DotaAiCoach.OpenDota` — so it can bind
to profile types without ever seeing `HttpClient`, the wire DTOs, the rate
limiter, or the cache.

## Consequences

- `tests/DotaAiCoach.Architecture.Tests/DependencyGraphTests.cs` gained
  `UiOverlay_references_no_OpenDota_directly_or_transitively`, the
  assembly-graph form of the rule the existing raw-text check only
  approximates for `System.Net.Http` — this is the stronger guarantee,
  checked mechanically rather than left to the textual proxy alone.
- `ProfileSessionFactory.Create()` is the single place OpenDota's HTTP
  client, rate limiter, and cache get constructed — exactly the same
  pattern as `CoachSessionFactory.Create()` for GSI, so the two firewalls
  read the same way to a future contributor.
- `App.xaml.cs` constructs the profile session at startup but does **not**
  auto-fetch (see docs/adr/0013's offline-first note) — the user must
  explicitly click Refresh in the profile window.
