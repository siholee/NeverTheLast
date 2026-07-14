using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public class MagicBolt : BaseNormalCode
    {
        public MagicBolt(NormalCodeContext context) : base(context)
        {
            CodeName = "마력탄";
            MaxStage = 3;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            int stageBonus = CurrentStage switch
            {
                1 => 100,
                2 => 200,
                _ => 300,
            };

            return (int)((Caster.GetBaseInt() + stageBonus) * critMultiplier);
        }

        protected override List<int> GetDamageTags()
        {
            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.NonContactAttack,
                DamageTag.Special,
            };
        }
    }

    public class QuetzalcoatlBall : NormalCode
    {
        public QuetzalcoatlBall(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            CodeName = "태양의 공";
            Cooldown = 2f;
            ManaAmount = 10;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            List<Unit> enemies = Target.GetAllEnemies(Caster).Where(unit => unit != null && unit.isActive).ToList();
            if (enemies.Count == 0)
            {
                StopCode();
                yield break;
            }

            Unit target = GridManager.Instance.TargetNearestEnemy(Caster).FirstOrDefault() ?? enemies[0];
            int bounces = Caster.GetCombatResource("Yoris");
            for (int hitIndex = 0; hitIndex <= bounces; hitIndex++)
            {
                if (!Caster.isActive || Caster.isControlled) break;
                enemies = Target.GetAllEnemies(Caster).Where(unit => unit != null && unit.isActive).ToList();
                if (enemies.Count == 0) break;
                if (target == null || !target.isActive) target = enemies[hitIndex % enemies.Count];

                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                float coefficient = hitIndex == 0 ? 60f : 30f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * coefficient * critMultiplier));
                var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NonContactAttack, DamageTag.Special };
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, tags, isCrit));
                // 풀 원소는 최초 대상에게만 부여한다.
                if (hitIndex == 0) target.GrantCombatElement(BaseEnums.UnitElement.Dendro);
                yield return new WaitForSeconds(0.12f);

                enemies = Target.GetAllEnemies(Caster).Where(unit => unit != null && unit.isActive).ToList();
                if (enemies.Count > 0) target = enemies[(hitIndex + 1) % enemies.Count];
            }

            Caster.RecoverMana(ManaAmount);
            StopCode();
        }

        public override void StopCode()
        {
            Caster.normalCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return Caster != null && Caster.isActive && Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
        }
    }
}
