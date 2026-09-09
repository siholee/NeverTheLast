using Effects.Buffs;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    public enum ColosseumUltimateStyle
    {
        Murmillo,
        Hoplomachus,
        Thraex,
        Retiarius,
        Secutor,
        Dimachaerus,
        Marcellus,
        Sabina,
        Spartacus = 9,
    }

    /// <summary>콜로세움 병종별 궁극기. 인간 검투사의 무기술과 맹수의 공격만 사용한다.</summary>
    public sealed class ColosseumUltimate : UltimateCode
    {
        private const string DuelMarkKey = "colosseum_thraex_duel";
        private readonly ColosseumUltimateStyle _style;

        public ColosseumUltimate(UltimateCodeContext context, ColosseumUltimateStyle style) : base(context)
        {
            _style = style;
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = style switch
            {
                ColosseumUltimateStyle.Murmillo => "철벽 진형",
                ColosseumUltimateStyle.Hoplomachus => "대열 붕괴",
                ColosseumUltimateStyle.Thraex => "일대일 지명",
                ColosseumUltimateStyle.Retiarius => "연환 투척",
                ColosseumUltimateStyle.Secutor => "집요한 추격",
                ColosseumUltimateStyle.Dimachaerus => "승자의 춤",
                ColosseumUltimateStyle.Marcellus => "월계관",
                ColosseumUltimateStyle.Sabina => "사냥 개시",
                ColosseumUltimateStyle.Spartacus => "한판 뒤집기",
                _ => "검투사의 비기",
            };
            Cooldown = style == ColosseumUltimateStyle.Spartacus ? 10f : 8f;
            CastingDelay = style == ColosseumUltimateStyle.Spartacus ? 0.9f : 0.65f;
            MaxStage = 1;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        public override void CastCode()
        {
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

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;

            switch (_style)
            {
                case ColosseumUltimateStyle.Murmillo:
                    ResolveMurmillo();
                    break;
                case ColosseumUltimateStyle.Hoplomachus:
                    ResolveHoplomachus(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Thraex:
                    ResolveThraex(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Retiarius:
                    ResolveRetiarius(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Secutor:
                    ResolveSecutor(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Dimachaerus:
                    yield return ResolveDimachaerus(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Marcellus:
                    ResolveMarcellus(isCrit, critMultiplier);
                    break;
                case ColosseumUltimateStyle.Sabina:
                    ResolveSabina();
                    break;
                case ColosseumUltimateStyle.Spartacus:
                    ResolveSpartacus(isCrit, critMultiplier);
                    break;
            }

            StopCode();
        }

        private void ResolveMurmillo()
        {
            Unit weakest = ColosseumCombat.Allies(Caster)
                .Where(unit => unit != Caster)
                .OrderBy(unit => ColosseumCombat.HealthRatio(unit))
                .FirstOrDefault();
            int selfShield = Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * 0.2f));
            Caster.AddShield(selfShield, Caster);
            weakest?.AddShield(Mathf.Max(1, Mathf.RoundToInt(weakest.HpMax * 0.15f)), Caster);
        }

        private void ResolveHoplomachus(bool isCrit, float critMultiplier)
        {
            Unit primary = HighestPriorityEnemy();
            if (primary == null) return;
            List<Unit> row = ColosseumCombat.Enemies(Caster)
                .Where(unit => unit.currentCell != null && primary.currentCell != null &&
                               unit.currentCell.yPos == primary.currentCell.yPos)
                .ToList();
            foreach (Unit target in row)
            {
                DealDamage(target, 1.25f, isCrit, critMultiplier, true);
                if (target.isActive)
                {
                    ColosseumCombat.AddStatus(
                        Caster, target, 6341, "colosseum_line_break", "대열 붕괴",
                        new ReceivingDamageMultiplierEffect(1.25f), 3,
                        BaseEnums.StatusCategory.Negative, false,
                        description: "받는 피해가 25% 증가합니다.");
                }
            }
        }

        private void ResolveThraex(bool isCrit, float critMultiplier)
        {
            Unit target = ColosseumCombat.Enemies(Caster)
                .OrderByDescending(unit => unit.HpMax)
                .FirstOrDefault();
            if (target == null) return;

            DealDamage(target, 1.4f, isCrit, critMultiplier);
            if (!target.isActive) return;
            ColosseumCombat.AddStatus(
                Caster, target, 6342, DuelMarkKey, "일대일 지명", null, 4,
                BaseEnums.StatusCategory.Negative, false,
                description: "트라엑스에게 지명되었습니다.");
            ColosseumCombat.AddStatus(
                Caster, Caster, 6343, "colosseum_duel_bonus", "결투의 집중",
                new ConditionalOutgoingDamageEffect((_, victim, _) =>
                    victim != null && victim.HasStatusKey(DuelMarkKey) ? 1.3f : 1f),
                4);
        }

        private void ResolveRetiarius(bool isCrit, float critMultiplier)
        {
            foreach (Unit target in ColosseumCombat.Enemies(Caster)
                         .OrderByDescending(unit => unit.CodeAcceleration)
                         .Take(3).ToList())
            {
                DealDamage(target, 0.9f, isCrit, critMultiplier, true);
                if (target.isActive)
                {
                    target.ControlStarts(new ControlContext(Caster, 1));
                    ColosseumCombat.ApplyCapture(
                        Caster,
                        target,
                        ColosseumCombat.HasPassive(Caster, 153) ? 3 : 2,
                        ColosseumCombat.HasPassive(Caster, 154));
                }
            }
        }

        private void ResolveSecutor(bool isCrit, float critMultiplier)
        {
            ColosseumCombat.AddStatus(
                Caster,
                Caster,
                6344,
                "colosseum_secutor_pursuit",
                "집요한 추격",
                new CompositeEffect(new CodeAccelerationBuffEffect(0.4f), new FullControlImmunityEffect()),
                3);

            Unit target = ColosseumCombat.Enemies(Caster)
                .OrderByDescending(unit => unit.currentCell != null &&
                                          Managers.GridManager.Instance != null &&
                                          unit.currentCell.xPos ==
                                          Managers.GridManager.Instance.GetRearColumn(unit.IsEnemy))
                .ThenByDescending(unit => ColosseumCombat.IsCaptured(unit))
                .FirstOrDefault();
            if (target != null) DealDamage(target, 1.5f, isCrit, critMultiplier);
        }

        private IEnumerator ResolveDimachaerus(bool isCrit, float critMultiplier)
        {
            for (int hit = 0; hit < 6; hit++)
            {
                Unit target = ColosseumCombat.Enemies(Caster)
                    .OrderBy(unit => ColosseumCombat.HealthRatio(unit))
                    .FirstOrDefault();
                if (target == null) yield break;

                bool wasActive = target.isActive;
                DealDamage(target, 0.45f, isCrit, critMultiplier, true);
                if (wasActive && !target.isActive && !ColosseumCombat.HasPassive(Caster, 163))
                {
                    yield break;
                }
                yield return new WaitForSeconds(0.12f);
            }
        }

        private void ResolveMarcellus(bool isCrit, float critMultiplier)
        {
            if (ColosseumCombat.HasPassive(Caster, 166))
            {
                ColosseumCombat.CleanseOneNegative(Caster);
            }
            Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * 0.2f)), Caster);
            foreach (Unit target in ColosseumCombat.Enemies(Caster).Take(3).ToList())
            {
                DealDamage(target, 0.8f, isCrit, critMultiplier, true);
            }
        }

        private void ResolveSabina()
        {
            bool hasFocusedHunt = ColosseumCombat.HasPassive(Caster, 170);
            IEnumerable<Unit> recipients = hasFocusedHunt
                ? ColosseumCombat.Allies(Caster)
                : new[] { Caster };
            float damageMultiplier = hasFocusedHunt ? 1.25f : 1.15f;
            foreach (Unit ally in recipients)
            {
                ColosseumCombat.AddStatus(
                    Caster,
                    ally,
                    6345,
                    $"colosseum_hunt_order_{Caster.GetHashCode()}",
                    "사냥 개시",
                    new ConditionalOutgoingDamageEffect((_, target, _) =>
                        target != null && target.HasStatusKey(ColosseumCombat.SabinaMarkKey)
                            ? damageMultiplier
                            : 1f),
                    3);
            }
        }

        private void ResolveSpartacus(bool isCrit, float critMultiplier)
        {
            List<Unit> enemies = ColosseumCombat.Enemies(Caster);
            int frontColumn = enemies.Count > 0 && Managers.GridManager.Instance != null
                ? Managers.GridManager.Instance.GetFrontColumn(enemies[0].IsEnemy)
                : 0;
            List<Unit> targets = enemies.Where(unit =>
                    unit.currentCell != null && unit.currentCell.xPos == frontColumn)
                .ToList();
            if (targets.Count == 0) targets = enemies;

            int damageDealt = 0;
            foreach (Unit target in targets)
            {
                damageDealt += DealDamage(target, 1.5f, isCrit, critMultiplier, true);
            }
            if (damageDealt > 0)
            {
                Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(damageDealt * 0.15f), Caster);
            }
        }

        private Unit HighestPriorityEnemy()
        {
            return ColosseumCombat.Enemies(Caster)
                .OrderByDescending(unit => unit.Priority)
                .FirstOrDefault();
        }

        private int DealDamage(
            Unit target,
            float atkRatio,
            bool isCrit,
            float critMultiplier,
            bool multiTarget = false)
        {
            if (target == null || !target.isActive) return 0;
            int hpBefore = target.HpCurr;
            int shieldBefore = target.ShieldCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(Mathf.RoundToInt(atkRatio * 50f)) * critMultiplier));
            target.TakeDamage(new DamageContext(
                Caster,
                damage,
                BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    multiTarget ? DamageTag.MultiTarget : DamageTag.SingleTarget,
                    DamageTag.UltAttack,
                    DamageTag.Physical,
                    DamageTag.ContactAttack,
                },
                isCrit));
            return Mathf.Max(0, hpBefore - target.HpCurr) + Mathf.Max(0, shieldBefore - target.ShieldCurr);
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive && ColosseumCombat.Enemies(Caster).Count > 0;
        }
    }
}
