using UnityEngine;
using System.Collections.Generic; 

public class Code
{
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
