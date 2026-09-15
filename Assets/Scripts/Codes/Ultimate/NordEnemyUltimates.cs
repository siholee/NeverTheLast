using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>노르드 적 궁극기가 쓰는 상태 ID.</summary>
    public static class NordEnemyUltimateStatusIds
    {
        public const int FrenzyArmorBreak = 8010;
    }

    /// <summary>
    /// 광전사 U — 광란.
    ///
    /// 단일 대상을 위력 80으로 네 번 벤다. 최대 체력의 20%를 낼 수 있으면 태워
    /// 소모량에 비례한 추가 피해를 얹고 방어력을 2턴간 20% 깎는다.
    ///
    /// <b>네 번으로 나눈 것은 내구에 약하라는 뜻이다.</b> 내구는 발마다 고정으로 차감되므로
    /// 다단히트가 특히 크게 눌린다. 광전사를 막는 길 하나를 장비 쪽에 남겨 둔다.
    /// </summary>
    public sealed class BerserkerFrenzy : SimpleUltimate
    {
        private const int StrikePower = 80;
        private const int StrikeCount = 4;
        private const float HpCostRatio = 0.20f;
        private const float SpentDamageCoefficient = 3f;
        private const float ArmorMultiplier = 0.80f;
        private const int ArmorBreakTurns = 2;

        public BerserkerFrenzy(UltimateCodeContext context)
            : base(context, "광란", 0.55f) { Power = StrikePower; }

        protected override void Resolve()
        {
            Unit target = Combat.CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            float spentRatio = Caster.TryConsumeAttackHp(HpCostRatio, false, out float spent) ? spent : 0f;
            float bonus = 1f + spentRatio * SpentDamageCoefficient;

            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
            };

            for (int i = 0; i < StrikeCount; i++)
            {
                if (target == null || !target.isActive || target.IsUntargetable) return;

                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(StrikePower, BaseEnums.PrimaryStat.STR) * critMultiplier * bonus));

                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit)
                {
                    SelfHpSpentRatio = spentRatio,
                });
            }

            if (target == null || !target.isActive) return;

            target.AddStatus(BuffStatus.Create(
                NordEnemyUltimateStatusIds.FrenzyArmorBreak, "berserker_frenzy_armor", CodeName,
                Caster, target, new ArmorShredEffect(ArmorMultiplier),
                duration: ArmorBreakTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "방어력이 20% 감소합니다."));
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Enemies().Count > 0;
    }

    /// <summary>
    /// 볼바 U — 셰이드.
    ///
    /// 세 단계가 서로 다른 스탯을 탄다. 아군 전체 치유는 CON, 피해는 INT다.
    /// 볼바의 주스탯이 CON이라 힐이 크고 딜은 곁가지에 머문다.
    ///
    /// 얼음은 <b>2단계의 단일 대상에게만</b> 붙는다. 그 한 명이 곧 광전사의 융해 표적이 되므로,
    /// 볼바가 누구를 겨누는지가 그 전투의 융해가 어디서 터질지를 정한다.
    /// </summary>
    public sealed class VolvaShade : SimpleUltimate
    {
        private const int HealPower = 50;
        private const int BoltPower = 70;
        private const int FieldPower = 40;

        public VolvaShade(UltimateCodeContext context)
            : base(context, "셰이드", 0.6f) { Power = BoltPower; }

        protected override void Resolve()
        {
            // 1단계 — 아군 전체 치유. CON 기반이라 볼바의 주스탯을 그대로 쓴다.
            int heal = Mathf.Max(1, Caster.SkillDamage(HealPower, BaseEnums.PrimaryStat.CON));
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.ModifyHp(ally.HpCurr + heal, Caster);
            }

            List<Unit> enemies = Enemies();
            if (enemies.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;

            // 2단계 — 투사체. 단일 대상에게 피해를 주고 얼음을 남긴다.
            Unit bolted = Combat.CombatTargets.PickByPriority(enemies);
            if (bolted != null)
            {
                int boltDamage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(BoltPower, BaseEnums.PrimaryStat.INT) * critMultiplier));
                bolted.TakeDamage(new DamageContext(Caster, boltDamage, BaseEnums.CodeType.Ultimate, new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.NonContactAttack, DamageTag.Special,
                }, isCrit));

                if (bolted.isActive)
                {
                    bolted.GrantCombatElement(
                        BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
                }
            }

            // 3단계 — 역장. 적 전체에 얕게 퍼진다.
            int fieldDamage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(FieldPower, BaseEnums.PrimaryStat.INT) * critMultiplier));
            var fieldTags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Special,
            };

            foreach (Unit enemy in Enemies())
            {
                enemy.TakeDamage(new DamageContext(Caster, fieldDamage, BaseEnums.CodeType.Ultimate, fieldTags, isCrit));
            }
        }

        /// <summary>치유가 앞에 있으므로 적이 없어도 값을 한다. 아군이 살아 있으면 시전한다.</summary>
        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }
}
