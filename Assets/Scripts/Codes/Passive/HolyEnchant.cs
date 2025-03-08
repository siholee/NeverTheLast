using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HolyEnchant : PassiveCode
{
    public HolyEnchant(PassiveCodeContext context) : base(context)
    {
        codeType = BaseEnums.CodeType.Passive;
        codeName = "호올리 인챈트";
        caster = context.caster;
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
            caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
        };

        Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})이 {codeName} 시전");
        // 시전자의 사망 이벤트에 핸들러 등록
        caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
    }

    protected void ApplyHolyEnchant()
    {
        var targetUnits = GridManager.Instance.TargetAllAllies(caster);
        var buffEffect = new HolyEnchantBuff();
        foreach (var targetUnit in targetUnits)
        {
            targetUnit.AddStatusEffect($"HolyEnchantBuff{caster.currentCell.xPos}{caster.currentCell.yPos}", buffEffect);
        }
    }

    public override void StopCode()
    {
        var targetUnits = GridManager.Instance.TargetAllAllies(caster);
        foreach (var targetUnit in targetUnits)
        {
            targetUnit.RemoveStatusEffect($"HolyEnchantBuff{caster.currentCell.xPos}{caster.currentCell.xPos}");
        }
    }
}