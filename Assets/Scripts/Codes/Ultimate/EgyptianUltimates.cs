using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>호루스 U — 우제트. 실제 4타 판정은 HorusNormalAttack이 스택 게이지와 함께 처리한다.</summary>
    public sealed class HorusWadjet : UltimateCode
    {
        public override bool IsAutoCast => false;
        public HorusWadjet(UltimateCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Ultimate; CodeName = "우제트"; Cooldown = 0; CastingDelay = 0f; }
        public override bool HasValidTarget() => false;
    }

    /// <summary>아누비스 U — 적 전열(비어 있으면 후열) 전체를 바위 창으로 꿰뚫고 전열 아군을 보호한다.</summary>
    public sealed class AnubisStoneSpear : UltimateCode
    {
        public AnubisStoneSpear(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "저승의 바위 창";
            Cooldown = 4;
            CastingDelay = 0.55f;
            Power = 80;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            yield return new WaitForSeconds(CastingDelay);
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            { StopCode(); yield break; }

            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable && unit.currentCell != null)
                .ToList();
            if (enemies.Count == 0) { StopCode(); yield break; }

            GridManager grid = GridManager.Instance;
            int front = grid != null ? grid.GetFrontColumn(enemies[0].IsEnemy) : (enemies[0].IsEnemy ? 1 : -1);
            int rear = grid != null ? grid.GetRearColumn(enemies[0].IsEnemy) : (enemies[0].IsEnemy ? 2 : -2);
            List<Unit> targets = enemies.Where(unit => unit.currentCell.xPos == front).ToList();
            if (targets.Count == 0) targets = enemies.Where(unit => unit.currentCell.xPos == rear).ToList();
            if (targets.Count == 0) targets = enemies;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.STR) * crit));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.MultiTarget, DamageTag.UltAttack,
                    DamageTag.Special, DamageTag.NonContactAttack,
                }, isCrit);

            var token = new ProjectileImpactToken();
            foreach (Unit target in targets)
            {
                GameManager.Instance?.sfxManager?.FireElementalProjectile(
                    Caster, target, 0.42f, ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear), null,
                    token.MarkImpact, context);
            }
            yield return ProjectileFlight.WaitForImpact(token, 0.42f);
            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive).ToList())
                target.TakeDamage(context);

            int ownFront = grid != null ? grid.GetFrontColumn(Caster.IsEnemy) : (Caster.IsEnemy ? 1 : -1);
            if (Caster.currentCell != null && Caster.currentCell.xPos == ownFront)
            {
                int shield = Mathf.Max(1, Caster.SkillDamage(140, BaseEnums.PrimaryStat.CON));
                foreach (Unit ally in global::Target.GetAllAllies(Caster)
                             .Where(unit => unit != null && unit.isActive && unit.currentCell != null &&
                                            unit.currentCell.xPos == ownFront))
                {
                    ally.AddShield(shield, Caster);
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

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    /// <summary>바스테트 U — 패시브형. 에어본 정점 추가공격은 BastetAirborneHunter가 처리한다.</summary>
    public sealed class BastetApexExecution : UltimateCode
    {
        public override bool IsAutoCast => false;
        public BastetApexExecution(UltimateCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Ultimate; CodeName = "천공 처형"; Cooldown = 0; CastingDelay = 0f; }
        public override bool HasValidTarget() => false;
    }
}
