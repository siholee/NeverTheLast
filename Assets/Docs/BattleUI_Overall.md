# BattleUI Overall Design

This document defines the intended BattleUI structure for NeverTheLast before the full rebuild. The battle scene UI has been cleared, so this spec should be treated as the source map for the next UI pass.

## Design Goal

BattleUI should keep the tactical board readable while still supporting deep RPG information. The main screen should stay sparse. Deeper information should open through click inspection, party dossier tabs, unit-specific dossier panels, reward overlays, combat feedback, and CK3-style tooltips.

## Core Layers

### 1. Battle HUD

Always visible during battle and preparation.

- Topbar: stage, round, phase, timer or enemy count, GameSPD, menu.
- Resources: tokens, reroll tickets, gold or other run currency if added later.
- Preparation controls: start round, reroll, shop tier controls, unit management shortcuts.
- Compact synergy summary: active synergy chips, stack counts, missing-threshold warning markers.

The HUD should be narrow and predictable. It should not cover the grid or compete with unit readability.

### 2. Click Inspect Panel

Clicking any unit opens a compact unit info panel.

- Ally click: name, level, HP, MP, shield, class, element, equipped items, active statuses, cooldowns, current target.
- Enemy click: name, level, HP, MP, shield, enemy type, intent, elite modifier, statuses, resistances, current target.
- The panel should be fast and lightweight. It is not the full dossier.
- It should update live while the selected unit remains active.

### 3. Party Dossier

The Party Dossier is one ally-focused panel that shows all five party units together. It is opened with tab hotkeys and should support direct tab entry.

- [B] Battle: five-unit combat overview, HP, MP, shield, class, element, equipment summary, active statuses, cooldowns.
- [C] Character: background, class identity, growth notes, progression history, personality or story metadata.
- [K] Skills: learned skills, learnable skills, locked skills, prerequisites, mana cost, cooldown, damage tags, status effects, targeting rules, cast priority.
- [I] Inventory: each hero has a personal inventory like Baldur's Gate 3. Show carried items, equipped items, transfer rules, item comparison, and usable item restrictions.
- [S] Synergies: active team synergies, contributing units, missing classes/elements, thresholds, active bonuses, projected changes when a unit is swapped.

The Party Dossier is the main team-management surface. It should be richer than the click inspect panel but still readable during preparation.

### 4. Unit-Specific Dossier

[X] opens the hero-specific dossier for the main character unit. Later, clicking a hero from the Party Dossier can open that hero's unit-specific dossier.

Recommended content:

- Full biography and background.
- Personal growth path.
- Personal skill tree and learning choices.
- Personal inventory and equipment.
- Upgrade history.
- Current run modifiers affecting that hero.

This panel can be deeper and more narrative than the Party Dossier.

### 5. Reward Overlay

Rewards need a dedicated modal/full-screen overlay because they are a major decision point.

- Reward cards with type, rarity, title, description, affected target, and requirement text.
- Preview of target unit or team effect.
- Comparison tooltip for item/stat changes.
- Synergy and skill-change indicators.
- Confirm, cancel, and reroll/skip if those mechanics exist.

Reward UI should pause normal interaction until the player chooses or exits the reward flow.

### 6. Combat Feedback Layer

Combat feedback should be separate from panels so it can remain readable over the grid.

- Floating damage, healing, shield, mana, and status numbers.
- Crit, block, miss, resist, immune, and status-applied labels.
- Skill cast labels.
- Target lines, tile highlights, and area-of-effect previews.
- Enemy spawn warnings.
- Victory, defeat, round start, and round end banners.

Feedback should be visually expressive but short-lived. Avoid permanent clutter.

### 7. Tooltip System

Use a CK3-style tooltip model.

- Delayed hover tooltip.
- Pinned tooltip mode.
- Nested tooltip links for stats, statuses, classes, skills, items, synergies, and keywords.
- Keyword coloring.
- Clean Korean text wrapping.
- Mouse and keyboard/gamepad support where practical.

Tooltips should explain game logic without forcing the player into full dossier panels.

## Hotkey Summary

- Click unit: open Click Inspect.
- [B]: open Party Dossier on Battle tab.
- [C]: open Party Dossier on Character tab.
- [K]: open Party Dossier on Skills tab.
- [I]: open Party Dossier on Inventory tab.
- [S]: open Party Dossier on Synergies tab.
- [X]: open Unit-Specific Dossier for the main character unit.
- Esc: close the topmost overlay or panel.

## Implementation Notes

- Keep panel logic separate from data formatting where possible.
- Prefer one UI root canvas for normal BattleUI, with separate high-sorting overlay roots for reward and modal flows if needed.
- Use named prefabs and components that map to this document: `BattleHUD`, `ClickInspectPanel`, `PartyDossier`, `UnitDossier`, `RewardOverlay`, `CombatFeedbackLayer`, `TooltipLayer`.
- Every dynamic text field should tolerate missing data and inactive units.
- UI should support both preparation and round-in-progress states.
- Inventory and skill-learning controls can start as read-only in combat if interaction timing is not finalized.

## Rebuild Priority

1. Battle HUD shell.
2. Click Inspect for ally/enemy.
3. Combat feedback layer basics.
4. Reward overlay.
5. Party Dossier [B], [K], [I].
6. Synergy tab [S].
7. Character tab [C].
8. Unit-Specific Dossier [X].
9. CK3-style tooltip nesting and pinning.
