# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NeverTheLast** is a Unity 6 (6000.4) roguelite RPG in C#. The player builds a party of 1–5 unlocked
mythological heroes and walks an **endless run** (infinite mode — the only game mode) of stages themed by
civilisation. Combat is **turn-based and fully automatic** — the player's decisions are made before the
fight (party, placement, deck), never during it.

> **2026-09 full rework (branch `rework/core-stats`).** Five stats, training, support cards, unlock
> passives, the training game mode, main/support roles, the old roster (52 allies, 154 enemies, ~20 themes)
> and all old attack effects were removed.
> The game now has **eight stats with LoL-style linear growth**, a shared item inventory with BG3-style
> short/long rests, **main-branch card visuals**, and a roster rebuilt one character at a time — currently
> **세이 (Sei), 시 (Shi), 사바흐 (Sabah), 찬드라 (Chandra), 히미코 (Himiko), 피그말리온 (Pygmalion), 바스테트 (Bastet), 잔 (Jean)**, fighting the
> **공허의 시험장** test theme. The pre-rework state is commit `a6194c9`.

Korean is the working language: comments, logs, commit messages, and all design docs are in Korean.

## Development Commands

There is no CLI build. Open the project in the **Unity Editor**.

- **Scenes**: `Assets/Scenes/MainMenu.unity` → `Assets/Scenes/Game.unity`
- **Run**: Play mode in the Editor
- **Input**: Unity's new Input System (`activeInputHandler: 2`)
- **Debug mode**: **F1** opens the debug panel in Play mode (theme lock, stage jump, party level,
  invincibility, speed, **fill both sides' ultimates**, **shield allies** — the latter exists to test
  Ran's 빅뱅). F2 wins the fight, F3 toggles ally invincibility, F4 cycles speed, F9 previews an event.
  `Core/DebugMode.cs` + `Managers/UI/DevTools/DebugOverlay.cs`, both behind
  `#if UNITY_EDITOR || DEVELOPMENT_BUILD` — see `Detail_07 §4.6`. A red banner stays on screen
  while any override is active, so balance is never measured with invincibility left on.

### Type-checking without the Editor

Unity generates `Assembly-CSharp.csproj` at the repo root. To compile-check a change without opening
the Editor, copy that csproj (so the generated one is not clobbered), add any **new** `.cs` files as
`<Compile Include="…" />`, **drop `<Compile>` entries for files that no longer exist**, redirect
`BaseIntermediateOutputPath` / `OutputPath` to a scratch folder, then `dotnet build <copy>.csproj`.
Editor scripts (`\Editor\` folders) belong to `Assembly-CSharp-Editor.csproj`; check them the same way
with a copy that references your runtime copy. Delete the copies and their output afterwards.
`LangVersion` is 9.0.

> New `.cs` files have no `.meta` until the Editor next gains focus. That is expected — do not create
> `.meta` files by hand.

## Documentation — read this first

**`Assets/Docs/Design/` is the design source of truth, and it is kept in sync with the data files.**
Before changing gameplay, read the relevant chapter; after changing gameplay, update it.

| Doc | Covers |
| --- | --- |
| `README.md` | Doc hierarchy and writing rules |
| `GDD_Main.md` / `GDD_Sub_Concepts.md` | Vision (no numbers) / concept definitions — **both carry a banner: parts predate the rework** |
| `Detail_01_Progression.md` | Run structure, stages, themes, enemy scaling, life |
| `Detail_02_Combat.md` | Battlefield, action values, damage & mitigation, effect hit, shields, control, damage tags, **card visuals (§10)** |
| `Detail_03_Character.md` | **8-stat model**, linear growth, level/EXP, mana, code system, **집중 (§4.5)**, unit schema |
| `Detail_04_Training.md` | **Abolished** — list of what the rework removed |
| `Detail_05_Economy.md` | Gold/tokens/tickets, shared items, food, reward flow |
| `Detail_06_Events.md` | Event schema, choice actions, VN presentation |
| `Detail_07_UI_Tech.md` | UI screens, manager inventory, data files, save system |
| `Detail_08_Confirmed_Characters.md` | Per-character combat spec, stats, visuals |
| `Detail_09` / `Detail_10` | Enemy roster & stage composition / boss specs |
| `Detail_11` / `Detail_12` | Code catalogue by ID / player code slots |
| `Detail_13` / `Detail_14` | Reward pool & tier odds / abolished equipment rules |
| `Detail_15_Party_Synergy.md` | Role taxonomy, archetypes, what the roster needs next |
| `Detail_16_Elements.md` | Innate vs. attached element, 28 elemental reactions (scale on 특수공격력), field states |
| `Design_Backlog.md` | Open design decisions that block work |

Writing rules that matter when editing: **a number lives in exactly one document**, unimplemented
things are marked 🔸/🔴, and the detail docs mirror `Assets/Resources/Data/*.yaml` and the code.

## Core Model

### Stats — eight stats, no derived stats

`BaseEnums.StatType`: **MaxHp · Attack · SpecialAttack · Defense · SpecialDefense · Speed · CritRate · CritDamage.**
The value in the data is the combat value; nothing is derived.

```
기초 스탯 = 기초값 + 성장치 × (레벨 − 1)                  UnitStats.LeveledBase (롤식 선형)
최종 스탯 = 기초 스탯 × (1 + Σ퍼센트) + Σ고정             체력 · 공격 · 방어 계열 · 속도
최종 스탯 = 기초 스탯 + Σ고정                             치명타 확률 · 치명타 피해
AV        = 10000 / 속도
피해      = 코드가 정한다 — 고정값 + 스탯 × 계수 (Code.StatDamage). 거의 전부 공격력 계수
받는 피해 = 피해 × K / (K + 경감 스탯),  K = 100 + 6 × (공격자 레벨 − 1)
효과 명중 = 기본 확률 × 2 × 특공 / (특공 + 특방)
```

| Stat | Role |
| --- | --- |
| Attack | The damage coefficient for almost every code. A very few use Defense or MaxHp instead (Ran uses Defense) |
| SpecialAttack | **Not a damage coefficient.** Elemental reaction power and harmful-effect hit chance only |
| Defense | Mitigates ordinary damage |
| SpecialDefense | Mitigates DoT (`CodeType.Effect`) and `ElementalReaction` damage; evades harmful effects |
| Speed | Action value |

- **Mitigation stat is chosen in one place**: `Combat.DamageMitigation.TryGetDefenseStat` —
  `TrueDamage` → none, DoT / `ElementalReaction` → SpecialDefense, everything else → Defense.
- K scales with the **attacker's** level so same-level mitigation stays constant as levels grow without
  bound (infinite mode). Every stat-vs-stat contest is a **ratio**, never a difference, for the same reason.
- Stats live in `Entities/UnitStats.cs` (base + growth arrays). Data comes from `Data/CombatantData.cs`,
  which `UnitData` and `EnemyData` both extend: `hpBase/hpGrowth`, `atkBase/…`, `spAtkBase/…`,
  `defBase/…`, `spDefBase/…`, `speedBase` (default 100), `critRateBase` / `critDamageBase` in
  percentage points (defaults 5 / 50).
- Enemies use the same formula; their level is the stage. There is no late-game growth acceleration.
- `Unit.AttributesUpdate()` recomputes the eight final stats and **preserves the HP ratio**.
- **There is no training or equipment.** Allies grow only by EXP levels and code effects.

### Mana

Mana fills on **actions and hits**, not time: normal action +20, hit taken +5 (not DoT, not
`BonusDamage`), kill +10 (`Unit.ManaPer*`), each times `ManaEfficiencyCurr`. `manaMax` is a per-unit
YAML value (default 100; Ran 120) that nothing else modifies. `CastUltimateCode` empties mana, then casts.

### Turn-based combat — `ActionScheduler`

Combat is not real time. `Managers/ActionScheduler.cs` owns the clock.

- AV = `10000 / Speed`; everyone's AV is decremented by exactly what the next actor needs.
- **Actions vs. triggers.** Only these are *actions* — **일반행동** (normal), **대체행동** (substitute —
  takes the normal action's slot), **추가행동** (additional), **궁극기** (ultimate). Passive codes,
  effects and event hooks are *triggers*: they never act, they only queue an action or change a value.
- Priority: `PriorityAdditional(0) → Additional(2) → Coordinated(3, unused) → Special(4) → Ultimate(5) → Normal(6)`.
  0 is just "an additional action a passive queued" — both 0 and 2 fire `OnAdditionalActivates`.
  Ran's 빅뱅 is a priority additional action.
- `SpecialCode` is an optional trigger-only slot. It uses `OnSpecialActivates`, never counts as an additional
  action, and the Lokapala gate orders Vayu (-100), Chandra (-50), then the remaining participants.
- **Ultimates do not consume a turn** and do not reset AV. Resource is the only gate.
- A **대체행동** still counts as one normal action — call `NotifyActionResolved()` even when it
  deals no damage.
- Duplicate suppression: one `(unit + kind + key)` may sit in the queue at a time.
- **Turns are the only gameplay time axis.** Durations, periodic passives, internal cooldowns and
  집중 all count turns. Wall-clock is only for presentation. `ActionValuePerSecond` survives for stats only.

Status durations and DoT advance on **`BaseEffect.OnOwnerTurn()`**. A passive that fires "every N turns"
extends **`PeriodicTurnPassive`**; an internal cooldown uses **`Combat.TurnCooldown` / `TargetTurnCooldown`**.

### 집중 (concentration) — `Combat/Concentration.cs`

Ties an effect's lifetime to its **caster**: counts the caster's turns (decrements on `OnTurnEnd`) and
ends early when the caster is controlled (`OnControlStarts`), dies, or the round ends. The code passes
an end callback that receives `EndReason` (Expired / Interrupted / Replaced / RoundEnd) and tears the
effect down. Ran's 창세의 노래 is the first user. See `Detail_03 §4.5`.

### Grid

`GridManager` — `x ∈ [-2, 2] excluding 0`, `y ∈ [1, 4]`. Negative x is the ally side.

| | Front | Rear |
| --- | --- | --- |
| Ally | x = -1 | x = -2 |
| Enemy | x = 1 | x = 2 |

Four slots per column, eight per side. Bench is a separate 5-slot array, not part of the field.
Use `GetFrontColumn(isEnemy)` / `GetRearColumn(isEnemy)` rather than hard-coding.

## Architecture

### Managers

Singletons via `Manager.Instance`, in `Assets/Scripts/Managers/`.

| Manager | Responsibility |
| --- | --- |
| `GameManager` | State machine, phase timers, life, event flow. Owns `ActionScheduler` |
| `RunManager` | Run lifetime (start/save/restore), event history, **single entry point for stage advance** |
| `RoundManager` | Stage/round/theme selection, enemy composition and placement (staggered entry), boss slots |
| `GridManager` | Cell creation, spawning (`SpawnUnit(..., playEntry, entryDelay)`), targeting, `heroList` / `enemyList` |
| `ActionScheduler` | Turn scheduling (plain class, not a MonoBehaviour). Announces an action only when it actually begins through `Combat.ActionAnnouncer`; forecast reservations expose stable keys so the HSR-style order rail can animate insertions/reorders without teleporting |
| `EventScheduler` | Event queue for inserting events at arbitrary points (plain class) |
| `RewardManager` | Reward pool generation and application |
| `CharacterSelectionManager` | Party rules (1–5, unlocked only, no duplicates). `IsUnlocked` is the single eligibility check; the first pick is the **lead** (`FindLeadUnit`) |
| `InventoryManager` | Gold/tokens/tickets, equipment validation |
| `DataManager` | Generic YAML loader (`Load<T>`, YamlDotNet) |
| `UIManager` | Screen routing |
| `AudioManager` | BGM + YAML logical SFX queues, pooled sources, speed merging, code mixer buses, ultimate ducking, harness logs. (`SfxManager` is an empty shell kept because the scene references it) |
| `DragAndDropManager` | Unit placement |

`SettingsManager` and the save system live in `Assets/Scripts/Core/`. Save format is **v11**
(`RunSaveData.CurrentVersion`); the rework discarded older saves.

### Data files — `Assets/Resources/Data/`

| File | Content | DTO |
| --- | --- | --- |
| `00_intro.yaml` | Intro sequence | `IntroData.cs` |
| `10_units.yaml` | Player units (Sei only) | `UnitData.cs` : `CombatantData` |
| `20_codes.yaml` | Code display data (passive/normal/ultimate) | — |
| `30_synergies.yaml` | Party roles, archetypes, per-lead recommended lineups | `SynergyData.cs` |
| `40_items.yaml` | Shared items and long-rest food | `ItemData.cs` |
| `50_tokens.yaml` | Token definitions | `TokenData.cs` |
| `60_enemies.yaml` | Enemies (test theme: 3 normal, 1 elite, 1 boss) and their `drops` | `EnemyData.cs` : `CombatantData` |
| `70_rounds.yaml` | Round types / composition patterns (fallback only) | `RoundData.cs` |
| `80_stages.yaml` | Themes, events, fixed bosses | `StageData.cs` |
| `90_rewards.yaml` | Per-round reward tier odds, common drops, consumables | `RewardData.cs` |
| `95_sfx_cues.yaml` | Logical SFX queues, clip candidates, random volume/pitch, voice/interval limits and ducking | `SfxCueData.cs` |
| `96_vfx.yaml` | Standard VFX parts, prepare/cast/hit/affected/screen skill compositions, status bindings | `VfxData.cs` / `VfxDefinitionCatalog` |

**Code IDs are per-slot namespaces.** Passive 201 and ultimate 201 are unrelated; `CodeFactory`
resolves them in separate switches.

| Space | Rule |
| --- | --- |
| Unit ID | 20-slot block per faction — Akasha 1~, Greek 20~, Takamagahara 40~, Vedic 60~, Nord 80~, Rome 100~, Egypt 120~, Mexica 140~, Gaul 160~, 화령 180~ |
| Ally normal / ultimate | **same number as the owner's unit ID** (Sei = 1 → N 1, U 1) |
| Ally unique passive | **owner's unit ID + 200** (Sei → 201) |
| Retired code range | 400~499 (equipment-granted passives were removed) |
| Enemy codes | 1000+ in per-theme 100-slot blocks. Shared test-theme codes: N 1001 돌격 / 1002 사격, U 1001~1004 |

Changing unit IDs breaks saves: bump `RunSaveData.CurrentVersion` with it.

### Codes (skills)

`Assets/Scripts/Codes/` — `Base/`, `Passive/`, `Normal/`, `Ultimate/`.

Every unit has **one unique passive + one normal action + one ultimate** — nothing else. There are
no unlock passives, no code capacity and no transfer. Codes have no cooldowns.

- `Code.StatDamage(flat, stat, ratio)` builds damage from the caster's current final stat;
  `RollCritical(out mult)` rolls crit. The old `Power` system is gone.
- **Normal actions extend `BaseNormalCode`**: `WaitForCast → SelectTarget → AttackTargetRouter.Prepare → ResolvePreAttack →
  Targeting.Announce → Deliver → NotifyActionResolved`. Override `BaseDamage(target)`, and optionally `IsContact`,
  `ProjectileFor(target)`, `OnAttackResolved`. Contact attacks charge the card into the target;
  others throw a projectile from the card's exact centre.
- **Ultimates extend `BaseUltimateCode`** and fill one `Perform()` coroutine (casting flag, ally/enemy
  lists, priority target, `BuildContext` are provided).
- A code that replaces `SkillCoroutine` wholesale must still call `NotifyActionResolved()`.
- Codes carry a grade (`Normal` silver / `Enhanced` gold / `Unique` purple); `IsUniquePassive` forces `Unique`.
- Combat resources shown on the card (Ran's 창세의 결정) register a display name with
  `Combat.CombatResourceLabels.Register(id, name)`.

### Effects and statuses

`BaseEffect` (`Effects/Base/`) has optional query hooks — `StatFlatModifier(stat)` /
`StatPercentModifier(stat)`, `ManaRecoveryMultiplierModifier`, `OutgoingDamageModifier`,
`ReceivingDamageModifier(unit, ctx)`, `DefenseStatMultiplierModifier`, `TryPreventDeath`,
`NegativeStatusResistanceChanceModifier`, `TargetPriorityAdditiveModifier`, `BlocksNormalAttack`,
healing/shield multipliers, and lifecycle (`OnApply` / `OnOwnerTurn` / `OnRemove`).
Override only what a code needs. A status effect that should glow on the card implements `ICardAura`.

Statuses (`UnitStatus`) wrap effects with id/key/duration/stack policy. Same key = same status.
Harmful statuses cast by an enemy roll **effect hit** in `UnitStatusController.Add` unless
`UnitStatus.SkipEffectHitRoll` is set (for codes that already rolled their own chance).

Damage flows through `Unit.TakeDamage(DamageContext)`:
`Unit.AnyDamageIncoming (static, pre-mitigation) → OnBeforeDamageTaken → CalculateFinalDamage
(DamageMitigation) → shield → HP → TryPreventDeath → hit mana → OnAfterDamageTaken / OnDamageDealt /
Unit.AnyDamageDealt → Die()`. `Die()` plays the card-shatter first, grants kill mana, then deactivates.
Code that subscribes to a static `Unit.Any*` event must unsubscribe on death / round end.

### Damage tags — `BaseClasses/DamageTags.cs`

The **ten-thousands digit is the category**; an attack takes one from each band.
10000 target scope · 20000 attack kind (`NormalAttack`, `UltAttack`, `AdditionalAttack`,
`CounterAttack`, `PassiveAttack`, `ElementalReaction`) · 30000 contact · 40000 conversion
(`ShieldPenetration`, `TrueDamage`, **`BonusDamage`** — damage added on top of another hit; it never
triggers further add-on damage and gives no hit mana). The weapon band (50000) is gone.

> Damage-over-time (`CodeType.Effect`) carries an **empty tag list**. Code that checks for contact
> must ask whether `ContactAttack` is present — never "is not `NonContactAttack`".

### Combat visuals — `Effects/Vfx/` + `Entities/View/`

The approved option-2 screen chrome is a bright sky citadel: `BattlefieldBackdrop` layers the generated
sky/pearl-glass environment, a low-opacity floor pattern, silver-blue formation marks and a thin centre
divider. Functional UI uses pearl-white light glass with `#253443` text and sky-blue interaction lines;
only panels floating directly over portrait/VFX art keep smoke glass and white text.
Battle/default-preparation framing uses occupied cards rather than the full 2×4 grid, compacts an isolated
rear column inward, scales sparse 1–3-card scenes up, and smoothly follows roster changes after `HoldCamera`.
The main menu uses `main_menu_sky_citadel_1672x941.png` behind Sei and a single right-side light-glass rail.

Every unit is a **free-standing illustration card** (`UnitCardView`): the full portrait stands without
a card-sized frame or rectangular hit-flash plate. The upper-right HUD is a bottom-up liquid element
energy orb with an element glyph, rotating ready ring, and a numeric combat-resource badge. Resources that require it
remain visible at zero and expand to show exact multi-digit values. HP/shield,
the thin action-progress line, and six mapped status icons live in one lower liquid-glass capsule.
Generated HUD PNGs are loaded from `Resources/UI/CardHud/` with runtime `Sprite.Create`; the capsule uses
a 96 px 9-slice border. The bright option-2 battlefield keeps this capsule as one `#26384A` smoke-glass
panel with white text/glyphs. A broad cool contact shadow separates white portrait art from the sky/pearl
board; do not add an enlarged portrait silhouette because it reads as a sticker fringe. A procedural colourless
alpha gradient dissolves only the lower artwork into the panel. The orb shell is neutral
silver-blue chrome even at 0%, and only its liquid carries the element colour. `StatusIcons` resolves
explicit status ID/key mappings before effect/category fallbacks.

| Module | Role |
| --- | --- |
| `CardMotion` | Facade: `Charge` (contact — lift, wind-up, dash, impact callback, return), `Throw`, `Channel` |
| `SkillFx` / `FlipbookFx` | Maple-style Cast-behind/front, optional Projectile, per-target timed Hit, Screen and Ground nodes; plays generated row×column sheets with exact impact-frame callbacks |
| `ErosionFx` | Built-in-pipeline hard-edge noise reveal/dissolve with white→element→dark edge bands; shared death silhouettes use it |
| `ProjectileFx` / `ProjectileStyle` | Owns centre-to-centre motion and the single impact callback; Hovl Flash/Projectile/Hit sets ride this path at default card ratios 0.30/0.34/0.40, track moving targets, and keep cool saturated alpha underlays under the projectile, launch flash, and hit on the bright board |
| `ImpactFx` | Contact Slam uses Hovl `Hit 17 nova violet`, a saturated directional impact star, a stronger 0.066–0.08 s hit stop, recoil/shake, and a critical-only `Dragon punch flash`; procedural Rock/ElementBurst/VoidPierce remain small supporting accents |
| `EntryFx` | Element-matched Hovl Flash/Hit at the feet, one non-additive edge sweep and landing dust; scales normal / elite / boss separately |
| `CardLifecycleFx` / `CardShatter` | Death exit: hit-stun, 1–2-frame white silhouette flash, hard-edge portrait erosion and upward elemental fragments; bosses add focus, explosion and shake |
| `VfxSpace` / `VfxParticles` | Procedural sprites + additive material and pooled particles. `AimFrom` derives rotation/mirroring from the caster→target vector; `BuildTargetHitSequence`/`ResolveTargetHits` schedule target-local hits after one caster action without drawing any route between targets. `ResolveSortingLayer` puts all combat renderers on the dedicated `CombatVfx` layer in front of cards; `VfxTiming` preserves at least 2–3 visible frames at 8× |
| `VfxArt` | Loads shared textures plus small sandstone debris, radial dust and `DustCone_Ochre`; `SandSlab_Main`, `AirShell`, `GeoSigil`, `EntryCircle`, `EntrySeal_SilverBlue_v2`, and `ShieldArch_Amber_v2` are retired from code |
| `Presets/HandpaintFx` / `VfxStyleSettings` | Wraps the purchased Unity Asset Store `Effect Collection - Handpaint Spritesheet` copies under `Resources/VfxTex/Handpaint/`, preserving per-sheet grids/FPS while applying runtime tint, split alpha-body/luminance-core composition, frame blending, low-alpha cell-boundary rejection and backdrop compensation. F1 and harness `vfxStyle` switch Character Default / Legacy / Handpaint; defaults are Handpaint for all eight current characters, with the previous paths preserved under Legacy |
| `VfxDefinitionCatalog` / `VfxComposer` | Loads `96_vfx.yaml` and composes reusable slots. `incomingImpact` aligns each sheet's authored `impactAnchor` to the portrait while leaving its body on the attacker side; `horizontalFacing` keeps Smoke3 level and mirrors it without target-row rotation. `peakHoldSeconds` keeps only a short component's crest visible, while a separate slot `impactDelay` preserves gameplay callbacks. Support buff reverses `Impact1` above the head, shield rotates a neutral-silver `Ball1` at the torso, cleanse/heal use their own pack sheets, and every support finish points into a 1.0→1.35→1.0 lower-HUD pulse. |
| `HovlFx` | Pools Resources-copied Hovl Flash/Projectile/Hit prefabs, disables movers/colliders/gravity and loose Sparks/Trail/Glow/nova billboard systems, and applies tint, alpha, card scale, overlay sorting, scaled-time playback, bright-board alpha backing, short nova-hit lifetimes, and stage-entry cleanup (`Resources/VfxPrefabs/Hovl/`). Geo/Void launch cues reuse the mesh `Dragon punch flash` magic circle; their projectile/hit silhouettes remain nova 18/17 |
| `ElementPalette` | Element colours, also used for damage-number accents |
| `Presets/RadiantVolleyFx` | Shared six-shot curved volley: 0.05 s spacing, six separated in-card impact points, and exactly one callback on the third shot's impact crest. Projectile art, impact sheet, palette and SFX cue are injected by style; `MirrorHorizontal` flips curves, launch offsets, distributed impacts and art together |
| `Presets/BowShotFx` | Legacy comparison preset retaining the old bow crest, heraldic impact and curved flight. The current Void Marksman recreates the visual grammar from `96_vfx.yaml` without projectile travel |
| `Presets/HeavyStrikeHitFx` | Archived dedicated implementation of the in-place recoil and target-centred heavy impact. The current Void Crusher recreates it from standard `96_vfx.yaml` parts |
| `EnemyNormalFx` / enemy ultimates | Redirected attacks keep the original attacker's class, colour and hit VFX. Wolf Smash is an enhanced version of the same three-line claw. Marksman uses a dark-edged Muzzle1 and horizontal Smoke3; Volley reuses their short crest variants for three distinct cast/hit/SFX pulses. Vanguard's enlarged dark Slash4 keeps its approved face/torso anchor. Dragon uses boss-scaled caster-twin Fire3 and an anchored, dark-bodied Explosion1 for both normal and ultimate hits. Damage counts and callback timing remain unchanged. |
| `GeoFx` | Sei defaults to one forward-directed Yellow cast plus six portrait-centred platinum impacts, with no projectile flight and exactly one callback at the third crest. Big Bang uses one directed cast, seven target-local shatters with the original selection cadence and 0.242 s callbacks, then only the real party-shield `affected`. Genesis uses one directed cast and a single combined buff/shield `affected` on allies other than Sei. Previous generated/projectile/extra-debris paths remain Legacy; the retired ground spike remains `Presets/RisingSpikeFx.Play(...)` |
| `ChandraFx` | Chandra defaults to one forward-directed Blue/White cast plus six portrait-centred moon impacts, with no projectile flight and one callback at the third crest. Soma removes reverse convergence and `MoonSanctuaryDome`: one directed cast is followed only by the selected non-caster ally's buff `affected`; the code grants no shield, so the VFX shows none. Changya starts all non-caster ally buff cues and all hostile target-local hits at the same instant, retaining only the short multi-hit rhythm and no connecting route. Previous generated/reverse-convergence/dome paths remain Legacy |
| `ShiFx` | Destruction Slash uses one `AimFrom`-directed `Slash1_Blue` caster-card swing followed by the shared `hit.claw.triple` on the target portrait. Shi-only data tints the alpha body black, edge blood red, and core crimson. Final Bell is a persistent ultimate: after every third slash, exactly one `Smoke3` spreads horizontally—small on the struck target at 3 and centre-out across the living enemy formation at 6/9. Tier 9 keeps the same size and changes contrast/core/screen rhythm only. Reprise waits for death departure and field layout completion before the next cast. Legacy keeps the old procedural crimson rake; `Presets/FlyingSlashFx.Play(...)` remains archival |
| `SabahFx` | Character-default normal attacks use `AimFrom` for the stationary `Slash1_Purple` and a portrait-confined `Lightning2_Purple`, preserving the ~0.64 s callback without spilling onto the floor or neighbours. Lightning Slash uses one caster-card swing followed by target-local lightning hits at 0.09 s intervals for up to three targets, with no middle blade or route. Azrael uses exactly one recoloured `Slash5_Blue` draw, one moderate formation-centre lightning strike, then centre-out `Smoke3`; the smoke opens one existing damage callback per enemy while shared critical remains unchanged. The former erosion/pool/card-travel/rake remains `Presets/ShadowAmbushFx` under Legacy |
| `EmberFx` | Himiko defaults to one fan-shaped Red cast and one portrait-centred Red Explosion hit; an attack-applied Blaze mark has no extra affected cue. At three Blaze stacks, exactly one causal Smoke burst replaces the mark cue without changing status/damage order. Pygmalion is a caster: short rust-orange twin `Fire3` flames precede one target-centred `Explosion2_Red` fire hit and one callback. Sharp Thorns follows its cast with the generic buff hit recoloured rose and no ground spike. Former travelling/generated paths remain Legacy |
| `BastetFx` | Bastet is internally `beast`. U122 replays N122's exact card motion, claw and SFX; only the action-order kind is Additional. P322 pauses the hostile wind-up halfway, clears focus, inserts a Passive action-order announcement, plays the short guard motion/crystal cue, restores focus, then lets the original attacker's hit VFX land on the redirected guard. |
| `JeanFx` | Jean is internally `fighter`: a short card advance replaces cast VFX, then one hit stage shows three dark blue FireAttack punch echoes and one portrait-centred white/steel impact before Smoke3 is causally pushed back from the same contact point. P207 shares Bastet's half-wind-up pause, focus release and Passive announcement, then uses the same strike grammar faster before a surviving hostile action resumes. U7 is one self-only sword-dance cast made from five readable blue/silver Slash5 silhouettes at a card-width radius, with no separate affected cue |

Cards animate in `LateUpdate`; `Tick` only refreshes the HUD. A flying card gets a +600 sort boost.
For every new or reworked direct attack—swords, magic, bows, beasts, allies, and enemies—the default
grammar is an in-place cast/fire effect plus a target hit effect. Do not add projectile flight or card travel;
only short in-place recoil, retreat, or partial advance is allowed. Multi-target actions use one caster action
followed by target-local hits, with no connecting blade, line, or route. Passive/ultimate follow-ups continue
from the finished first hit at the same target rather than restarting the cast cycle. Their visuals are composed
from `96_vfx.yaml` parts.
The old `CharacterView`, `AttackVisuals`, `MeleeVfx`, `CombatVfx` and `Effects/Projectiles` are gone —
do not reintroduce weapon-class visuals.

## Common Tasks

### Adding a unit

1. `10_units.yaml` — `characterType: Unlocked | Locked` (no main/support flags), eight `…Base` / `…Growth`
   pairs, `manaMax`, `codes`, tags, `archetypeTags`. No `levelPassives` or proficiencies — units have no unlock codes or equipment. Reference growth per level: HP +90 · ATK +5 · SpATK +3.5 · DEF +5 · SpDEF +4
   (`Detail_03 §1.1`); speed and crit do not grow unless the unit needs it.
2. Portrait/standing in `Resources/Sprite/{Portraits,Standings}/Allies`. Portrait imports are enforced by
   `Editor/PortraitTextureImportPolicy.cs`: RGBA32 uncompressed, mipmaps off, Bilinear and Clamp. Keep the
   source PNG alpha clean; do not add per-file compression overrides.
3. Implement the three codes under `Codes/[Passive|Normal|Ultimate]/` (`BaseNormalCode`,
   `BaseUltimateCode`, `UniquePassiveCode`), register in `CodeFactory`
4. Register display data in `20_codes.yaml`; add the unit to `30_synergies.yaml`
5. Compose the unit's visuals from standard parts in `96_vfx.yaml` using the SFX logical key. Prefer colour,
   scale, direction and timing parameters over a new per-skill drawing; give custom statuses an `affected`
   binding only when the category default is insufficient.
6. Add the unit's normal/passive/ultimate prefixes to `SfxCueRoutes`, register their phase queues in
   `95_sfx_cues.yaml`, and map only the trimmed clips actually used under `Resources/Audio/Sfx/`
7. Update `Detail_08` (spec), `Detail_11` / `Detail_12` (index), `Detail_15` (roles)

Direct attacks that support target substitution must call `Combat.AttackTargetRouter.Prepare` for the complete target list,
then synchronously `ResolvePreAttack` after redirects and before `Targeting.Announce`, VFX, or damage. Re-check `CanContinue`
after interception so a dead or controlled attacker cancels the original action. `Passive` ultimate resources stay full in the HUD,
do not receive/spend mana, and start/stop their persistent code with the round lifecycle.

### Adding a theme

1. Enemy portraits first — a theme without art does not ship
2. `60_enemies.yaml` — register normal/elite/boss under a new enemy `themeId`
3. `80_stages.yaml` — add to `stageThemes` with `enemyThemeId` matching, plus `stagePatterns`,
   a Resources-relative `backgroundKey` (no extension), and an `ambientColor` (#RRGGBB) tint — see `Detail_07 §1.12`
4. Add a 5-slot event under `events`
5. Update `Detail_09` (roster/composition), `Detail_10` (bosses), `Detail_01` (theme table)

Themes with `enabled: false` are filtered out of the rotation by `RoundManager.ActiveThemes()`.
A stage's `stagePatterns` entry takes priority over lone `midBossId`/`bossId` spawning.

### Adding a shared item

All items live in `InventoryManager.ItemIdsInHand`; units do not carry or equip them. Add the record to
`40_items.yaml` and keep the DTO in `ItemData.cs` in sync. Long-rest food uses `category: Food`, a positive
`foodValue`, and a `shopPrice`; connect deliberate drops through enemy `drops` or `90_rewards.yaml`.

Preparation offers only shop, rest, shared inventory and deck editing. Short rest heals the living party;
long rest consumes food, revives and fully restores the party, and refills short-rest charges. The exact
costs and recovery values live in `Detail_01` / `Detail_05` and `GameManager`.

### Adding a status effect

Extend `BaseEffect`, override only the hooks you need, and apply via
`unit.AddStatus(BuffStatus.Create(id, key, name, caster, owner, effect, …))`.
Pick a status ID that does not collide — check the existing constants first
(`ControlStatuses`, `ElementalReaction`, per-code constants such as `SeiSongOfGenesis.BuffStatusId`).

## Conventions

- Comments and logs are Korean, and they explain **why**, not what. Match the surrounding density.
- Commit messages are a single Korean sentence in plain present tense, describing the change from
  the player's or the system's point of view (see `git log`).
- Prefer the existing shared helpers over re-deriving the same query in each code:
  `Combat.CombatTargets` (alive enemies / allies-including-self / highest-priority pick),
  `Combat.DamageMitigation` (mitigation stat, effect hit, reaction amplifier),
  `Combat.Concentration`, `Effects.Buffs` shared modifier effects (`StatModifierEffect`,
  `ReceivingDamageMultiplierEffect`, `OutgoingDamageMultiplierEffect`, `ArmorShredEffect`,
  `ManaEfficiencyEffect`) — never re-declare a one-line unconditional modifier under a new name,
  `Unit.IsOnField` (the single "standing on the battlefield" predicate — never re-derive it from
  `currentCell.yPos`), `Effects.Neutral.Taunt` (taunt id, key and `Has()`), `Code.WaitForCast`
  (the casting-delay loop), and `Effects.Vfx.CardMotion` for any card movement.
- A code that replaces `SkillCoroutine` wholesale must still call `NotifyActionResolved()`
  when the action actually lands — `OnNormalActionResolved` is what "per normal action" passives count.
- No automated tests exist, and the old verification menus were removed with the rework. Verify
  gameplay changes in Play mode, and type-check with the csproj-copy trick above.
