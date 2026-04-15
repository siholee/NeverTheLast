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
            // 캐스팅 연출
            if (CastingDelay > 0f)
                yield return new WaitForSeconds(CastingDelay);

            // 4연타 효과 처리
            for (int i = 0; i < 4; i++)
            {
                TargetUnits = GridManager.Instance.TargetNearestEnemy(Caster);
                if (TargetUnits.Count == 0)
                {
                    break;
                }
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                DamageContext context = new(Caster, (int)(Caster.AtkCurr * 0.8f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget }, isCrit);

                foreach (var target in TargetUnits)
                {
                    GameManager.Instance.sfxManager.FireSingleProjectile(_prefab, Caster, target, 0.2f);
                    yield return new WaitForSeconds(0.2f);
                    target.TakeDamage(context);
                    Caster.RecoverMana(ManaAmount);
                }
            }
            StopCode();
        }

        public override void StopCode()
        {
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
        {
            return GridManager.Instance.TargetNearestEnemy(Caster).Count > 0;
        }
    }
}