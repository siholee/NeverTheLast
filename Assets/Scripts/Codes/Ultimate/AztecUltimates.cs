using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Ultimate
{
    public enum AztecUltimateStyle
    {
        Jaguar,
        Eagle,
        SerpentPriest,
        HummingbirdPriest,
        OwlShaman,
        Coyote,
        TlalocHighPriest,
        Tezcatlipoca,
    }

    /// <summary>아즈텍 병종별 궁극기. 일반 병종은 피해·보호막·단순 버프만 사용한다.</summary>
    public sealed class AztecUltimate : UltimateCode
    {
        private readonly AztecUltimateStyle _style;

        public AztecUltimate(UltimateCodeContext context, AztecUltimateStyle style) : base(context)
        {
            _style = style;
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = style switch
            {
                AztecUltimateStyle.Jaguar => "재규어 휩쓸기",
                AztecUltimateStyle.Eagle => "태양의 투창",
                AztecUltimateStyle.SerpentPriest => "독안개",
                AztecUltimateStyle.HummingbirdPriest => "벌새의 노래",
                AztecUltimateStyle.OwlShaman => "밤의 파동",
                AztecUltimateStyle.Coyote => "코요테의 난무",
                AztecUltimateStyle.TlalocHighPriest => "신전의 폭우",
                AztecUltimateStyle.Tezcatlipoca => "연기 나는 거울",
                _ => "아즈텍 비기",
            };
            Cooldown = style switch
            {
                AztecUltimateStyle.TlalocHighPriest => 8f,
                AztecUltimateStyle.Tezcatlipoca => 10f,
                _ => 7f,
            };
            CastingDelay = style == AztecUltimateStyle.Tezcatlipoca ? 0.9f : 0.65f;
            MaxStage = 1;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (Caster == null || !Caster.isActive || Caster.isControlled)
                {
                    StopCode();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            switch (_style)
            {
                case AztecUltimateStyle.Jaguar:
                    ResolveJaguar();
                    break;
                case AztecUltimateStyle.Eagle:
                    ResolveEagle();
                    break;
                case AztecUltimateStyle.SerpentPriest:
                    ResolveSerpentPriest();
                    break;
                case AztecUltimateStyle.HummingbirdPriest:
                    ResolveHummingbirdPriest();
                    break;
                case AztecUltimateStyle.OwlShaman:
                    ResolveOwlShaman();
                    break;
                case AztecUltimateStyle.Coyote:
                    yield return ResolveCoyote();
                    break;
                case AztecUltimateStyle.TlalocHighPriest:
                    ResolveTlalocHighPriest();
                    break;
                case AztecUltimateStyle.Tezcatlipoca:
                    ResolveTezcatlipoca();
                    break;
            }

            StopCode();
        }

        private void ResolveJaguar()
        {
            List<Unit> enemies = AztecCombat.Enemies(Caster);
            Unit primary = enemies.OrderByDescending(unit => unit.Priority).FirstOrDefault();
            if (primary == null) return;

            List<Unit> targets = primary.currentCell == null
                ? new List<Unit> { primary }
                : enemies.Where(unit =>
                    unit.currentCell != null && unit.currentCell.yPos == primary.currentCell.yPos).ToList();
            foreach (Unit target in targets)
            {
                DealDamage(target, 1.2f, true, true, true);
            }
            Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * 0.2f)), Caster);
        }

        private void ResolveEagle()
        {
            Unit target = AztecCombat.Enemies(Caster).OrderBy(AztecCombat.HealthRatio).FirstOrDefault();
            if (target != null) DealDamage(target, 2f, true, false);
        }

        private void ResolveSerpentPriest()
        {
            float duration = AztecCombat.HasPassive(Caster, 190) ? 5f : 3.5f;
            int stacks = AztecCombat.HasPassive(Caster, 191) ? 2 : 1;
            foreach (Unit target in AztecCombat.Enemies(Caster))
            {
                DealDamage(target, 0.55f, false, true);
                if (!target.isActive) continue;
                for (int stack = 0; stack < stacks; stack++)
                {
                    AztecCombat.ApplyPoison(Caster, target, duration, 8f);
                }
            }
        }

        private void ResolveHummingbirdPriest()
        {
            int amount = AztecCombat.HasPassive(Caster, 194) ? 7 : 5;
            float duration = AztecCombat.HasPassive(Caster, 195) ? 9f : 6f;
            foreach (Unit ally in AztecCombat.Allies(Caster))
            {
                AztecCombat.AddStatus(
                    Caster,
                    ally,
                    7450,
                    $"aztec_hummingbird_song_{Caster.GetHashCode()}",
                    "벌새의 노래",
                    new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, amount),
                    duration,
                    description: $"DEX +{amount}");
            }
        }

        private void ResolveOwlShaman()
        {
            foreach (Unit target in AztecCombat.Enemies(Caster))
            {
                DealDamage(target, 1f, false, true);
            }
        }

        private IEnumerator ResolveCoyote()
        {
            Unit target = AztecCombat.Enemies(Caster).OrderByDescending(unit => unit.Priority).FirstOrDefault();
            int hits = AztecCombat.HasPassive(Caster, 203) ? 5 : 4;
            for (int hit = 0; hit < hits && target != null && target.isActive; hit++)
            {
                DealDamage(target, 0.45f, true, false);
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ResolveTlalocHighPriest()
        {
            foreach (Unit target in AztecCombat.Enemies(Caster))
            {
                DealDamage(target, 0.8f, false, true);
                if (target != null && target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Hydro);
                }
            }
            Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * 0.15f)), Caster);
        }

        private void ResolveTezcatlipoca()
        {
            List<Unit> enemies = AztecCombat.Enemies(Caster);
            // 위력은 스킬마다 다르므로, '가장 위협적인 적' 기준을 주스탯 크기로 본다.
            Unit reflected = enemies
                .OrderByDescending(unit => unit.GetBasePrimaryStat(unit.MainPrimaryStat))
                .FirstOrDefault();
            UnitStatus stolen = reflected?.GetAllStatuses().FirstOrDefault(status => status.IsBeneficial);
            if (stolen != null)
            {
                reflected.RemoveStatus(stolen.StatusId);
            }

            AztecCombat.AddStatus(
                Caster,
                Caster,
                7470,
                "aztec_stolen_reflection",
                "훔친 거울상",
                new AztecAllDamageEffect(1.2f),
                6f,
                description: "주는 피해가 20% 증가합니다.");

            foreach (Unit target in enemies)
            {
                DealDamage(target, 0.95f, false, true);
                if (target != null && target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Void);
                }
            }
        }

        private void DealDamage(
            Unit target,
            float ratio,
            bool physical,
            bool multiTarget,
            bool contact = false)
        {
            if (target == null || !target.isActive) return;
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(Mathf.RoundToInt(ratio * 50f)) * critMultiplier));
            target.TakeDamage(new DamageContext(
                Caster,
                damage,
                BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    multiTarget ? DamageTag.MultiTarget : DamageTag.SingleTarget,
                    DamageTag.UltAttack,
                    physical ? DamageTag.Physical : DamageTag.Special,
                    contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
                },
                isCrit));
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            if (Caster == null || !Caster.isActive) return false;
            return _style == AztecUltimateStyle.HummingbirdPriest
                ? AztecCombat.Allies(Caster).Count > 0
                : AztecCombat.Enemies(Caster).Count > 0;
        }
    }

    internal sealed class AztecAllDamageEffect : Effects.Base.BaseEffect
    {
        private readonly float _multiplier;
        public AztecAllDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _multiplier : 1f;
    }
}
