using BaseClasses;
using Entities;
using StatusEffects.Effects;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    /// <summary>
    /// 원소 반응 발동 처리기.
    /// ElementSystem으로부터 반응 타입을 받아 실제 효과를 적용한다.
    /// BattleManager가 소유.
    /// </summary>
    public class ReactionHandler
    {
        private readonly BattleManager _battleManager;
        private readonly ElementSystem _elementSystem;

        public ReactionHandler(BattleManager battleManager, ElementSystem elementSystem)
        {
            _battleManager  = battleManager;
            _elementSystem  = elementSystem;
        }

        /// <summary>
        /// 원소 적용 → 반응 처리 → 최종 데미지 배율 반환.
        /// 반응 자체(상태이상, AoE 등)는 내부에서 직접 처리.
        /// </summary>
        /// <returns>데미지 배율 (증폭 반응이면 1.5 또는 2.0, 나머지는 1.0)</returns>
        public float HandleReaction(Unit attacker, Unit target, ElementType element, DamageContext ctx)
        {
            ElementType aura = target.CurrentAura;
            ReactionType reaction = _elementSystem.ApplyElement(target, element);

            if (reaction == ReactionType.None)
                return 1.0f;

            Debug.Log($"[반응] {target.UnitName}에게 {reaction} 발동 (오라: {aura}, 트리거: {element})");

            switch (reaction)
            {
                // ── 증폭 반응 (데미지 배율 반환) ────────────────────────────────
                case ReactionType.Vaporize:
                case ReactionType.Melt:
                    return _elementSystem.GetAmplifyMultiplier(aura, element);

                // ── Superconduct: 물리 방어 -40%, 2턴 ────────────────────────────
                case ReactionType.Superconduct:
                    target.AddStatusEffect("reaction_superconduct", new SuperconductDebuff(2));
                    return 1.0f;

                // ── Frozen: 대상 행동불능 1턴 ────────────────────────────────────
                case ReactionType.Frozen:
                    target.AddStatusEffect("reaction_frozen", new FrozenDebuff(1));
                    target.ControlStarts(new ControlContext(attacker, 1));
                    return 1.0f;

                // ── Burning: Pyro DoT 2턴 ────────────────────────────────────────
                case ReactionType.Burning:
                    target.AddStatusEffect("reaction_burning",
                        new BurningDoT(source: attacker, turns: 2, damageMultiplier: 0.25f));
                    return 1.0f;

                // ── Electrocharged: Electro DoT 2턴 ─────────────────────────────
                case ReactionType.Electrocharged:
                    target.AddStatusEffect("reaction_electrocharged",
                        new BurningDoT(source: attacker, turns: 2, damageMultiplier: 0.2f,
                                       element: ElementType.Electro));
                    return 1.0f;

                // ── Overloaded: AoE Pyro (BattleManager로 위임) ──────────────────
                case ReactionType.Overloaded:
                    _battleManager.HandleOverloaded(attacker, target);
                    return 1.0f;

                // ── 기타 반응: 로그만 출력 (추후 구현) ───────────────────────────
                case ReactionType.Bloom:
                    Debug.Log($"[반응 stub] Bloom — 다음 턴 Dendro 폭발 예정 (미구현)");
                    return 1.0f;

                case ReactionType.Quicken:
                    Debug.Log($"[반응 stub] Quicken — Aggravate/Spread 준비 (미구현)");
                    return 1.0f;

                case ReactionType.Swirl:
                    Debug.Log($"[반응 stub] Swirl — {element} 원소 확산 (미구현)");
                    return 1.0f;

                case ReactionType.Crystallize:
                    Debug.Log($"[반응 stub] Crystallize — 방패 생성 (미구현)");
                    return 1.0f;

                default:
                    return 1.0f;
            }
        }
    }
}
