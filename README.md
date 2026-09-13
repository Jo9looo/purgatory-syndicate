# Purgatory Syndicate

**Tactical exorcism in Purgatory.** A 2.5D turn-based tactical RPG whose combat is built around contested clashes, coin rolls, and a seven-sin affinity system.

Combat draws from *Limbus Company* and *Library of Ruina*: two skills meet head-on, power is rolled coin by coin, and the loser pays for it. The project is currently a **playable whitebox** — the full battle loop, targeting, HUD, and a three-stage run are in place, using primitive capsules instead of final art.

| | |
|---|---|
| Engine | Unity 6 (`6000.5.7f1`) |
| Genre | 2.5D turn-based tactical RPG |
| Input | Mouse (drag targeting) + keyboard |
| Status | Whitebox prototype — playable end to end |

Internal design notes and sprint history live in [`DEVELOPMENT_PLAN.md`](DEVELOPMENT_PLAN.md).

---

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [How to play](#how-to-play)
- [Combat systems](#combat-systems)
- [Campaign](#campaign)
- [Roster](#roster)
- [Repository layout](#repository-layout)
- [Development notes](#development-notes)

---

## Features

- **Full turn loop** — speed roll, targeting, timeline, execution, turn end, win / lose
- **Drag targeting** — drag from an ally (or from a skill slot) onto an enemy; a curved arrow stays locked until the turn ends and can be reassigned
- **Four-skill loadout** — three active attacks plus one defensive skill (Block, Dodge, or Counter)
- **Multi-round clashes** — contested attacks trade rounds; losing a round burns a coin; the winner strikes with remaining coins
- **Stagger** — a second vitality bar; breaking it skips the unit for a full turn and doubles incoming damage
- **Sin system** — seven affinities, a weakness cycle, same-sin resonance, and a per-unit Sin resource with skill costs and cooldowns
- **Status effects** — Bleed, Fragile, Power Up, Power Down (duration, stacking, shown on hover and above units)
- **Rule-based enemy AI** — defends when stagger is critical, focuses the weakest ally, prefers effective / affordable skills
- **Three-stage run** — win to advance, restart the current stage, or return to the menu and reset

What is **not** in yet: final character art, polished UGUI, VFX / SFX, camera work, and serious number balancing.

---

## Requirements

- [Unity Hub](https://unity.com/download) with editor **6000.5.7f1** (see `ProjectSettings/ProjectVersion.txt`)
- Git, if you are cloning this repository

The project uses Unity's **new Input System**. No extra packages need to be installed by hand after the first Hub import.

---

## Getting started

```bash
git clone https://github.com/Jo9looo/purgatory-syndicate.git
```

1. Open **Unity Hub → Add**, and select the cloned folder.
2. If Hub asks for an editor version, choose **6000.5.7f1** (or let it download that version).
3. Wait for Unity to regenerate `Library/`. That folder is gitignored on purpose; every machine builds its own cache.
4. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

**MULAI** loads the battle scene. You can also press Play on `Assets/Scenes/SampleScene.unity` directly — `BattleBootstrap` spawns the current stage's encounter at runtime.

---

## How to play

Each allied unit has a **skill bar** at the bottom of the screen (four slots). A gold triangle floats above the unit you currently control.

### Targeting

1. **Drag** from an ally onto an enemy. A yellow arc appears from the ally's head and follows the cursor; dropping on an enemy locks a red arrow.
2. Or **press and drag a skill slot (1–3)** — the arrow still originates from the character, not the UI.
3. Slot **4** is the defensive skill. It is self-targeted; no drag is required.
4. Hover any unit (ally or enemy, living or downed) for a full stat breakdown: base stats, current HP / Stagger / Sin, skills, and buffs / debuffs.
5. When every ready ally has an order, press **Enter** to lock the turn. Review the timeline, then **Enter** or **Space** to execute.

Locked arrows persist through the timeline and execution, then clear at turn end. Drag the same ally again to retarget. Dropping on empty space keeps the previous target.

Skills that are on cooldown or that cost more Sin than you have are greyed out and cannot be selected.

### Controls

| Input | Phase | Action |
|---|---|---|
| Left-drag | Targeting | Assign an attack (from a unit or from a skill slot) |
| Hover | Any | Inspect a unit |
| `1` `2` `3` | Targeting | Select an active skill on the hovered / dragged ally |
| `4` | Targeting | Queue the defensive skill |
| `Enter` | Targeting | Confirm all orders (only when every ally is assigned) |
| `Enter` / `Space` | Timeline | Execute the turn |
| `R` | Any | Restart the current stage |
| `N` | Victory | Advance to the next stage |
| `M` | Battle over | Return to the main menu (resets the run) |

---

## Combat systems

### Turn flow

```
Roll Speed → Targeting → Action Sorting → Execution → Turn End
                                                              |
                         next turn <--------------------------+
                              or Battle Over
```

Speed is rolled from each character's configured range. Higher speed acts first. Ties: allies before enemies, then by name.

### Clash

If A targets B and B targets A, the actions **clash** instead of resolving as two one-sided hits.

1. Both units lunge to the midpoint.
2. Each round, both sides roll power with their **remaining** coins.
3. The round loser loses one coin. A draw rerolls that round (capped at 12).
4. When one side has no coins left, the winner lands a finishing attack rolled with leftover coins.

Unopposed attacks apply full rolled power immediately. If the original target dies earlier in the turn, the attack redirects to a random living opponent.

### Power formula

```
power = basePower
      + heads from remaining coins
      + 2          if the skill's sin matches the actor's affinity
      + resonance  (+1 per extra same-sin attacker on that team)
      + Power Up
      - Power Down
```

### Stagger

Incoming damage also depletes Stagger 1:1 (while the unit is not already staggered). At 0 Stagger the unit **breaks**: it skips one full turn and takes x2 HP damage. Recovery happens at the end of the skipped turn, with a full Stagger bar.

### Sin

Seven affinities: **Wrath, Sloth, Gluttony, Gloom, Pride, Envy, Lust**.

**Weakness cycle** (each sin is strong against the next):

```
Wrath → Sloth → Gluttony → Gloom → Pride → Envy → Lust → Wrath
```

- Attack sin beats a defender affinity → **Effective** (+50% damage)
- Defender affinity beats the attack sin → **Resist** (-25% damage)
- Both apply → Effective wins

**Resonance** — if two or more units on the same team attack with the same sin in one turn, those attacks gain `count - 1` bonus power.

**Sin resource** — each unit has a Sin pool (max 6). Units start a battle with 1 and gain +1 at the start of every turn. Skills spend Sin when they resolve and may go on cooldown afterwards.

### Defense (one turn)

Defensive stances activate at the **start** of execution, before any attack lands.

| Kind | Effect |
|---|---|
| **Block** | Reduces incoming damage (default -50%) |
| **Dodge** | Chance to avoid the hit entirely (default 50%) |
| **Counter** | Takes the hit, then strikes back (does not chain) |

### Status effects

Applied when a hitting skill lands (or on self, for self-buffs). Same type stacks potency; duration refreshes to the longer value. Ticks down at turn end.

| Effect | What it does |
|---|---|
| **Bleed** | Deals potency damage when the unit acts (can stagger or kill) |
| **Fragile** | Incoming damage increased by potency % |
| **Power Up** | +potency on every power roll |
| **Power Down** | -potency on every power roll |

### Enemy AI

Enemies are not random. In order they:

1. Take a defensive stance if their Stagger is at or below 30% and the skill is usable
2. Target the living ally with the lowest HP (then lowest Stagger)
3. Pick the best affordable, off-cooldown attack — scoring effective matchups, affinity match, base power, and on-hit effects
4. Fall back to defense if no attack is usable

---

## Campaign

A static run index survives scene reloads and resets from the main menu.

| Stage | Encounter | Setup |
|---|---|---|
| 1 | Purgatory Gate — First Contact | 2v2 |
| 2 | Inner Court — The Pack | 2v3 |
| 3 | Throne Depths — Last Rite | 2v4 |

Victory on stage 1 or 2: press **N** to load the next encounter. Victory on stage 3 ends the run. **R** restarts only the current stage. **M** (or **MULAI** from the menu) returns the run to stage 1.

Encounter assets live at `Assets/Resources/Battle/Encounter_Stage1-3.asset`.

---

## Roster

Default party and opposition (whitebox stand-ins):

| Unit | Role | Defense | Affinities |
|---|---|---|---|
| **Exorcist** | Ally | Counter | Wrath, Pride |
| **Priestess** | Ally | Block | Gloom, Lust |
| **Demon** | Enemy | Block | Pride, Gluttony |
| **Imp** | Enemy | Dodge | Envy, Sloth |

Example skill identities:

| Skill | Notes |
|---|---|
| Wrath Strike | Cost 1 — inflicts Bleed |
| Pride Crush | Cost 2, 1-turn cooldown — inflicts Fragile |
| Gloom Lament | Cost 1 — inflicts Power Down |
| Sloth Guard | Cost 1 — Power Up on self |
| Gluttony Devour | Cost 2, cooldown — Bleed |
| Iron Block / Shadow Dodge / Vengeful Counter | Slot 4, per character |

---

## Repository layout

```
Assets/
  Scripts/Battle/
    Enums/          SinType, BattleState, SkillKind, StatusEffectType
    Data/           ScriptableObject definitions (skills, stats, characters, encounters)
    Core/           Runtime: manager, targeting, mouse, HUD, motion, AI
  Scripts/Menu/     Main menu (UGUI, built at runtime)
  Resources/Battle/ Designer assets loaded at runtime
  Scenes/           MainMenu.unity, SampleScene.unity (battle)
Packages/           Unity package manifest
ProjectSettings/    Editor / build / Input System settings
DEVELOPMENT_PLAN.md Design roadmap and decision log
```

Runtime combat data is **ScriptableObjects**. Mutable battle state lives on `MonoBehaviour`s (`BattleEntity`, `BattleManager`). Battle logic does not depend on the HUD; the HUD only reads public state.

`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, and IDE project files are excluded by `.gitignore`. Do not commit them.

---

## Development notes

- Namespace: `PurgatorySyndicate.Battle`
- Input: new Input System (`Keyboard.current`, `Mouse.current`) — not the legacy `Input` class
- Battle HUD is **OnGUI** for the whitebox. The main menu is UGUI with `InputSystemUIInputModule`
- Important events are logged with a `[ClassName]` prefix
- After pulling, open the project in the same Unity version so `.meta` GUIDs stay stable

### Typical git workflow

```bash
git add .
git commit -m "Short reason for the change"
git push
```

---

This repository is a work-in-progress prototype. Systems and numbers will change as the whitebox is playtested.
