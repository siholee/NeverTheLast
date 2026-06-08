using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Managers;
using StatusEffects.Base;
using UnityEngine;

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

        // ── 기본 스탯 (STR / DEX / CON / INT / LUK) ────────────────────────────
        [SerializeField] private int strStat;
        [SerializeField] private int dexStat;
        [SerializeField] private int conStat;
        [SerializeField] private int intStat;
        [SerializeField] private int lukStat;

        public int StrStat { get => strStat; protected set => strStat = value; }
        public int DexStat { get => dexStat; protected set => dexStat = value; }
        public int ConStat { get => conStat; protected set => conStat = value; }
        public int IntStat { get => intStat; protected set => intStat = value; }
        public int LukStat { get => lukStat; protected set => lukStat = value; }

        // ── 파생 스탯 (현재 상태) ────────────────────────────────────────────────
        [SerializeField] private int hpCurr;
        [SerializeField] private int hpMax;
        [SerializeField] private int manaCurr;
        [SerializeField] private int manaMax;
        [SerializeField] private int atkCurr;
        [SerializeField] private int defCurr;
        [SerializeField] private float critChanceCurr;
        [SerializeField] private float critDamageCurr;
        [SerializeField] private float speedCurr;

        public int HpCurr { get => hpCurr; protected set => hpCurr = value; }
        public int HpMax { get => hpMax; protected set => hpMax = value; }
        public int ManaCurr { get => manaCurr; protected set => manaCurr = value; }
        public int ManaMax { get => manaMax; protected set => manaMax = value; }
        public int AtkCurr { get => atkCurr; protected set => atkCurr = value; }
        public int DefCurr { get => defCurr; protected set => defCurr = value; }
        public float CritChanceCurr { get => critChanceCurr; protected set => critChanceCurr = value; }
        public float CritMultiplierCurr { get => critDamageCurr; protected set => critDamageCurr = value; }
        public float SpeedCurr { get => speedCurr; protected set => speedCurr = value; }

        public bool isCasting;    // 스킬 시전 중
        public bool isControlled; // 행동 불가 상태
        public int controlTurns;  // 제어 남은 턴 수

        [SerializeField] private int manaBase; // 궁극기 에너지 최대치 (YAML에서 로드)
        public int ManaBase { get => manaBase; protected set => manaBase = value; }

        [SerializeField] private List<int> synergies;
        public List<int> Synergies { get => synergies; protected set => synergies = value; }

        // ── 패시브 플래그 (영구 효과) ──────────────────────────────────────────
        /// <summary>영구 패시브 효과 플래그 집합. PassiveCode.CastCode()에서 설정됨.</summary>
        public HashSet<string> PermanentPassiveFlags { get; } = new HashSet<string>();

        /// <summary>
        /// CON을 계수로 사용하는 스킬의 실질 CON 수치.<br/>
        /// P1 패시브(SeiP1) 활성 시 CON + INT를 반환.
        /// </summary>
        public int GetEffectiveCon()
            => PermanentPassiveFlags.Contains("SeiP1") ? ConStat + IntStat : ConStat;

        // ── 인벤토리 (Phase 4) ────────────────────────────────────────────────
        public Inventory Inventory { get; private set; }

        // ── 원소 시스템 ───────────────────────────────────────────────────────
        public BaseEnums.ElementType NativeElement { get; protected set; } = BaseEnums.ElementType.Physical;
        public BaseEnums.ElementType CurrentAura { get; set; } = BaseEnums.ElementType.Physical;

        // ── 주목도 & 그리드 라인 ──────────────────────────────────────────────
        public int ThreatLevelBase { get; set; } = 1;
        public BaseEnums.GridLine CurrentLine { get; set; } = BaseEnums.GridLine.Frontline;
        public int EffectiveThreat => ThreatLevelBase + (CurrentLine == BaseEnums.GridLine.Frontline ? 1 : 0);

        // 상태효과
        protected Dictionary<string, StatusEffect> StatusEffects;
        protected Dictionary<int, SynergyEffect> SynergyEffects;

        // ── 코드 (스킬) ──────────────────────────────────────────────────────
        public PassiveCode PassiveCode;
        /// <summary>기본 공격 코드 (SP 생성; 마력탄 등). Basic 행동 시 실행.</summary>
        public NormalCode BasicCode;
        /// <summary>클래스 스킬 코드 (클래스 고유 행동; 명상 등). YAML class_skill 슬롯.</summary>
        public NormalCode ClassCode;
        /// <summary>스킬 코드 (SP 소모; 암석포 등). Normal 행동 시 실행.</summary>
        public NormalCode NormalCode;
        public UltimateCode UltimateCode;

        // 이벤트
        private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict;

        public string PortraitPath;

        void Awake()
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            SynergyEffects = new Dictionary<int, SynergyEffect>();
        }

        public virtual void InitializeUnit(bool _isEnemy, int _id)
        {
            ID = _id;
            IsEnemy = _isEnemy;
            if (_id == 0) return;

            Inventory = new Inventory(this);
            LoadData(_isEnemy, _id);
            CurrentAura = BaseEnums.ElementType.Physical;
            AttributesUpdate();
            HpCurr = HpMax;
            ManaCurr = 0;

            AddListener<(Unit, DamageContext)>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
            AddListener<Unit>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
            AddListener<Unit>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
        }

        protected virtual void LoadData(bool _isEnemy, int _id)
        {
            var data = GameManager.Instance.unitDataList.units.FirstOrDefault(e => e.id == _id);
            LoadSprite(data.portrait, _isEnemy);
            Level = 1;
            UnitName = data.name;

            // 원소 속성 파싱
            NativeElement = System.Enum.TryParse<BaseEnums.ElementType>(data.element, true, out var parsedElement)
                ? parsedElement : BaseEnums.ElementType.Physical;

            Synergies = data.synergies ?? new List<int>();

            // ── 새 스탯 시스템 ──────────────────────────────────────────────
            StrStat = data.str;
            DexStat = data.dex;
            ConStat = data.con;
            IntStat = data.intel;
            LukStat = data.luk;
            ManaBase = data.manaBase;

            StatusEffects = new Dictionary<string, StatusEffect>();
            isCasting = false;
            isControlled = false;
            controlTurns = 0;

            // 코드 생성 (codes 딕셔너리에서 basic/normal/ultimate/passive 키 조회)
            int passiveId     = data.codes.TryGetValue("passive",     out var p)  ? p  : 0;
            int basicId       = data.codes.TryGetValue("basic",       out var b)  ? b  : 0;
            int classSkillId  = data.codes.TryGetValue("class_skill", out var cs) ? cs : 0;
            int normalId      = data.codes.TryGetValue("normal",      out var n)  ? n  : 0;
            int ultimateId    = data.codes.TryGetValue("ultimate",    out var u)  ? u  : 0;

            PassiveCode  = CodeFactory.CreatePassiveCode  (passiveId,    new PassiveCodeContext  { Caster = this });
            BasicCode    = CodeFactory.CreateBasicCode    (basicId,      new NormalCodeContext   { Caster = this });
            ClassCode    = CodeFactory.CreateClassCode    (classSkillId, new NormalCodeContext   { Caster = this });
            NormalCode   = CodeFactory.CreateNormalCode   (normalId,     new NormalCodeContext   { Caster = this });
            UltimateCode = CodeFactory.CreateUltimateCode (ultimateId,   new UltimateCodeContext { Caster = this });
        }

        protected virtual void LoadSprite(string _name, bool _isEnemy)
        {
            string path = $"Sprite/Portraits/{_name}";
            PortraitPath = path;
            Sprite sprite = Resources.Load<Sprite>(path);
            currentCell.portraitRenderer.sprite = sprite;
        }

        // ── 파생 스탯 공식 ──────────────────────────────────────────────────────
        /// <summary>HP = CON*300 + 500</summary>
        public virtual int GetBaseHp()    => ConStat * 300 + 500;
        /// <summary>ATK = INT*20 + STR*20 (범용 공격력; 스킬은 INT/CON 직접 참조)</summary>
        public virtual int GetBaseAtk()   => IntStat * 20 + StrStat * 20;
        /// <summary>DEF = CON*10 + STR*5</summary>
        public virtual int GetBaseDef()   => ConStat * 10 + StrStat * 5;
        /// <summary>CritChance = LUK * 0.05 (5% per LUK)</summary>
        public virtual float GetBaseCritChance()  => LukStat * 0.05f;
        /// <summary>CritMultiplier = 1.5 + LUK * 0.05</summary>
        public virtual float GetBaseCritDamage()  => 1.5f + LukStat * 0.05f;
        /// <summary>Speed = DEX*10 + 60</summary>
        public virtual float GetBaseSpeed() => DexStat * 10f + 60f;

        /// <summary>
        /// 스탯 수치 갱신. 상태효과·시너지·인벤토리 보너스를 반영 후 파생 스탯 재계산.
        /// </summary>
        public virtual void AttributesUpdate()
        {
            float hpMul = 0f, hpAdd = 0f;
            float atkMul = 0f, atkAdd = 0f;
            float defMul = 0f, defAdd = 0f;
            float critChanceAdd = 0f, critMultiplierAdd = 0f;
            float speedMul = 0f, speedAdd = 0f;

            foreach (var effectPair in StatusEffects)
            {
                hpMul   += effectPair.Value.HpMultiplicativeModifier(this);
                hpAdd   += effectPair.Value.HpAdditiveModifier(this);
                atkMul  += effectPair.Value.AtkMultiplicativeModifier(this);
                atkAdd  += effectPair.Value.AtkAdditiveModifier(this);
                defMul  += effectPair.Value.DefMultiplicativeModifier(this);
                defAdd  += effectPair.Value.DefAdditiveModifier(this);
                critChanceAdd     += effectPair.Value.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effectPair.Value.CritMultiplierAdditiveModifier(this);
                speedMul += effectPair.Value.SpeedMultiplicativeModifier(this);
                speedAdd += effectPair.Value.SpeedAdditiveModifier(this);
            }
            foreach (var effectPair in SynergyEffects)
            {
                hpMul   += effectPair.Value.HpMultiplicativeModifier(this);
                hpAdd   += effectPair.Value.HpAdditiveModifier(this);
                atkMul  += effectPair.Value.AtkMultiplicativeModifier(this);
                atkAdd  += effectPair.Value.AtkAdditiveModifier(this);
                defMul  += effectPair.Value.DefMultiplicativeModifier(this);
                defAdd  += effectPair.Value.DefAdditiveModifier(this);
                critChanceAdd     += effectPair.Value.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effectPair.Value.CritMultiplierAdditiveModifier(this);
                speedMul += effectPair.Value.SpeedMultiplicativeModifier(this);
                speedAdd += effectPair.Value.SpeedAdditiveModifier(this);
            }

            // 인벤토리 장비 보너스
            int   invAtk  = Inventory != null ? Inventory.TotalAtkBonus  : 0;
            int   invDef  = Inventory != null ? Inventory.TotalDefBonus  : 0;
            int   invHp   = Inventory != null ? Inventory.TotalHpBonus   : 0;
            float invCrit = Inventory != null ? Inventory.TotalCritBonus : 0f;
            float invSpd  = Inventory != null ? Inventory.TotalSpdBonus  : 0f;

            float healthRatio = (HpMax > 0) ? (float)HpCurr / HpMax : 1f;

            HpMax  = Mathf.RoundToInt(GetBaseHp() * (1f + hpMul) + hpAdd) + invHp;
            ManaMax = ManaBase;
            AtkCurr = Mathf.RoundToInt(GetBaseAtk() * (1f + atkMul) + atkAdd) + invAtk;
            DefCurr = Mathf.RoundToInt(GetBaseDef() * (1f + defMul) + defAdd) + invDef;
            CritChanceCurr    = GetBaseCritChance() + critChanceAdd + invCrit;
            CritMultiplierCurr = GetBaseCritDamage() + critMultiplierAdd;

            float oldSpeed = SpeedCurr;
            SpeedCurr = GetBaseSpeed() * (1f + speedMul) + speedAdd + invSpd;

            if (Mathf.Abs(oldSpeed - SpeedCurr) > 0.001f && Managers.BattleManager.Instance != null)
                Managers.BattleManager.Instance.OnSpeedChanged(this, oldSpeed, SpeedCurr);

            HpCurr = Mathf.RoundToInt(HpMax * healthRatio);
        }

        // ── 이벤트 처리 메소드 ────────────────────────────────────────────────

        public virtual void Spawn(Cell cell, bool _isEnemy, int _id)
        {
            ActivateUnit();
            InitializeUnit(_isEnemy, _id);
            Invoke(BaseEnums.UnitEventType.OnSpawn, this);
        }

        public virtual void Die(Unit attacker)
        {
            Invoke(BaseEnums.UnitEventType.OnDeath, (this, attacker));
            if (Managers.BattleManager.Instance != null)
                Managers.BattleManager.Instance.UnregisterUnit(this);
            DeactivateUnit();
        }

        public virtual void TakeDamage(DamageContext context)
        {
            Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, (this, context.Attacker));
            Invoke(BaseEnums.UnitEventType.OnTakingDamage, (this, context));
            Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, (this, context.Attacker));
        }

        public virtual void ControlStarts(ControlContext context)
        {
            isControlled = true;
            controlTurns = context.TurnDuration;
            Invoke(BaseEnums.UnitEventType.OnControlStarts, (this, context));
        }

        public virtual void ControlEnds()
        {
            isControlled = false;
            controlTurns = 0;
            Invoke(BaseEnums.UnitEventType.OnControlEnds, this);
        }

        public virtual void CastPassiveCode()
        {
            PassiveCode?.CastCode();
            Invoke(BaseEnums.UnitEventType.OnPassiveActivates, this);
        }

        /// <summary>클래스 스킬 실행 (ClassCode). ClassSkill 행동 시 호출.</summary>
        public virtual void CastClassCode()
        {
            ClassCode?.CastCode();
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, this);
        }

        /// <summary>기본 공격 실행 (BasicCode). Basic 행동 시 SP 생성과 함께 호출.</summary>
        public virtual void CastBasicCode()
        {
            if (BasicCode != null)
                BasicCode.CastCode();
            else
                NormalCode?.CastCode(); // BasicCode 없으면 NormalCode로 폴백
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, this);
        }

        public virtual void CastNormalCode()
        {
            NormalCode?.CastCode();
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, this);
        }

        public virtual void CastUltimateCode()
        {
            ManaCurr = 0;
            currentCell.manaBarObj.transform.localScale = new Vector3(0f, 1f, 1f);
            UltimateCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnUltimateActivates, this);
        }

        /// <summary>HP를 직접 설정 (RunManager 세이브 복원, RewardManager 회복용).</summary>
        public void ModifyHp(int newHp)
        {
            HpCurr = Mathf.Clamp(newHp, 0, HpMax);
            if (currentCell?.hpBarObj != null)
                currentCell.hpBarObj.transform.localScale =
                    new Vector3((float)HpCurr / HpMax, 1f, 1f);
        }

        public virtual void RecoverMana(int amount)
        {
            ManaCurr = Mathf.Min(ManaCurr + amount, ManaMax);
            currentCell.manaBarObj.transform.localScale = new Vector3((float)ManaCurr / ManaMax, 1f, 1f);
        }

        // ── 디폴트 이벤트 핸들러 ───────────────────────────────────────────────

        protected void DefaultTakeDamageEvent((Unit self, DamageContext context) selfContext)
        {
            Unit self = selfContext.self;
            DamageContext context = selfContext.context;
            int effectiveDef = self.DefCurr - context.Penetration;
            float receivingDamageModifier = 1f;
            foreach (var effectPair in StatusEffects)
                receivingDamageModifier *= effectPair.Value.ReceivingDamageModifier(self);

            float reactionMult = 1f;
            if (context.Element != BaseEnums.ElementType.Physical
                && Managers.BattleManager.Instance?.ReactionHandler != null)
            {
                reactionMult = Managers.BattleManager.Instance.ReactionHandler
                    .HandleReaction(context.Attacker, self, context.Element, context);
            }

            int damageReceived = (int)(receivingDamageModifier * reactionMult * context.Damage / (1 + effectiveDef * 0.01f));
            int hpBeforeHit = self.HpCurr;
            self.HpCurr -= damageReceived;
            currentCell.hpBarObj.transform.localScale = new Vector3((float)self.HpCurr / self.HpMax, 1f, 1f);
            Debug.Log($"{self.UnitName}은(는) {context.Attacker.UnitName}에게 {damageReceived}의 {(context.IsCrit ? "치명" : "")}{(reactionMult > 1f ? "반응+" : "")}피해를 받았습니다. 체력: {hpBeforeHit} → {self.HpCurr}");
            if (self.HpCurr <= 0)
                self.Die(context.Attacker);
        }

        protected void DefaultRoundStartEvent(Unit self)
        {
            CastPassiveCode();
        }

        /// <summary>Phase 5: PersistsAcrossRounds = true인 효과는 유지.</summary>
        protected void DefaultRoundEndEvent(Unit self)
        {
            var keysToRemove = new List<string>();
            foreach (var pair in StatusEffects)
            {
                if (!pair.Value.PersistsAcrossRounds)
                    keysToRemove.Add(pair.Key);
            }
            foreach (var key in keysToRemove)
                StatusEffects.Remove(key);
            AttributesUpdate();
        }

        // ── 런 보너스 ─────────────────────────────────────────────────────────
        /// <summary>
        /// 런 중 획득한 보너스를 기본 스탯에 추가 후 AttributesUpdate() 호출.<br/>
        /// 매개변수는 기존 RewardManager 호환성을 위해 유지되며, 내부적으로 새 스탯에 매핑됨.
        /// </summary>
        public void AddRunBonus(int atkBase = 0, int defBase = 0, float critChanceBase = 0f,
                                float speedBase = 0f, int hpBase = 0)
        {
            // INT +1 per 20 ATK bonus
            IntStat += atkBase / 20;
            // CON +1 per 300 HP bonus or 10 DEF bonus
            ConStat += hpBase / 300 + defBase / 10;
            // DEX +1 per 10 Speed bonus
            DexStat += (int)(speedBase / 10f);
            // LUK +1 per 5% CritChance bonus
            LukStat += (int)(critChanceBase / 0.05f);
            AttributesUpdate();
        }

        // ── 유닛 활성화/비활성화 ────────────────────────────────────────────────

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
        }

        // ── 상태효과 관리 ──────────────────────────────────────────────────────

        public System.Collections.Generic.IReadOnlyDictionary<string, StatusEffect> GetStatusEffects()
            => StatusEffects;

        public void AddStatusEffect(string identifier, StatusEffect effect)
        {
            Debug.Log($"{UnitName}에게 {identifier} 상태효과 부여");
            StatusEffects[identifier] = effect;
            AttributesUpdate();
        }

        public void RemoveStatusEffect(string identifier)
        {
            Debug.Log($"{UnitName}에게서 {identifier} 상태효과 제거됨");
            StatusEffects.Remove(identifier);
            AttributesUpdate();
        }

        public void SetSynergyEffect(int synergyId, SynergyEffect effect)
        {
            if (effect.Stack == 0)
                SynergyEffects.Remove(synergyId);
            else
            {
                SynergyEffects[synergyId] = effect;
                Debug.Log($"{UnitName}에게 {effect.SynergyName} {effect.Stack} 시너지 적용");
            }
        }

        // ── 이벤트 리스너 관리 ────────────────────────────────────────────────

        public void AddListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (!_eventDict.ContainsKey(eventType))
                RemoveAllListeners(eventType);
            _eventDict[eventType] = (Action<T>)_eventDict[eventType] + action;
        }

        public void RemoveListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (_eventDict.ContainsKey(eventType))
                _eventDict[eventType] = (Action<T>)_eventDict[eventType] - action;
        }

        public void RemoveAllListeners(BaseEnums.UnitEventType eventType)
        {
            if (_eventDict.ContainsKey(eventType))
                _eventDict[eventType] = null;
            else
                _eventDict.Add(eventType, null);
        }

        public void Invoke<T>(BaseEnums.UnitEventType eventType, T context)
        {
            if (_eventDict.TryGetValue(eventType, out var value))
                ((Action<T>)value)?.Invoke(context);
        }
    }
}
