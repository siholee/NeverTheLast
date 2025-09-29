using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 아탈란테 전용 일반공격: 사냥꾼의 활
    /// 일반공격 시 적중한 대상에게 공격력의 10%에 해당하는 맹독 부여
    /// </summary>
    public class AtlantaArchery : NormalCode
    {
        public AtlantaArchery(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            Caster = context.Caster;
            Cooldown = 1.2f;
            CodeName = "사냥꾼의 활";
        }

        public override void CastCode()
        {
            var target = GetTarget();
            if (target == null)
            {
                Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos}): 공격할 타겟이 없음");
                return;
            }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.RoundToInt(Caster.AtkCurr * critMultiplier);

            // 아탈란테는 항상 비접촉 공격
            List<int> damageTags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack };

            DamageContext context = new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, damageTags, isCrit);

            // 투사체 발사 후 피해 적용
            if (GameManager.Instance.sfxManager.ProjectilePrefabs.ContainsKey("Arrow"))
            {
                GameManager.Instance.sfxManager.FireSingleProjectile(
                    GameManager.Instance.sfxManager.ProjectilePrefabs["Arrow"], 
                    Caster, target, 0.3f);
            }

            // 피해 적용
            target.TakeDamage(context);

            // 사냥꾼의 독 부여
            ApplyHuntersVenom(target);

            Caster.normalCooldown = Cooldown;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {target.UnitName}({target.currentCell.xPos}, {target.currentCell.yPos})에게 {CodeName} 사용");
        }

        private void ApplyHuntersVenom(Unit target)
        {
            if (target != null && target.isActive && target.IsEnemy != Caster.IsEnemy)
            {
                // 공격력의 10%에 해당하는 독 부여
                int poisonDamage = Mathf.RoundToInt(Caster.AtkCurr * 0.1f);
                string identifier = $"HuntersVenomPoison_{Caster.currentCell.xPos}_{Caster.currentCell.yPos}";
                var poisonEffect = new PoisonEffect(Caster, identifier, poisonDamage);
                target.AddStatusEffect(identifier, poisonEffect);
                
                Debug.Log($"{Caster.UnitName}이 {target.UnitName}에게 사냥꾼의 독 부여 (피해: {poisonDamage})");
            }
        }

        private Unit GetTarget()
        {
            var targets = GridManager.Instance.TargetNearestEnemy(Caster);
            return targets.Count > 0 ? targets[0] : null;
        }

        public override void StopCode()
        {
            // 일반공격은 특별한 정리 작업 없음
        }

        public override bool HasValidTarget()
        {
            return GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
        }
    }
}