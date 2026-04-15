# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NeverTheLast is a **turn-based tactical RPG** built with **Unity 6000.0.53f1** (C#). Players place hero units on a grid and fight waves of enemies, with synergy and skill systems driving combat.

**Language:** C# with Korean comments/documentation throughout the codebase.

## Development Environment

- **Engine:** Unity Editor 6000.0.53f1 — no CLI build scripts; build via Unity Editor (File > Build and Run)
- **IDE:** VS Code with "Attach to Unity" debugger (`.vscode/launch.json`)
- **No automated tests or linting** configured
- **Data format:** YAML files parsed at runtime via YamlDotNet (`Resources.Load<TextAsset>`)

## Architecture

### Manager Singletons

The game is driven by singleton MonoBehaviour managers on a `DontDestroyOnLoad` GameObject:

- **GameManager** — central orchestrator; holds references to all other managers and game data lists; drives the state machine
- **GridManager** — manages a 3x9 grid (X: -4..4, Y: 1..3, spacing 2.1 units); maintains `heroList` and `enemyList`; each cell has a pre-placed inactive Unit that gets activated on spawn
- **RoundManager** — wave/round spawning from YAML data; called from `GameManager.Update()`
- **DataManager** — loads and deserializes YAML data files into typed C# lists
- **UIManager** — top panel display (gold, life, stage, speed)
- **SfxManager** — visual effects and projectile management

### Game State Machine

`Preparation` → `RoundInProgress` → `RoundEnd` → back to `Preparation` (or `GameOver`). Transitions via `GameManager.NextGameState()`.

### Unit Entity System

`Unit` (in `Entities/`) is the core game entity for both heroes and enemies. Key design patterns:

- **Stats** have base values + per-level + per-upgrade increments, recalculated via `AttributesUpdate()` — must be called after any stat-affecting change (status effects, synergies)
- **Event system** uses a `Dictionary<UnitEventType, Delegate>` with typed tuple parameters (e.g., `(Unit self, DamageContext context)`). Register via `AddListener<T>()`, fire via `Invoke()`. Event parameter types are documented as comments on the `UnitEventType` enum in `BaseClasses/BaseEnums.cs`
- **Status effects** that don't register a removal trigger persist forever — always wire up cleanup (e.g., on caster death)

### Code (Skill) System

Three-tier hierarchy under `Scripts/Codes/`:

- **PassiveCode** — auto-activates (e.g., HolyEnchant)
- **NormalCode** — mana-cost + cooldown (e.g., FireBlast, AuricMandate)
- **UltimateCode** — high-power abilities (e.g., Laevateinn)

Each implements: `CastCode()`, `SkillCoroutine()` (coroutine for sustained effects), `StopCode()`, `HasValidTarget()`. New skills are instantiated via `CodeFactory`.

### Status Effects & Synergies

- `StatusEffect` base class provides additive/multiplicative modifier methods per stat
- `SynergyEffect` extends StatusEffect with stack/duration support; instantiated via `SynergyEffectFactory`
- Synergies are recalculated via `GameManager.CalculateSynergies()` based on active heroes

### Damage Pipeline

`DamageContext` flows through three sequential events: `OnBeforeDamageTaken` → `OnTakingDamage` → `OnAfterDamageTaken`. Context carries attacker, damage, crit info, code type, damage tags, and penetration.

## Data Files

YAML data lives in `Assets/Resources/Data/` with numbered prefixes indicating load order:

| File | Content |
|------|---------|
| `10_units.yaml` | Unit definitions (stats, synergy IDs, code IDs) |
| `20_codes.yaml` | Skill/code definitions |
| `30_status.yaml` | Status effect templates (DOT, StatBuff, Stun) |
| `40_synergies.yaml` | Synergy definitions with max stacks |
| `50_elements.yaml` | Element/item data grouped by cost |
| `60_elites.yaml` | Elite unit data |
| `70_rounds.yaml` | Round-based enemy spawn queues by cell index |

Corresponding C# data classes are in `DataManager.cs`.

## Namespace Conventions

Code is organized by namespace matching the directory structure:
`BaseClasses`, `Codes.Base`, `Codes.Normal`, `Codes.Passive`, `Codes.Ultimate`, `Entities`, `Managers`, `Managers.UI`, `StatusEffects.Base`, `StatusEffects.Effects`, `StatusEffects.SynergyEffects`, `Helpers`
