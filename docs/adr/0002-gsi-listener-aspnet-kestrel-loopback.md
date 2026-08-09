# ADR 0002: GSI listener via self-hosted Kestrel, loopback-only

## Status

Accepted

## Context

The GSI listener must receive HTTP POST payloads from the local Dota 2
client and respond fast (a slow response causes the Dota client to back
off, per its `timeout` config value). It runs on the same machine as the
game client — no external network exposure is needed or wanted.

## Decision

Use ASP.NET Core's self-hosted Kestrel (`WebApplication.CreateSlimBuilder`)
bound explicitly to `127.0.0.1` (not `0.0.0.0`), with a single
`MapPost` endpoint. The endpoint reads the request body, hands it to
`GsiPipeline.Ingest` for parsing/merging, and returns `200 OK`
immediately after — parsing/merging happens synchronously but is fast
(pure in-memory JSON deserialization and field-wise merge, no I/O), so
this doesn't risk the response-time requirement.

Considered and rejected: raw `HttpListener` (more manual plumbing for no
real benefit given ASP.NET Core is already a natural fit for this
project's .NET stack); a full ASP.NET Core Web SDK project
(`Microsoft.NET.Sdk.Web`) for the `Gsi` project (rejected — `Gsi` stays a
plain class library with a `FrameworkReference` to
`Microsoft.AspNetCore.App`, so it can be hosted in-process by `Host`/the
WPF app rather than needing to run as a separate web application).

## Consequences

- Loopback-only binding means no firewall/network exposure concerns.
- The optional `auth` token (checked against `payload.auth.token` in the
  body, not an HTTP header — that's where GSI puts it) is the only thing
  distinguishing a real Dota client POST from anything else hitting the
  same local port; a mismatch causes the payload to be silently dropped
  (not 401'd), since teaching a legitimate client to retry aggressively
  would be worse than just ignoring a bad payload once.
- `GsiListener.BoundPort` supports `Port = 0` (ephemeral port) for tests,
  avoiding port collisions across parallel test runs.
