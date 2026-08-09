# Forbidden Techniques and Data

These are prohibited unconditionally — not weighed case by case, not
allowed behind a flag, not acceptable "just for testing" in a codebase
that could ship. If a design requires one of these, the design is wrong,
not the rule.

## Why "technically retrievable" isn't the bar

Dota 2's fog-of-war and information-hiding design is not an accidental gap
— it's a deliberate competitive mechanic. A tool that reconstructs hidden
information through a side channel (memory, packets, injected code) is
functionally a cheat, even if:
- the underlying value is "the same" as something visible elsewhere,
- the intent is "just coaching, not cheating",
- no automated action is taken on it.

The reason GSI-sourced data is fine and memory-read data is not, even when
they'd report the identical number, is that GSI is the sanctioned channel
Valve built for exactly this purpose — it exposes only what Valve decided
is fair for a player's own tools to see. Memory reading bypasses that
decision entirely and gets everything, including what Valve chose to
withhold. The legitimacy is about the channel, not the payload.

## Forbidden techniques

**Process memory reading** — Attaching to or reading the Dota 2 process's
memory to extract game state. Bypasses GSI's information boundary
entirely; can read anything the game engine knows, including hidden
opponent state. Forbidden regardless of what specific value is targeted.

**DLL injection** — Injecting code into the Dota 2 process. Same
information-boundary bypass as memory reading, plus opens a second class
of risk: it can also be used to *act* (automate inputs), not just
observe, and is broadly recognized by anti-cheat systems as compromising
game integrity. Forbidden outright.

**Packet manipulation** — Capturing or manipulating the game's network
traffic to extract or alter game state. Same boundary-bypass problem as
memory reading (the packet stream carries authoritative server state that
GSI deliberately doesn't expose in full), with the added risk of
manipulation affecting other players' games. Forbidden outright.

**Automated input injection** — Any mechanism that sends synthetic
mouse/keyboard input to control the game client on the player's behalf.
This is prohibited independent of the fair-play-visibility question above:
even if every input were based on 100% legitimate `LIVE_ALLOWED` data,
automating play input directly is bot-play, not coaching, and violates
the coaching-not-playing premise of this project.

**Automated ability casting** — A specific case of automated input: never
cast abilities on the player's behalf. The application may *recommend*
("consider using X now") but must never execute the cast.

**Automated item purchasing** — A specific case of automated input: never
purchase items on the player's behalf. The application may recommend a
purchase; the player buys it.

## Forbidden data (regardless of how it's obtained)

Even through a legitimate channel, some information should never surface
in a live feature because the player themselves would not have legitimate
access to it during the live game:

**Fog-of-war information** — Any state about areas/units the player's
client hasn't actually revealed. If GSI or CV ever exposes something that
turns out to include fog-of-war-hidden state (a bug, an over-broad field),
treat it as `FORBIDDEN` for live use regardless of the source channel's
usual trustworthiness — the channel being generally legitimate doesn't
make every field it emits legitimate.

**Hidden/unobserved enemy locations** — Enemy positions the player hasn't
actually seen (via vision, ward, or direct sighting) in the current game.

**Unobserved enemy items** — Enemy inventory contents not shown on the
player's own screen at any point (see `VISIBLE_SCREEN` in
`classification-categories.md` for the legitimate version: items *are*
fine to read once actually visible).

**Hidden wards** — Ward positions the player hasn't personally seen
placed, or that haven't been revealed to them (e.g. via a Sentry/dust
reveal or observer ward line of sight).

## If you're unsure

If a proposed data source or technique isn't clearly covered by this list
or by `classification-categories.md`, don't guess in the permissive
direction. Flag it explicitly to the user/reviewer with your reasoning for
why it might be borderline, and default to treating it as `FORBIDDEN` or
`SPECTATOR_ONLY` until it's explicitly cleared — the cost of a false
negative here (blocking something that was actually fine) is much lower
than the cost of a false positive (shipping something that breaks fair
play).
