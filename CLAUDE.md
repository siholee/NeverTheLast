# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NeverTheLast** is a Unity 6 (6000.4) roguelite RPG in C#. The player builds a party of mythological
heroes, walks a run of stages themed by civilisation, and **raises one main character** across the run.
Combat is **turn-based and fully automatic** — the player's decisions are made before the fight
(party, placement, equipment, training), never during it.

Korean is the working language: comments, logs, commit messages, and all design docs are in Korean.

## Development Commands

There is no CLI build. Open the project in the **Unity Editor**.

- **Scenes**: `Assets/Scenes/MainMenu.unity` → `Assets/Scenes/Game.unity`
- **Run**: Play mode in the Editor
- **Input**: Unity's new Input System (`activeInputHandler: 2`)
- **Debug mode**: **F1** opens the debug panel in Play mode (theme lock, stage jump, party level,
  invincibility, speed). F2 wins the fight, F3 toggles ally invincibility, F4 cycles speed,
  F9 previews an event. `Core/DebugMode.cs` + `Managers/UI/DevTools/DebugOverlay.cs`, both behind
  `#if UNITY_EDITOR || DEVELOPMENT_BUILD` — see `Detail_07 §4.6`. A red banner stays on screen
  while any override is active, so balance is never measured with invincibility left on.

### Type-checking without the Editor

Unity generates `Assembly-CSharp.csproj` at the repo root. To compile-check a change without opening
the Editor, copy that csproj (so the generated one is not clobbered), add any **new** `.cs` files as
`<Compile Include="…" />`, redirect `BaseIntermediateOutputPath` / `OutputPath` to a scratch folder,
then `dotnet build <copy>.csproj`. Delete the copy and its output afterwards. `LangVersion` is 9.0.

> New `.cs` files have no `.meta` until the Editor next gains focus. That is expected — do not create
> `.meta` files by hand.

## Documentation — read this first

**`Assets/Docs/Design/` is the design source of truth, and it is kept in sync with the data files.**
Before changing gameplay, read the relevant chapter; after changing gameplay, update it.

| Doc | Covers |
| --- | --- |
| `README.md` | Doc hierarchy and writing rules |
| `GDD_Main.md` / `GDD_Sub_Concepts.md` | Vision (no numbers) / concept definitions |
| `Detail_01_Progression.md` | Run structure, stages, themes, enemy scaling, life |
| `Detail_02_Combat.md` | Battlefield, action values, damage formula, shields, damage tags |
| `Detail_03_Character.md` | 5-stat model, derived stats, code system, unit schema |
| `Detail_04_Training.md` | Focused training, support cards, bond, passive transfer |
| `Detail_05_Economy.md` | Gold/tokens/tickets, equipment, reward flow |
| `Detail_06_Events.md` | Event schema, choice actions, VN presentation |
| `Detail_07_UI_Tech.md` | UI screens, manager inventory, data files, save system |
| `Detail_08_Confirmed_Characters.md` | Per-character combat spec and unlock passives |
| `Detail_09` / `Detail_10` | Enemy roster & stage composition / boss specs |
| `Detail_11` / `Detail_12` | Code catalogue by ID / player code & proficiency index |
| `Detail_13` / `Detail_14` | Reward pool & tier odds / equipment tier spec & list |
| `Detail_15_Party_Synergy.md` | Role taxonomy, composition archetypes, per-main recommended lineups |
| `Detail_16_Elements.md` | Innate vs. attached element, 28 elemental reactions, reaction codes, field states |
| `Design_Backlog.md` | Open design decisions that block work |

Writing rules that matter when editing: **a number lives in exactly one document**, unimplemented
things are marked 🔸/🔴, and the detail docs mirror `Assets/Resources/Data/*.yaml` and the code.

## Core Model

### Stats — five stats, nothing else

**There is no Attack or Defense stat.** Every combat number derives from five stats.

```
피해     = 스킬 위력 × 주스탯 × 0.2      (위력은 스킬마다 고정, 포켓몬식)
최대체력 = CON × 100
방어력   = STR × 1                       (받는 피해 배율 = 기준값 / (기준값 + 방어력))
기준값   = 100 + 10 × (레벨 − 1)
스탯     = base + incrementLvl × (Level − 1) + incrementUpgrade × 강화횟수
```

| Stat | Second role |
| --- | --- |
| STR | Defense + equipment weight limit |
| DEX | Action speed |
| CON | Max HP |
| INT | Mana efficiency |
| LUK | Crit chance |

Main stat gets ×1.2, sub stat ×1.1. `Unit.AttributesUpdate()` recomputes derived values and
**preserves the HP ratio**, so a max-HP multiplier scales current HP with it.

### Turn-based combat — `ActionScheduler`

Combat is not real time. `Managers/ActionScheduler.cs` owns the clock.

- Speed = `100 × ActionSpeedCurr`, action value `AV = 10000 / speed` (lower acts sooner).
  Time is not advanced continuously — everyone's AV is decremented by exactly what the next actor needs.
- **Actions vs. triggers.** Only five things are *actions* — they take a scheduler slot and resolve:
  **일반행동** (normal), **대체행동** (substitute — takes the normal action's slot), **추가행동**
  (additional), **특수행동** (special), **궁극기** (ultimate). Passive codes, effects and event hooks
  are *triggers*: they never act, they only queue an action or change a value. See `Detail_03 §4.5-A`.
- Action priority: `PriorityAdditional(0) → Special(1) → Additional(2) → Ultimate(3) → Normal(4)`.
  0 is just "an additional action a passive queued" — same nature as 2, and both fire
  `OnAdditionalActivates` (`ActionScheduler.IsAdditional`).
- **특수행동 is opened by exactly one thing** — Indra's ultimate `신들의 왕`. It hands every
  Lokapala on the field their SP code. It skips a turn like an additional action but fires
  `OnSpecialActivates` instead, so additional-action counters do not see it. Always go through
  `Combat.SpecialAction.OpenGate`; never call `EnqueueSpecial` directly. See `Detail_03 §4.5-C`.
- **Ultimates do not consume a turn.** They queue as soon as the resource fills and do not reset AV.
  There is no second gate — resource is the only thing standing between a unit and its ultimate.
  A mana unit's total is `manaMax` in its YAML entry: an **inherent per-unit stat** that level,
  upgrades, training and effects never touch, so it does not pass through `UnitStats` or
  `AttributesUpdate`. With cooldowns gone it is the one knob for ultimate cadence (INT sets the
  fill rate, `manaMax` sets the amount). Everyone sits at 100 today.
- A **대체행동** still counts as one normal action — call `NotifyActionResolved()` even when it
  deals no damage. The old term 강화 일반행동 and the `Empowered*` identifiers are gone.
- Duplicate suppression: one `(unit + kind + key)` may sit in the queue at a time.
- **Turns are the only gameplay time axis.** Durations, periodic passives and internal cooldowns
  all count `Unit.TurnCount`. Seconds survive in exactly one place: ultimate-resource accrual,
  which converts flowed AV at `ActionValuePerSecond`. Wall-clock is only for presentation
  (projectile flight, cast animation) — never for combat resolution.
- `SyncParticipants()` picks up units spawned mid-round automatically.

Status durations, damage-over-time and periodic passives advance on **`BaseEffect.OnOwnerTurn()`**,
not per frame. A passive that fires "every N turns" extends **`PeriodicTurnPassive`**
(`Codes/Base/`) instead of hand-rolling a counter; an internal cooldown uses
**`Combat.TurnCooldown` / `TargetTurnCooldown`** rather than `Time.time`.

### Grid

`GridManager` — `x ∈ [-2, 2] excluding 0`, `y ∈ [1, 4]`. Negative x is the ally side.

| | Front | Rear |
| --- | --- | --- |
| Ally | x = -1 | x = -2 |
| Enemy | x = 1 | x = 2 |

Four slots per column, eight per side. Bench is a separate 9-slot array, not part of the field.
Use `GetFrontColumn(isEnemy)` / `GetRearColumn(isEnemy)` rather than hard-coding.

## Architecture

### Managers

Singletons via `Manager.Instance`, in `Assets/Scripts/Managers/`.

| Manager | Responsibility |
| --- | --- |
| `GameManager` | State machine, phase timers, life, event flow. Owns `ActionScheduler` |
| `RunManager` | Run lifetime (start/save/restore), bond, event history, **single entry point for stage advance** |
| `RoundManager` | Stage/round/theme selection, enemy composition and placement, boss slots |
| `GridManager` | Cell creation, spawning, targeting, `heroList` / `enemyList` |
| `ActionScheduler` | Turn scheduling (plain class, not a MonoBehaviour) |
| `EventScheduler` | Event queue for inserting events at arbitrary points (plain class) |
| `TrainingManager` | Focused training, support rolls, passive transfer (**static class**) |
| `RewardManager` | Reward pool generation and application |
| `CharacterSelectionManager` | Party rules (max 5, one main, no duplicates) |
| `InventoryManager` | Gold/tokens/tickets, equipment validation |
| `DataManager` | Generic YAML loader (`Load<T>`, YamlDotNet) |
| `UIManager` | Screen routing |
| `AudioManager` / `SfxManager` | BGM & SFX / combat effects |
| `DragAndDropManager` | Unit placement |

`SettingsManager` and the save system live in `Assets/Scripts/Core/`.

### Data files — `Assets/Resources/Data/`

| File | Content | DTO |
| --- | --- | --- |
| `00_intro.yaml` | Intro sequence | `IntroData.cs` |
| `10_units.yaml` | Player units | `UnitData.cs` |
| `20_codes.yaml` | Code display data (passive/normal/ultimate) | — |
| `30_synergies.yaml` | Party roles, archetypes, per-main recommended lineups | `SynergyData.cs` |
| `40_items.yaml` | Equipment (rewards come from enemy `drops` + `commonDropItemIds`) | `ItemData.cs` |
| `50_tokens.yaml` | Token definitions | `TokenData.cs` |
| `60_enemies.yaml` | Enemies (normal/elite/boss) and their `drops` | `EnemyData.cs` |
| `70_rounds.yaml` | Round types / composition patterns (fallback only) | `RoundData.cs` |
| `80_stages.yaml` | Themes, events, fixed bosses | `StageData.cs` |
| `90_rewards.yaml` | Per-round reward tier odds, common drops, consumables | `RewardData.cs` |

**Code IDs are per-slot namespaces.** Passive 22 and ultimate 22 are unrelated; `CodeFactory`
resolves them in separate switches.

**IDs are allocated by faction and by code family** — see `Detail_03 §7.4` and `Detail_11 §1.5`.

| Space | Rule |
| --- | --- |
| Unit ID | 20-slot block per faction — Akasha 1~, Greek 20~, Takamagahara 40~, Vedic 60~, Nord 80~, Rome 100~, Egypt 120~, Mexica 140~, Gaul 160~ |
| Ally normal / ultimate | **same number as the owner's unit ID** (Orion = 22 → N 22, U 22) |
| Ally unique passive | **owner's unit ID + 200** (Orion → 222) |
| Ally special action (SP) | **same number as the owner's unit ID**, Lokapala only |
| Field state | `Combat.Battlefield` — a board-wide state, not a `UnitStatus`. Duration counts the caster's turns; cleared on round end |
| Ally shared unlock passive | 1~179 |
| Equipment-granted passive | 400~499 |
| Summon codes (normal / ultimate) | 500~599 |
| Enemy codes | 1000+ in per-theme 100-slot blocks (공용 1000 · 콜로세움 1100 · 로마 1200 · 메히코 1300 · 아스완 1400 · 공허 1500 · 노르드 1600 · 일본 천공 1700) |

Enemy codes are dispatched by **range arms whose ID gaps are the style index**
(`(LegionNormalStyle)(codeId - 1200)`), so append new enemy codes at the end of a family —
never in the middle. Changing unit IDs breaks saves: bump `RunSaveData.CurrentVersion` with it.

### Codes (skills)

`Assets/Scripts/Codes/` — `Base/`, `Passive/`, `Normal/`, `Ultimate/`.

Every unit has **one unique passive + one normal attack + one unique ultimate**, plus N unlock
passives from `levelPassives`. **There is no code-capacity cap** — every unlock whose level
condition is met is learned. INT used to gate this (`max(3, INT)`) and silently dropped the
highest-level unlocks on low-INT units; that gate is gone and INT now only drives mana efficiency.

- `CodeFactory` maps numeric IDs to classes — three switches, one per slot.
- `UniquePassiveCode` sets `Transferable = false`. **Unique passives are never transferred**;
  support cards can only pass on unlock passives. (The old degraded-transfer system is gone.)
- **No code has a cooldown.** DEX-driven action value sets the cadence of normal actions,
  and the ultimate resource sets the cadence of ultimates. `Code.Cooldown` and
  `Unit.ultimateCooldown` are gone: an ultimate queues the moment its resource fills.
- Codes carry a **grade**: `CodeGrade.Normal` (silver), `Enhanced` (gold), or `Unique` (purple).
  A silver code sets `SupersededByCodeId` to the gold code that replaces it, and
  `Unit.TryCastPassiveCode` refuses to fire it when the owner has learned that gold code.
  **`Unique` sits outside that ladder** — setting `IsUniquePassive` forces the grade to `Unique`
  and clears `SupersededByCodeId`, so a unique passive can neither replace nor be replaced.
  Field-wide auras (different owners) keep their existing highest-only handling. See `Detail_12 §3.3`.
- Power supports a proportional term: `위력 = Power + 스탯 × PowerStatCoefficient` (`Code.CurrentPower`).

### Effects and statuses

`BaseEffect` (`Effects/Base/`) is one class with **many optional query hooks** — stat modifiers,
incoming/outgoing damage multipliers, healing/shield multipliers, `TryPreventDeath`, `TryNullifyHit` (hit-count invulnerability),
`MaxHpMultiplierModifier`, `HpSegmentCount`, and lifecycle (`OnApply` / `OnOwnerTurn` / `OnRemove`).
Override only what a code needs. `Unit` aggregates every active effect when computing a value.

Statuses (`UnitStatus`) wrap effects with id/key/duration/stack policy. Same key = same status;
`StatusStackPolicy` picks between Replace / ExtendDuration / Stack / Ignore.

Damage flows through `Unit.TakeDamage(DamageContext)`:
`OnBeforeDamageTaken → OnTakingDamage (mitigation, shield, HP) → death check → OnAfterDamageTaken`,
and `OnDamageDealt` fires on the attacker.

### Damage tags — `BaseClasses/DamageTags.cs`

The **ten-thousands digit is the category**; an attack takes one from each band.
10000 target scope · 20000 attack kind · 30000 contact · 40000 penetration · 50000 weapon class.

> Damage-over-time (`CodeType.Effect`) carries an **empty tag list**. Code that checks for contact
> must ask whether `ContactAttack` is present — never "is not `NonContactAttack`".

## Common Tasks

### Adding a unit

1. `10_units.yaml` — stats, `codes`, `levelPassives`, proficiencies, tags
2. Portrait/standing in `Resources/Sprite/{Portraits,Standings}/Allies` (enemy art uses `Enemies/{Normal,Elite,Boss}`)
3. Implement codes under `Codes/[Passive|Normal|Ultimate]/`, register in `CodeFactory`
4. Register display data in `20_codes.yaml`
5. Update `Detail_08` (spec) and `Detail_12` (index)

Growth must follow the rule in `Detail_08`: **main +2, sub +2, others +1** (Sei/Shi: main +3, sub +2).
A unit with **two sub stats** splits that budget instead of doubling it — each sub grows +1 and the
training bonus is +5% per sub rather than +10%. Nine units do this: 라이트 · 니콜 · 피그말리온 ·
아스클레피아 · 아마테라스 · 야마 · 이카리아 · 마리 · 프레이아.

**46 units in total**, with two standing exceptions. **Gaudi** has no sub stat at all
(main +2, everything else +1). **Jean** has no weapon proficiency at all, by design — she fights
bare-handed and carries no starting weapon, so any weapon she holds contributes weight and
nothing else. **Do not add a martial-arts proficiency**; the empty weapon slot is the condition
future bare-hand-only codes will key off.
Gaudi's and Light's base stats were not in the original design and are marked 🟡 in `Detail_08`.

### Adding a theme

1. Enemy portraits first — a theme without art does not ship
2. `60_enemies.yaml` — register normal/elite/boss under a new enemy `themeId`
3. `80_stages.yaml` — add to `stageThemes` with `enemyThemeId` matching, plus `stagePatterns`
   and an `ambientColor` (#RRGGBB) for the battlefield backdrop — see `Detail_07 §1.12`
4. Add a 5-slot event under `events`
5. Update `Detail_09` (roster/composition), `Detail_10` (bosses), `Detail_01` (theme table)

Themes with `enabled: false` are filtered out of the rotation by `RoundManager.ActiveThemes()`.
A stage's `stagePatterns` entry takes priority over lone `midBossId`/`bossId` spawning.

### Adding equipment

Every item is defined by four things and **the tier fixes the magnitudes** — see `Detail_14 §2`.

| Element | Field | Decided by |
| --- | --- | --- |
| Category | `category` | the item |
| Stat | `statBonuses` | *which* stat by the item, *how much* by the **tier** |
| Weight | `weight` | the **category** (never changes with tier) |
| Wear condition | `requiredProficiency` | the **category**; Clothing/Helmet/Necklace/Ring/Shoes have none |

```
티어 계수 P = T1:1 · T2:3 · T3:5 · T4:7 · T5:9
5스탯 = +P      CritRate · CritDamage · Durability = +2P     (한 장비에 스탯 하나)
방어구 고정 내구 = T1 값 × M,  M = T1:1 · T2:2 · T3:3.25 · T4:4.75 · T5:6.5
```

T1·T2 grant no code; **T3+ grant exactly one** `codeGrants` entry. Proficiency gates
`statBonuses`, `codeGrants` and durability together (`Unit.CanUseEquipmentEffects`) — without it
only the weight applies. `statBonuses` accepts the five stats plus `CritRate` / `CritDamage` /
`Durability` (`EquipmentStatKeys` parses them; `Unit.GetEquipmentSecondaryStatBonus` sums them).

### Adding a status effect

Extend `BaseEffect`, override only the hooks you need, and apply via
`unit.AddStatus(BuffStatus.Create(id, key, name, caster, owner, effect, …))`.
Pick a status ID that does not collide — check the existing constants first
(`ControlStatuses`, `ElementalReaction`, and the per-theme `…StatusIds` classes).

## Conventions

- Comments and logs are Korean, and they explain **why**, not what. Match the surrounding density.
- Commit messages are a single Korean sentence in plain present tense, describing the change from
  the player's or the system's point of view (see `git log`).
- Prefer the existing shared helpers over re-deriving the same query in each code:
  `Combat.CombatTargets` (alive enemies / allies-including-self / highest-priority pick),
  `Effects.Buffs` shared modifier effects (`ReceivingDamageMultiplierEffect`,
  `OutgoingDamageMultiplierEffect`, `PrimaryStatMultiplierEffect`, `CritMultiplierBonusEffect`,
  `DamageOverTimeApplicationEffect`, `ArmorShredEffect`, `AttackTriggeredHealingEffect`,
  `ExcessCritConversionEffect`, `ManaEfficiencyEffect`) — never re-declare a one-line
  unconditional modifier under a new name,
  `Unit.IsOnField` (the single "standing on the battlefield" predicate — never re-derive it from
  `currentCell.yPos`, which is null for summons),
  `Combat.Summons` (summon damage rules), `Effects.Neutral.Taunt` (taunt id, key and `Has()`),
  `Code.WaitForCast` (the casting-delay loop), and `AswanCombat`-style per-theme helper classes.
- A code that replaces `SkillCoroutine` wholesale must still call `NotifyActionResolved()`
  when the action actually lands — `OnNormalActionResolved` is what "per normal action"
  passives such as 철벽(6) count.
- No automated tests exist. Verify gameplay changes in Play mode, and type-check with the
  csproj-copy trick above.
