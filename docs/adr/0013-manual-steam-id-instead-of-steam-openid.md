# ADR 0013: Manual Steam ID entry instead of Steam OpenID

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` specifies Steam OpenID login for Fase 2. Steam
OpenID 2.0 requires a local HTTP listener for the callback, opening a
browser, and validating the provider's signed response — real surface
area for what it produces: a Steam ID. This project's GSI feed already
reports the local player's Steam64 in `player.steamid` whenever a match is
active (see `DotaAiCoach.Domain.Match.PlayerSnapshot.SteamId`), so most of
the time the value OpenID would authenticate is already sitting in
`ICoachSession.CurrentState.Player.SteamId`.

## Decision

Skip Steam OpenID for this phase. `SteamId.TryParse` (in
`DotaAiCoach.PlayerProfile.Identity`) accepts either a pasted Steam64 (the
GSI-reported form, or what a Steam profile URL contains) or a bare Steam32
(OpenDota's `account_id` form) and converts consistently
(`steam32 = steam64 - 76561197960265728`). The profile window pre-fills
its Steam ID field from `OverlayViewModel.DetectedSteamId` when a GSI feed
is active, so in the common case (Dota running, GSI connected) the user
never has to type anything — but nothing is authenticated; the value is
just a lookup key into OpenDota's public API.

## Consequences

- No credential handling, no browser popup, no local OAuth callback
  listener — this profile feature works identically whether or not Dota
  is currently running, as long as the user knows or has previously
  entered their Steam ID.
- There is no verification that the entered Steam ID belongs to the
  person using the app — this is acceptable because the feature only
  reads *public* OpenDota data (per docs/FAIR_PLAY.md's `PublicPreflight`
  classification), the same data anyone could already look up by visiting
  opendota.com with any Steam ID.
- Revisit trigger: if a future phase needs to *write* data tied to an
  authenticated identity (e.g. a synced settings/profile account, not just
  read-only public match history), Steam OpenID becomes necessary and this
  ADR should be superseded rather than amended.
