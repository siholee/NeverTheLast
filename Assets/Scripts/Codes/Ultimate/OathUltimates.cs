using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 시구르드 U — 파프니르 살해.
    ///
    /// 단일 고화력에 <b>내구 관통</b>을 붙였다. 용의 비늘을 가른 칼이라는 뜻이기도 하고,
    /// 내구가 높은 방어구형 적 앞에서 이 궁극기만은 값을 한다는 뜻이기도 하다.
    /// 쓰고 나면 맹세가 협공을 한 번 불러온다(<c>OathEffect</c>가 궁극기 발동을 듣는다).
    /// </summary>
    public sealed class SigurdFafnirsBane : SimpleUltimate
    {
        private const int UltimatePower = 150;

        public SigurdFafnirsBane(UltimateCodeContext context) : base(context, "파프니르 살해", 0.6f)
        {
            Power = UltimatePower;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical, DamageTag.Slash };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));

            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.ContactAttack, DamageTag.Physical, DamageTag.Slash,
                    DamageTag.DurabilityPenetration,
                },
                isCrit));

            if (target.isActive)
            {
                target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 브륀힐드 U — 선고.
    ///
    /// 적 전체에 얼음을 얹는다. <b>협공 응수와 가장 잘 맞는 궁극기</b>다 —
    /// 판 전체에 얼음이 깔린 직후 시구르드의 불이 들어오면 융해가 한 번에 여러 자리에서 터진다.
    /// </summary>
    public sealed class BrynhildVerdict : SimpleUltimate
    {
        private const int UltimatePower = 80;

        public BrynhildVerdict(UltimateCodeContext context) : base(context, "선고", 0.6f)
        {
            Power = UltimatePower;
            CodeTags = new List<int> { DamageTag.NonContactAttack, DamageTag.Physical, DamageTag.Pierce };
        }

        protected override void Resolve()
        {
            List<Unit> targets = Enemies();
            if (targets.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));

            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive) continue;

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.NonContactAttack, DamageTag.Physical, DamageTag.Pierce,
                    },
                    isCrit));

                if (target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
