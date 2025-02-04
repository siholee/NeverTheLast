using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class Unit : MonoBehaviour
{
    public GameManager gameManager;
    public bool isActive = false;

    // 기본 식별 정보
    public int id;
    public bool isEnemy;
    public string unitName;

    public int level;

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

    // 비율 피해 감소
    public float damageReduction;
    public int damageReductionBuff;

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

    // 마나
    public int currentMana;
    public int maxMana;
    public float manaChargeRate;
    public int manaChargeBuff;

    // 코드
    public int passiveCodeId;
    public int normalCodeId;
    public int ultimateCodeId;
    public CodeBase passiveCode;
    public CodeBase normalCode;
    public CodeBase ultimateCode;
    public float[] codeCooldowns;
    public bool isCasting;

    /// <summary>
    /// 속성별 가하는 피해 증가를 관리하는 딕셔너리
    /// key: 속성 이름 (예: "Fire", "Ice")
    /// value: 피해 증가 비율 (예: 0.2f는 20% 증가)
    /// </summary>
    public Dictionary<string, float> damageIncreaseByAttribute = new Dictionary<string, float>();

    /// <summary>
    /// 태그그별 받는 피해 감소를 관리하는 딕셔너리
    /// key: 속성 ID (예: DamageTag.SINGLE_TARGET 등 Helper/helper.cs에서 추가)
    /// value: 피해 감소 비율 (예: 30은은 30% 감소)
    /// </summary>
    public Dictionary<int, int> damageReductionByAttribute = new Dictionary<int, int>();

    /// <summary>
    /// 현재 유닛이 위치한 셀
    /// </summary>
    public Cell currentCell;

    /// <summary>
    /// 활성화 후 초기 정보 설정
    /// </summary>
    public virtual void InitProcess(bool isEnemy, int id)
    {
        this.id = id;
        if (id > 0)
        {
            passiveCode = CodeFactory.CreateCode(passiveCodeId, new CodeCreationContext { caster = this });
            normalCode = CodeFactory.CreateCode(normalCodeId, new CodeCreationContext { caster = this });
            ultimateCode = CodeFactory.CreateCode(ultimateCodeId, new CodeCreationContext { caster = this });
            codeCooldowns = new float[] { 0, 0, ultimateCode.cooldown };
            isCasting = false;
        }
    }

    public void LoadSprite(string name, bool isEnemy)
    {
        Debug.LogWarning($"Sprite/Portraits/{(isEnemy ? "Enemies" : "Heroes")}/{name}");
        Sprite sprite = Resources.Load<Sprite>($"Sprite/Portraits/{(isEnemy ? "Enemies" : "Heroes")}/{name}");
        currentCell.portraitRenderer.sprite = sprite;
    }


    /// <summary>
    /// 상태 업데이트 함수
    /// </summary>
    public void StatusUpdate()
    {
        float hpRate = (float)currentHp / maxHp;
        maxHp = (int)(baseHp * (1 + (hpMultiplicativeBuff * 0.01f)) + hpAdditiveBuff);
        currentHp = Mathf.Min((int)(maxHp * hpRate), maxHp);
        currentCell.hpBarObj.transform.localScale = new Vector3(hpRate, 1, 1);
        atk = (int)(baseAtk * (1 + (atkMultiplicativeBuff * 0.01f)) + atkAdditiveBuff);
        def = (int)(baseDef * (1 + (defMultiplicativeBuff * 0.01f)) + defAdditiveBuff);
        critChance = baseCritChance + critChanceBuff;
        critDamage = baseCritDamage + critDamageBuff;
        damageReduction = damageReductionBuff * 0.01f;
        manaChargeRate = 1 + manaChargeBuff * 0.01f;
        manaChargeBuff = 0;
        cooldown = baseCooldown * (1 + cooldownMultiplicativeBuff * 0.01f) + cooldownAdditiveBuff;
    }

    /// <summary>
    /// 데미지를 입는 함수
    /// </summary>
    /// <param name="Damage">입는 데미지</param>
    /// <param name="armorPenetration">방어력 관통 수치</param>
    /// <param name="damageTags">입는 데미지의 속성들</param>
    public void TakeDamage(float Damage, int armorPenetration, List<int> damageTags)
    {
        // 방어력 관통 적용 (방어력에서 선 차감)
        float effectiveDEF = Mathf.Max(def - armorPenetration, 0);

        // 데미지 계산: Damage * (1 / (1 + DEF * 0.01f))
        float damageReceived = Damage * (1f / (1f + effectiveDEF * 0.01f));

        // 속성별 피해 감소 적용
        foreach (int attr in damageTags)
        {
            if (damageReductionByAttribute.ContainsKey(attr))
            {
                float reduction = damageReductionByAttribute[attr];
                damageReceived *= 1f - reduction;
            }
        }
        damageReceived *= 1 - damageReduction;

        // 체력 차감
        int hpBeforeHit = currentHp;
        currentHp -= Mathf.FloorToInt(damageReceived);
        Debug.Log($"{unitName}은(는) {hpBeforeHit}의 체력을 지닌 채 {damageReceived}의 피해를 받았습니다. 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            currentHp = 0;
            Debug.Log($"{unitName}은(는) 사망했습니다.");
            // 추가적인 사망 처리 로직 (예: 게임 오브젝트 비활성화 등)
            DeactivateUnit();
            gameManager.skillManager.OnCasterDeath(this);
        }
    }

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
        if (isActive)
        {
            StatusUpdate();
            codeCooldowns = codeCooldowns.Select(x => Mathf.Max(x - Time.deltaTime * cooldown, 0)).ToArray();
            if (!isCasting)
            {
                if (codeCooldowns[0] == 0)
                {
                    bool success = gameManager.skillManager.RegisterSkill(this, passiveCode);
                    if (success)
                    {
                        codeCooldowns[0] = passiveCode.cooldown;
                        Debug.Log($"{unitName}의 패시브 코드가 발동되었습니다.");
                    }
                }
                if (codeCooldowns[1] == 0)
                {
                    if (codeCooldowns[2] == 0 && currentMana >= 100)
                    {
                        bool success = gameManager.skillManager.RegisterSkill(this, ultimateCode);
                        if (success)
                        {
                            codeCooldowns[2] = ultimateCode.cooldown;
                            currentMana = 0;
                            Debug.Log($"{unitName}의 궁극 코드가 발동되었습니다.");
                        }
                    }
                    else
                    {
                        bool success = gameManager.skillManager.RegisterSkill(this, normalCode);
                        if (success)
                        {
                            codeCooldowns[1] = normalCode.cooldown;
                            Debug.Log($"{unitName}의 일반 코드가 발동되었습니다.");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 속성별 피해 증가를 추가하는 함수
    /// </summary>
    /// <param name="attribute">속성 이름</param>
    /// <param name="increase">피해 증가 비율 (0 ~ 1)</param>
    public void AddDamageIncrease(string attribute, float increase)
    {
        if (damageIncreaseByAttribute.ContainsKey(attribute))
        {
            damageIncreaseByAttribute[attribute] += increase;
        }
        else
        {
            damageIncreaseByAttribute.Add(attribute, increase);
        }
    }

    /// <summary>
    /// 속성별 피해 감소를 추가하는 함수
    /// </summary>
    /// <param name="attribute">속성 id(Helpes/Helper.cs)</param>
    /// <param name="reduction">피해 감소 비율 (0 ~ 1)</param>
    public void AddDamageReduction(int tag, int reduction)
    {
        if (damageReductionByAttribute.ContainsKey(tag))
        {
            damageReductionByAttribute[tag] += reduction;
        }
        else
        {
            damageReductionByAttribute.Add(tag, reduction);
        }
    }

    public void ActivateUnit(bool isEnemy, int id)
    {
        isActive = true;
        this.isEnemy = isEnemy;
        InitProcess(isEnemy, id);
        currentCell.isOccupied = true;
        currentCell.hpBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = true;
    }

    public void DeactivateUnit()
    {
        isActive = false;
        currentCell.isOccupied = false;
        currentCell.portraitRenderer.sprite = null;
        currentCell.hpBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = false;
    }

    public void RecoverMana(int amount)
    {
        currentMana = Mathf.Min((int)(currentMana + amount * manaChargeRate), maxMana);
    }

    public int SetBase(int n, int lv, int increase)
    {
        return n + lv * increase;
    }
}
