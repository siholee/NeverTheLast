using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 잔 N — 단일 적에게 STR 기반 위력 60의 접촉 타격. 30% 고정 확률로 얼음을 부착한다.
    ///
    /// <c>벌크업</c>(Lv.26)을 익혔다면 <b>전투 시작 후 첫 일반행동</b>이 대체행동으로 바뀌어
    /// 공격 대신 자신의 STR을 10% 올린다.
    /// </summary>
    public sealed class JeanIronFist : BaseNormalCode
    {
        private const int NormalPower = 60;
        private const float CryoChance = 0.30f;

        private bool _substitute;

        public JeanIronFist(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.4f;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical };
        }

        public override void CastCode()
        {
            _substitute = Caster != null && Caster.HasLearnedPassiveCode(JeanCodeIds.BulkUp) &&
                          JeanBulkUp.TryConsume(Caster);
            CodeName = _substitute ? "대체행동" : "일반행동";
            base.CastCode();
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (!_substitute)
            {
                yield return base.SkillCoroutine();
                yield break;
            }

            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            JeanBulkUp.Apply(Caster);

            // 공격하지 않는 행동도 '일반행동을 했다'로 세어야 철벽(6) 같은 코드가 어긋나지 않는다.
            NotifyActionResolved();
            StopCode();
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            if (Random.value > CryoChance) return;

            target.GrantCombatElement(
                BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
