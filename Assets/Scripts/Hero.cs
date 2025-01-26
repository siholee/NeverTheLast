using System.Collections.Generic;
using UnityEngine;

public class Hero : Unit
{
    // 쿨타임 감소 (최대 50% 제한)
    private float CT_REDUCE; // 쿨타임 감소 값
    public float CooldownReduction // 읽기 전용 프로퍼티
    {
        get { return Mathf.Min(CT_REDUCE, 0.5f); } // 최대 50%로 제한
    }

    // 스킬 슬롯 (코드 슬롯)
    public Code[] ModuleSlot = new Code[5];

    // 속성별 피해 증가
    public Dictionary<int, float> DamageAmplificationByAttribute = new Dictionary<int, float>();

    public void Attack(string targetType, int attribute, int damage, float coefficient, int elementId)
    {
        // 속성별 피해 증가를 고려한 총 데미지 계산
        float amplification = DamageAmplificationByAttribute.ContainsKey(elementId) 
                                ? DamageAmplificationByAttribute[elementId] 
                                : 0f;
        int totalDamage = Mathf.FloorToInt(damage * coefficient * (1 + amplification));

        // 타겟 계산 및 데미지 적용 로직 구현
        Debug.Log($"[{NAME}] 공격: {totalDamage} 데미지 (타입: {targetType}, 속성: {attribute}, 요소 ID: {elementId})");

        // 예시: 타겟을 찾아 데미지를 적용하는 로직 추가
        // 예를 들어, 적 유닛 리스트에서 타겟을 선택하고 GetDamage 호출
    }
}
