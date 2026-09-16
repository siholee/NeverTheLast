using System.Collections.Generic;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>범용 거인 U — 고정 피해 + STR 기반 위력의 단일 접촉 타격.</summary>
    public sealed class GenericGiantHammerfall : SimpleUltimate
    {
        private readonly int _flatDamage;

        public GenericGiantHammerfall(UltimateCodeContext context, int flatDamage, int statPower)
            : base(context, "거인의 철퇴", 0.55f)
        {
            _flatDamage = flatDamage;
            Power = statPower;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int baseDamage = _flatDamage + Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR);
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (isCrit ? Caster.CritMultiplierCurr : 1f)));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.ContactAttack, DamageTag.Physical,
                },
                isCrit));
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 혹한의 강림 — 적 전체를 치고 얼음을 얹은 뒤 판을 다시 눈으로 덮는다.
    ///
    /// 눈을 <b>갱신</b>하는 것이 요점이다. 보스의 고유 P가 자기 턴마다 2턴짜리 눈을 깔지만
    /// DEX 10으로는 그 사이가 벌어진다. 궁극기가 3턴을 새로 매겨 판이 식지 않게 한다.
    /// </summary>
    public sealed class FrostGiantAdvent : SimpleUltimate
    {
        private const int SnowTurns = 3;
        private const int FlatDamage = 140;

        public FrostGiantAdvent(UltimateCodeContext context) : base(context, "혹한의 강림", 0.7f)
        {
            Power = 120;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override void Resolve()
        {
            List<Unit> targets = Enemies();
            if (targets.Count == 0) return;

            Battlefield.Set(FieldKind.Snow, Caster, SnowTurns);

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int baseDamage = FlatDamage + Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR);
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (isCrit ? Caster.CritMultiplierCurr : 1f)));

            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive) continue;
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
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
