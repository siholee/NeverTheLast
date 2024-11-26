using System.Collections.Generic;
using UnityEngine;

public class Hero : MonoBehaviour
{
    // 기본 스탯
    public int HP_CURRENT;
    public int HP_MAX;
    public int HP_BASE;
    public int ATK;
    public int DEF;
    public int ATK_BASE;
    public int DEF_BASE;

    // 쿨타임 감소
    private float CT_REDUCE; // 쿨타임 감소 (최대 50% 제한)

    public float CooldownReduction // 읽기 전용 프로퍼티
    {
        get { return Mathf.Min(CT_REDUCE, 0.5f); } // 최대값 제한
    }


    // 버프
    private int additiveHPBuff;  // HP의 합연산 버프
    private float multiplicativeHPBuff; // HP의 곱연산 버프
    private int additiveATKBuff; // ATK의 합연산 버프
    private float multiplicativeATKBuff; // ATK의 곱연산 버프
    private int additiveDEFBuff; // DEF의 합연산 버프
    private float multiplicativeDEFBuff; // DEF의 곱연산 버프

    // 코드 (스킬) 슬롯
    public Code[] ModuleSlot = new Code[5];

    // 속성별 가하는 피해 증가
    public Dictionary<int, float> DamageAmplificationByAttribute = new Dictionary<int, float>();

    // 스탯 업데이트
    public void UpdateStats()
    {
        HP_MAX = Mathf.FloorToInt(HP_BASE * (1 + multiplicativeHPBuff) + additiveHPBuff);
        ATK = Mathf.FloorToInt(ATK_BASE * (1 + multiplicativeATKBuff) + additiveATKBuff);
        DEF = Mathf.FloorToInt(DEF_BASE * (1 + multiplicativeDEFBuff) + additiveDEFBuff);

        if (HP_CURRENT > HP_MAX) HP_CURRENT = HP_MAX; // 현재 HP 제한
    }

    // 데미지 계산
    public void GetDamage(int damage)
    {
        int reducedDamage = Mathf.Max(0, damage - DEF); // 방어력에 따른 데미지 감소
        HP_CURRENT -= reducedDamage;

        if (HP_CURRENT <= 0)
        {
            HP_CURRENT = 0;
            Debug.Log($"{name} has died.");
        }
    }

    // 공격 함수
    public void Attack(string targetType, int attribute, float coefficient, int elementId)
    {
        int totalDamage = Mathf.FloorToInt(ATK * coefficient * (1 + DamageAmplificationByAttribute.GetValueOrDefault(elementId, 0f)));

        // 타겟 계산
        Debug.Log($"Attacking with {totalDamage} damage (Type: {targetType}, Attribute: {attribute}, Element ID: {elementId}).");
    }

    public void GenerateCode(int id)
    {
        // 코드 데이터를 로드
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/03_Code");
        if (jsonFile == null)
        {
            Debug.LogError("03_Code.JSON not found.");
            return;
        }

        // JSON 데이터 파싱
        CodeDataWrapper codeDataWrapper = JsonUtility.FromJson<CodeDataWrapper>(jsonFile.text);
        CodeData codeData = codeDataWrapper.codes.Find(c => c.id == id);

        if (codeData != null)
        {
            Code newCode = new Code(this, codeData);
            Debug.Log($"Code {codeData.name} (ID: {id}) generated for {name}.");
        }
        else
        {
            Debug.LogError($"Code with ID {id} not found in 03_Code.JSON.");
        }
    }

}
