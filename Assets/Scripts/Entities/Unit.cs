using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Core;
using Managers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Entities
{
    public class Unit : MonoBehaviour
    {
        /// <summary>모든 진영의 사망을 관찰해야 하는 필드 패시브용 전역 전투 이벤트.</summary>
        public static event Action<Unit, Unit> AnyUnitDied;

        public bool isActive = false;

        // 기본 식별정보
        [SerializeField] private int id;
        [SerializeField] private bool isEnemy;
        [SerializeField] private string unitName;
        [SerializeField] private string element;
        [SerializeField] private string mainStat;
        [SerializeField] private string subStat;
        [SerializeField] private List<int> equippedItemIds = new();
        [SerializeField] private List<LearnedPassiveSaveData> learnedPassiveRecords = new();
        [SerializeField] private List<int> grantedPassiveCodeIds = new();
        [SerializeField] private int level;
        // 육성(트레이닝) 레벨. 육성 페이즈에서 메인 캐릭터가 훈련할 때마다 증가한다.
        // 레벨 해금 패시브(#2/#3) 해금 판정에만 사용되며, 클래스 파생 Level과 분리되어 저장/복원된다.
        [SerializeField] private int trainingLevel;
        public int ID { get => id; protected set => id = value; }
        public bool IsEnemy { get => isEnemy; protected set => isEnemy = value; }
        public string UnitName { get => unitName; protected set => unitName = value; }
        public string Element { get => element; protected set => element = value; }
        public string MainStat { get => mainStat; protected set => mainStat = value; }
        public string SubStat { get => subStat; protected set => subStat = value; }
        public IReadOnlyList<int> EquippedItemIds => equippedItemIds;
        public IReadOnlyList<LearnedPassiveSaveData> LearnedPassiveRecords => learnedPassiveRecords;
        public IReadOnlyList<int> GrantedPassiveCodeIds => grantedPassiveCodeIds;
        public int Level { get => level; protected set => level = value; }
        // 육성 레벨. 육성 페이즈에서만 증가. 저장/복원 대상.
        public int TrainingLevel { get => trainingLevel; protected set => trainingLevel = value; }
        // 레벨 해금 패시브 판정에 쓰이는 유효 레벨 = 기본 Level + 육성 레벨.
        public int PassiveUnlockLevel => Level + TrainingLevel;

        public Cell currentCell; // 위치중인 셀

        // 유닛 스탯 모델 (5스탯/전투 스탯/강화 — 계산은 UnitStats 담당)
        [SerializeField] private UnitStats stats = new();
        public UnitStats Stats => stats;

        [SerializeField] private float codeAccelerationRunBonus;
        public int StrUpgrade { get => stats.StrUpgrade; protected set => stats.StrUpgrade = value; }
        public int DexUpgrade { get => stats.DexUpgrade; protected set => stats.DexUpgrade = value; }
        public int ConUpgrade { get => stats.ConUpgrade; protected set => stats.ConUpgrade = value; }
        public int IntUpgrade { get => stats.IntUpgrade; protected set => stats.IntUpgrade = value; }
        public int LukUpgrade { get => stats.LukUpgrade; protected set => stats.LukUpgrade = value; }
        public float CodeAccelerationRunBonus { get => codeAccelerationRunBonus; protected set => codeAccelerationRunBonus = value; }

        // 유닛 현재 상태
        [SerializeField] private int hpCurr;
        [SerializeField] private int manaCurr;
        [SerializeField] private int atkCurr;
        [SerializeField] private int defCurr;
        [SerializeField] private float critChanceCurr;
        [SerializeField] private float critDamageCurr;
        [SerializeField] private float codeAcceleration;
        [SerializeField] private float attackSpeedCurr;
        [SerializeField] private float evasionChanceCurr;
        [SerializeField] private float healingBonusCurr;
        [SerializeField] private float manaEfficiencyCurr;
        [SerializeField] private float codeActivationChanceCurr;
        [SerializeField] private int shieldMax;   // 최대 방어막 (획득한 총 방어막)
        [SerializeField] private int shieldCurr;  // 현재 방어막 (라운드 끝까지 유지)
        [SerializeField] private float shieldBonusCurr;
        [SerializeField] private BaseEnums.UltimateResourceType ultimateResourceType = BaseEnums.UltimateResourceType.Mana;
        [SerializeField] private string ultimateResourceName = "마나";
        public int HpCurr { get => hpCurr; protected set => hpCurr = value; }
        public int ManaCurr { get => manaCurr; protected set => manaCurr = value; }
        public int AtkCurr { get => atkCurr; protected set => atkCurr = value; }
        public int DefCurr { get => defCurr; protected set => defCurr = value; }
        public float CritChanceCurr { get => critChanceCurr; protected set => critChanceCurr = value; }
        public float CritMultiplierCurr { get => critDamageCurr; protected set => critDamageCurr = value; }
        public float CodeAcceleration { get => codeAcceleration; protected set => codeAcceleration = value; }
        public float AttackSpeedCurr { get => attackSpeedCurr; protected set => attackSpeedCurr = value; }
        public float EvasionChanceCurr { get => evasionChanceCurr; protected set => evasionChanceCurr = value; }
        public float HealingBonusCurr { get => healingBonusCurr; protected set => healingBonusCurr = value; }
        public float ManaEfficiencyCurr { get => manaEfficiencyCurr; protected set => manaEfficiencyCurr = value; }
        public float CodeActivationChanceCurr { get => codeActivationChanceCurr; protected set => codeActivationChanceCurr = value; }
        public int ShieldMax { get => shieldMax; protected set => shieldMax = value; }
        public int ShieldCurr { get => shieldCurr; protected set => shieldCurr = value; }
        public float ShieldBonusCurr { get => shieldBonusCurr; protected set => shieldBonusCurr = value; }
        public BaseEnums.UltimateResourceType UltimateResourceType { get => ultimateResourceType; protected set => ultimateResourceType = value; }
        public string UltimateResourceName { get => ultimateResourceName; protected set => ultimateResourceName = value; }

        public bool isCasting; // 스킬 시전중
        public float castingTime;
        public bool isControlled; // 행동 불가 상태
        public float controlDuration;
        
        // 일반공격 타겟팅
        public Unit currentNormalTarget; // 현재 일반공격 타겟

        // 타겟팅 우선도 (-3 ~ 3, 높을수록 우선순위 높음)
        [SerializeField] private int priority = 0;
        public int Priority { get => priority; set => priority = Mathf.Clamp(value, -3, 3); }

        // 파생/자원 최대치 (스탯 원본은 UnitStats가 소유)
        [SerializeField] private int hpMax;
        [SerializeField] private int ultimateResourceMax;
        [SerializeField] private int manaMax;

        // 스탯 접근 위임 (외부 호출 호환용)
        public int StrBase { get => stats.StrBase; protected set => stats.StrBase = value; }
        public int StrIncrementLvl { get => stats.StrIncrementLvl; protected set => stats.StrIncrementLvl = value; }
        public int StrIncrementUpgrade { get => stats.StrIncrementUpgrade; protected set => stats.StrIncrementUpgrade = value; }
        public int DexBase { get => stats.DexBase; protected set => stats.DexBase = value; }
        public int DexIncrementLvl { get => stats.DexIncrementLvl; protected set => stats.DexIncrementLvl = value; }
        public int DexIncrementUpgrade { get => stats.DexIncrementUpgrade; protected set => stats.DexIncrementUpgrade = value; }
        public int ConBase { get => stats.ConBase; protected set => stats.ConBase = value; }
        public int ConIncrementLvl { get => stats.ConIncrementLvl; protected set => stats.ConIncrementLvl = value; }
        public int ConIncrementUpgrade { get => stats.ConIncrementUpgrade; protected set => stats.ConIncrementUpgrade = value; }
        public int IntBase { get => stats.IntBase; protected set => stats.IntBase = value; }
        public int IntIncrementLvl { get => stats.IntIncrementLvl; protected set => stats.IntIncrementLvl = value; }
        public int IntIncrementUpgrade { get => stats.IntIncrementUpgrade; protected set => stats.IntIncrementUpgrade = value; }
        public int LukBase { get => stats.LukBase; protected set => stats.LukBase = value; }
        public int LukIncrementLvl { get => stats.LukIncrementLvl; protected set => stats.LukIncrementLvl = value; }
        public int LukIncrementUpgrade { get => stats.LukIncrementUpgrade; protected set => stats.LukIncrementUpgrade = value; }
        public int HpMax { get => hpMax; protected set => hpMax = value; }
        public int UltimateResourceMax { get => ultimateResourceMax; protected set => ultimateResourceMax = value; }
        public int ManaMax { get => manaMax; protected set => manaMax = value; }
        public int AtkBase { get => stats.AtkBase; protected set => stats.AtkBase = value; }
        public int AtkIncrementLvl { get => stats.AtkIncrementLvl; protected set => stats.AtkIncrementLvl = value; }
        public int DefBase { get => stats.DefBase; protected set => stats.DefBase = value; }
        public int DefIncrementLvl { get => stats.DefIncrementLvl; protected set => stats.DefIncrementLvl = value; }

        // 유닛 상태(버프/디버프) 컨테이너
        protected readonly UnitStatusController StatusController = new();

        // 유닛 코드(스킬) 정보
        // 유닛 고유 패시브 목록: [0] = 초기 패시브(항상 활성), 이후 = 레벨/INT 용량에 따라 해금되는 패시브.
        protected List<PassiveCode> PassiveCodes = new();
        // 아직 해금되지 않은 레벨 패시브 정의. RefreshLevelPassives()에서 Level에 도달하면 PassiveCodes로 승격한다.
        protected List<LevelPassiveData> PendingLevelPassives = new();
        protected List<PassiveCode> ItemPassiveCodes = new();
        protected NormalCode NormalCode;
        protected UltimateCode UltimateCode;
        public float normalCooldown;
        public float ultimateCooldown;
        protected EquipmentLoadout EquipmentLoadout;
        private readonly HashSet<BaseEnums.UnitElement> _combatElements = new();
        private readonly Dictionary<string, int> _combatResources = new();
        private readonly Dictionary<string, int> _combatResourceMaximums = new();
        private int _baseNormalCodeId;
        private int _baseUltimateCodeId;
        private int _baseNormalCodeStage = 1;
        private int _baseUltimateCodeStage = 1;

        public int CarryWeightMax => 5 + Mathf.Max(0, GetBaseStr());
        public int CarryWeightCurrent => EquipmentLoadout?.GetTotalWeight() ?? 0;
        public int MaxCodeCount => Mathf.Max(3, GetBaseInt());
        public int LearnedCodeCount => 2 + PassiveCodes.Count;

        // 이벤트
        private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict;

        [FormerlySerializedAs("portraitPath")] public string PortraitPath;

        void Awake()
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            InitializeStatusController();
            EquipmentLoadout = new EquipmentLoadout();
        }

        private void InitializeStatusController()
        {
            stats.Initialize(this);
            StatusController.Initialize(
                this,
                () => { AttributesUpdate(); UpdateInfoTabIfShowing(); },
                NotifyBeneficialEffectReceived);
        }

        public virtual void InitializeUnit(bool _isEnemy, int _id)
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            InitializeStatusController();
            PassiveCodes = new List<PassiveCode>();
            PendingLevelPassives = new List<LevelPassiveData>();
            ItemPassiveCodes = new List<PassiveCode>();
            learnedPassiveRecords = new List<LearnedPassiveSaveData>();
            grantedPassiveCodeIds = new List<int>();
            _combatElements.Clear();
            _combatResources.Clear();
            _combatResourceMaximums.Clear();
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
            
            // Cell의 HP 바 초기화 및 업데이트
            currentCell.InitializeHpBar();
            // Cell의 MP 바 초기화 및 업데이트
            currentCell.InitializeMpBar();
            
            ManaCurr = 0;
            ShieldMax = 0;
            ShieldCurr = 0;
            UpdateShieldBar(); // 방어막 바 초기화
            AddListener<EventContext>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnUpdate, DefaultUpdateEvent);
        }

        protected virtual void LoadData(bool _isEnemy, int _id)
        {
            // 적 유닛인 경우 EnemyData에서 로드
            if (_isEnemy)
            {
                EnemyDataList enemyDataList = GameManager.Instance.dataManager.FetchEnemyDataList();
                EnemyData enemyData = enemyDataList.enemies.FirstOrDefault(e => e.id == _id);
                
                if (enemyData == null)
                {
                    Debug.LogError($"Enemy data not found for ID: {_id}");
                    return;
                }
                
                LoadSprite(enemyData.portrait, _isEnemy);
                Level = Mathf.Max(0, GameManager.Instance?.RoundManager?.EnemyLevel ?? 0);
                UnitName = enemyData.name;
                Element = string.IsNullOrWhiteSpace(enemyData.element) ? "None" : enemyData.element;
                ResetCombatElements();
                MainStat = "";
                SubStat = "";
                LoadStatData(
                    enemyData.strBase, enemyData.strIncrementLvl, enemyData.strIncrementUpgrade,
                    enemyData.dexBase, enemyData.dexIncrementLvl, enemyData.dexIncrementUpgrade,
                    enemyData.conBase, enemyData.conIncrementLvl, enemyData.conIncrementUpgrade,
                    enemyData.intBase, enemyData.intIncrementLvl, enemyData.intIncrementUpgrade,
                    enemyData.lukBase, enemyData.lukIncrementLvl, enemyData.lukIncrementUpgrade,
                    enemyData.atkBase, enemyData.atkIncrementLvl,
                    enemyData.defBase, enemyData.defIncrementLvl);
                ConfigureUltimateResource(enemyData.ultimateResourceType, enemyData.ultimateResourceName, enemyData.ultimateResourceMax);
                CodeAcceleration = 1f;

                isCasting = false;
                castingTime = 0f;
                isControlled = false;
                controlDuration = 0f;
                currentNormalTarget = null;

                LoadPassiveCodes(enemyData.codes["passive"], enemyData.levelPassives);
                _baseNormalCodeId = enemyData.codes["normal"];
                _baseUltimateCodeId = enemyData.codes["ultimate"];
                NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
                UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
                ApplyCodeStages(enemyData.codeStages);
                normalCooldown = NormalCode.Cooldown;
                ultimateCooldown = UltimateCode.Cooldown;
            }
            // 아군(영웅) 유닛인 경우 기존 UnitData에서 로드
            else
            {
                UnitData data = GameManager.Instance.unitDataList.units.FirstOrDefault(e => e.id == _id);
                LoadSprite(data.portrait, _isEnemy);
                Level = 1;
                UnitName = data.name;
                Element = string.IsNullOrWhiteSpace(data.element) ? "None" : data.element;
                ResetCombatElements();
                MainStat = data.mainStat ?? "";
                SubStat = data.subStat ?? "";
                LoadStatData(
                    data.strBase, data.strIncrementLvl, data.strIncrementUpgrade,
                    data.dexBase, data.dexIncrementLvl, data.dexIncrementUpgrade,
                    data.conBase, data.conIncrementLvl, data.conIncrementUpgrade,
                    data.intBase, data.intIncrementLvl, data.intIncrementUpgrade,
                    data.lukBase, data.lukIncrementLvl, data.lukIncrementUpgrade,
                    data.atkBase, data.atkIncrementLvl,
                    data.defBase, data.defIncrementLvl);
                ConfigureUltimateResource(data.ultimateResourceType, data.ultimateResourceName, data.ultimateResourceMax);
                CodeAcceleration = 1f;

                isCasting = false;
                castingTime = 0f;
                isControlled = false;
                controlDuration = 0f;
                currentNormalTarget = null; // 일반공격 타겟 초기화

                LoadPassiveCodes(data.codes["passive"], data.levelPassives);
                _baseNormalCodeId = data.codes["normal"];
                _baseUltimateCodeId = data.codes["ultimate"];
                NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
                UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
                ApplyCodeStages(data.codeStages);
                EquipStartingItems(data.startingItemIds);
                normalCooldown = NormalCode.Cooldown;
                ultimateCooldown = UltimateCode.Cooldown;
                
                // 유닛별 패시브 StatusEffect 적용
                ApplyPassiveStatusEffect();
            }
        }

        private void EquipStartingItems(List<int> startingItemIds)
        {
            equippedItemIds.Clear();
            EquipmentLoadout = new EquipmentLoadout();
            ItemPassiveCodes?.Clear();
            if (startingItemIds == null) return;

            foreach (int itemId in startingItemIds)
            {
                if (!TryEquipItem(itemId, out string reason))
                {
                    Debug.LogWarning($"[장비] {UnitName} 시작 아이템 {itemId} 장착 실패: {reason}");
                }
            }
        }

        public bool TryEquipItem(int itemId, out string reason)
        {
            reason = null;
            ItemData itemData = GetItemData(itemId);
            if (itemData == null)
            {
                reason = $"아이템 데이터를 찾을 수 없습니다: {itemId}";
                return false;
            }

            int prospectiveWeight = EquipmentLoadout.GetProspectiveWeight(itemData);
            if (prospectiveWeight > CarryWeightMax)
            {
                reason = $"장비 중량이 한도를 초과합니다. ({prospectiveWeight}/{CarryWeightMax})";
                return false;
            }

            if (!EquipmentLoadout.TryEquip(itemData, HasEquipmentProficiency, out reason))
            {
                return false;
            }

            RefreshEquippedItemIds();
            RefreshEquipmentCodeGrants();
            AttributesUpdate();
            currentCell?.UpdateUI();
            return true;
        }

        private void RefreshEquipmentCodeGrants()
        {
            foreach (PassiveCode itemPassiveCode in ItemPassiveCodes)
            {
                itemPassiveCode?.StopCode();
            }
            ItemPassiveCodes.Clear();

            NormalCode?.StopCode();
            UltimateCode?.StopCode();
            NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
            UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
            NormalCode?.SetStage(_baseNormalCodeStage);
            UltimateCode?.SetStage(_baseUltimateCodeStage);

            foreach (ItemData equippedItem in EquipmentLoadout.GetEquippedItemData())
            {
                ApplyItemCodeGrants(equippedItem);
            }
        }

        private void RefreshEquippedItemIds()
        {
            equippedItemIds = EquipmentLoadout.GetEquippedItemData()
                .Where(item => item != null)
                .Select(item => item.id)
                .ToList();
        }

        private ItemData GetItemData(int itemId)
        {
            ItemDataList itemDataList = GameManager.Instance?.itemDataList ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            return itemDataList?.items?.FirstOrDefault(item => item.id == itemId);
        }

        // 클래스 시스템이 제거되어 장비 숙련도 제한은 없다. 모든 장비는 슬롯/양손무기 규칙만 따른다.
        private bool HasEquipmentProficiency(EquipmentProficiency proficiency) => true;

        private void ApplyItemCodeGrants(ItemData itemData)
        {
            if (itemData.codeGrants == null) return;

            foreach (EquipmentCodeGrant codeGrant in itemData.codeGrants)
            {
                if (codeGrant == null) continue;

                string codeSlot = codeGrant.slot?.Trim().ToLowerInvariant();
                switch (codeSlot)
                {
                    case "normal":
                    case "normalattack":
                    case "normal_attack":
                    case "일반공격":
                        NormalCode?.StopCode();
                        NormalCode = CodeFactory.CreateNormalCode(codeGrant.codeId, new NormalCodeContext { Caster = this });
                        NormalCode?.SetStage(codeGrant.stage);
                        normalCooldown = NormalCode?.Cooldown ?? 0f;
                        break;
                    case "passive":
                    case "패시브":
                        PassiveCode itemPassiveCode = CodeFactory.CreatePassiveCode(codeGrant.codeId, new PassiveCodeContext { Caster = this });
                        itemPassiveCode?.SetStage(codeGrant.stage);
                        if (itemPassiveCode != null)
                        {
                            ItemPassiveCodes.Add(itemPassiveCode);
                        }
                        break;
                    case "ultimate":
                    case "궁극기":
                        UltimateCode?.StopCode();
                        UltimateCode = CodeFactory.CreateUltimateCode(codeGrant.codeId, new UltimateCodeContext { Caster = this });
                        UltimateCode?.SetStage(codeGrant.stage);
                        ultimateCooldown = UltimateCode?.Cooldown ?? 0f;
                        break;
                }
            }
        }

        internal int GetEquipmentStatBonus(BaseEnums.PrimaryStat stat)
        {
            if (EquipmentLoadout == null) return 0;

            int total = 0;
            foreach (ItemData itemData in EquipmentLoadout.GetEquippedItemData())
            {
                if (itemData?.statBonuses == null) continue;

                foreach (EquipmentStatBonus bonus in itemData.statBonuses)
                {
                    if (bonus == null || string.IsNullOrWhiteSpace(bonus.stat)) continue;
                    if (Enum.TryParse(bonus.stat, true, out BaseEnums.PrimaryStat bonusStat) && bonusStat == stat)
                    {
                        total += bonus.amount;
                    }
                }
            }

            return total;
        }

        private void ConfigureUltimateResource(string resourceType, string resourceName, int resourceMax)
        {
            UltimateResourceType = string.Equals(resourceType, "Stack", StringComparison.OrdinalIgnoreCase)
                ? BaseEnums.UltimateResourceType.Stack
                : BaseEnums.UltimateResourceType.Mana;
            UltimateResourceName = string.IsNullOrWhiteSpace(resourceName)
                ? (UltimateResourceType == BaseEnums.UltimateResourceType.Mana ? "마나" : "스택")
                : resourceName;
            UltimateResourceMax = resourceMax > 0 ? resourceMax : 100;
        }

        private void ApplyCodeStages(Dictionary<string, int> codeStages)
        {
            if (codeStages == null) return;

            if (codeStages.TryGetValue("passive", out int passiveStage) && PassiveCodes.Count > 0)
            {
                // codeStages.passive는 초기 패시브([0])에만 적용한다.
                // 레벨 해금 패시브의 단계는 levelPassives 각 항목의 stage 필드로 지정한다.
                PassiveCodes[0]?.SetStage(passiveStage);
                if (learnedPassiveRecords.Count > 0)
                {
                    learnedPassiveRecords[0].stage = passiveStage;
                }
            }
            if (codeStages.TryGetValue("normal", out int normalStage))
            {
                _baseNormalCodeStage = normalStage;
                NormalCode?.SetStage(normalStage);
            }
            if (codeStages.TryGetValue("ultimate", out int ultimateStage))
            {
                _baseUltimateCodeStage = ultimateStage;
                UltimateCode?.SetStage(ultimateStage);
            }
        }

        /// <summary>
        /// 유닛의 패시브 코드를 로드한다.
        /// 초기 패시브(innatePassiveId)는 항상 활성화되고, 레벨 해금 패시브는 현재 Level 조건을
        /// 만족하는 것만 즉시 활성화한다. 나머지는 PendingLevelPassives에 보관했다가
        /// 레벨업 시 RefreshLevelPassives()로 승격한다.
        /// </summary>
        private void LoadPassiveCodes(int innatePassiveId, List<LevelPassiveData> levelPassives)
        {
            PassiveCodes.Clear();
            PendingLevelPassives.Clear();

            PassiveCode innate = CodeFactory.CreatePassiveCode(innatePassiveId, new PassiveCodeContext { Caster = this });
            if (innate != null)
            {
                PassiveCodes.Add(innate);
                AddLearnedPassiveRecord(innatePassiveId, innate.CurrentStage, innate.Transferable);
            }

            if (levelPassives != null)
            {
                foreach (LevelPassiveData def in levelPassives)
                {
                    if (def == null) continue;
                    PendingLevelPassives.Add(def);
                }
            }

            RefreshLevelPassives();
        }

        /// <summary>
        /// 현재 Level과 INT 코드 용량을 만족한 패시브를 활성 목록으로 승격한다.
        /// 레벨업(예: 육성 페이즈, 업그레이드) 이후 호출한다. 이미 활성화된 패시브는 중복 추가하지 않는다.
        /// </summary>
        protected void RefreshLevelPassives()
        {
            if (PendingLevelPassives.Count == 0) return;

            List<LevelPassiveData> eligible = PendingLevelPassives
                .Where(def => def != null && PassiveUnlockLevel >= def.unlockLevel)
                .OrderBy(def => def.unlockLevel)
                .ToList();

            foreach (LevelPassiveData def in eligible)
            {
                if (LearnedCodeCount >= MaxCodeCount) break;

                PassiveCode code = CodeFactory.CreatePassiveCode(def.codeId, new PassiveCodeContext { Caster = this });
                if (code != null)
                {
                    if (def.stage > 0) code.SetStage(def.stage);
                    PassiveCodes.Add(code);
                    AddLearnedPassiveRecord(def.codeId, code.CurrentStage, code.Transferable);
                }
                PendingLevelPassives.Remove(def);
            }

            PendingLevelPassives.RemoveAll(def => def == null);
        }

        private void AddLearnedPassiveRecord(int codeId, int stage, bool transferable = true)
        {
            if (codeId <= 0) return;

            LearnedPassiveSaveData existing = learnedPassiveRecords
                .FirstOrDefault(record => record != null && record.codeId == codeId);
            if (existing != null)
            {
                existing.stage = Mathf.Max(existing.stage, stage);
                existing.transferable = existing.transferable && transferable;
                return;
            }

            learnedPassiveRecords.Add(new LearnedPassiveSaveData
            {
                codeId = codeId,
                stage = Mathf.Max(1, stage),
                transferable = transferable,
            });
        }

        public bool LearnTransferredPassive(int codeId, int stage)
        {
            if (codeId <= 0) return false;
            if (LearnedCodeCount >= MaxCodeCount) return false;
            if (learnedPassiveRecords.Any(record => record != null && record.codeId == codeId))
            {
                return false;
            }

            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = this });
            if (code == null) return false;
            if (!code.Transferable) return false;

            code.SetStage(stage);
            PassiveCodes.Add(code);
            AddLearnedPassiveRecord(codeId, code.CurrentStage, code.Transferable);
            return true;
        }

        /// <summary>사건/보상으로 획득한 런 영구 패시브를 코드 용량과 무관하게 추가한다.</summary>
        public bool GrantPermanentPassive(int codeId, int stage = 1)
        {
            if (codeId <= 0 || grantedPassiveCodeIds.Contains(codeId)) return false;
            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = this });
            if (code == null) return false;
            code.SetStage(stage);
            PassiveCodes.Add(code);
            grantedPassiveCodeIds.Add(codeId);
            AddLearnedPassiveRecord(codeId, code.CurrentStage, code.Transferable);
            return true;
        }

        /// <summary>
        /// 육성: 지정한 5스탯의 강화 수치를 증가시킨다. 즉시 스탯을 재계산한다.
        /// </summary>
        public void AddStatUpgrade(BaseEnums.PrimaryStat stat, int amount)
        {
            if (amount == 0) return;

            stats.AddUpgrade(stat, amount);
            AttributesUpdate();
            RefreshLevelPassives();
            currentCell?.UpdateUI();
        }

        /// <summary>
        /// 육성: 트레이닝 레벨을 올리고 레벨 해금 패시브를 갱신한다.
        /// 육성 페이즈에서 메인 캐릭터에게 호출한다.
        /// </summary>
        public void GainTrainingLevel(int amount = 1)
        {
            if (amount <= 0) return;

            TrainingLevel += amount;
            RefreshLevelPassives();
            AttributesUpdate();
            currentCell?.UpdateUI();
        }

        private void LoadStatData(
            int dataStrBase, int dataStrIncrementLvl, int dataStrIncrementUpgrade,
            int dataDexBase, int dataDexIncrementLvl, int dataDexIncrementUpgrade,
            int dataConBase, int dataConIncrementLvl, int dataConIncrementUpgrade,
            int dataIntBase, int dataIntIncrementLvl, int dataIntIncrementUpgrade,
            int dataLukBase, int dataLukIncrementLvl, int dataLukIncrementUpgrade,
            int dataAtkBase, int dataAtkIncrementLvl,
            int dataDefBase, int dataDefIncrementLvl)
        {
            stats.Load(
                dataStrBase, dataStrIncrementLvl, dataStrIncrementUpgrade,
                dataDexBase, dataDexIncrementLvl, dataDexIncrementUpgrade,
                dataConBase, dataConIncrementLvl, dataConIncrementUpgrade,
                dataIntBase, dataIntIncrementLvl, dataIntIncrementUpgrade,
                dataLukBase, dataLukIncrementLvl, dataLukIncrementUpgrade,
                dataAtkBase, dataAtkIncrementLvl,
                dataDefBase, dataDefIncrementLvl);
        }

        /// <summary>
        /// 유닛별 패시브 StatusEffect 적용
        /// 영구적으로 유지되는 패시브 효과를 StatusEffect로 추가
        /// </summary>
        protected virtual void ApplyPassiveStatusEffect()
        {
            // 기존 레거시 스탯 패시브는 5스탯/코드 구조로 이전하면서 제거했다.
            // 추가 유닛 패시브는 Code 또는 새 Status 시스템으로 연결한다.
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
        /// <summary>
        /// 현재 보유 중인 모든 상태의 효과 객체 열거 (스탯 질의 훅 집계용)
        /// </summary>
        protected IEnumerable<Effects.Base.BaseEffect> ActiveEffectObjects()
        {
            return StatusController.ActiveEffectObjects();
        }

        protected virtual void AttributesUpdate()
        {
            // 스탯 수정치 계산
            float critChanceAdd = 0f;
            float critMultiplierAdd = 0f;
            float shieldBonusAdd = 0f;
            float codeAccelerationAdd = 0f;
            float manaRecoveryMultiplier = 1f;

            foreach (var effect in ActiveEffectObjects())
            {
                critChanceAdd += effect.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effect.CritMultiplierAdditiveModifier(this);
                shieldBonusAdd += effect.ShieldBonusAdditiveModifier(this);
                codeAccelerationAdd += effect.CodeAccelerationAdditiveModifier(this);
                manaRecoveryMultiplier *= effect.ManaRecoveryMultiplierModifier(this);
            }
            // hp 비율 저장
            float healthRatio = (HpMax > 0) ? (float)HpCurr / HpMax : 1f;

            HpMax = GetDerivedHp();
            ManaMax = GetUltimateResourceMax();
            AtkCurr = GetDerivedAtk();
            DefCurr = GetDerivedDef();
            CritChanceCurr = Mathf.Clamp01(GetDerivedCritChance() + critChanceAdd);
            CritMultiplierCurr = Mathf.Max(1f, GetDerivedCritDamage() + critMultiplierAdd);
            EvasionChanceCurr = GetDerivedEvasionChance();
            HealingBonusCurr = GetDerivedHealingBonus();
            ShieldBonusCurr = Mathf.Max(0f, GetDerivedShieldBonus() + shieldBonusAdd);
            ManaEfficiencyCurr = GetDerivedManaEfficiency() * manaRecoveryMultiplier;
            CodeActivationChanceCurr = GetDerivedCodeActivationChance();
            CodeAcceleration = Mathf.Max(0.1f, GetDerivedCodeAcceleration() + CodeAccelerationRunBonus + codeAccelerationAdd);
            AttackSpeedCurr = GetDerivedAttackSpeed();

            // hp 비율 복구
            HpCurr = Mathf.RoundToInt(HpMax * healthRatio);
            
            // 방어막 바 업데이트
            UpdateShieldBar();
        }

        /// <summary>
        /// 방어막 바의 시각적 표시를 업데이트
        /// </summary>
        protected virtual void UpdateShieldBar()
        {
            // Cell의 통합 UI 시스템을 통해 방어막 바 업데이트
            if (currentCell != null)
            {
                currentCell.UpdateUI(); // Cell에서 HP + 방어막 바 통합 처리
            }
            else
            {
                Debug.LogWarning($"[방어막 바] {UnitName}의 currentCell이 null입니다!");
            }
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
            EventContext context = new EventContext(this);
            Invoke(BaseEnums.UnitEventType.OnSpawn, context);
        }

        /// <summary>
        /// 유닛 사망 이벤트
        /// </summary>
        /// <param name="attacker">막타친 적 유닛(사망 시 이벤트 처리용)</param>
        public virtual void Die(Unit attacker)
        {
            EventContext context = new EventContext(this, attacker);
            Invoke(BaseEnums.UnitEventType.OnDeath, context);
            AnyUnitDied?.Invoke(this, attacker);
            if (attacker != null && attacker != this)
            {
                attacker.Invoke(BaseEnums.UnitEventType.OnKill, new EventContext(attacker, this));
            }
            if (isEnemy) GameManager.Instance.OnKillEnemy();
            DeactivateUnit();
        }

        /// <summary>
        /// 유닛 피해 처리 이벤트<br/>
        /// 다른 피해 처리 이벤트를 등록하지 않았을 경우 기본 피해 처리 이벤트(DefaultTakeDamageEvent) 호출
        /// </summary>
        /// <param name="context">피해 정보 컨텍스트</param>
        public virtual void TakeDamage(DamageContext context)
        {
            if (context == null) return;
            Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, new EventContext(this, context.Attacker, context));
            Invoke(BaseEnums.UnitEventType.OnTakingDamage, new EventContext(this, null, context));
            Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, new EventContext(this, context.Attacker));
        }

        /// <summary>
        /// 유닛이 행동불능 상태가 되었을 때 호출되는 이벤트
        /// </summary>
        /// <param name="context">공격자와 지속시간을 지닌 제어 정보 컨텍스트</param>
        public virtual void ControlStarts(ControlContext context)
        {
            isControlled = true;
            controlDuration = context.Duration;
            Invoke(BaseEnums.UnitEventType.OnControlStarts, new EventContext(this, context.Attacker));
        }

        /// <summary>
        /// 유닛이 행동불능 상태에서 벗어났을 때 호출되는 이벤트
        /// </summary>
        public virtual void ControlEnds()
        {
            isControlled = false;
            controlDuration = 0f;
            Invoke(BaseEnums.UnitEventType.OnControlEnds, new EventContext(this));
        }

        public virtual void CastPassiveCode()
        {
            foreach (PassiveCode passiveCode in PassiveCodes)
            {
                TryCastPassiveCode(passiveCode);
            }
            foreach (PassiveCode itemPassiveCode in ItemPassiveCodes)
            {
                TryCastPassiveCode(itemPassiveCode);
            }

            Invoke(BaseEnums.UnitEventType.OnPassiveActivates, new EventContext(this));
        }

        private void TryCastPassiveCode(PassiveCode passiveCode)
        {
            if (passiveCode == null) return;

            if (!TryPassCodeActivation(passiveCode))
            {
                Debug.Log($"{UnitName}의 패시브 코드 {passiveCode.CodeName} 발동 실패");
                return;
            }

            passiveCode.CastCode();
        }

        public virtual void CastNormalCode()
        {
            NormalCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, new EventContext(this));
        }

        public virtual void CastUltimateCode()
        {
            ConsumeUltimateResource();
            // Cell의 통합 UI 시스템 사용
            currentCell.UpdateUI();

            UltimateCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnUltimateActivates, new EventContext(this));
        }

        public virtual void RecoverMana(int amount)
        {
            if (UltimateResourceType != BaseEnums.UltimateResourceType.Mana)
            {
                return;
            }

            int adjustedAmount = Mathf.Max(0, Mathf.RoundToInt(amount * ManaEfficiencyCurr));
            AddUltimateResource(adjustedAmount);
        }

        public virtual void AddUltimateResource(int amount)
        {
            ManaCurr += amount;
            if (ManaCurr > ManaMax)
            {
                ManaCurr = ManaMax;
            }
            // Cell의 통합 UI 시스템 사용
            currentCell.UpdateUI();
        }

        public void ResetCombatElements()
        {
            _combatElements.Clear();
            if (Enum.TryParse(Element, true, out BaseEnums.UnitElement innateElement) &&
                innateElement != BaseEnums.UnitElement.None)
            {
                _combatElements.Add(innateElement);
            }
        }

        public void GrantCombatElement(BaseEnums.UnitElement elementToGrant)
        {
            if (elementToGrant == BaseEnums.UnitElement.None) return;
            if (_combatElements.Add(elementToGrant))
            {
                AttributesUpdate();
                currentCell?.UpdateUI();
            }
        }

        public bool HasCombatElement(BaseEnums.UnitElement elementToCheck)
        {
            return _combatElements.Contains(elementToCheck);
        }

        public string GetCombatElementDisplay()
        {
            return _combatElements.Count == 0 ? "None" : string.Join(", ", _combatElements);
        }

        public void SetCombatResourceMaximum(string resourceId, int maximum, bool resetCurrent = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            _combatResourceMaximums[resourceId] = Mathf.Max(0, maximum);
            if (resetCurrent || !_combatResources.ContainsKey(resourceId))
            {
                _combatResources[resourceId] = 0;
            }
            else
            {
                _combatResources[resourceId] = Mathf.Min(_combatResources[resourceId], _combatResourceMaximums[resourceId]);
            }
            currentCell?.UpdateUI();
        }

        public int GetCombatResource(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) && _combatResources.TryGetValue(resourceId, out int value) ? value : 0;
        }

        public int GetCombatResourceMaximum(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) && _combatResourceMaximums.TryGetValue(resourceId, out int value) ? value : 0;
        }

        public int AddCombatResource(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount == 0) return GetCombatResource(resourceId);
            int maximum = GetCombatResourceMaximum(resourceId);
            int next = Mathf.Clamp(GetCombatResource(resourceId) + amount, 0, maximum);
            _combatResources[resourceId] = next;
            currentCell?.UpdateUI();
            return next;
        }

        public bool TryConsumeCombatResource(string resourceId, int amount)
        {
            if (amount <= 0) return true;
            int current = GetCombatResource(resourceId);
            if (current < amount) return false;
            _combatResources[resourceId] = current - amount;
            currentCell?.UpdateUI();
            return true;
        }

        public bool CanActAndAttack()
        {
            return isActive && !isControlled && !isCasting && NormalCode != null && NormalCode.HasValidTarget();
        }

        public virtual bool CanCastUltimateCode()
        {
            return ManaCurr >= ManaMax;
        }

        protected virtual void ConsumeUltimateResource()
        {
            ManaCurr = 0;
        }

        public void ModifyHp(int newHp)
        {
            if (newHp > HpCurr)
            {
                float receivedMultiplier = 1f;
                foreach (var effect in ActiveEffectObjects())
                {
                    receivedMultiplier *= effect.HealingReceivedMultiplierModifier(this);
                }
                int healingAmount = Mathf.RoundToInt((newHp - HpCurr) * (1f + HealingBonusCurr) * receivedMultiplier);
                HpCurr = Mathf.Clamp(HpCurr + healingAmount, 0, HpMax);
            }
            else
            {
                HpCurr = Mathf.Clamp(newHp, 0, HpMax);
            }
            currentCell?.UpdateUI();
        }

        private bool TryPassCodeActivation(Code code)
        {
            if (code == null) return false;
            if (code.IgnoresActivationChance) return true;
            if (code.ActivationChance < 0f) return true;

            float statBonus = 0f;
            if (code.CodeTags.Contains(BaseClasses.DamageTag.Physical))
            {
                statBonus += GetBaseDex() * 0.01f;
            }
            if (code.CodeTags.Contains(BaseClasses.DamageTag.Special))
            {
                statBonus += GetBaseInt() * 0.01f;
            }

            float chance = Mathf.Clamp01((code.ActivationChance + statBonus) * code.ActivationChanceMultiplier);
            return UnityEngine.Random.value <= chance;
        }

        public void AddRunBonus(
            int hpUpgrade = 0,
            int atkUpgrade = 0,
            int defUpgrade = 0,
            int intUpgrade = 0,
            int critChanceUpgrade = 0,
            int critMultiplierUpgrade = 0,
            float codeAccelerationBonus = 0f)
        {
            // 보상 종류별 매핑: atk/def → STR, hp → CON, crit 계열 → LUK, 가속 → DEX(+런 보너스)
            StrUpgrade += atkUpgrade + defUpgrade;
            DexUpgrade += Mathf.RoundToInt(codeAccelerationBonus * 20f);
            ConUpgrade += hpUpgrade;
            IntUpgrade += intUpgrade;
            LukUpgrade += critChanceUpgrade + critMultiplierUpgrade;
            CodeAccelerationRunBonus += codeAccelerationBonus;
            AttributesUpdate();
            currentCell?.UpdateUI();
        }

        public void RestoreRunState(Core.UnitSaveData saveData)
        {
            if (saveData == null) return;

            foreach (int passiveCodeId in saveData.grantedPassiveCodeIds ?? new List<int>())
            {
                GrantPermanentPassive(passiveCodeId);
            }

            TrainingLevel = saveData.trainingLevel;
            // 육성 레벨이 복원되면 그에 맞는 레벨 해금 패시브를 다시 활성화한다.
            RefreshLevelPassives();
            StrUpgrade = saveData.strUpgrade;
            DexUpgrade = saveData.dexUpgrade;
            ConUpgrade = saveData.conUpgrade;
            IntUpgrade = saveData.intUpgrade;
            LukUpgrade = saveData.lukUpgrade;
            CodeAccelerationRunBonus = saveData.codeAccelerationBonus;
            if (saveData.equippedItemIds != null && saveData.equippedItemIds.Count > 0)
            {
                EquipStartingItems(saveData.equippedItemIds);
            }
            AttributesUpdate();
            ModifyHp(saveData.currentHP);
        }

        /// <summary>
        /// 방어막을 추가하는 메서드
        /// </summary>
        /// <param name="amount">추가할 방어막 양</param>
        public virtual void AddShield(int amount)
        {
            amount = ApplyShieldBonus(amount);
            int previousShieldMax = ShieldMax;
            int previousShieldCurr = ShieldCurr;
            
            // ShieldMax와 ShieldCurr 둘 다 증가
            ShieldMax += amount;
            ShieldCurr += amount;
            
            AttributesUpdate(); // 상태 효과 반영을 위해 스탯 업데이트
            UpdateShieldBar(); // 방어막 바 시각 업데이트
            Debug.Log($"[AddShield] {UnitName}: Max {previousShieldMax}→{ShieldMax}, Curr {previousShieldCurr}→{ShieldCurr} (HP: {HpCurr}/{HpMax})");
        }

        /// <summary>
        /// 방어막을 설정하는 메서드 (기존 방어막을 덮어씀)
        /// </summary>
        /// <param name="amount">설정할 방어막 양</param>
        public virtual void SetShield(int amount)
        {
            amount = ApplyShieldBonus(amount);
            ShieldMax = amount;
            ShieldCurr = amount;
            AttributesUpdate(); // 상태 효과 반영을 위해 스탯 업데이트
            UpdateShieldBar(); // 방어막 바 시각 업데이트
            Debug.Log($"[SetShield] {UnitName}의 방어막이 {amount}로 설정되었습니다 (Max={ShieldMax}, Curr={ShieldCurr})");
        }

        private int ApplyShieldBonus(int amount)
        {
            return Mathf.Max(0, Mathf.RoundToInt(amount * (1f + ShieldBonusCurr)));
        }

        /// <summary>
        /// 방어막을 제거하는 메서드 (피해를 받을 때 호출)
        /// </summary>
        /// <param name="amount">제거할 방어막 양</param>
        public virtual void RemoveShield(int amount)
        {
            int previousShieldCurr = ShieldCurr;
            ShieldCurr = Mathf.Max(0, ShieldCurr - amount);
            
            // ShieldCurr이 0이 되면 ShieldMax도 초기화
            if (ShieldCurr == 0)
            {
                ShieldMax = 0;
                Debug.Log($"[RemoveShield] {UnitName}의 방어막이 모두 소진되어 초기화됨 (Curr {previousShieldCurr}→0, Max 0)");
            }
            else
            {
                Debug.Log($"[RemoveShield] {UnitName}의 방어막 감소: Curr {previousShieldCurr}→{ShieldCurr} (Max={ShieldMax})");
            }
            
            AttributesUpdate(); // 상태 효과 반영을 위해 스탯 업데이트
            UpdateShieldBar(); // 방어막 바 시각 업데이트
        }

        private void Update()
        {
            if (!isActive) return;
            if (GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress)
            {
                // 벤치에 있는 유닛은 공격하지 않음
                if (GridManager.Instance && GridManager.Instance.IsBenchCell(currentCell))
                {
                    return;
                }
                
                Invoke(BaseEnums.UnitEventType.OnUpdate, new EventContext(this, null, null, Time.deltaTime));
                
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
                    if (ultimateCooldown <= 0f && UltimateCode.HasValidTarget() && CanCastUltimateCode())
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
                    normalCooldown = Mathf.Max(0f, normalCooldown - Time.deltaTime * CodeAcceleration * AttackSpeedCurr);
                }
            }
        }

        // 디폴트 이벤트 액션(이거 참고해서 다른 이벤트 만들어 넣으면 됨)

        /// <summary>
        /// 다른 피해 처리 이벤트를 등록하지 않았을 경우 기본 피해 처리 이벤트<br/>
        /// 피해량 = (1 + (방어력 - 관통력) * 0.01) * 피해량<br/>
        /// </summary>
        /// <param name="context">EventContext객체</param>
        /// <remarks>
        /// context.Grantee = 피해를 받은 유닛(Unit) <br/>
        /// context.DmgCtx = 피해 정보 (TakeDamageContext)
        /// </remarks>
        protected void DefaultTakeDamageEvent(EventContext context)
        {
            Unit self = context.Grantee;
            DamageContext dmgCtx = context.DmgCtx;
            if (dmgCtx == null || dmgCtx.IsCancelled)
            {
                currentCell?.UpdateUI();
                return;
            }
            float receivingDamageModifier = 1f;
            foreach (var effect in ActiveEffectObjects())
            {
                receivingDamageModifier *= effect.ReceivingDamageModifier(self);
            }
            
            bool canEvade = dmgCtx.CodeType != BaseEnums.CodeType.Effect;
            if (canEvade && UnityEngine.Random.value < self.EvasionChanceCurr)
            {
                currentCell.UpdateUI();
                Debug.Log($"{self.UnitName}이(가) 공격을 회피했습니다. 회피율: {self.EvasionChanceCurr * 100f:F1}%");
                return;
            }
            
            int damageReceived = self.CalculateDamageAfterDefense(dmgCtx, receivingDamageModifier);
            int hpBeforeHit = self.HpCurr;
            int shieldBeforeHit = self.ShieldCurr;
            
            // 방어막 처리
            bool hasShieldPenetration = dmgCtx.DamageTags.Contains(BaseClasses.DamageTag.ShieldPenetration);
            bool isDamageOverTime = dmgCtx.CodeType == BaseEnums.CodeType.Effect; // 지속피해 (맹독, 화상 등)
            
            if (self.ShieldCurr > 0 && !hasShieldPenetration && !isDamageOverTime)
            {
                // 방어막이 있고 관통이 아니고 지속피해가 아닌 경우
                if (self.ShieldCurr >= damageReceived)
                {
                    // 방어막이 데미지를 모두 흡수
                    self.RemoveShield(damageReceived); // RemoveShield 메서드 사용 (ShieldCurr=0이 되면 ShieldMax도 초기화)
                    damageReceived = 0;
                }
                else
                {
                    // 방어막이 일부만 흡수하고 나머지는 체력에서 차감
                    int remainingDamage = damageReceived - self.ShieldCurr;
                    self.RemoveShield(self.ShieldCurr); // 방어막 모두 소진 (자동으로 Max도 0으로)
                    self.HpCurr -= remainingDamage;
                }
                // 방어막 변경 후 시각 업데이트
                UpdateShieldBar();
            }
            else
            {
                // 방어막 무시하고 체력에서 직접 차감
                self.HpCurr -= damageReceived;
            }
            
            // Cell의 통합 UI 업데이트 메서드 사용
            currentCell.UpdateUI();

            int damageDealt = Mathf.Max(0, hpBeforeHit - self.HpCurr) + Mathf.Max(0, shieldBeforeHit - self.ShieldCurr);
            if (dmgCtx.Attacker != null && damageDealt > 0)
            {
                dmgCtx.Attacker.Invoke(
                    BaseEnums.UnitEventType.OnDamageDealt,
                    new DamageResolvedContext(dmgCtx.Attacker, self, dmgCtx, damageDealt));
            }
            
            // Debug.Log($"{self.UnitName}은(는) {dmgCtx.Attacker.UnitName}에게 {damageReceived}의 {(dmgCtx.IsCrit ? "치명" : "")}피해를 받았습니다. 체력: {hpBeforeHit} -> {self.HpCurr}");
            if (self.HpCurr <= 0)
            {
                self.Die(dmgCtx.Attacker);
            }
        }

        private int CalculateDamageAfterDefense(DamageContext dmgCtx, float receivingDamageModifier)
        {
            int defenseStat = GetDefenseStatForDamage(dmgCtx);
            float defenseStatMultiplier = dmgCtx.DefenseStatMultiplier;
            float outgoingDamageModifier = 1f;
            if (dmgCtx.Attacker != null)
            {
                foreach (var effect in dmgCtx.Attacker.ActiveEffectObjects())
                {
                    outgoingDamageModifier *= effect.OutgoingDamageModifier(dmgCtx.Attacker, this, dmgCtx);
                    defenseStatMultiplier *= effect.DefenseStatMultiplierModifier(dmgCtx.Attacker, this, dmgCtx);
                }
            }
            float scaledDefense = defenseStat * Mathf.Max(0f, defenseStatMultiplier);

            float defenseMultiplier = scaledDefense >= 0f
                ? 1f / (1f + scaledDefense * 0.01f)
                : 2f / (1f - scaledDefense * 0.01f);

            float flatReducedDamage = Mathf.Max(1f, dmgCtx.Damage * outgoingDamageModifier - scaledDefense);
            return Mathf.Max(1, Mathf.RoundToInt(flatReducedDamage * defenseMultiplier * receivingDamageModifier));
        }

        private int GetDefenseStatForDamage(DamageContext dmgCtx)
        {
            return DefCurr;
        }

        /// <summary>
        /// 라운드 시작 처리 이벤트
        /// </summary>
        protected void DefaultRoundStartEvent(EventContext context)
        {
            Debug.Log($"[라운드 시작] {UnitName}의 DefaultRoundStartEvent 호출됨");
            ResetCombatElements();
            CastPassiveCode();
        }

        /// <summary>
        /// 라운드 종료 처리 이벤트
        /// </summary>
        protected void DefaultRoundEndEvent(EventContext context)
        {
            // 상태를 라운드 종료 시 정리 (OnRemove 호출로 이벤트 리스너 등 해제)
            StatusController.ClearAll();
            ResetCombatElements();
            _combatResources.Clear();
            _combatResourceMaximums.Clear();
            ShieldMax = 0;   // 라운드 종료 시 방어막 최대치 초기화
            ShieldCurr = 0;  // 라운드 종료 시 방어막 현재치 초기화
            AttributesUpdate();
            UpdateShieldBar(); // 방어막 바 UI 업데이트 (비활성화)
        }
        
        /// <summary>
        /// 매 프레임 실행 이벤트
        /// </summary>
        protected void DefaultUpdateEvent(EventContext context)
        {
            // 상태 효과 틱 + 만료 상태 제거
            StatusController.Tick(context.FloatParam);
        }

        // ===== 스탯 계산 (UnitStats 위임) =====

        public int GetBasePrimaryStat(BaseEnums.PrimaryStat stat) => stats.GetBasePrimaryStat(stat);
        public int GetBaseStr() => stats.GetBaseStr();
        public int GetBaseDex() => stats.GetBaseDex();
        public int GetBaseCon() => stats.GetBaseCon();
        public int GetBaseInt() => stats.GetBaseInt();
        public int GetBaseLuk() => stats.GetBaseLuk();
        public int GetGrowthStatValue(BaseEnums.PrimaryStat stat) => stats.GetGrowthStatValue(stat);

        /// <summary>상태 효과의 5스탯 가산 보정 합계 (UnitStats에서 역참조)</summary>
        internal int GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat stat)
        {
            int total = 0;
            foreach (var effect in ActiveEffectObjects())
            {
                total += effect.PrimaryStatAdditiveModifier(this, stat);
            }

            return total;
        }

        /// <summary>상태 효과의 5스탯 배율 보정 적용 (UnitStats에서 역참조)</summary>
        internal int ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat stat, int value)
        {
            float multiplier = 1f;
            foreach (var effect in ActiveEffectObjects())
            {
                multiplier *= effect.PrimaryStatMultiplierModifier(this, stat);
            }
            return Mathf.Max(0, Mathf.RoundToInt(value * multiplier));
        }

        public int GetDerivedHp() => stats.GetDerivedHp();
        public int GetDerivedMana() => stats.GetDerivedMana();

        public int GetUltimateResourceMax()
        {
            return UltimateResourceType == BaseEnums.UltimateResourceType.Mana
                ? stats.GetDerivedMana()
                : Mathf.Max(1, UltimateResourceMax);
        }

        public int GetDerivedAtk() => stats.GetDerivedAtk();
        public int GetDerivedDef() => stats.GetDerivedDef();
        public float GetDerivedCritChance() => stats.GetDerivedCritChance();
        public float GetDerivedCritDamage() => stats.GetDerivedCritDamage();
        public float GetDerivedCodeAcceleration() => stats.GetDerivedCodeAcceleration();
        public float GetDerivedAttackSpeed() => stats.GetDerivedAttackSpeed();
        public float GetDerivedEvasionChance() => stats.GetDerivedEvasionChance();
        public float GetDerivedHealingBonus() => stats.GetDerivedHealingBonus();
        public float GetDerivedShieldBonus() => stats.GetDerivedShieldBonus();
        public float GetDerivedManaEfficiency() => stats.GetDerivedManaEfficiency();
        public float GetDerivedCodeActivationChance() => stats.GetDerivedCodeActivationChance();

        // 유닛 활성화 상태 관리
        public void ActivateUnit()
        {
            isActive = true;
            currentCell.isOccupied = true;
            
            // Cell의 통합 UI 관리 사용
            currentCell.SetOccupiedUnit(this);
        }

        public void DeactivateUnit()
        {
            isActive = false;
            ID = 0;
            currentCell.isOccupied = false;
            currentCell.portraitRenderer.sprite = null;
            
            // Cell의 통합 UI 관리 사용
            currentCell.SetOccupiedUnit(null);
            
            currentCell.reservedTime = 2f;
            currentNormalTarget = null; // 타겟 초기화
        }

        // 유닛 상태효과 관리
        public void NotifyBeneficialEffectReceived(Unit grantor)
        {
            Invoke(BaseEnums.UnitEventType.OnBeneficialEffectReceived, new EventContext(this, grantor));
            if (grantor != null)
            {
                grantor.Invoke(BaseEnums.UnitEventType.OnBeneficialEffectGranted, new EventContext(grantor, this));
            }
        }

        // InfoTab 업데이트 헬퍼 메서드
        private void UpdateInfoTabIfShowing()
        {
            var gameManager = Managers.GameManager.Instance;
            if (gameManager != null && gameManager.uiManager != null && 
                gameManager.uiManager.infoTab != null && 
                gameManager.uiManager.infoTab.gameObject.activeInHierarchy)
            {
                // InfoTab이 열려있고 현재 유닛이 표시되고 있다면 자동으로 업데이트됨 (Update 메서드에서 처리)
                // 별도 호출 불필요 - InfoTab 자체에서 지속적으로 업데이트
            }
        }

        // 이벤트 리스너 관리
        // 각 UnitEventType은 하나의 컨텍스트 타입(Action<T>)만 사용한다. 서로 다른 T를
        // 같은 이벤트에 섞어 등록/발행하면 과거에는 InvalidCastException으로 크래시했으나,
        // 아래 메서드들은 타입이 어긋나면 크래시 대신 오류 로그를 남기고 무시한다.
        public void AddListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (action == null) return;

            if (!_eventDict.TryGetValue(eventType, out var existing) || existing == null)
            {
                _eventDict[eventType] = action;
                return;
            }

            if (existing is Action<T> typed)
            {
                _eventDict[eventType] = typed + action;
            }
            else
            {
                Debug.LogError($"[Unit] 이벤트 {eventType}에 이미 {existing.GetType()} 리스너가 등록되어 있어 {typeof(Action<T>)} 리스너를 추가할 수 없습니다.");
            }
        }

        public void RemoveListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (action == null) return;

            if (_eventDict.TryGetValue(eventType, out var existing) && existing is Action<T> typed)
            {
                _eventDict[eventType] = typed - action;
            }
        }

        public void RemoveAllListeners(BaseEnums.UnitEventType eventType)
        {
            _eventDict[eventType] = null;
        }

        public void Invoke<T>(BaseEnums.UnitEventType eventType, T context)
        {
            if (!_eventDict.TryGetValue(eventType, out var value) || value == null) return;

            if (value is Action<T> typed)
            {
                typed.Invoke(context);
            }
            else
            {
                Debug.LogError($"[Unit] 이벤트 {eventType} 발행 타입 {typeof(T)}이(가) 등록된 리스너 타입 {value.GetType()}과(와) 일치하지 않습니다.");
            }
        }
        
        // ===== Status 시스템 메서드들 (UnitStatusController 위임) =====

        /// <summary>상태 추가 - 중첩 정책에 따라 처리</summary>
        public void AddStatus(Status.UnitStatus status)
        {
            StatusController.Add(status);
            UpdateInfoTabIfShowing();
        }

        /// <summary>상태 ID로 상태 제거 (가장 오래된 것 하나만 제거)</summary>
        public void RemoveStatus(int statusId)
        {
            StatusController.Remove(statusId);
        }

        /// <summary>특정 상태를 보유 중인지 확인</summary>
        public bool HasStatus(int statusId)
        {
            return StatusController.Has(statusId);
        }

        /// <summary>키로 상태 보유 여부 확인 (시전자별 키 등 문자열 키 기반 상태용)</summary>
        public bool HasStatusKey(string key)
        {
            return StatusController.HasKey(key);
        }

        /// <summary>키로 상태 제거 (일치하는 모든 상태 제거)</summary>
        public void RemoveStatusByKey(string key)
        {
            StatusController.RemoveByKey(key);
        }

        /// <summary>상태 가져오기 (가장 오래된 것 반환)</summary>
        public Status.UnitStatus GetStatus(int statusId)
        {
            return StatusController.Get(statusId);
        }

        /// <summary>특정 StatusId의 모든 상태 가져오기</summary>
        public List<Status.UnitStatus> GetAllStatuses(int statusId)
        {
            return StatusController.GetAll(statusId);
        }

        /// <summary>모든 상태 목록 반환 (복사본)</summary>
        public List<Status.UnitStatus> GetAllStatuses()
        {
            return StatusController.GetAll();
        }

        /// <summary>모든 상태 가져오기 (UI 표시용)</summary>
        public List<Status.UnitStatus> GetStatuses()
        {
            return StatusController.GetLive();
        }

        /// <summary>현재 보유한 상태 중 지속피해 효과가 하나라도 있는지 확인한다.</summary>
        public bool HasDamageOverTimeStatus()
        {
            return ActiveEffectObjects().Any(effect => effect.IsDamageOverTime);
        }

        /// <summary>현재 지속피해 효과를 1초간 정산한 예상 피해 총합.</summary>
        public int GetEstimatedDamageOverTimePerSecond()
        {
            return ActiveEffectObjects()
                .Where(effect => effect.IsDamageOverTime)
                .Sum(effect => Mathf.Max(0, effect.EstimateDamagePerSecond()));
        }

        /// <summary>이 유닛이 부여하는 지속피해량 배율.</summary>
        public float GetDamageOverTimeApplicationMultiplier()
        {
            float multiplier = 1f;
            foreach (var effect in ActiveEffectObjects())
            {
                multiplier *= effect.DamageOverTimeApplicationMultiplier(this);
            }
            return Mathf.Max(0f, multiplier);
        }
    }
}
