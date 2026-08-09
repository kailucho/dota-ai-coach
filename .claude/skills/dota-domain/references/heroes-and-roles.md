# Heroes, Roles, Positions, Lanes

## Positions (1-5)

Position is the primary structuring concept for role in Dota 2 — more
precise than informal labels like "carry" or "support". Use position
numbers as the canonical representation; treat words like "carry",
"mid", "offlaner", "support" as human-readable labels derived from
position, not the source of truth.

- **Position 1 (hard carry)** — farm-priority core, typically safe lane.
  Weak early, scales into the primary right-click damage source late game.
  Farm efficiency and item timings matter more than early kill participation.
- **Position 2 (midlaner)** — solo mid lane. Usually the fastest hero to a
  power spike via level advantage and rune control. Often the primary
  gank/tempo enabler in the early-mid game.
- **Position 3 (offlaner)** — takes the hard lane (contested, against the
  enemy's safe-lane duo). Prioritizes survivability and utility over farm;
  often initiates fights. Farm priority is lower than pos 1/2.
- **Position 4 (soft support / roaming support)** — roams, sets up kills,
  contests the offlane, takes some early farm but yields it as the game
  progresses. Bridges early aggression and mid-game utility.
- **Position 5 (hard support)** — lowest farm priority, sacrifices personal
  resources (wards, smokes, sentries, town portal scrolls) for the team.
  Enables the safe-lane carry to farm uncontested.

Farm priority in a normal draft is roughly 1 > 2 > 3 > 4 > 5, but this is a
default, not a rule — a "farming pos 4" or a "supportive pos 1" both exist
and change what correct advice looks like.

## Lanes

- **Safe lane** — the lane where the defending team has the terrain
  advantage (easier to retreat toward own tier-1 tower). Usually pos 1 +
  pos 5.
- **Mid lane** — solo lane, contested equally by both teams. Usually pos 2.
- **Off lane (hard lane)** — the lane where the attacking team has the
  terrain disadvantage; contested by the enemy's safe-lane duo. Usually
  pos 3, sometimes joined by pos 4.

Lane outcome (who gets more XP/gold, who is forced off the lane, who dies)
materially changes what's correct for the rest of the game — a lane-losing
pos 1 often needs different itemization/timing advice than one who dominated
their lane.

## Hero archetypes (non-exhaustive, for reasoning not classification)

These are overlapping tendencies, not a taxonomy to force every hero into:

- **Right-click carry** — wins via auto-attack DPS scaling with items.
- **Spellcaster/nuker** — wins via burst magic damage, often falls off
  relatively as the game goes long unless itemized for it.
- **Initiator** — has a reliable way to start a fight (long-range stun,
  jump-in, area disable).
- **Pusher/split-pusher** — generates map pressure via lane creeps/summons
  rather than direct teamfight presence.
- **Support/utility** — wins via enabling teammates: healing, saves,
  vision, disables, buffs/debuffs — direct damage is secondary.

A hero can shift archetype by build (e.g. a normally supportive hero built
as a core), so archetype should be inferred from role + items + observed
behavior in a given game, not hardcoded per hero.

## Damage types

- **Physical** — mitigated by armor; countered by armor items
  (e.g. -armor debuffs boost it, +armor items reduce it).
- **Magical** — mitigated by magic resistance; countered by magic
  resistance items and spell immunity (BKB-type effects), unless the
  spell is explicitly marked to pierce spell immunity.
- **Pure** — ignores both armor and magic resistance; rare, high-value
  damage type, generally cannot be itemized against directly (only HP/evasion
  effects reduce its impact).

When reasoning about "is this hero/item good into that threat", damage type
of the threat plus the target's mitigation options is a core input — a
hero itemized for magic resistance is still vulnerable to a physical-damage
threat, and vice versa.

## Crowd control (CC) taxonomy

CC severity matters more than CC presence — "has a stun" is a weak signal
compared to "has how long a disable, on what cooldown, at what range".

- **Hard disable** — stun, hex, fear, sleep: the target cannot act at all.
- **Soft disable** — root (can't move, can still act), silence (can't cast),
  slow (movement/attack speed reduction).
- **Positional CC** — knockback, pull, forced movement — denies positioning
  rather than action directly.

Spell immunity (BKB-type effects) blocks most hard/soft disables but not
all — some abilities are explicitly designed to pierce it. When reasoning
about whether a disable "works," check whether it's documented as
piercing spell immunity rather than assuming either way.
