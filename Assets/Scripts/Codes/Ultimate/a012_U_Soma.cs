using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Ultimate
{
    public class a012_U_Soma : UltimateCode
    {
        /// <summary>사양 "CON의 n%에 해당하는 방어막" = 위력 100.</summary>
        private const int SelfShieldPower = 100;

        /// <summary>최다 피해 아군에게 주는 올스탯 = 찬드라 INT의 25%.</summary>
        private const float AllStatIntRatio = 0.25f;

        private const float AllStatDuration = 6f;

        public a012_U_Soma(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 8f;
            CodeName = "소마";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    StopCode();
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 1) 자신에게 CON 기반 방어막
            int shieldAmount = Mathf.Max(1, Caster.SkillDamage(SelfShieldPower, BaseEnums.PrimaryStat.CON));
            Caster.AddShield(shieldAmount, Caster);
            Caster.NotifyBeneficialEffectReceived(Caster);

            // 2) 이번 전투에서 가장 많은 피해를 입힌 아군에게 올스탯 버프
            List<Unit> allies = Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            Unit topDealer = SelectBuffTarget(allies);

            if (topDealer != null)
            {
                int allStat = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * AllStatIntRatio));
                var status = BuffStatus.Create(
                    BuffStatusIds.SomaSpeed, $"SomaAllStat_{Caster.GetEntityId()}", CodeName,
                    Caster, topDealer,
                    new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, allStat),
                    duration: AllStatDuration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"모든 기본 스탯이 {allStat} 증가합니다.");
                status.AddEffect(new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, allStat));
                status.AddEffect(new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.CON, allStat));
                status.AddEffect(new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.INT, allStat));
                status.AddEffect(new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.LUK, allStat));
                topDealer.AddStatus(status);
                topDealer.NotifyBeneficialEffectReceived(Caster);

                Debug.Log($"[소마] 방어막 {shieldAmount} / 최다 피해 {topDealer.UnitName}에게 올스탯 +{allStat} ({AllStatDuration}초)");
            }
            StopCode();
        }

        public override void StopCode()
        {
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive;
        }

        private Unit SelectBuffTarget(List<Unit> allies)
        {
            if (allies == null || allies.Count == 0) return null;

            int highestDamage = allies.Max(unit => unit.RoundDamageDealt);
            if (highestDamage > 0)
            {
                List<Unit> damageLeaders = allies.Where(unit => unit.RoundDamageDealt == highestDamage).ToList();
                return damageLeaders[Random.Range(0, damageLeaders.Count)];
            }

            int highestLevel = allies.Max(unit => unit.Level);
            List<Unit> candidates = allies.Where(unit => unit.Level == highestLevel).ToList();
            int highestAllStats = candidates.Max(TotalPrimaryStats);
            candidates = candidates.Where(unit => TotalPrimaryStats(unit) == highestAllStats).ToList();

            if (GridManager.Instance != null)
            {
                int rearColumn = GridManager.Instance.GetRearColumn(Caster.IsEnemy);
                List<Unit> rearCandidates = candidates
                    .Where(unit => unit.currentCell != null && unit.currentCell.xPos == rearColumn)
                    .ToList();
                if (rearCandidates.Count > 0) candidates = rearCandidates;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private static int TotalPrimaryStats(Unit unit)
        {
            return unit.GetBaseStr() + unit.GetBaseDex() + unit.GetBaseCon() + unit.GetBaseInt() + unit.GetBaseLuk();
        }

    }

    /// <summary>케찰코아틀 궁극기: 적 전체 위력 80 특수 피해 + 풀 원소 부여.</summary>
    public sealed class QuetzalcoatlYorisUltimate : UltimateCode
    {
        public QuetzalcoatlYorisUltimate(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "초록빛 심판";
            Cooldown = 8f;
            CastingDelay = 0.6f;
            Power = 80;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
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

            List<Unit> enemies = Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.INT) * critMultiplier));

            if (GameManager.Instance?.sfxManager?.ProjectilePrefabs != null &&
                GameManager.Instance.sfxManager.ProjectilePrefabs.TryGetValue("FireBlast", out var prefab))
            {
                foreach (Unit target in enemies)
                    // 특수 피해라 직선형이다.
                    GameManager.Instance.sfxManager.FireSingleProjectile(
                        prefab, Caster, target, 0.25f,
                        ProjectilePathType.Linear, ProjectileFlight.DataFor(ProjectilePathType.Linear));
                yield return new WaitForSeconds(0.25f);
            }

            foreach (Unit target in enemies.Where(unit => unit != null && unit.isActive))
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack },
                    isCrit));
                target.GrantCombatElement(BaseEnums.UnitElement.Dendro);
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive && Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
        }
    }
}
