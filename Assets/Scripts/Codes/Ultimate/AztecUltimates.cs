using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    public enum AztecUltimateStyle
    {
        Jaguar,
        EliteJaguar,
        Eagle,
        EliteEagle,
        SerpentPriest,
        EliteSerpentPriest,
        Tezcatlipoca,
    }

    /// <summary>새 메히코 테마의 궁극기. 테스카틀리포카만 스택형 소환 궁극기를 사용한다.</summary>
    public sealed class AztecUltimate : UltimateCode
    {
        private readonly AztecUltimateStyle _style;

        public AztecUltimate(UltimateCodeContext context, AztecUltimateStyle style) : base(context)
        {
            _style = style;
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = style switch
            {
                AztecUltimateStyle.Jaguar => "대지 분쇄",
                AztecUltimateStyle.EliteJaguar => "왕의 대지 분쇄",
                AztecUltimateStyle.Eagle => "맹독 화살",
                AztecUltimateStyle.EliteEagle => "왕의 맹독 화살",
                AztecUltimateStyle.SerpentPriest => "풍요의 의식",
                AztecUltimateStyle.EliteSerpentPriest => "대풍요의 의식",
                AztecUltimateStyle.Tezcatlipoca => "검은 태양의 전열",
                _ => "메히코 비기",
            };
            Cooldown = style == AztecUltimateStyle.Tezcatlipoca ? 0f : 4f;
            CastingDelay = style == AztecUltimateStyle.Tezcatlipoca ? 0.9f : 0.6f;
            MaxStage = 1;

            (int flat, float coefficient, BaseEnums.PrimaryStat stat) = PowerOf(style);
            Power = flat;
            PowerStatCoefficient = coefficient;
            PowerStat = stat;
        }

        private static (int, float, BaseEnums.PrimaryStat) PowerOf(AztecUltimateStyle style) => style switch
        {
            AztecUltimateStyle.Jaguar => (100, 0.6f, BaseEnums.PrimaryStat.STR),
            AztecUltimateStyle.EliteJaguar => (150, 0.8f, BaseEnums.PrimaryStat.STR),
            AztecUltimateStyle.Eagle => (100, 0.8f, BaseEnums.PrimaryStat.STR),
            AztecUltimateStyle.EliteEagle => (120, 0.8f, BaseEnums.PrimaryStat.STR),
            AztecUltimateStyle.SerpentPriest => (200, 0.8f, BaseEnums.PrimaryStat.INT),
            AztecUltimateStyle.EliteSerpentPriest => (200, 0.8f, BaseEnums.PrimaryStat.INT),
            _ => (0, 0f, BaseEnums.PrimaryStat.INT),
        };

        private bool IsJaguar => _style is AztecUltimateStyle.Jaguar or AztecUltimateStyle.EliteJaguar;
        private bool IsEagle => _style is AztecUltimateStyle.Eagle or AztecUltimateStyle.EliteEagle;
        private bool IsSerpent => _style is AztecUltimateStyle.SerpentPriest or AztecUltimateStyle.EliteSerpentPriest;

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
                if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsJaguar) ResolveJaguar();
            else if (IsEagle) yield return ResolveEagle();
            else if (IsSerpent) ResolveSerpentPriest();
            else ResolveTezcatlipoca();

            StopCode();
        }

        private void ResolveJaguar()
        {
            Unit target = HighestPriorityEnemy();
            if (target == null) return;
            DealDamage(target, BaseEnums.PrimaryStat.STR, DamageTag.Physical, DamageTag.ContactAttack);
            if (target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Geo, 3, Caster);
        }

        private IEnumerator ResolveEagle()
        {
            Unit target = HighestPriorityEnemy();
            if (target == null) yield break;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.Physical,
                    DamageTag.NonContactAttack, DamageTag.Arrow,
                }, isCrit);

            const float flight = 0.42f;
            var token = new ProjectileImpactToken();
            ProjectilePathType path = ProjectileFlight.PathFor(context);
            GameManager.Instance?.sfxManager?.FireElementalProjectile(
                Caster, target, flight, path, ProjectileFlight.DataFor(path), null,
                token.MarkImpact, context);
            yield return ProjectileFlight.WaitForImpact(token, flight);

            if (target == null || !target.isActive) yield break;
            target.TakeDamage(context);
            if (target.isActive) AztecCombat.ApplyPoison(Caster, target);
        }

        private void ResolveSerpentPriest()
        {
            int healing = Mathf.Max(1, Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT));
            foreach (Unit ally in AztecCombat.AlliesIncludingSelf(Caster))
            {
                ally.ModifyHp(ally.HpCurr + healing, Caster);
                if (ally.isActive) ally.GrantCombatElement(BaseEnums.UnitElement.Dendro, 3, Caster);
            }
        }

        private void ResolveTezcatlipoca()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            int frontColumn = grid.GetFrontColumn(Caster.IsEnemy);
            foreach (Unit ally in AztecCombat.AlliesIncludingSelf(Caster)
                         .Where(unit => unit != Caster && unit.currentCell != null && unit.currentCell.xPos == frontColumn)
                         .ToList())
            {
                grid.RetireUnit(ally);
            }

            for (int y = grid.yMin; y <= grid.yMax; y++)
            {
                if (!grid.IsCellAvailable(frontColumn, y)) continue;

                grid.SpawnUnit(frontColumn, y, Caster.IsEnemy, AztecCombat.EliteJaguarWarriorId);

                // 라운드 도중 소환된 유닛은 일반 스폰과 달리 OnRoundStart를 거치지 않는다.
                // 즉시 패시브를 켜야 행동불능 추적과 현재 레벨 해금 패시브가 이번 전투부터 동작한다.
                List<Unit> side = Caster.IsEnemy ? grid.enemyList : grid.heroList;
                Unit summoned = side.LastOrDefault(unit => unit != null && unit.isActive &&
                    unit.ID == AztecCombat.EliteJaguarWarriorId && unit.currentCell != null &&
                    unit.currentCell.xPos == frontColumn && unit.currentCell.yPos == y);
                summoned?.CastPassiveCode();
            }

            Caster.AddStatus(BuffStatus.Create(
                7660, $"tezcatlipoca_vulnerability_{Guid.NewGuid():N}", "현현의 균열",
                Caster, Caster, new ReceivingDamageMultiplierEffect(1.5f),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: false,
                category: BaseEnums.StatusCategory.Negative,
                description: "받는 피해가 50% 증가합니다. 중첩됩니다."));

            AztecCombat.ApplyTezcatlipocaFormation(Caster);
        }

        private Unit HighestPriorityEnemy() => AztecCombat.Enemies(Caster)
            .OrderByDescending(unit => unit.Priority).FirstOrDefault();

        private void DealDamage(Unit target, BaseEnums.PrimaryStat stat, int damageType, int contactType)
        {
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower, stat) * critMultiplier));
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, damageType, contactType }, isCrit));
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
            return IsSerpent || _style == AztecUltimateStyle.Tezcatlipoca || AztecCombat.Enemies(Caster).Count > 0;
        }
    }
}
