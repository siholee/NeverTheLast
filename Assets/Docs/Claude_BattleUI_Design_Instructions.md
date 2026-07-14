# Claude BattleUI Design Instructions

Use this document when asking Claude to design BattleUI for NeverTheLast. Claude's output should be structured enough for Codex to implement in Unity without reinterpreting vague visual direction.

## Context For Claude

NeverTheLast is a turn-based tactical RPG built in Unity. The player manages a five-unit party, places heroes on a grid, fights enemy waves, earns rewards, manages per-character inventories, learns skills, and builds synergies.

The UI should feel like a tactical RPG command interface, not a landing page or marketing screen. It should be dense but readable, quiet during combat, and rich when the player opens dossier panels.

## Design Principles

- Prioritize tactical board readability.
- Keep always-visible HUD small and stable.
- Put deep RPG information into dossier tabs and tooltips.
- Treat click inspection, party management, rewards, combat feedback, and tooltips as separate UI layers.
- Use compact controls and predictable panel positions.
- Avoid decorative clutter that covers units or grid cells.
- Use Korean-ready typography and allow longer Korean labels to wrap cleanly.
- Design for mouse-first PC play, with keyboard hotkeys visible only where naturally useful.

## Required Screens

Claude should design these screens or states:

1. Normal battle HUD.
2. Preparation HUD with shop/resource controls.
3. Ally click inspect panel.
4. Enemy click inspect panel.
5. Party Dossier with [B], [C], [K], [I], [S] tabs.
6. Unit-Specific Dossier opened by [X].
7. Reward overlay.
8. Combat feedback examples over the grid.
9. CK3-style tooltip and pinned nested tooltip state.

## Required Components

Claude must specify components by implementation-friendly names:

- `BattleHUD`
- `Topbar`
- `ResourceStrip`
- `PreparationControls`
- `SynergySummary`
- `ClickInspectPanel`
- `PartyDossier`
- `UnitDossier`
- `RewardOverlay`
- `CombatFeedbackLayer`
- `TooltipLayer`

For each component, Claude should include:

- Purpose.
- Position and size behavior.
- Key child elements.
- Empty/loading/disabled states.
- Interaction states.
- Data required from code.

## Hotkeys And Panels

Claude should preserve this interaction model:

- Click ally or enemy: open `ClickInspectPanel`.
- [B]: open `PartyDossier` on Battle tab.
- [C]: open `PartyDossier` on Character tab.
- [K]: open `PartyDossier` on Skills tab.
- [I]: open `PartyDossier` on Inventory tab.
- [S]: open `PartyDossier` on Synergies tab.
- [X]: open `UnitDossier` for the main character unit.
- Esc: close the topmost panel or overlay.

## Tooltip Requirements

Design a CK3-style tooltip system:

- Delayed hover open.
- Pinned tooltip mode.
- Nested linked terms.
- Keyword colors.
- Short title, body, and stat rows.
- Support for long Korean text.
- Tooltip should not cover the cursor target when avoidable.

Claude should show at least these tooltip examples:

- Skill tooltip.
- Status effect tooltip.
- Class or element tooltip.
- Item comparison tooltip.
- Synergy threshold tooltip.

## Visual Direction

The UI should feel like a serious tactical RPG with clear information hierarchy.

Recommended traits:

- Dark neutral foundation with restrained accent colors.
- Strong contrast for HP, MP, shield, rarity, danger, and positive/negative effects.
- Crisp rectangular panels with small radii.
- Icon-first controls where icons are clear.
- Dense but aligned stat rows.
- Minimal animation: panel open/close, tooltip pinning, combat text, highlight pulses.

Avoid:

- Landing-page hero composition.
- Huge decorative cards.
- Heavy gradients or single-color theme dominance.
- Large ornamental backgrounds.
- Text-heavy instructions inside the game screen.
- UI that covers the board without clear modal intent.

## Output Format Claude Should Use

Ask Claude to return the design in this exact structure:

```md
# BattleUI Design Proposal

## UX Overview

## Screen Layouts

## Component Specifications

## States And Interactions

## Tooltip System

## Visual Tokens

## Unity Implementation Notes

## Open Questions
```

Claude should include dimensions as relative layout rules, not only static pixel mockups. Example: "right inspector occupies 22-28% width on desktop and collapses into a modal below 1280px."

## Codex Handoff Rules

The design must be implementable in Unity UGUI or UI Toolkit.

Claude should:

- Name every reusable component.
- Describe anchor behavior.
- List required serialized fields.
- List icons or image assets needed.
- Define visual states for buttons, tabs, cards, tooltips, and selected units.
- Mention which data can be stubbed during the first implementation.

Claude should not:

- Invent code architecture unrelated to the Unity project.
- Require assets that do not exist without saying they are new.
- Hide important game state behind purely decorative visuals.
- Produce only a moodboard.

## Prompt Template

Use this prompt when asking Claude for a design pass:

```text
Design the BattleUI for NeverTheLast using the attached BattleUI_Overall.md and these Claude design instructions.

Return an implementation-ready design spec, not just visual inspiration.
The UI must include: BattleHUD, ClickInspectPanel, PartyDossier tabs [B]/[C]/[K]/[I]/[S], UnitDossier [X], RewardOverlay, CombatFeedbackLayer, and CK3-style TooltipLayer.

Prioritize tactical board readability, Korean text fit, Unity implementation details, and clear component naming for Codex.
Use the requested output structure exactly.
```
