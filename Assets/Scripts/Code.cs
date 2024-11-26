using UnityEngine;
using System.Collections.Generic; 

public class Code
{
    public Hero ModuleOwner; // 소유자
    public float CT_BASE; // 기본 쿨타임
    public float CT; // 현재 쿨타임
    public bool isPassive; // 패시브 여부
    public int Counter; // 횟수 제한

    public Code(Hero owner, CodeData codeData)
    {
        ModuleOwner = owner;
        CT_BASE = codeData.cooldown;
        CT = CT_BASE;
        isPassive = codeData.isPassive;
        Counter = codeData.counter;
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (!isPassive && CT > 0)
        {
            CT -= deltaTime * (1 - ModuleOwner.CooldownReduction); // 프로퍼티를 통해 접근
            if (CT < 0) CT = 0;
        }
    }

    public void Activate()
    {
        if (CT <= 0 && !isPassive)
        {
            Debug.Log($"Activating Code owned by {ModuleOwner.name}.");
            CT = CT_BASE; // 쿨타임 초기화
        }
    }
}

[System.Serializable]
public class CodeData
{
    public int id;           // 코드 ID
    public string name;      // 코드 이름
    public float cooldown;   // 기본 쿨타임
    public bool isPassive;   // 패시브 여부
    public int counter;      // 최대 사용 횟수
    public int attribute;    // Attribute (HP, ATK, DEF 등)
    public float coefficient; // 스킬 계수
    public int elementId;    // 속성 ID
}

[System.Serializable]
public class CodeDataWrapper
{
    public List<CodeData> codes; // 코드 목록
}
