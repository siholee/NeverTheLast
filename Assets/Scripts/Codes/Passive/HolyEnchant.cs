using System;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Passive
{
    public class HolyEnchant : PassiveCode
    {
        public HolyEnchant(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "호올리 인챈트";
            Caster = context.Caster;
            // effects = new Dictionary<string, OldEffectBase>();
        }

        public override void CastCode()
        {
            ApplyHolyEnchant();
            // 시전자 사망 시 효과를 멈추기 위한 이벤트 핸들러 생성
            Action<(Unit, Unit)> onDeathHandler = null;
            // 익명함수를 변수화해 이벤트 핸들러에서 삭제가 가능하도록 함
            onDeathHandler = (deathInfo) =>
            {
                StopCode();
                // 부여한 상태효과를 해제하고 스스로를 제거해 일회성 처리로 만듬
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
            };

            Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
            // 시전자의 사망 이벤트에 핸들러 등록
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
        }

        protected void ApplyHolyEnchant()
        {
            var targetUnits = GridManager.Instance.TargetAllAllies(Caster);
            var buffEffect = new HolyEnchantBuff();
            foreach (var targetUnit in targetUnits)
            {
                targetUnit.AddStatusEffect($"HolyEnchantBuff{Caster.currentCell.xPos}{Caster.currentCell.yPos}", buffEffect);
            }
        }

        public override void StopCode()
        {
            var targetUnits = GridManager.Instance.TargetAllAllies(Caster);
            foreach (var targetUnit in targetUnits)
            {
                targetUnit.RemoveStatusEffect($"HolyEnchantBuff{Caster.currentCell.xPos}{Caster.currentCell.xPos}");
            }
        }
    }
}