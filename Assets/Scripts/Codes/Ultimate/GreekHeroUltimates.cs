using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Ultimate
{
    public sealed class OrionHeavyBlow : UltimateCode
    {
        private const int ArrowCount = 12;

        public OrionHeavyBlow(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "화살비";
            Power = 10;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow };
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

            for (int arrow = 0; arrow < ArrowCount; arrow++)
            {
                List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                    .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                    .ToList();
                if (enemies.Count == 0) break;

                Unit target = enemies[Random.Range(0, enemies.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(Power, BaseEnums.PrimaryStat.STR) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.Physical,
                        DamageTag.NonContactAttack, DamageTag.Arrow,
                    },
                    isCrit));
                yield return new WaitForSeconds(0.06f);
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    public sealed class TheseusRecoveryStrike : UltimateCode
    {
        // 기획에 수치가 없어 1차 밸런스 기준으로 확정한 임시식: 잃은 체력 25% + CON×10.
        private const float MissingHpRatio = 0.25f;
        private const int ConHealScale = 10;

        public TheseusRecoveryStrike(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "회복의 일격";
            Power = 100;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
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

            Unit target = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .OrderByDescending(unit => unit.Priority)
                .FirstOrDefault();
            if (target == null)
            {
                StopCode();
                yield break;
            }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(Power, BaseEnums.PrimaryStat.DEX) *
                                                       (isCrit ? Caster.CritMultiplierCurr : 1f)));
            if (GameManager.Instance?.sfxManager?.ProjectilePrefabs != null &&
                GameManager.Instance.sfxManager.ProjectilePrefabs.TryGetValue("FireBlast", out var prefab))
            {
                // 접촉(근접) 공격이라 포물선을 씌우지 않는다.
                GameManager.Instance.sfxManager.FireSingleProjectile(
                    prefab, Caster, target, 0.25f,
                    ProjectilePathType.Linear, ProjectileFlight.DataFor(ProjectilePathType.Linear),
                    null, true);
            }
            yield return new WaitForSeconds(0.25f);
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.Physical, DamageTag.ContactAttack },
                isCrit));

            int missingHp = Mathf.Max(0, Caster.HpMax - Caster.HpCurr);
            int heal = Mathf.RoundToInt(missingHp * MissingHpRatio) + Caster.GetBaseCon() * ConHealScale;
            Caster.ModifyHp(Caster.HpCurr + Mathf.Max(0, heal), Caster);
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }
}
