using System.Collections.Generic;
using BaseClasses;
using Entities;
using static BaseClasses.BaseEnums;

namespace Managers
{
    /// <summary>
    /// 원소 오라 관리 및 반응 테이블 조회.
    /// Genshin Impact 원소 반응 테이블을 정확히 구현.
    /// BattleManager가 소유하며 ReactionHandler에서 조회.
    /// </summary>
    public class ElementSystem
    {
        // (오라 원소, 트리거 원소) → 반응 타입
        private static readonly Dictionary<(ElementType, ElementType), ReactionType> _table
            = new Dictionary<(ElementType, ElementType), ReactionType>
        {
            // ── Pyro 오라 ───────────────────────────────────────────────────────
            { (ElementType.Pyro,    ElementType.Hydro),   ReactionType.Vaporize },       // ×1.5 (트리거 Hydro)
            { (ElementType.Pyro,    ElementType.Electro), ReactionType.Overloaded },
            { (ElementType.Pyro,    ElementType.Cryo),    ReactionType.Melt },           // ×2.0 (트리거 Cryo)
            { (ElementType.Pyro,    ElementType.Dendro),  ReactionType.Burning },
            { (ElementType.Pyro,    ElementType.Anemo),   ReactionType.Swirl },
            { (ElementType.Pyro,    ElementType.Geo),     ReactionType.Crystallize },

            // ── Hydro 오라 ──────────────────────────────────────────────────────
            { (ElementType.Hydro,   ElementType.Pyro),    ReactionType.Vaporize },       // ×2.0 (트리거 Pyro)
            { (ElementType.Hydro,   ElementType.Electro), ReactionType.Electrocharged },
            { (ElementType.Hydro,   ElementType.Cryo),    ReactionType.Frozen },
            { (ElementType.Hydro,   ElementType.Dendro),  ReactionType.Bloom },
            { (ElementType.Hydro,   ElementType.Anemo),   ReactionType.Swirl },
            { (ElementType.Hydro,   ElementType.Geo),     ReactionType.Crystallize },

            // ── Electro 오라 ─────────────────────────────────────────────────────
            { (ElementType.Electro, ElementType.Pyro),    ReactionType.Overloaded },
            { (ElementType.Electro, ElementType.Hydro),   ReactionType.Electrocharged },
            { (ElementType.Electro, ElementType.Cryo),    ReactionType.Superconduct },
            { (ElementType.Electro, ElementType.Dendro),  ReactionType.Quicken },
            { (ElementType.Electro, ElementType.Anemo),   ReactionType.Swirl },
            { (ElementType.Electro, ElementType.Geo),     ReactionType.Crystallize },

            // ── Cryo 오라 ───────────────────────────────────────────────────────
            { (ElementType.Cryo,    ElementType.Pyro),    ReactionType.Melt },           // ×1.5 (트리거 Pyro)
            { (ElementType.Cryo,    ElementType.Hydro),   ReactionType.Frozen },
            { (ElementType.Cryo,    ElementType.Electro), ReactionType.Superconduct },
            { (ElementType.Cryo,    ElementType.Anemo),   ReactionType.Swirl },
            { (ElementType.Cryo,    ElementType.Geo),     ReactionType.Crystallize },

            // ── Dendro 오라 ─────────────────────────────────────────────────────
            { (ElementType.Dendro,  ElementType.Pyro),    ReactionType.Burning },
            { (ElementType.Dendro,  ElementType.Hydro),   ReactionType.Bloom },
            { (ElementType.Dendro,  ElementType.Electro), ReactionType.Quicken },

            // ── Quicken 오라 (Quicken 반응 후 생성) ──────────────────────────────
            { (ElementType.Anemo,   ElementType.Pyro),    ReactionType.Swirl },
            { (ElementType.Anemo,   ElementType.Hydro),   ReactionType.Swirl },
            { (ElementType.Anemo,   ElementType.Electro), ReactionType.Swirl },
            { (ElementType.Anemo,   ElementType.Cryo),    ReactionType.Swirl },

            // ── Geo 오라 ────────────────────────────────────────────────────────
            { (ElementType.Geo,     ElementType.Pyro),    ReactionType.Crystallize },
            { (ElementType.Geo,     ElementType.Hydro),   ReactionType.Crystallize },
            { (ElementType.Geo,     ElementType.Electro), ReactionType.Crystallize },
            { (ElementType.Geo,     ElementType.Cryo),    ReactionType.Crystallize },
        };

        /// <summary>
        /// 대상에 원소 적용 → 반응 타입 반환. 오라 갱신 포함.
        /// Physical → 반응 없음, 오라 유지.
        /// </summary>
        public ReactionType ApplyElement(Unit target, ElementType incoming)
        {
            if (incoming == ElementType.Physical)
                return ReactionType.None;

            ElementType currentAura = target.CurrentAura;

            if (currentAura == ElementType.Physical)
            {
                // 오라 없음 → 오라 부여
                target.CurrentAura = incoming;
                return ReactionType.None;
            }

            if (currentAura == incoming)
            {
                // 같은 원소 → 오라 강화 (반응 없음)
                return ReactionType.None;
            }

            // 반응 조회
            if (_table.TryGetValue((currentAura, incoming), out ReactionType reaction))
            {
                // 대부분의 반응은 오라를 소모 (Physical로 초기화)
                // Burning, Electrocharged는 지속되지만 단순화 위해 일단 소모
                target.CurrentAura = ElementType.Physical;
                return reaction;
            }

            // 미지정 조합 → 오라 갱신만
            target.CurrentAura = incoming;
            return ReactionType.None;
        }

        /// <summary>
        /// Vaporize / Melt 증폭 배율 반환.
        /// aura = 오라 원소, trigger = 트리거 원소.
        /// </summary>
        public float GetAmplifyMultiplier(ElementType aura, ElementType trigger)
        {
            // Vaporize: Pyro 오라 + Hydro 트리거 → ×1.5
            if (aura == ElementType.Pyro && trigger == ElementType.Hydro) return 1.5f;
            // Vaporize: Hydro 오라 + Pyro 트리거 → ×2.0
            if (aura == ElementType.Hydro && trigger == ElementType.Pyro) return 2.0f;
            // Melt: Pyro 오라 + Cryo 트리거 → ×2.0
            if (aura == ElementType.Pyro && trigger == ElementType.Cryo) return 2.0f;
            // Melt: Cryo 오라 + Pyro 트리거 → ×1.5
            if (aura == ElementType.Cryo && trigger == ElementType.Pyro) return 1.5f;

            return 1.0f;
        }
    }
}
