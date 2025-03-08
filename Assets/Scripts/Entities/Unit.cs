using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public bool isActive = false;

    // 기본 식별정보
    [SerializeField] private int _id;
    [SerializeField] private bool _isEnemy;
    [SerializeField] private string _unitName;
    [SerializeField] private int _level;
    public int id { get => _id; protected set => _id = value; }
    public bool isEnemy { get => _isEnemy; protected set => _isEnemy = value; }
    public string unitName { get => _unitName; protected set => _unitName = value; }
    public int level { get => _level; protected set => _level = value; }

    public Cell currentCell; // 위치중인 셀

    // 유닛 강화 횟수
    [SerializeField] private int _hpUpgrade;
    [SerializeField] private int _atkUpgrade;
    [SerializeField] private int _defUpgrade;
    [SerializeField] private int _critChanceUpgrade;
    [SerializeField] private int _critMultiplierUpgrade;
    public int hpUpgrade { get => _hpUpgrade; protected set => _hpUpgrade = value; }
    public int atkUpgrade { get => _atkUpgrade; protected set => _atkUpgrade = value; }
    public int defUpgrade { get => _defUpgrade; protected set => _defUpgrade = value; }
    public int critChanceUpgrade { get => _critChanceUpgrade; protected set => _critChanceUpgrade = value; }
    public int critMultiplierUpgrade { get => _critMultiplierUpgrade; protected set => _critMultiplierUpgrade = value; }

    // 유닛 현재 상태
    [SerializeField] private int _hpCurr;
    [SerializeField] private int _manaCurr;
    [SerializeField] private int _atkCurr;
    [SerializeField] private int _defCurr;
    [SerializeField] private float _critChanceCurr;
    [SerializeField] private float _critDamageCurr;
    [SerializeField] private float _codeAcceleration;
    public int hpCurr { get => _hpCurr; protected set => _hpCurr = value; }
    public int manaCurr { get => _manaCurr; protected set => _manaCurr = value; }
    public int atkCurr { get => _atkCurr; protected set => _atkCurr = value; }
    public int defCurr { get => _defCurr; protected set => _defCurr = value; }
    public float critChanceCurr { get => _critChanceCurr; protected set => _critChanceCurr = value; }
    public float critMultiplierCurr { get => _critDamageCurr; protected set => _critDamageCurr = value; }
    public float codeAcceleration { get => _codeAcceleration; protected set => _codeAcceleration = value; }

    public bool isCasting; // 스킬 시전중
    public float castingTime;
    public bool isControlled; // 행동 불가 상태
    public float controlDuration;

    // 유닛 스탯 수치
    // 인스펙터 노출용용
    [SerializeField] private int _hpBase;
    [SerializeField] private int _hpIncrementLvl;
    [SerializeField] private int _hpIncrementUpgrade;
    [SerializeField] private int _hpMax;
    [SerializeField] private int _manaBase;
    [SerializeField] private int _manaMax;
    [SerializeField] private int _atkBase;
    [SerializeField] private int _atkIncrementLvl;
    [SerializeField] private int _atkIncrementUpgrade;
    [SerializeField] private int _defBase;
    [SerializeField] private int _defIncrementLvl;
    [SerializeField] private int _defIncrementUpgrade;
    [SerializeField] private float _critChanceBase;
    [SerializeField] private float _critChanceIncrementLvl;
    [SerializeField] private float _critChanceIncrementUpgrade;
    [SerializeField] private float _critMultiplierBase;
    [SerializeField] private float _critMultiplierIncrementLvl;
    [SerializeField] private float _critMultiplierIncrementUpgrade;
    [SerializeField] private List<int> _synergies;

    // 실제 수치 관리용
    public int hpBase { get => _hpBase; protected set => _hpBase = value; }
    public int hpIncrementLvl { get => _hpIncrementLvl; protected set => _hpIncrementLvl = value; }
    public int hpIncrementUpgrade { get => _hpIncrementUpgrade; protected set => _hpIncrementUpgrade = value; }
    public int hpMax { get => _hpMax; protected set => _hpMax = value; }
    public int manaBase { get => _manaBase; protected set => _manaBase = value; }
    public int manaMax { get => _manaMax; protected set => _manaMax = value; }
    public int atkBase { get => _atkBase; protected set => _atkBase = value; }
    public int atkIncrementLvl { get => _atkIncrementLvl; protected set => _atkIncrementLvl = value; }
    public int atkIncrementUpgrade { get => _atkIncrementUpgrade; protected set => _atkIncrementUpgrade = value; }
    public int defBase { get => _defBase; protected set => _defBase = value; }
    public int defIncrementLvl { get => _defIncrementLvl; protected set => _defIncrementLvl = value; }
    public int defIncrementUpgrade { get => _defIncrementUpgrade; protected set => _defIncrementUpgrade = value; }
    public float critChanceBase { get => _critChanceBase; protected set => _critChanceBase = value; }
    public float critChanceIncrementLvl { get => _critChanceIncrementLvl; protected set => _critChanceIncrementLvl = value; }
    public float critChanceIncrementUpgrade { get => _critChanceIncrementUpgrade; protected set => _critChanceIncrementUpgrade = value; }
    public float critMultiplierBase { get => _critMultiplierBase; protected set => _critMultiplierBase = value; }
    public float critMultiplierIncrementLvl { get => _critMultiplierIncrementLvl; protected set => _critMultiplierIncrementLvl = value; }
    public float critMultiplierIncrementUpgrade { get => _critMultiplierIncrementUpgrade; protected set => _critMultiplierIncrementUpgrade = value; }

    public List<int> synergies { get => _synergies; protected set => _synergies = value; }

    // 유닛 상태효과(버프/디버프)
    protected Dictionary<string, StatusEffect> statusEffects; // 이펙트 이름(식별자), 이펙트 효과. 동일 식별자는 중첩되지 않고 덮어씌워짐짐

    // 유닛 코드(스킬) 정보
    protected PassiveCode passiveCode;
    protected NormalCode normalCode;
    protected UltimateCode ultimateCode;
    public float normalCooldown;
    public float ultimateCooldown;

    // 이벤트
    private Dictionary<BaseEnums.UnitEventType, Delegate> eventDict;

    void Awake()
    {
        eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
    }

    public virtual void InitializeUnit(bool isEnemy, int id)
    {
        this.id = id;
        this.isEnemy = isEnemy;
        // 유닛 데이터가 없을 경우 바로 종료
        if (id == 0)
        {
            return;
        }
        LoadData(isEnemy, id);
        AttributesUpdate();
        hpCurr = hpMax;
        manaCurr = 0;
        AddListener<(Unit, DamageContext)>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
        AddListener<Unit>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
        AddListener<Unit>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
    }

    protected virtual void LoadData(bool isEnemy, int id)
    {
        UnitData data = GameManager.Instance.unitDataList.units.FirstOrDefault(e => e.id == id);
        LoadSprite(data.portrait, isEnemy);
        level = 0;
        unitName = data.name;
        synergies = data.synergies;
        hpBase = data.hpBase;
        hpIncrementLvl = data.hpIncrementLvl;
        hpIncrementUpgrade = data.hpIncrementUpgrade;
        atkBase = data.atkBase;
        atkIncrementLvl = data.atkIncrementLvl;
        atkIncrementUpgrade = data.atkIncrementUpgrade;
        defBase = data.defBase;
        defIncrementLvl = data.defIncrementLvl;
        defIncrementUpgrade = data.defIncrementUpgrade;
        critChanceBase = data.critChance;
        critChanceIncrementLvl = data.critChanceIncrementLvl;
        critChanceIncrementUpgrade = data.critChanceIncrementUpgrade;
        critMultiplierBase = data.critMultiplier;
        critMultiplierIncrementLvl = data.critMultiplierIncrementLvl;
        critMultiplierIncrementUpgrade = data.critMultiplierIncrementUpgrade;
        manaBase = data.manaBase;
        codeAcceleration = 1f;

        statusEffects = new Dictionary<string, StatusEffect>();
        isCasting = false;
        castingTime = 0f;
        isControlled = false;
        controlDuration = 0f;

        passiveCode = CodeFactory.CreatePassiveCode(data.codes["passive"], new PassiveCodeContext { caster = this });
        normalCode = CodeFactory.CreateNormalCode(data.codes["normal"], new NormalCodeContext { caster = this });
        ultimateCode = CodeFactory.CreateUltimateCode(data.codes["ultimate"], new UltimateCodeContext { caster = this });
        normalCooldown = normalCode.cooldown;
        ultimateCooldown = ultimateCode.cooldown;
    }

    protected virtual void LoadSprite(string name, bool isEnemy)
    {
        Sprite sprite = Resources.Load<Sprite>($"Sprite/Portraits/{name}");
        currentCell.portraitRenderer.sprite = sprite;
        // currentCell.portraitRenderer.flipX = isEnemy; // 카드면 미사용, 일러라면 적일 경우 x축 반전전
    }

    /// <summary>
    /// 유닛의 스탯 수치 업데이트<br/>
    /// 매 프레임마다 호출하기엔 무거운 것 같으니 상태 변화가 있을 때만 호출
    /// </summary>
    protected virtual void AttributesUpdate()
    {
        // 스탯 수정치 계산
        float hpMul = 0f;
        float hpAdd = 0f;
        float atkMul = 0f;
        float atkAdd = 0f;
        float defMul = 0f;
        float defAdd = 0f;
        float critChanceAdd = 0f;
        float critMultiplierAdd = 0f;
        float codeAccelMul = 0f;

        foreach (var effectPair in statusEffects)
        {
            hpMul += effectPair.Value.HpMultiplicativeModifier(this);
            hpAdd += effectPair.Value.HpAdditiveModifier(this);
            atkMul += effectPair.Value.AtkMultiplicativeModifier(this);
            atkAdd += effectPair.Value.AtkAdditiveModifier(this);
            defMul += effectPair.Value.DefMultiplicativeModifier(this);
            defAdd += effectPair.Value.DefAdditiveModifier(this);
            critChanceAdd += effectPair.Value.CritChanceAdditiveModifier(this);
            critMultiplierAdd += effectPair.Value.CritMultiplierAdditiveModifier(this);
            codeAccelMul += effectPair.Value.CodeAccelationMultiplicativeModifier(this);
        }

        // hp 비율 저장
        float healthRatio = (hpMax > 0) ? (float)hpCurr / hpMax : 1f;

        hpMax = Mathf.RoundToInt(GetBaseHp() * (1f + hpMul) + hpAdd);
        manaMax = manaBase;
        atkCurr = Mathf.RoundToInt(GetBaseAtk() * (1f + atkMul) + atkAdd);
        defCurr = Mathf.RoundToInt(GetBaseDef() * (1f + defMul) + defAdd);
        critChanceCurr = GetBaseCritChance() + critChanceAdd;
        critMultiplierCurr = GetBaseCritDamage() + critMultiplierAdd;
        codeAcceleration = 1f + codeAccelMul;

        // hp 비율 복구
        hpCurr = Mathf.RoundToInt(hpMax * healthRatio);
    }

    // 이벤트를 처리하는 메소드들
    /// <summary>
    /// 유닛 소환 이벤트
    /// </summary>
    /// <param name="cell">유닛이 위치한 셀</param>
    /// <param name="isEnemy">진영</param>
    /// <param name="id">유닛의 데이터상 id</param>
    public virtual void Spawn(Cell cell, bool isEnemy, int id)
    {
        ActivateUnit();
        InitializeUnit(isEnemy, id);
        Invoke(BaseEnums.UnitEventType.OnSpawn, this);
    }

    /// <summary>
    /// 유닛 사망 이벤트
    /// </summary>
    /// <param name="attacker">막타친 적 유닛(사망 시 이벤트 처리용)</param>
    public virtual void Die(Unit attacker)
    {
        Invoke(BaseEnums.UnitEventType.OnDeath, (this, attacker));
        DeactivateUnit();
    }

    /// <summary>
    /// 유닛 피해 처리 이벤트<br/>
    /// 다른 피해 처리 이벤트를 등록하지 않았을 경우 기본 피해 처리 이벤트(DefaultTakeDamageEvent) 호출
    /// </summary>
    /// <param name="context">피해 정보 컨텍스트</param>
    public virtual void TakeDamage(DamageContext context)
    {
        Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, (this, context.attacker));
        Invoke(BaseEnums.UnitEventType.OnTakingDamage, (this, context));
        Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, (this, context.attacker));
    }

    /// <summary>
    /// 유닛이 행동불능 상태가 되었을 때 호출되는 이벤트
    /// </summary>
    /// <param name="context">공격자와 지속시간을 지닌 제어 정보 컨텍스트</param>
    public virtual void ControlStarts(ControlContext context)
    {
        isControlled = true;
        controlDuration = context.duration;
        Invoke(BaseEnums.UnitEventType.OnControlStarts, (this, context));
    }

    /// <summary>
    /// 유닛이 행동불능 상태에서 벗어났을 때 호출되는 이벤트
    /// </summary>
    public virtual void ControlEnds()
    {
        isControlled = false;
        controlDuration = 0f;
        Invoke(BaseEnums.UnitEventType.OnControlEnds, this);
    }

    public virtual void CastPassiveCode()
    {
        passiveCode.CastCode();
        Invoke(BaseEnums.UnitEventType.OnPassiveActivates, this);
    }

    public virtual void CastNormalCode()
    {
        normalCode.CastCode();
        Invoke(BaseEnums.UnitEventType.OnNormalActivates, this);
    }

    public virtual void CastUltimateCode()
    {
        manaCurr = 0;
        currentCell.manaBarObj.transform.localScale = new Vector3(0f, 1f, 1f);
        ultimateCode.CastCode();
        Invoke(BaseEnums.UnitEventType.OnUltimateActivates, this);
    }

    public virtual void RecoverMana(int amount)
    {
        manaCurr += amount;
        if (manaCurr > manaMax)
        {
            manaCurr = manaMax;
        }
        currentCell.manaBarObj.transform.localScale = new Vector3((float)manaCurr / manaMax, 1f, 1f);
    }

    private void Update()
    {
        if (!isActive) return;
        if (GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress)
        {
            if (isControlled)
            {
                controlDuration -= Time.deltaTime;
                if (controlDuration <= 0f)
                {
                    ControlEnds();
                }
            }
            if (!isControlled && !isCasting)
            {
                if (ultimateCooldown <= 0f && ultimateCode.HasValidTarget() && manaCurr >= manaMax)
                {
                    CastUltimateCode();
                }
                else
                {
                    if (normalCooldown <= 0f && normalCode.HasValidTarget())
                    {
                        CastNormalCode();
                    }
                }
                ultimateCooldown = Mathf.Max(0f, ultimateCooldown - Time.deltaTime * codeAcceleration);
                normalCooldown = Mathf.Max(0f, normalCooldown - Time.deltaTime * codeAcceleration);
            }
        }
    }

    // 디폴트 이벤트 액션(이거 참고해서 다른 이벤트 만들어 넣으면 됨)

    /// <summary>
    /// 다른 피해 처리 이벤트를 등록하지 않았을 경우 기본 피해 처리 이벤트<br/>
    /// 피해량 = (1 + (방어력 - 관통력) * 0.01) * 피해량<br/>
    /// </summary>
    /// <param name="selfContext">(자신(Unit)과 피해 정보(TakeDamageContext)) 튜플</param>
    /// <remarks>
    /// selfContext.self = 피해를 받은 유닛(Unit) <br/>
    /// selfContext.context = 피해 정보 (TakeDamageContext)
    /// </remarks>
    protected void DefaultTakeDamageEvent((Unit self, DamageContext context) selfContext)
    {
        Unit self = selfContext.self;
        DamageContext context = selfContext.context;
        int effectiveDef = self.defCurr - context.penetration;
        float receivingDamageModifier = 1f;
        foreach (var effectPair in statusEffects)
        {
            receivingDamageModifier *= effectPair.Value.ReceivingDamageModifier(self);
        }
        int damageReceived = (int)(receivingDamageModifier * context.damage / (1 + effectiveDef * 0.01f));
        int hpBeforeHit = self.hpCurr;
        self.hpCurr -= damageReceived;
        currentCell.hpBarObj.transform.localScale = new Vector3((float)self.hpCurr / self.hpMax, 1f, 1f);
        Debug.Log($"{self.unitName}은(는) {context.attacker.unitName}에게 {damageReceived}의 {(context.isCrit ? "치명" : "")}피해를 받았습니다. 체력: {hpBeforeHit} -> {self.hpCurr}");
        if (self.hpCurr <= 0)
        {
            self.Die(context.attacker);
        }
    }

    /// <summary>
    /// 라운드 시작 처리 이벤트
    /// </summary>
    protected void DefaultRoundStartEvent(Unit self)
    {
        CastPassiveCode();
    }

    /// <summary>
    /// 라운드 종료 처리 이벤트
    /// </summary>
    protected void DefaultRoundEndEvent(Unit self)
    {
        statusEffects.Clear();
        AttributesUpdate();
    }

    // 베이스 스탯 수치 반환
    public virtual int GetBaseHp()
    {
        return hpBase + hpIncrementLvl * level + hpIncrementUpgrade * hpUpgrade;
    }

    public virtual int GetBaseAtk()
    {
        return atkBase + atkIncrementLvl * level + atkIncrementUpgrade * atkUpgrade;
    }

    public virtual int GetBaseDef()
    {
        return defBase + defIncrementLvl * level + defIncrementUpgrade * defUpgrade;
    }

    public virtual float GetBaseCritChance()
    {
        return critChanceBase + critChanceIncrementLvl * level + critChanceIncrementUpgrade * critChanceUpgrade;
    }

    public virtual float GetBaseCritDamage()
    {
        return critMultiplierBase + critMultiplierIncrementLvl * level + critMultiplierIncrementUpgrade * critMultiplierUpgrade;
    }

    // 유닛 활성화 상태 관리
    public void ActivateUnit()
    {
        isActive = true;
        currentCell.isOccupied = true;
        currentCell.hpBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = true;
        currentCell.hpBarObj.transform.localScale = Vector3.one;
        currentCell.manaBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = true;
        currentCell.manaBarObj.transform.localScale = new Vector3(0f, 1f, 1f);
    }

    public void DeactivateUnit()
    {
        isActive = false;
        id = 0;
        currentCell.isOccupied = false;
        currentCell.portraitRenderer.sprite = null;
        currentCell.hpBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = false;
        currentCell.manaBarObj.transform.Find("Bar Sprite").GetComponent<SpriteRenderer>().enabled = false;
        currentCell.reservedTime = 2f;
    }

    // 유닛 상태효과 관리
    public void AddStatusEffect(string identifier, StatusEffect effect)
    {
        // 같은 버프여도 서로 다른 유닛이 부여하면 중첩가능
        Debug.Log($"{unitName}에게 {identifier} 상태효과 부여");
        if (statusEffects.ContainsKey(identifier))
        {
            statusEffects[identifier] = effect;
        }
        else
        {
            statusEffects.Add(identifier, effect);
        }
        AttributesUpdate();
    }

    public void RemoveStatusEffect(string identifier)
    {
        Debug.Log($"{unitName}에게서 {identifier} 상태효과 제거됨");
        if (statusEffects.ContainsKey(identifier))
        {
            statusEffects.Remove(identifier);
        }
        AttributesUpdate();
    }

    // 이벤트 리스너 관리
    public void AddListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
    {
        if (!eventDict.ContainsKey(eventType))
        {
            RemoveAllListners(eventType);
        }
        eventDict[eventType] = (Action<T>)eventDict[eventType] + action;
    }

    public void RemoveListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
    {
        if (eventDict.ContainsKey(eventType))
        {
            eventDict[eventType] = (Action<T>)eventDict[eventType] - action;
        }
    }

    public void RemoveAllListners(BaseEnums.UnitEventType eventType)
    {
        if (eventDict.ContainsKey(eventType))
        {
            eventDict[eventType] = null;
        }
        else
        {
            eventDict.Add(eventType, null);
        }
    }

    public void Invoke<T>(BaseEnums.UnitEventType eventType, T context)
    {
        if (eventDict.ContainsKey(eventType))
        {
            ((Action<T>)eventDict[eventType])?.Invoke(context);
        }
    }
}
