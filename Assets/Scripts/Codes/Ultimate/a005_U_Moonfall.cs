using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Ultimate
{
    /// <summary>
    /// 아탈란테 궁극기. 전열 단일 대상에게 위력 100을 가한 뒤 후열 전체에 위력 60을 가한다.
    /// 전열이 비어 있으면 후열 단일 대상에게 최초 타격만 적용한다.
    /// </summary>
    public sealed class a005_U_Moonfall : UltimateCode
    {
        private readonly HS_Poolable _prefab;

        public a005_U_Moonfall(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "달빛 추격";
            Cooldown = 3;
            CastingDelay = 1f;
            Power = 100;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow };
            GameManager.Instance?.sfxManager?.ProjectilePrefabs?.TryGetValue("Levateinn", out _prefab);
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
                .Where(unit => unit != null && unit.isActive && unit.currentCell != null)
                .ToList();
            if (enemies.Count == 0)
            {
                StopCode();
                yield break;
            }

            int frontColumn = GridManager.Instance.GetFrontColumn(enemies[0].IsEnemy);
            int rearColumn = GridManager.Instance.GetRearColumn(enemies[0].IsEnemy);
            List<Unit> frontEnemies = enemies.Where(unit => unit.currentCell.xPos == frontColumn).ToList();
            bool frontExists = frontEnemies.Count > 0;
            List<Unit> primaryPool = frontExists
                ? frontEnemies
                : enemies.Where(unit => unit.currentCell.xPos == rearColumn).ToList();
            if (primaryPool.Count == 0) primaryPool = enemies;
            Unit primary = primaryPool.OrderByDescending(unit => unit.Priority).First();

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int primaryDamage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(100, BaseEnums.PrimaryStat.DEX) * critMultiplier));
            var primaryContext = new DamageContext(
                Caster, primaryDamage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.Physical,
                    DamageTag.NonContactAttack, DamageTag.Arrow,
                }, isCrit);

            // 활에서 나가는 화살이라 곡선형이다.
            var primaryToken = new ProjectileImpactToken();
            GameManager.Instance?.sfxManager?.FireElementalProjectile(
                Caster, primary, 0.35f, ProjectilePathType.ParabolicArc,
                ProjectileFlight.DataFor(ProjectilePathType.ParabolicArc), _prefab,
                primaryToken.MarkImpact, primaryContext);
            yield return ProjectileFlight.WaitForImpact(primaryToken, 0.35f);
            if (primary != null && primary.isActive)
            {
                primary.TakeDamage(primaryContext);
            }

            // 전열이 있었을 때만 후열 전체에 두 번째 효과가 발동한다.
            if (frontExists)
            {
                List<Unit> rearEnemies = enemies
                    .Where(unit => unit != null && unit.isActive && unit.currentCell.xPos == rearColumn)
                    .ToList();
                int rearDamage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(60, BaseEnums.PrimaryStat.DEX) * critMultiplier));
                var rearContext = new DamageContext(
                    Caster, rearDamage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.MultiTarget, DamageTag.UltAttack, DamageTag.Physical,
                        DamageTag.NonContactAttack, DamageTag.Arrow,
                    }, isCrit);
                var rearToken = new ProjectileImpactToken();
                foreach (Unit target in rearEnemies)
                {
                    GameManager.Instance?.sfxManager?.FireProjectileFromPoint(
                        primary.transform.position, Caster, target, 0.15f,
                        ProjectilePathType.ParabolicArc,
                        ProjectileFlight.DataFor(ProjectilePathType.ParabolicArc),
                        rearToken.MarkImpact, rearContext);
                }
                yield return ProjectileFlight.WaitForImpact(rearToken, 0.15f);
                foreach (Unit target in rearEnemies.Where(unit => unit != null && unit.isActive))
                {
                    target.TakeDamage(rearContext);
                }
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
            => Caster != null && Caster.isActive && Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
    }
}
