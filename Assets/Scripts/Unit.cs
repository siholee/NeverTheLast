using System;
using System.Collections.Generic;
using UnityEngine;


public class Unit : MonoBehaviour
{
    public GameManager gameManager;
    public bool isActive = false;

    // 기본 식별 정보
    public int id;
    public bool side; // true: 아군, false: 적군
    public string unitName;

    // 체력 관련 능력치
    public int currentHp;
    public int maxHp;
    public int baseHp;
    public float hpMultiplicativeBuff;
    public float hpAdditiveBuff;

    // 공격력 관련 능력치
    public int atk;
    public int baseAtk;
    public float atkMultiplicativeBuff;
    public float atkAdditiveBuff;

    // 방어력 관련 능력치
    public int def;
    public int baseDef;
    public float defMultiplicativeBuff;
    public float defAdditiveBuff;

    // 치명타 확률 및 피해
    public float critChance;
    public float baseCritChance;
    public float critChanceBuff;

    public float critDamage;
    public float baseCritDamage;
    public float critDamageBuff;

    // 쿨타임
    public float cooldown;
    public float baseCooldown;
    public float cooldownMultiplicativeBuff;
    public float cooldownAdditiveBuff;

    // 코드
    public int passiveCodeId;
    public int normalCodeId;
    public int ultimateCodeId;

    /// <summary>
    /// 속성별 가하는 피해 증가를 관리하는 딕셔너리
    /// key: 속성 이름 (예: "Fire", "Ice")
    /// value: 피해 증가 비율 (예: 0.2f는 20% 증가)
    /// </summary>
    public Dictionary<string, float> DamageIncreaseByAttribute = new Dictionary<string, float>();

    /// <summary>
    /// 태그그별 받는 피해 감소를 관리하는 딕셔너리
    /// key: 속성 ID (예: DamageTag.SINGLE_TARGET 등 Helper/helper.cs에서 추가)
    /// value: 피해 감소 비율 (예: 30은은 30% 감소)
    /// </summary>
    public Dictionary<int, int> DamageReductionByAttribute = new Dictionary<int, int>();

    /// <summary>
    /// 현재 유닛이 위치한 셀
    /// </summary>
    public Cell currentCell;

    /// <summary>
    /// 활성화 후 초기 정보 설정
    /// </summary>
    public virtual void InitProcess(bool isHero, int id) {
        this.id = id;
    }

    public void LoadSprite(string name, bool isHero)
    {
        Sprite sprite = Resources.Load<Sprite>($"Sprite/Portraits/{(isHero ? "Heroes" : "Enemies")}/{name}");
        currentCell.portraitRenderer.sprite = sprite;
    }


    /// <summary>
    /// 상태 업데이트 함수
    /// </summary>
    public void StatusUpdate()
    {
        maxHp = (int)(baseHp * (1 + (hpMultiplicativeBuff * 0.01f)) + hpAdditiveBuff);
        atk = (int)(baseAtk * (1 + (atkMultiplicativeBuff * 0.01f)) + atkAdditiveBuff);
        def = (int)(baseDef * (1 + (defMultiplicativeBuff * 0.01f)) + defAdditiveBuff);
        critChance = baseCritChance + critChanceBuff;
        critDamage = baseCritDamage + critDamageBuff;
        cooldown = Mathf.Max((baseCooldown - cooldownAdditiveBuff) * (1 - cooldownMultiplicativeBuff * 0.01f), baseCooldown * 0.5f);

        // 현재 HP가 최대 HP를 초과하지 않도록 조정
        if (currentHp > maxHp)
            currentHp = maxHp;
    }

    /// <summary>
    /// 데미지를 입는 함수
    /// </summary>
    /// <param name="Damage">입는 데미지</param>
    /// <param name="armorPenetration">방어력 관통 수치</param>
    /// <param name="damageTags">입는 데미지의 속성들</param>
    public void TakeDamage(float Damage, int armorPenetration, int[] damageTags)
    {
        // 방어력 관통 적용 (방어력에서 선 차감)
        float effectiveDEF = Mathf.Max(def - armorPenetration, 0);

        // 데미지 계산: Damage * (1 / (1 + DEF * 0.01f))
        float damageReceived = Damage * (1f / (1f + effectiveDEF * 0.01f));

        // 속성별 피해 감소 적용
        foreach(int attr in damageTags)
        {
            if(DamageReductionByAttribute.ContainsKey(attr))
            {
                float reduction = DamageReductionByAttribute[attr];
                damageReceived *= 1f - reduction;
            }
        }

        // 체력 차감
        currentHp -= Mathf.FloorToInt(damageReceived);
        Debug.Log($"{unitName}은(는) {damageReceived}의 피해를 받았습니다. 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            currentHp = 0;
            Debug.Log($"{unitName}은(는) 사망했습니다.");
            // 추가적인 사망 처리 로직 (예: 게임 오브젝트 비활성화 등)
            gameManager.poolManager.DeactivateUnit(gameObject);
            gameManager.skillManager.OnCasterDeath(this);
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
    // public (int totalDamage, string[] receivedAttributes) Attack(Unit target, string attribute, int coefficient, string[] attackAttributes, int armorPenetration)
    // {
    //     // 선택한 속성에 따른 스탯 결정
    //     float baseStat = 0f;
    //     switch(attribute)
    //     {
    //         case "HP_MAX":
    //             baseStat = maxHp;
    //             break;
    //         case "ATK":
    //             baseStat = atk;
    //             break;
    //         case "DEF":
    //             baseStat = def;
    //             break;
    //         default:
    //             Debug.LogError("Invalid attribute for attack. Choose 'HP_MAX', 'ATK', or 'DEF'.");
    //             return (0, null);
    //     }

    //     // 기본 데미지 계산
    //     float damage = coefficient * baseStat;

    //     // 치명타 판단
    //     bool isCritical = UnityEngine.Random.value <= (critChance * 0.01f);
    //     float criticalMultiplier = isCritical ? (1 + critDamage  * 0.01f) : 1f;

    //     // 가하는 피해 증가 적용
    //     float damageIncreaseMultiplier = 1f;
    //     foreach(string attr in attackAttributes)
    //     {
    //         if(DamageIncreaseByAttribute.ContainsKey(attr))
    //         {
    //             damageIncreaseMultiplier += DamageIncreaseByAttribute[attr];
    //         }
    //     }

    //     // 최종 데미지 계산
    //     float totalDamageFloat = damage * criticalMultiplier * damageIncreaseMultiplier;
    //     int totalDamage = Mathf.FloorToInt(totalDamageFloat);

    //     // 치명타 발생 로그
    //     if(isCritical)
    //     {
    //         Debug.Log($"{unitName}은(는) 치명타를 터뜨렸습니다!");
    //     }

    //     // 공격 로그
    //     Debug.Log($"{unitName}이(가) {target.unitName}에게 {(isCritical ? "치명타로 인해 " : "")}{totalDamage}의 {attribute} 기반 공격을 가합니다. (속성: {string.Join(", ", attackAttributes)})");

    //     // 대상에게 데미지 적용
    //     target.TakeDamage(totalDamage, armorPenetration, attackAttributes);

    //     // 공격 결과 반환
    //     return (totalDamage, attackAttributes);
    // }

    /// <summary>
    /// 예시: 초기화 시 속성별 피해 증가 및 감소 설정
    /// </summary>
    private void Start()
    {
        gameManager = GameManager.Instance;

        // 예시: 전체공격 피격 피해 30% 감소
        // AddDamageReduction(DamageTag.ALL_TARGET, 30); // 전체공격 피격 피해 30% 감소

        // 다른 속성들도 필요에 따라 추가
    }

    private void Update()
    {
        
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
    /// <param name="attribute">속성 id(Helpes/Helper.cs)</param>
    /// <param name="reduction">피해 감소 비율 (0 ~ 1)</param>
    public void AddDamageReduction(int tag, int reduction)
    {
        if (DamageReductionByAttribute.ContainsKey(tag))
        {
            DamageReductionByAttribute[tag] += reduction;
        }
        else
        {
            DamageReductionByAttribute.Add(tag, reduction);
        }
    }
}
