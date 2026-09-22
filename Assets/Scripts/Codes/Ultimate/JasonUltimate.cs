using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>이아손 U — 광역 포격 후 아군 전체에게 자신의 INT 절반을 2턴간 부여한다.</summary>
    public sealed class JasonArgoDeparture : UltimateCode
    {
        public JasonArgoDeparture(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "아르고 호 출항";
            CastingDelay = 0.55f;
            Power = 100;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.NonContactAttack };
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * crit));
            foreach (Unit enemy in Combat.CombatTargets.AliveEnemies(Caster).ToList())
            {
                enemy.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>
                {
                    DamageTag.AllTarget, DamageTag.UltAttack,
                    DamageTag.Physical, DamageTag.NonContactAttack,
                }, isCrit));
            }

            // 자기 자신도 이 버프를 받으므로 현재 표시 INT를 그대로 다시 읽으면 시전할 때마다
            // 이전 아르고 버프가 복리로 불어난다. 같은 키의 기존 부여분만 제외하고 새 값을 계산한다.
            int previousArgoBonus = Caster.ActiveStatuses
                .Where(status => status.Key == "jason_argo_departure_int")
                .SelectMany(status => status.Effects)
                .Select(instance => instance.EffectObject)
                .OfType<JasonGrantedIntEffect>()
                .Sum(effect => effect.Amount);
            int sourceInt = Mathf.Max(0, Caster.GetBaseInt() - previousArgoBonus);
            int intBonus = Mathf.Max(0, Mathf.FloorToInt(sourceInt * 0.5f));
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    5361, "jason_argo_departure_int", CodeName, Caster, ally,
                    new JasonGrantedIntEffect(intBonus),
                    duration: 2,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"INT +{intBonus}."));
            }

            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Combat.CombatTargets.AliveEnemies(Caster).Count > 0;
    }

    internal sealed class JasonGrantedIntEffect : BaseEffect
    {
        private readonly int _amount;
        internal int Amount => _amount;
        public JasonGrantedIntEffect(int amount) : base(0, amount) => _amount = amount;
        public override bool IsBeneficial => true;
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.INT ? _amount : 0;
    }
}
