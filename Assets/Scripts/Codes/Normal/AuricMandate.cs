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
            Element       = BaseEnums.ElementType.Physical;
            SkillCategory = BaseEnums.SkillCategory.Skill;   // 스킬 (SP 소모)
            SpCost        = 2;
            TargetType    = BaseEnums.TargetType.Single;     // 단일 타겟 (다중 히트는 자동 리타겟)
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

            // 4연타 효과 처리: 첫 타겟은 BattleManager가 사전 결정, 이후 타겟은 자동 리타겟
            for (int i = 0; i < 4; i++)
            {
                if (i > 0)
                {
                    // 첫 히트 이후: 자동 리타겟 (동률 시 무작위, 플레이어 입력 없음)
                    TargetUnits = GridManager.Instance.GetAutoSingleTarget(Caster);
                }
                if (TargetUnits == null || TargetUnits.Count == 0)
                {
                    break;
                }
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                DamageContext context = new(Caster, (int)(Caster.AtkCurr * 0.8f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget }, Element, isCrit);

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