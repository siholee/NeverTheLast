using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Test
{
    /// <summary>
    /// 아군에게 방어막을 부여하는 테스트 스킬
    /// </summary>
    public class GiveShield : UltimateCode
    {
        public GiveShield(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            Caster = context.Caster;
            Cooldown = 5f;
            CodeName = "방어막 부여";
            CastingDelay = 1f;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            // 캐스팅 딜레이
            float elapsedTime = 0f;
            while (elapsedTime < CastingDelay)
            {
                if (Caster.isControlled || !Caster.isActive)
                {
                    Debug.Log($"{Caster.UnitName}의 {CodeName} 시전이 방해됨");
                    StopCode();
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"{Caster.UnitName}이 {CodeName}을 시전했습니다!");

            // 모든 아군에게 방어막 부여
            List<Unit> allies = GetAllAllies();
            int shieldAmount = (int)(Caster.AtkCurr * 0.8f); // 공격력의 80%

            foreach (Unit ally in allies)
            {
                if (ally != null && ally.isActive)
                {
                    // 기존 방어막이 있으면 추가, 없으면 새로 부여
                    ally.AddShield(shieldAmount);

                    // 방어막 상태 효과 추가 (시각적 표시 및 관리용)
                    string shieldIdentifier = $"Shield_{ally.GetInstanceID()}_{Time.time}";
                    ShieldEffect shieldEffect = new ShieldEffect(Caster, shieldIdentifier, 0);
                    ally.AddStatusEffect(shieldIdentifier, shieldEffect);

                    Debug.Log($"{ally.UnitName}에게 {shieldAmount}의 방어막 부여!");
                }
            }

            StopCode();
        }

        private List<Unit> GetAllAllies()
        {
            GridManager gridManager = GameObject.FindFirstObjectByType<GridManager>();
            if (gridManager == null) return new List<Unit>();

            bool casterIsAlly = gridManager.heroList.Contains(Caster);
            List<Unit> targetList = casterIsAlly ? gridManager.heroList : gridManager.enemyList;

            List<Unit> activeAllies = new List<Unit>();
            foreach (Unit unit in targetList)
            {
                if (unit && unit.isActive)
                {
                    activeAllies.Add(unit);
                }
            }

            return activeAllies;
        }
    }
}