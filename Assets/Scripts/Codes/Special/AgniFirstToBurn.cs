using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>
    /// 아그니 — 특수행동 「제일 먼저 태울 곳」(Detail_08 §17.5). 로카팔라 묶음 +25.
    ///
    /// 이 특수행동 자체는 피해가 없는 <b>준비</b>다. 같은 묶음을 연 인드라의 초월기 완료로 얻은 특성 표 한 장을
    /// 아그니 특성이 이 특수행동 끝까지 보류해 두면(<see cref="TryHold"/>), 여기서 중심을 고른 뒤 그 표를 집중 포격
    /// (중심 공격력 180% · 나머지 45% · 전원 불 부착)으로 돌려준다. 추가행동이 특수행동보다 먼저 나가므로 인드라(+50)보다 앞선다.
    ///
    ///   · 중심: 실제 물 → 얼음 → 풀 재료가 붙은 적 가운데 공격 가능한 기본 우선도 → 낮은 체력 비율 → 고정 배치 순.
    ///     공격 가능한 적에게 재료가 없고 다른 적에게만 있으면, 그 원소 하나를 원래 적에게서 떼어 중심으로 옮긴다
    ///     (반응 없이 · 남은 지속 보존). 복제하거나 소비된 재료를 되살리지 않는다.
    ///   · 보류한 표가 없으면 아무것도 하지 않는다(무료 추공 · 별도 카운트 초기화 없음).
    ///   · 특수행동이 취소 · 제어 · 사망 · 라운드 종료로 열리지 못하면 묶음이 끝날 때 기본 추공으로 돌려주고, 그것도 불가능하면 공통 취소를 따른다.
    /// </summary>
    public sealed class AgniFirstToBurn : BaseSpecialCode
    {
        private static readonly BaseEnums.UnitElement[] Materials =
        {
            BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Cryo, BaseEnums.UnitElement.Dendro,
        };

        /// <summary>준비가 끝난 순간(아그니, 중심, 재료를 내준 적 · 없으면 null, 옮긴 원소).</summary>
        public static event Action<Unit, Unit, Unit, BaseEnums.UnitElement> AnySetup;

        private AgniBurnToTheEnd _heldBy;

        public override int OpenOrder => 25;
        public bool HasHeldTicket => _heldBy != null;

        public AgniFirstToBurn(SpecialCodeContext context) : base(context) => CodeName = "제일 먼저 태울 곳";

        /// <summary>특성이 표 한 장을 맡긴다. 이미 한 장을 들고 있으면 받지 않는다(묶음당 한 장).</summary>
        internal bool TryHold(AgniBurnToTheEnd passive)
        {
            if (passive == null || _heldBy != null) return false;
            _heldBy = passive;
            // 특수행동이 열리지 못한 채 묶음이 끝나도 표가 갇히지 않게 묶음 정리에 기본 추공 복귀를 건다.
            SpecialAction.RegisterBatchCleanup(ReleaseAsBasicIfHeld);
            return true;
        }

        private void ReleaseAsBasicIfHeld()
        {
            AgniBurnToTheEnd passive = _heldBy;
            _heldBy = null;
            passive?.ReleaseHeld(null);
        }

        protected override IEnumerator Perform()
        {
            AgniBurnToTheEnd passive = _heldBy;
            if (passive == null) yield break;

            List<Unit> enemies = Enemies();
            Unit center = SelectCenter(enemies, out Unit donor, out BaseEnums.UnitElement material);
            if (center == null) yield break;   // 표는 묶음 정리에서 기본 추공으로 돌아간다

            yield return DirectAttack.HitOne("agni.special_setup", Caster, center, () =>
            {
                if (donor != null && donor.isActive && center.isActive && donor.HasAttachedElement(material))
                {
                    int remaining = Mathf.Max(1, donor.GetAttachedElementRemainingTurns(material));
                    if (center.GrantCombatElement(material, remaining, Caster, suppressReaction: true))
                        donor.RemoveCombatElement(material);
                    else donor = null; // 받는 쪽이 저항하면 재료는 원래 대상에 보존한다.
                }
                else
                {
                    donor = null;
                }
            }, stopOnControl: true, presentation: impact => Effects.Vfx.VedicTrioFx.AgniSpecialSetup(Caster, impact));

            if (_heldBy != passive || !DirectAttack.CasterCanContinue(Caster, true)) yield break;
            _heldBy = null;
            AnySetup?.Invoke(Caster, center, donor, donor != null ? material : BaseEnums.UnitElement.None);
            passive.ReleaseHeld(center);
        }

        /// <summary>
        /// 집중 포격의 중심. 공격 가능 = 지금 기본 대상 규칙이 노릴 수 있는 가장 높은 우선도 무리(전열 · 도발 규칙 그대로).
        /// 그 무리에 재료가 없으면 재료를 가진 다른 적(<paramref name="donor"/>)과 옮길 원소를 함께 돌려준다.
        /// </summary>
        public static Unit SelectCenter(IReadOnlyList<Unit> enemies, out Unit donor, out BaseEnums.UnitElement material)
        {
            donor = null;
            material = BaseEnums.UnitElement.None;
            List<Unit> alive = enemies?.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable &&
                                                      unit.HpCurr > 0).ToList()
                               ?? new List<Unit>();
            if (alive.Count == 0) return null;

            int top = alive.Max(unit => unit.Priority);
            List<Unit> attackable = alive.Where(unit => unit.Priority == top).ToList();
            foreach (BaseEnums.UnitElement element in Materials)
            {
                Unit holder = Order(attackable.Where(unit => unit.HasAttachedElement(element))).FirstOrDefault();
                if (holder != null) return holder;
            }

            Unit center = Order(attackable).First();
            foreach (BaseEnums.UnitElement element in Materials)
            {
                Unit holder = Order(alive.Where(unit => unit != center && unit.HasAttachedElement(element))).FirstOrDefault();
                if (holder == null) continue;
                donor = holder;
                material = element;
                break;
            }
            return center;
        }

        /// <summary>낮은 체력 비율 → 고정 배치(전열부터, 위 칸부터).</summary>
        private static IEnumerable<Unit> Order(IEnumerable<Unit> units)
            => units.OrderBy(unit => unit.HpMax > 0 ? (float)unit.HpCurr / unit.HpMax : 1f)
                .ThenBy(unit => unit.currentCell != null ? Mathf.Abs(unit.currentCell.xPos) : 99)
                .ThenBy(unit => unit.currentCell != null ? unit.currentCell.yPos : 99);
    }
}
