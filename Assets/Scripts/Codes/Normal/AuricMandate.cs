using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using Codes.Base;
using Entities;
using Helpers;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public class AuricMandate : NormalCode
    {
        private readonly HS_Poolable _prefab;
        public AuricMandate(NormalCodeContext context) : base(context)
        {
            _prefab = GameManager.Instance.sfxManager.ProjectilePrefabs["AuricMandate"];
            CodeType = BaseEnums.CodeType.Normal;
            Caster = context.Caster;
            Cooldown = 2f;
            CastingDelay = 0.2f;
            CodeName = "성광의 권능";
            ManaAmount = 4;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 효과 처리
            for (int i = 0; i < 4; i++)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                TargetUnits = GridManager.Instance.TargetNearestEnemy(Caster);
                if (TargetUnits.Count == 0)
                {
                    yield break;
                }
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                DamageContext context = new(Caster, (int)(Caster.AtkCurr * 0.8f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget }, isCrit);
                Caster.StartCoroutine(FireProjectile(TargetUnits, 0.2f, context));
                yield return new WaitForSeconds(0.2f);
            }
            StopCode();
        }

        public override void StopCode()
        {
            Caster.normalCooldown = Cooldown;
            Caster.isCasting = false;
        }

        private IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            foreach (var target in targets)
            {
                GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, delay);
                yield return new WaitForSeconds(delay);
                target.TakeDamage(context);
                Caster.RecoverMana(ManaAmount);
            }
        }

        public override bool HasValidTarget()
        {
            return GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
        }
    }
}