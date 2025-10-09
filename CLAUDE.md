# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NeverTheLast** is a Unity-based auto-chess/tower defense hybrid game written in C#. Players strategically place hero units on a grid to fight waves of enemies, managing resources, synergies, and unit upgrades through rounds.

## Development Commands

### Unity Editor
- Open the project in **Unity Editor** (no CLI build commands configured)
- Main scene: `Assets/Scenes/SampleScene.unity`
- Platform target: Android (`com.solid.autochess`)
- Scripting backend: IL2CPP (Android)

### Common Operations
- **Run game**: Play mode in Unity Editor (F5 or play button)
- **Build for Android**: Unity Editor → File → Build Settings → Android
- Git workflow: Standard git commands (commit, push, branch)

## Code Architecture

### Core Game Loop

The game follows a state machine pattern managed by `GameManager`:
- **Preparation** → **RoundInProgress** → **RoundEnd** → (repeat or **GameOver**)
- Timer-based transitions with automatic round start after preparation time
- Life system: Player loses life equal to remaining enemies when round ends/times out

### Manager Hierarchy (Singleton Pattern)

All key systems use singleton instances accessed via `Manager.Instance`:

1. **GameManager** (`Assets/Scripts/Managers/GameManager.cs`)
   - Central game state controller
   - Manages game loop, life system, kill count tracking
   - References all other managers (UIManager, GridManager, ShopManager, etc.)
   - Handles round transitions and ally field state snapshots for restoration

2. **GridManager** (`Assets/Scripts/Managers/GridManager.cs`)
   - Manages battle grid (xMin: -4, xMax: 4, yMin: 0, yMax: 3)
   - Manages bench grid (9 slots, separate from battle field)
   - Handles unit spawning, cell occupation, and targeting logic
   - Maintains `heroList` and `enemyList` (active units)
   - **Important**: Bench units (y=0) do not participate in combat

3. **DataManager** (`Assets/Scripts/Managers/DataManager.cs`)
   - Loads YAML data files from `Resources/Data/`:
     - `10_units.yaml` - Unit stats, codes, synergies
     - `40_synergies.yaml` - Synergy definitions
     - `50_tokens.yaml` - Resource token data
     - `70_rounds.yaml` - Enemy spawn patterns
   - Uses YamlDotNet for deserialization

4. **RoundManager** (`Assets/Scripts/Managers/RoundManager.cs`)
   - Manages enemy wave spawning from round data
   - Tracks remaining queued enemies for round completion
   - Handles spawn timing and positions

5. **UIManager** - UI updates, game status display, info tabs
6. **ShopManager** - Unit shop, reroll system, tier-based unit pools
7. **InventoryManager** - Resource token management
8. **DragAndDropManager** - Unit placement and movement
9. **SfxManager** - Audio effects

### Entity System

**Unit** (`Assets/Scripts/Entities/Unit.cs`) - Base class for all characters:
- Properties: HP, Mana, Atk, Def, CritChance, CritMultiplier, Shield
- Three code types (skills):
  - **PassiveCode**: Activated on round start
  - **NormalCode**: Basic attack with cooldown
  - **UltimateCode**: Special ability requiring full mana
- Event-driven architecture: Uses `UnitEventType` enum for lifecycle hooks
  - `OnRoundStart`, `OnRoundEnd`, `OnSpawn`, `OnDeath`
  - `OnTakingDamage`, `OnBeforeDamageTaken`, `OnAfterDamageTaken`
  - `OnUpdate`, `OnPassiveActivates`, `OnNormalActivates`, `OnUltimateActivates`
- Status effects system: Dictionary-based with unique identifiers
- Synergy effects: Applied based on team composition
- **Shield mechanics**: Blocks damage before HP (unless ShieldPenetration tag or damage-over-time effect)
- **Targeting**: Units maintain `currentNormalTarget` for basic attacks, with priority system (-3 to 3)

### Code (Skill) System

**Code** (`Assets/Scripts/Codes/Base/Code.cs`) - Abstract skill base class:
- Properties: CodeType, CodeName, Caster, TargetUnits, Cooldown, CastingDelay
- Implementations in `Assets/Scripts/Codes/`:
  - `Passive/` - Passive abilities (triggered on round start)
  - `Normal/` - Basic attacks (e.g., `NormalAttack.cs`, unit-specific variants)
  - `Ultimate/` - Ultimate abilities (require full mana)
- **CodeFactory** (`Assets/Scripts/Codes/Base/CodeFactory.cs`): Creates code instances by ID
- Coroutine-based execution for delayed/multi-hit effects

### Status Effects

**StatusEffect** (`Assets/Scripts/StatusEffects/Base/StatusEffect.cs`):
- Base class providing stat modifier methods
- Stack-based and duration-based effects
- Modifiers: HP, Atk, Def, CritChance, CritMultiplier, CodeAcceleration
- Special modifiers: ReceivingDamageModifier, HealingReceivedModifier
- Interfaces:
  - `ITemporalEffect`: Effects that update each frame (poison, burn)
  - `IStackEffect`: Effects that stack (not yet fully implemented)
  - `IHpChangeEffect`: Effects triggered by HP changes

**SynergyEffect** (`Assets/Scripts/StatusEffects/Base/SynergyEffect.cs`):
- Extends StatusEffect for team synergy bonuses
- Automatically applied/removed based on unit composition
- Examples: `LeaderSynergy`, `SentinelSynergy`, `SniperSynergy`, `AkashaSynergy`

### Data Structures

**Context Objects** (`Assets/Scripts/BaseClasses/Contexts.cs`):
- `EventContext`: Generic event data (Grantee, Attacker, DamageContext, DeltaTime)
- `DamageContext`: Damage calculation data (Damage, IsCrit, Penetration, CodeType, DamageTags)
- `ControlContext`: CC effect data (Attacker, Duration)
- `PassiveCodeContext`, `NormalCodeContext`, `UltimateCodeContext`: Skill execution contexts

**Enums** (`Assets/Scripts/BaseClasses/Enums.cs`):
- `GameState`: Preparation, RoundInProgress, RoundEnd, GameOver
- `UnitEventType`: 17 event types for unit lifecycle
- `CodeType`: Passive, Normal, Ultimate, Effect
- `DamageTag`: FlatDamage, DefensePenetration, SplitDamage, ShieldPenetration (in `Helpers.cs`)

### Helper Systems

**Helpers** (`Assets/Scripts/Helpers/helper.cs`):
- `DamageTag` enum: Additional damage modifiers beyond base enums
- Utility functions for damage calculation and targeting

**SerializableDictionary** (`Assets/Scripts/BaseClasses/SerializableDictionary.cs`):
- Unity-serializable dictionary implementation for Inspector

## Key Design Patterns

### Event-Driven Unit Lifecycle
Units register event listeners for specific triggers. Default handlers can be overridden:
```csharp
AddListener<EventContext>(UnitEventType.OnTakingDamage, CustomTakeDamageEvent);
Invoke(UnitEventType.OnTakingDamage, context);
```

### Damage Calculation Pipeline
1. `OnBeforeDamageTaken` event fires
2. `OnTakingDamage` calculates: `damage / (1 + effectiveDef * 0.01)` with status effect modifiers
3. Shield absorption (if applicable)
4. HP reduction and bar update
5. `OnAfterDamageTaken` event fires
6. Death check and `OnDeath` event

### Round Flow
1. **Preparation Phase**: Players arrange units, shop, upgrade (30s default timer)
2. `StartRound()`: Save ally field state, start progress timer (60s)
3. **Combat Phase**:
   - Units execute codes based on cooldowns and mana
   - RoundManager spawns enemies from queue
   - Round ends when: all enemies defeated OR all allies defeated OR timeout
4. **Round End**:
   - Calculate damage (remaining enemies = life loss)
   - Restore ally field to saved state
   - Clear status effects, reset shields
   - Transition to next Preparation phase

### Factory Pattern for Dynamic Creation
- **CodeFactory**: Creates skill instances from numeric IDs
- **SynergyEffectFactory**: Creates synergy effect instances from synergy IDs

## Important Implementation Details

### Cell Coordinate System
- Battle field: x ∈ [-4, 4], y ∈ [0, 3]
- Bench: Separate 9-slot array, not part of battle grid
- y=0 (bench) units are excluded from combat checks
- Cell names follow `Cell_x_y` format

### Unit Activation/Deactivation
- `ActivateUnit()`: Sets `isActive=true`, occupies cell, shows HP/mana bars
- `DeactivateUnit()`: Sets `isActive=false`, clears cell, adds 2s reservation timer
- Inactive units are skipped in combat loops

### Stat Calculation
- Base stats + (IncrementLvl × Level) + (IncrementUpgrade × UpgradeCount)
- Modifiers applied: `FinalStat = BaseStat × (1 + MultiplicativeSum) + AdditiveSum`
- `AttributesUpdate()` must be called after stat-affecting changes

### Shield System
- Shields block damage before HP loss
- Exceptions: `ShieldPenetration` damage tag, damage-over-time effects (`CodeType.Effect`)
- Shields persist entire round, cleared on `OnRoundEnd`
- Shield bar UI updates via `UpdateShieldBar()`

### Status Effect Identifiers
- Same identifier = non-stacking (overwrites)
- Different casters can stack same buff type by using unique identifiers
- Example: `HolyEnchantBuff` is non-stackable, `BurnEffect` extends duration

## Common Development Patterns

### Adding a New Unit
1. Add unit data to `Resources/Data/10_units.yaml`
2. Create portrait sprite in `Resources/Sprite/Portraits/`
3. Implement codes in `Assets/Scripts/Codes/[Passive|Normal|Ultimate]/`
4. Register codes in CodeFactory if using new IDs

### Adding a New Status Effect
1. Extend `StatusEffect` base class
2. Implement modifier methods (e.g., `AtkMultiplicativeModifier`)
3. If temporal: implement `ITemporalEffect.OnUpdate()`
4. Apply via `unit.AddStatusEffect(identifier, effectInstance)`

### Adding a New Synergy
1. Add synergy data to `Resources/Data/40_synergies.yaml`
2. Create synergy effect class extending `SynergyEffect`
3. Register in `SynergyEffectFactory`
4. System auto-applies based on unit composition in `GameManager.CalculateSynergies()`

### Debugging Combat
- Enable logs in `Unit.cs` `DefaultTakeDamageEvent()` for damage breakdown
- Check `GridManager.OnRoundStart()` logs to verify event propagation
- Monitor `GameManager.Update()` for state transitions and timer issues
- Use `InfoTab` (UI) to inspect unit stats, buffs, and synergies in real-time

## Project Structure

```
Assets/
├── Scripts/
│   ├── BaseClasses/        # Enums, Contexts, Info structs
│   ├── Entities/           # Unit.cs (core entity logic)
│   ├── Managers/           # All singleton managers
│   ├── Codes/              # Skill implementations (Passive, Normal, Ultimate)
│   ├── StatusEffects/      # Buff/debuff system
│   ├── Helpers/            # Utility functions
│   └── Cell.cs             # Grid cell logic
├── Resources/
│   ├── Data/               # YAML game data files
│   └── Sprite/Portraits/   # Unit portrait images
└── Scenes/                 # Unity scenes

Library/PackageCache/       # Unity packages (ignore for development)
```

## Notes for Future Development

- Game is in active development; recent commits focus on shield mechanics and InfoTab UI
- Korean language comments/logs are common in codebase
- Third-party assets: Hovl Studio effects, TextMesh Pro
- No automated tests currently implemented
- Uses Unity's new Input System (active input handler: 2)
