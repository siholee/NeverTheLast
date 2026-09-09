using System.Collections.Generic;
using BaseClasses;
using Combat;
using Codes.Passive;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    internal sealed class SeedOverdriveEffect : Effects.Base.BaseEffect
    {
        private readonly float _dexMultiplier;
        public SeedOverdriveEffect(float dexMultiplier) : base(0, dexMultiplier) => _dexMultiplier = dexMultiplier;

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? _dexMultiplier : 1f;
    }

    /// <summary>파멸·종말의 씨앗 U — 2턴 DEX 강화 및 일반행동 1발 추가.</summary>
    public sealed class GenericSeedOverdrive : SimpleUltimate
    {
        public GenericSeedOverdrive(UltimateCodeContext context)
            : base(context, "과부하", 4, 0.35f) { }

        protected override void Resolve()
        {
            bool overload = Caster.HasStatus(GenericSeedStatusIds.Overload) &&
                            Caster.HpMax > 0 && Caster.HpCurr <= Caster.HpMax * 0.30f;
            float dexMultiplier = overload ? 1.25f : 1.05f;
            Caster.AddStatus(BuffStatus.Create(
                GenericSeedStatusIds.Overdrive, "seed_overdrive", CodeName,
                Caster, Caster, new SeedOverdriveEffect(dexMultiplier),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: overload
                    ? "2턴 동안 DEX +25%, 일반행동 발사 수 +1."
                    : "2턴 동안 DEX +5%, 일반행동 발사 수 +1."));
        }
    }

    /// <summary>혹한의 씨앗 U — 단일 적 DEX 기반 위력 150 + 얼음 부착.</summary>
    public sealed class FrostSeedBeam : SimpleUltimate
    {
        private const int BeamPower = 150;

        public FrostSeedBeam(UltimateCodeContext context)
            : base(context, "빙결포", 4, 0.45f)
        {
            Power = BeamPower;
            CodeTags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
            };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * crit));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>(CodeTags), isCrit));

            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
            if (!Caster.HasStatus(GenericSeedStatusIds.FrostRay)) return;

            target.AddStatus(BuffStatus.Create(
                GenericSeedStatusIds.Slow, $"seed_slow_{Caster.GetEntityId()}", "둔화",
                Caster, target, new SeedSlowEffect(),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "DEX가 제공하는 행동 속도가 2/3으로 감소합니다."));
        }

        // 대상이 없으면 자원을 태우지 않는다. 거인의 철퇴와 같은 규칙이다.
        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
