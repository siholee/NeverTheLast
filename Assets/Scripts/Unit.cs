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
    public int statHp;
    public int growthHp;
    public int upgradeHp;

    // 공격력 관련 능력치
    public int atk; // 현재 공격력
    public int baseAtk; // 기본 공격력
    public float atkMultiplicativeBuff; // 공퍼
    public float atkAdditiveBuff; // 깡공
    public int statAtk; // 기초 공격력
    public int growthAtk; // 성장 공격력
    public int upgradeAtk; // 공격력 업그레이드 횟수

    // 방어력 관련 능력치
    public int def;
    public int baseDef;
    public float defMultiplicativeBuff;
    public float defAdditiveBuff;
    public int statDef;
    public int growthDef;
    public int upgradeDef;
    public float damageReduction;
    public int damageReductionBuff;

    // 치명타 확률 관련 능력치
    public float critChance;
    public float baseCritChance;
    public float critChanceBuff;
    public float statCritChance;
    public float growthCritChance;
    public float upgradeCritChance;

    // 치명타 피해 관련 능력치
    public float critDamage;
    public float baseCritDamage;
    public float critDamageBuff;
    public float statCritDamage;
    public float growthCritDamage;
    public float upgradeCritDamage;

    // 쿨타임
    public float cooldown;
    public float baseCooldown;
    public float cooldownMultiplicativeBuff;
    public float cooldownAdditiveBuff;

    // 마나 관련 능력치
    public int currentMana;
    public int maxMana;
    public float manaChargeRate;
    public int manaChargeBuff;

    // 코드 관련 변수들
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
    /// 태그별 받는 피해 감소를 관리하는 딕셔너리
    /// key: 속성 ID (예: DamageTag.SINGLE_TARGET 등)
    /// value: 피해 감소 비율 (예: 30은 30% 감소)
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
    /// 능력치 기본값들을 산출하는 함수 (모든 스탯에 대해)
    /// </summary>
    public void SetBase()
    {
        // 공격력 기본값 계산
        baseAtk = statAtk + (level * growthAtk) + (upgradeAtk * growthAtk);
        // 체력 기본값 계산
        baseHp = statHp + (level * growthHp) + (upgradeHp * growthHp);
        // 방어력 기본값 계산
        baseDef = statDef + (level * growthDef) + (upgradeDef * growthDef);
        // 치명타 확률 기본값 계산
        baseCritChance = statCritChance + (level * growthCritChance) + (upgradeCritChance * growthCritChance);
        // 치명타 피해 기본값 계산
        baseCritDamage = statCritDamage + (level * growthCritDamage) + (upgradeCritDamage * growthCritDamage);
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
        // 비율 피해 감소 등 나머지 스탯 업데이트
        damageReduction = damageReductionByAttribute.ContainsKey(0) ? damageReductionByAttribute[0] * 0.01f : 0f;
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
        float effectiveDEF = Mathf.Max(def - armorPenetration, 0);
        float damageReceived = Damage * (1f / (1f + effectiveDEF * 0.01f));

        foreach (int attr in damageTags)
        {
            if (damageReductionByAttribute.ContainsKey(attr))
            {
                float reduction = damageReductionByAttribute[attr];
                damageReceived *= 1f - reduction;
            }
        }
        damageReceived *= 1 - damageReduction;

        int hpBeforeHit = currentHp;
        currentHp -= Mathf.FloorToInt(damageReceived);
        Debug.Log($"{unitName}은(는) {hpBeforeHit}의 체력을 지닌 채 {damageReceived}의 피해를 받았습니다. 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            currentHp = 0;
            Debug.Log($"{unitName}은(는) 사망했습니다.");
            DeactivateUnit();
            gameManager.skillManager.OnCasterDeath(this);
        }
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        // 추가 설정이 필요하다면 이곳에 작성
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
}
