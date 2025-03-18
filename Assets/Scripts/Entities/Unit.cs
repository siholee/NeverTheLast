using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Managers;
using StatusEffects.Base;
using UnityEngine;
using UnityEngine.Serialization;

namespace Entities
{
    public class Unit : MonoBehaviour
    {
        public bool isActive = false;

        // 기본 식별정보
        [SerializeField] private int id;
        [SerializeField] private bool isEnemy;
        [SerializeField] private string unitName;
        [SerializeField] private int level;
        public int ID { get => id; protected set => id = value; }
        public bool IsEnemy { get => isEnemy; protected set => isEnemy = value; }
        public string UnitName { get => unitName; protected set => unitName = value; }
        public int Level { get => level; protected set => level = value; }

        public Cell currentCell; // 위치중인 셀

        // 유닛 강화 횟수
        [SerializeField] private int hpUpgrade;
        [SerializeField] private int atkUpgrade;
        [SerializeField] private int defUpgrade;
        [SerializeField] private int critChanceUpgrade;
        [SerializeField] private int critMultiplierUpgrade;
        public int HpUpgrade { get => hpUpgrade; protected set => hpUpgrade = value; }
        public int AtkUpgrade { get => atkUpgrade; protected set => atkUpgrade = value; }
        public int DefUpgrade { get => defUpgrade; protected set => defUpgrade = value; }
        public int CritChanceUpgrade { get => critChanceUpgrade; protected set => critChanceUpgrade = value; }
        public int CritMultiplierUpgrade { get => critMultiplierUpgrade; protected set => critMultiplierUpgrade = value; }

        // 유닛 현재 상태
        [SerializeField] private int hpCurr;
        [SerializeField] private int manaCurr;
        [SerializeField] private int atkCurr;
        [SerializeField] private int defCurr;
        [SerializeField] private float critChanceCurr;
        [SerializeField] private float critDamageCurr;
        [SerializeField] private float codeAcceleration;
        public int HpCurr { get => hpCurr; protected set => hpCurr = value; }
        public int ManaCurr { get => manaCurr; protected set => manaCurr = value; }
        public int AtkCurr { get => atkCurr; protected set => atkCurr = value; }
        public int DefCurr { get => defCurr; protected set => defCurr = value; }
        public float CritChanceCurr { get => critChanceCurr; protected set => critChanceCurr = value; }
        public float CritMultiplierCurr { get => critDamageCurr; protected set => critDamageCurr = value; }
        public float CodeAcceleration { get => codeAcceleration; protected set => codeAcceleration = value; }

        public bool isCasting; // 스킬 시전중
        public float castingTime;
        public bool isControlled; // 행동 불가 상태
        public float controlDuration;

        // 유닛 스탯 수치
        // 인스펙터 노출용용
        [SerializeField] private int hpBase;
        [SerializeField] private int hpIncrementLvl;
        [SerializeField] private int hpIncrementUpgrade;
        [SerializeField] private int hpMax;
        [SerializeField] private int manaBase;
        [SerializeField] private int manaMax;
        [SerializeField] private int atkBase;
        [SerializeField] private int atkIncrementLvl;
        [SerializeField] private int atkIncrementUpgrade;
        [SerializeField] private int defBase;
        [SerializeField] private int defIncrementLvl;
        [SerializeField] private int defIncrementUpgrade;
        [SerializeField] private float critChanceBase;
        [SerializeField] private float critChanceIncrementLvl;
        [SerializeField] private float critChanceIncrementUpgrade;
        [SerializeField] private float critMultiplierBase;
        [SerializeField] private float critMultiplierIncrementLvl;
        [SerializeField] private float critMultiplierIncrementUpgrade;
        [SerializeField] private List<int> synergies;

        // 실제 수치 관리용
        public int HpBase { get => hpBase; protected set => hpBase = value; }
        public int HpIncrementLvl { get => hpIncrementLvl; protected set => hpIncrementLvl = value; }
        public int HpIncrementUpgrade { get => hpIncrementUpgrade; protected set => hpIncrementUpgrade = value; }
        public int HpMax { get => hpMax; protected set => hpMax = value; }
        public int ManaBase { get => manaBase; protected set => manaBase = value; }
        public int ManaMax { get => manaMax; protected set => manaMax = value; }
        public int AtkBase { get => atkBase; protected set => atkBase = value; }
        public int AtkIncrementLvl { get => atkIncrementLvl; protected set => atkIncrementLvl = value; }
        public int AtkIncrementUpgrade { get => atkIncrementUpgrade; protected set => atkIncrementUpgrade = value; }
        public int DefBase { get => defBase; protected set => defBase = value; }
        public int DefIncrementLvl { get => defIncrementLvl; protected set => defIncrementLvl = value; }
        public int DefIncrementUpgrade { get => defIncrementUpgrade; protected set => defIncrementUpgrade = value; }
        public float CritChanceBase { get => critChanceBase; protected set => critChanceBase = value; }
        public float CritChanceIncrementLvl { get => critChanceIncrementLvl; protected set => critChanceIncrementLvl = value; }
        public float CritChanceIncrementUpgrade { get => critChanceIncrementUpgrade; protected set => critChanceIncrementUpgrade = value; }
        public float CritMultiplierBase { get => critMultiplierBase; protected set => critMultiplierBase = value; }
        public float CritMultiplierIncrementLvl { get => critMultiplierIncrementLvl; protected set => critMultiplierIncrementLvl = value; }
        public float CritMultiplierIncrementUpgrade { get => critMultiplierIncrementUpgrade; protected set => critMultiplierIncrementUpgrade = value; }

        public List<int> Synergies { get => synergies; protected set => synergies = value; }

        // 유닛 상태효과(버프/디버프)
        protected Dictionary<string, StatusEffect> StatusEffects; // 이펙트 이름(식별자), 이펙트 효과. 동일 식별자는 중첩되지 않고 덮어씌워짐짐
        protected Dictionary<int, SynergyEffect> SynergyEffects;

        // 유닛 코드(스킬) 정보
        protected PassiveCode PassiveCode;
        protected NormalCode NormalCode;
        protected UltimateCode UltimateCode;
        public float normalCooldown;
        public float ultimateCooldown;

        // 이벤트
        private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict;

        [FormerlySerializedAs("portraitPath")] public string PortraitPath;

        void Awake()
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            SynergyEffects = new Dictionary<int, SynergyEffect>();
        }

        public virtual void InitializeUnit(bool _isEnemy, int _id)
        {
            ID = _id;
            IsEnemy = _isEnemy;
            // 유닛 데이터가 없을 경우 바로 종료
            if (_id == 0)
            {
                return;
            }
            LoadData(_isEnemy, _id);
            AttributesUpdate();
            HpCurr = HpMax;
            ManaCurr = 0;
            AddListener<(Unit, DamageContext)>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
            AddListener<Unit>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
            AddListener<Unit>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
        }

        protected virtual void LoadData(bool _isEnemy, int _id)
        {
            UnitData data = GameManager.Instance.unitDataList.units.FirstOrDefault(e => e.id == _id);
            LoadSprite(data.portrait, _isEnemy);
            Level = 0;
            UnitName = data.name;
            Synergies = data.synergies;
            HpBase = data.hpBase;
            HpIncrementLvl = data.hpIncrementLvl;
            HpIncrementUpgrade = data.hpIncrementUpgrade;
            AtkBase = data.atkBase;
            AtkIncrementLvl = data.atkIncrementLvl;
            AtkIncrementUpgrade = data.atkIncrementUpgrade;
            DefBase = data.defBase;
            DefIncrementLvl = data.defIncrementLvl;
            DefIncrementUpgrade = data.defIncrementUpgrade;
            CritChanceBase = data.critChance;
            CritChanceIncrementLvl = data.critChanceIncrementLvl;
            CritChanceIncrementUpgrade = data.critChanceIncrementUpgrade;
            CritMultiplierBase = data.critMultiplier;
            CritMultiplierIncrementLvl = data.critMultiplierIncrementLvl;
            CritMultiplierIncrementUpgrade = data.critMultiplierIncrementUpgrade;
            ManaBase = data.manaBase;
            CodeAcceleration = 1f;

            StatusEffects = new Dictionary<string, StatusEffect>();
            isCasting = false;
            castingTime = 0f;
            isControlled = false;
            controlDuration = 0f;

            PassiveCode = CodeFactory.CreatePassiveCode(data.codes["passive"], new PassiveCodeContext { Caster = this });
            NormalCode = CodeFactory.CreateNormalCode(data.codes["normal"], new NormalCodeContext { Caster = this });
            UltimateCode = CodeFactory.CreateUltimateCode(data.codes["ultimate"], new UltimateCodeContext { Caster = this });
            normalCooldown = NormalCode.Cooldown;
            ultimateCooldown = UltimateCode.Cooldown;
        }

        protected virtual void LoadSprite(string _name, bool _isEnemy)
        {
            string path = $"Sprite/Portraits/{_name}";
            PortraitPath = path;
            Sprite sprite = Resources.Load<Sprite>(path);
            currentCell.portraitRenderer.sprite = sprite;
            // currentCell.portraitRenderer.flipX = isEnemy; // 카드면 미사용, 일러라면 적일 경우 x축 반전
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

            foreach (var effectPair in StatusEffects)
            {
                hpMul += effectPair.Value.HpMultiplicativeModifier(this);
                hpAdd += effectPair.Value.HpAdditiveModifier(this);
                atkMul += effectPair.Value.AtkMultiplicativeModifier(this);
                atkAdd += effectPair.Value.AtkAdditiveModifier(this);
                defMul += effectPair.Value.DefMultiplicativeModifier(this);
                defAdd += effectPair.Value.DefAdditiveModifier(this);
                critChanceAdd += effectPair.Value.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effectPair.Value.CritMultiplierAdditiveModifier(this);
                codeAccelMul += effectPair.Value.CodeAccelerationMultiplicativeModifier(this);
            }
            foreach (var effectPair in SynergyEffects)
            {
                hpMul += effectPair.Value.HpMultiplicativeModifier(this);
                hpAdd += effectPair.Value.HpAdditiveModifier(this);
                atkMul += effectPair.Value.AtkMultiplicativeModifier(this);
                atkAdd += effectPair.Value.AtkAdditiveModifier(this);
                defMul += effectPair.Value.DefMultiplicativeModifier(this);
                defAdd += effectPair.Value.DefAdditiveModifier(this);
                critChanceAdd += effectPair.Value.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effectPair.Value.CritMultiplierAdditiveModifier(this);
                codeAccelMul += effectPair.Value.CodeAccelerationMultiplicativeModifier(this);
            }

            // hp 비율 저장
            float healthRatio = (HpMax > 0) ? (float)HpCurr / HpMax : 1f;

            HpMax = Mathf.RoundToInt(GetBaseHp() * (1f + hpMul) + hpAdd);
            ManaMax = ManaBase;
            AtkCurr = Mathf.RoundToInt(GetBaseAtk() * (1f + atkMul) + atkAdd);
            DefCurr = Mathf.RoundToInt(GetBaseDef() * (1f + defMul) + defAdd);
            CritChanceCurr = GetBaseCritChance() + critChanceAdd;
            CritMultiplierCurr = GetBaseCritDamage() + critMultiplierAdd;
            CodeAcceleration = 1f + codeAccelMul;

            // hp 비율 복구
            HpCurr = Mathf.RoundToInt(HpMax * healthRatio);
        }

        // 이벤트를 처리하는 메소드들
        /// <summary>
        /// 유닛 소환 이벤트
        /// </summary>
        /// <param name="cell">유닛이 위치한 셀</param>
        /// <param name="_isEnemy">진영</param>
        /// <param name="_id">유닛의 데이터상 id</param>
        public virtual void Spawn(Cell cell, bool _isEnemy, int _id)
        {
            ActivateUnit();
            InitializeUnit(_isEnemy, _id);
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
            Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, (this, context.Attacker));
            Invoke(BaseEnums.UnitEventType.OnTakingDamage, (this, context));
            Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, (this, context.Attacker));
        }

        /// <summary>
        /// 유닛이 행동불능 상태가 되었을 때 호출되는 이벤트
        /// </summary>
        /// <param name="context">공격자와 지속시간을 지닌 제어 정보 컨텍스트</param>
        public virtual void ControlStarts(ControlContext context)
        {
            isControlled = true;
            controlDuration = context.Duration;
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
            PassiveCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnPassiveActivates, this);
        }

        public virtual void CastNormalCode()
        {
            NormalCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, this);
        }

        public virtual void CastUltimateCode()
        {
            ManaCurr = 0;
            currentCell.manaBarObj.transform.localScale = new Vector3(0f, 1f, 1f);
            UltimateCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnUltimateActivates, this);
        }

        public virtual void RecoverMana(int amount)
        {
            ManaCurr += amount;
            if (ManaCurr > ManaMax)
            {
                ManaCurr = ManaMax;
            }
            currentCell.manaBarObj.transform.localScale = new Vector3((float)ManaCurr / ManaMax, 1f, 1f);
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
                    if (ultimateCooldown <= 0f && UltimateCode.HasValidTarget() && ManaCurr >= ManaMax)
                    {
                        CastUltimateCode();
                    }
                    else
                    {
                        if (normalCooldown <= 0f && NormalCode.HasValidTarget())
                        {
                            CastNormalCode();
                        }
                    }
                    ultimateCooldown = Mathf.Max(0f, ultimateCooldown - Time.deltaTime * CodeAcceleration);
                    normalCooldown = Mathf.Max(0f, normalCooldown - Time.deltaTime * CodeAcceleration);
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
            int effectiveDef = self.DefCurr - context.Penetration;
            float receivingDamageModifier = 1f;
            foreach (var effectPair in StatusEffects)
            {
                receivingDamageModifier *= effectPair.Value.ReceivingDamageModifier(self);
            }
            int damageReceived = (int)(receivingDamageModifier * context.Damage / (1 + effectiveDef * 0.01f));
            int hpBeforeHit = self.HpCurr;
            self.HpCurr -= damageReceived;
            currentCell.hpBarObj.transform.localScale = new Vector3((float)self.HpCurr / self.HpMax, 1f, 1f);
            Debug.Log($"{self.UnitName}은(는) {context.Attacker.UnitName}에게 {damageReceived}의 {(context.IsCrit ? "치명" : "")}피해를 받았습니다. 체력: {hpBeforeHit} -> {self.HpCurr}");
            if (self.HpCurr <= 0)
            {
                self.Die(context.Attacker);
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
            StatusEffects.Clear();
            AttributesUpdate();
        }

        // 베이스 스탯 수치 반환
        public virtual int GetBaseHp()
        {
            return HpBase + HpIncrementLvl * Level + HpIncrementUpgrade * HpUpgrade;
        }

        public virtual int GetBaseAtk()
        {
            return AtkBase + AtkIncrementLvl * Level + AtkIncrementUpgrade * AtkUpgrade;
        }

        public virtual int GetBaseDef()
        {
            return DefBase + DefIncrementLvl * Level + DefIncrementUpgrade * DefUpgrade;
        }

        public virtual float GetBaseCritChance()
        {
            return CritChanceBase + CritChanceIncrementLvl * Level + CritChanceIncrementUpgrade * CritChanceUpgrade;
        }

        public virtual float GetBaseCritDamage()
        {
            return CritMultiplierBase + CritMultiplierIncrementLvl * Level + CritMultiplierIncrementUpgrade * CritMultiplierUpgrade;
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
            ID = 0;
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
            Debug.Log($"{UnitName}에게 {identifier} 상태효과 부여");
            if (StatusEffects.ContainsKey(identifier))
            {
                StatusEffects[identifier] = effect;
            }
            else
            {
                StatusEffects.Add(identifier, effect);
            }
            AttributesUpdate();
        }

        public void RemoveStatusEffect(string identifier)
        {
            Debug.Log($"{UnitName}에게서 {identifier} 상태효과 제거됨");
            if (StatusEffects.ContainsKey(identifier))
            {
                StatusEffects.Remove(identifier);
            }
            AttributesUpdate();
        }
        
        public void SetSynergyEffect(int synergyId, SynergyEffect effect)
        {
            if (effect.Stack == 0)
            {
                SynergyEffects.Remove(synergyId);
            }
            else
            {
                SynergyEffects[synergyId] = effect;
                Debug.Log($"{UnitName}에게 {effect.SynergyName} {effect.Stack} 시너지 적용");
            }
        }

        // 이벤트 리스너 관리
        public void AddListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (!_eventDict.ContainsKey(eventType))
            {
                RemoveAllListeners(eventType);
            }
            _eventDict[eventType] = (Action<T>)_eventDict[eventType] + action;
        }

        public void RemoveListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (_eventDict.ContainsKey(eventType))
            {
                _eventDict[eventType] = (Action<T>)_eventDict[eventType] - action;
            }
        }

        public void RemoveAllListeners(BaseEnums.UnitEventType eventType)
        {
            if (_eventDict.ContainsKey(eventType))
            {
                _eventDict[eventType] = null;
            }
            else
            {
                _eventDict.Add(eventType, null);
            }
        }

        public void Invoke<T>(BaseEnums.UnitEventType eventType, T context)
        {
            if (_eventDict.TryGetValue(eventType, out var value))
            {
                ((Action<T>)value)?.Invoke(context);
            }
        }
    }
}
