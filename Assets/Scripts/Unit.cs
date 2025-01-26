using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using UnityEngine;


public class Unit : MonoBehaviour
{
    public GameManager gameManager;
    public bool isActive = false;

    // 기본 식별 정보
    public int ID;
    public bool SIDE; // true: 아군, false: 적군
    public string NAME;

    // 체력 관련 능력치
    public int HP_CURRENT;
    public int HP_MAX;
    public int HP_BASE;
    public float HP_MULBUFF;
    public float HP_SUMBUFF;

    // 공격력 관련 능력치
    public int ATK;
    public int ATK_BASE;
    public float ATK_MULBUFF;
    public float ATK_SUMBUFF;

    // 방어력 관련 능력치
    public int DEF;
    public int DEF_BASE;
    public float DEF_MULBUFF;
    public float DEF_SUMBUFF;

    // 치명타 확률 및 피해
    public float CRT_POS;
    public float CRT_POS_BASE;
    public float CRT_POS_BUFF;

    public float CRT_DMG;
    public float CRT_DMG_BASE;
    public float CRT_DMG_BUFF;

    // 쿨타임
    public float CT;
    public float CT_BASE;
    public float CT_MULBUFF;
    public float CT_SUMBUFF;

    /// <summary>
    /// 속성별 가하는 피해 증가를 관리하는 딕셔너리
    /// key: 속성 이름 (예: "Fire", "Ice")
    /// value: 피해 증가 비율 (예: 0.2f는 20% 증가)
    /// </summary>
    public Dictionary<string, float> DamageIncreaseByAttribute = new Dictionary<string, float>();

    /// <summary>
    /// 속성별 받는 피해 감소를 관리하는 딕셔너리
    /// key: 속성 이름 (예: "Fire", "Ice")
    /// value: 피해 감소 비율 (예: 0.3f는 30% 감소)
    /// </summary>
    public Dictionary<string, float> DamageReductionByAttribute = new Dictionary<string, float>();

    /// <summary>
    /// 현재 유닛이 위치한 셀
    /// </summary>
    public Cell currentCell;

    /// <summary>
    /// 활성화 후 초기 정보 설정
    /// </summary>
    public virtual void InitProcess(bool isHero, int id) {}


    /// <summary>
    /// 상태 업데이트 함수
    /// </summary>
    public void StatusUpdate()
    {
        HP_MAX = (int)(HP_BASE * (1 + (HP_MULBUFF * 0.01f)) + HP_SUMBUFF);
        ATK = (int)(ATK_BASE * (1 + (ATK_MULBUFF * 0.01f)) + ATK_SUMBUFF);
        DEF = (int)(DEF_BASE * (1 + (DEF_MULBUFF * 0.01f)) + DEF_SUMBUFF);
        CRT_POS = CRT_POS_BASE + CRT_POS_BUFF;
        CRT_DMG = CRT_DMG_BASE + CRT_DMG_BUFF;
        CT = CT_BASE * (1 - CT_SUMBUFF);

        // 현재 HP가 최대 HP를 초과하지 않도록 조정
        if (HP_CURRENT > HP_MAX)
            HP_CURRENT = HP_MAX;
    }

    /// <summary>
    /// 데미지를 입는 함수
    /// </summary>
    /// <param name="Damage">입는 데미지</param>
    /// <param name="armorPenetration">방어력 관통 수치</param>
    /// <param name="damageAttributes">입는 데미지의 속성들</param>
    public void TakeDamage(float Damage, int armorPenetration, string[] damageAttributes)
    {
        // 방어력 관통 적용 (방어력에서 선 차감)
        float effectiveDEF = Mathf.Max(DEF - armorPenetration, 0);

        // 데미지 계산: Damage * (1 / (1 + DEF * 0.01f))
        float damageReceived = Damage * (1f / (1f + effectiveDEF * 0.01f));

        // 속성별 피해 감소 적용
        foreach(string attr in damageAttributes)
        {
            if(DamageReductionByAttribute.ContainsKey(attr))
            {
                float reduction = DamageReductionByAttribute[attr];
                damageReceived *= (1f - reduction);
            }
        }

        // 체력 차감
        HP_CURRENT -= Mathf.FloorToInt(damageReceived);
        Debug.Log($"{NAME}은(는) {damageReceived}의 피해를 받았습니다. 남은 체력: {HP_CURRENT}");

        if (HP_CURRENT <= 0)
        {
            HP_CURRENT = 0;
            Debug.Log($"{NAME}은(는) 사망했습니다.");
            // 추가적인 사망 처리 로직 (예: 게임 오브젝트 비활성화 등)
            gameManager.poolManager.DeactivateUnit(gameObject);
        }
    }

    /// <summary>
    /// 공격 함수
    /// </summary>
    /// <param name="target">공격 대상 유닛</param>
    /// <param name="attribute">공격에 사용할 속성 ("HP_MAX", "ATK", "DEF")</param>
    /// <param name="coefficient">공격 계수 (정수)</param>
    /// <param name="attackAttributes">공격 시 가하는 피해 증가 속성들</param>
    /// <param name="armorPenetration">방어력 관통 수치</param>
    /// <returns>총 데미지와 받은 속성 배열</returns>
    public (int totalDamage, string[] receivedAttributes) Attack(Unit target, string attribute, int coefficient, string[] attackAttributes, int armorPenetration)
    {
        // 선택한 속성에 따른 스탯 결정
        float baseStat = 0f;
        switch(attribute)
        {
            case "HP_MAX":
                baseStat = HP_MAX;
                break;
            case "ATK":
                baseStat = ATK;
                break;
            case "DEF":
                baseStat = DEF;
                break;
            default:
                Debug.LogError("Invalid attribute for attack. Choose 'HP_MAX', 'ATK', or 'DEF'.");
                return (0, null);
        }

        // 기본 데미지 계산
        float damage = coefficient * baseStat;

        // 치명타 판단
        bool isCritical = Random.value <= (CRT_POS / 100f);
        float criticalMultiplier = isCritical ? (1 + CRT_DMG / 100f) : 1f;

        // 가하는 피해 증가 적용
        float damageIncreaseMultiplier = 1f;
        foreach(string attr in attackAttributes)
        {
            if(DamageIncreaseByAttribute.ContainsKey(attr))
            {
                damageIncreaseMultiplier += DamageIncreaseByAttribute[attr];
            }
        }

        // 최종 데미지 계산
        float totalDamageFloat = damage * criticalMultiplier * damageIncreaseMultiplier;
        int totalDamage = Mathf.FloorToInt(totalDamageFloat);

        // 치명타 발생 로그
        if(isCritical)
        {
            Debug.Log($"{NAME}은(는) 치명타를 터뜨렸습니다!");
        }

        // 공격 로그
        Debug.Log($"{NAME}이(가) {target.NAME}에게 {(isCritical ? "치명타로 인해 " : "")}{totalDamage}의 {attribute} 기반 공격을 가합니다. (속성: {string.Join(", ", attackAttributes)})");

        // 대상에게 데미지 적용
        target.TakeDamage(totalDamage, armorPenetration, attackAttributes);

        // 공격 결과 반환
        return (totalDamage, attackAttributes);
    }

    /// <summary>
    /// 예시: 초기화 시 속성별 피해 증가 및 감소 설정
    /// </summary>
    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // 예시: Fire 속성 20% 피해 증가, Ice 속성 30% 피해 감소
        AddDamageIncrease("Fire", 0.2f); // 20% 증가
        AddDamageReduction("Ice", 0.3f); // 30% 감소

        // 다른 속성들도 필요에 따라 추가
    }

    /// <summary>
    /// 속성별 피해 증가를 추가하는 함수
    /// </summary>
    /// <param name="attribute">속성 이름</param>
    /// <param name="increase">피해 증가 비율 (0 ~ 1)</param>
    public void AddDamageIncrease(string attribute, float increase)
    {
        if (DamageIncreaseByAttribute.ContainsKey(attribute))
        {
            DamageIncreaseByAttribute[attribute] += increase;
        }
        else
        {
            DamageIncreaseByAttribute.Add(attribute, increase);
        }
    }

    /// <summary>
    /// 속성별 피해 감소를 추가하는 함수
    /// </summary>
    /// <param name="attribute">속성 이름</param>
    /// <param name="reduction">피해 감소 비율 (0 ~ 1)</param>
    public void AddDamageReduction(string attribute, float reduction)
    {
        if (DamageReductionByAttribute.ContainsKey(attribute))
        {
            DamageReductionByAttribute[attribute] += reduction;
        }
        else
        {
            DamageReductionByAttribute.Add(attribute, reduction);
        }
    }
}
