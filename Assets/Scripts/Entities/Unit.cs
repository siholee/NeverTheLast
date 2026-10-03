using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Helpers;
using Managers;
using UnityEngine;

namespace Entities
{
    /// <summary>
    /// 전장의 유닛 한 명 — 아군이든 적이든 같은 클래스다.
    ///
    /// 2026-09 전면 개편으로 다시 썼다. 바뀐 뼈대:
    ///   · 스탯은 여덟(<see cref="BaseEnums.StatType"/>). 기초값 + 성장치 × (레벨 − 1), 훈련 강화는 없다.
    ///   · 피해는 <b>공격자 레벨 기준</b> 경감 — 받는 피해 = 피해 × K / (K + 방어 스탯).
    ///     대부분의 피해는 방어력, 지속피해 · 원소 반응은 특수방어력, 고정 피해는 경감 없음.
    ///   · 궁극기 마나는 <b>행동과 피격</b>으로 찬다(일반행동 +20 · 피격 +5 · 처치 +10).
    ///   · 옛 캐릭터 전용 체계(소환수 · 강인도 · 내구 · 중량 · 시약 · 레벨 해금 패시브)는 걷어냈다.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        /// <summary>모든 진영의 사망을 관찰하는 전역 이벤트. (쓰러진 쪽, 공격자)</summary>
        public static event Action<Unit, Unit> AnyUnitDied;
        /// <summary>해로운 상태가 실제로 적용되거나 지속시간이 갱신된 순간. (부여자, 대상, 상태)</summary>
        public static event Action<Unit, Unit, Status.UnitStatus> AnyNegativeStatusGranted;
        /// <summary>어느 유닛이든 실제 피해를 가한 순간.</summary>
        public static event Action<DamageResolvedContext> AnyDamageDealt;
        /// <summary>
        /// 어느 유닛이든 피해를 받기 <b>직전</b>(경감 · 보호막 처리 전). (피격자, 피해 정보)
        /// 보호막이 흡수하기 전 상태를 봐야 하는 효과(세이의 '빅뱅')가 듣는다.
        /// </summary>
        public static event Action<Unit, DamageContext> AnyDamageIncoming;
        /// <summary>원소 반응이 일어났음을 알린다. (반응을 일으킨 쪽, 대상, 반응 이름)</summary>
        public static event Action<Unit, Unit, string> AnyElementalReaction;
        /// <summary>원소가 부착될 때 발행한다. (부여자, 대상, 부여 원소)</summary>
        public static event Action<Unit, Unit, BaseEnums.UnitElement> AnyCombatElementGranted;

        // ── 궁극기 마나 ─────────────────────────────────────────────
        // 스타레일처럼 행동과 피격이 마나를 만든다. 전투 시간 비례 충전(INT)은 폐지했다.
        // 결과적으로 속도가 궁극기 주기를 정하고, manaMax가 유닛별 손잡이가 된다.

        /// <summary>일반행동(대체행동 포함)이 실제로 끝났을 때 얻는 마나.</summary>
        public const int ManaPerNormalAction = 20;
        /// <summary>피해를 받을 때 얻는 마나. 지속피해는 세지 않는다.</summary>
        public const int ManaPerHitTaken = 5;
        /// <summary>적을 처치했을 때 얻는 마나.</summary>
        public const int ManaPerKill = 10;
        /// <summary>유닛 데이터가 최대 마나를 적지 않았을 때 쓰는 값.</summary>
        public const int DefaultManaMax = 100;

        /// <summary>원소 부착 기본 지속 <b>턴</b> 수. 부착된 유닛의 턴으로 센다.</summary>
        public const int CommonElementAuraDuration = 5;

        // 레벨업 EXP 곡선: Lv N → N+1 에 필요한 EXP = Base + PerLevel × (N − 1)
        public const int ExpBaseRequirement = 100;
        public const int ExpRequirementPerLevel = 28;

        public bool isActive;

        // ── 식별 ─────────────────────────────────────────────────
        [SerializeField] private int id;
        [SerializeField] private bool isEnemy;
        [SerializeField] private string unitName;
        [SerializeField] private string element;
        [SerializeField] private string vfxColor;
        [SerializeField] private string vfxClass;
        [SerializeField] private List<string> unitTags = new();
        [SerializeField] private List<string> archetypeTags = new();
        // 적 등급(normal / elite / boss). 아군은 비어 있다.
        [SerializeField] private string unitTier = "";
        [SerializeField] private int level = 1;
        [SerializeField] private int exp;
        [SerializeField] private int roundDamageDealt;
        [SerializeField] private int untargetableSourceCount;

        public int ID { get => id; protected set => id = value; }
        public bool IsEnemy { get => isEnemy; protected set => isEnemy = value; }
        public string UnitName { get => unitName; protected set => unitName = value; }
        public string Element { get => element; protected set => element = value; }
        public string VfxColorHex => vfxColor ?? "";
        public string VfxClass => vfxClass ?? "";
        public IReadOnlyList<string> UnitTags => unitTags;
        /// <summary>전투 성향 UI 태그(신속 · 지원 · 방어 등). 적은 비어 있다.</summary>
        public IReadOnlyList<string> ArchetypeTags => archetypeTags;
        /// <summary>데이터가 정한 선제 반격 기회의 기초 최대치. 보정이 생겨도 코드에 상한을 두지 않는다.</summary>
        public int CounterUsesBase { get; private set; }
        public bool HasArchetypeTag(string tag)
            => !string.IsNullOrWhiteSpace(tag) && archetypeTags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
        /// <summary>적 등급. 아군은 빈 문자열이다.</summary>
        public string UnitTier => unitTier ?? "";
        /// <summary>유닛 레벨. 적은 현재 스테이지가, 아군은 누적 EXP가 결정한다.</summary>
        public int Level { get => level; protected set => level = Mathf.Max(1, value); }
        public int Exp { get => exp; protected set => exp = value; }
        public bool IsUntargetable => untargetableSourceCount > 0;
        /// <summary>이번 라운드에 이 유닛이 실제로 가한 누적 피해.</summary>
        public int RoundDamageDealt { get => roundDamageDealt; private set => roundDamageDealt = value; }

        /// <summary>서 있는 칸. 대기석이면 yPos가 0 이하다.</summary>
        public Cell currentCell;
        public bool IsBench => currentCell != null && currentCell.yPos <= 0;

        /// <summary>
        /// <b>전장에 서 있는가.</b> 대상 지정 · 행동 순서 · 필드 판정이 모두 이 하나를 본다.
        /// 대상 지정 가능 여부(<see cref="IsUntargetable"/>)는 따로 본다.
        /// </summary>
        public bool IsOnField => isActive && currentCell != null && currentCell.yPos > 0 && currentCell.isOccupied;

        // ── 스탯 ─────────────────────────────────────────────────
        [SerializeField] private UnitStats stats = new();
        public UnitStats Stats => stats;

        // 레벨과 상태 효과를 반영한 최종 스탯 캐시. AttributesUpdate가 채운다.
        private readonly float[] _finalStats = new float[UnitStats.StatCount];

        [SerializeField] private int hpCurr;
        [SerializeField] private int hpMax;
        [SerializeField] private int manaCurr;
        [SerializeField] private int manaMax = DefaultManaMax;
        [SerializeField] private int manaMaxBase = DefaultManaMax;
        [SerializeField] private BaseEnums.UltimateResourceType ultimateResourceType = BaseEnums.UltimateResourceType.Mana;
        [SerializeField] private string ultimateResourceName = "마나";
        [SerializeField] private int shieldMax;
        [SerializeField] private int shieldCurr;

        public int HpCurr { get => hpCurr; protected set => hpCurr = value; }
        public int HpMax { get => hpMax; protected set => hpMax = value; }
        public int ManaCurr { get => manaCurr; protected set => manaCurr = value; }
        public int ManaMax { get => manaMax; protected set => manaMax = value; }
        public BaseEnums.UltimateResourceType UltimateResourceType => ultimateResourceType;
        public string UltimateResourceName => ultimateResourceName;
        public int ShieldMax { get => shieldMax; protected set => shieldMax = value; }
        public int ShieldCurr { get => shieldCurr; protected set => shieldCurr = value; }

        /// <summary>레벨과 상태 효과를 반영한 최종 스탯.</summary>
        public float StatValue(BaseEnums.StatType stat) => _finalStats[(int)stat];

        /// <summary>레벨만 반영한 기초 스탯(효과 전). 캐릭터 시트의 "기초"에 쓴다.</summary>
        public float BaseStat(BaseEnums.StatType stat) => stats.LeveledBase(stat);

        public int AtkCurr => Mathf.RoundToInt(StatValue(BaseEnums.StatType.Attack));
        public int SpAtkCurr => Mathf.RoundToInt(StatValue(BaseEnums.StatType.SpecialAttack));
        public int DefCurr => Mathf.RoundToInt(StatValue(BaseEnums.StatType.Defense));
        public int SpDefCurr => Mathf.RoundToInt(StatValue(BaseEnums.StatType.SpecialDefense));
        public float SpeedCurr => Mathf.Max(1f, StatValue(BaseEnums.StatType.Speed));
        public float CritChanceCurr => Mathf.Clamp01(StatValue(BaseEnums.StatType.CritRate));
        /// <summary>치명타가 터졌을 때 곱하는 배율 = 1 + 치명타 피해.</summary>
        public float CritMultiplierCurr => 1f + Mathf.Max(0f, StatValue(BaseEnums.StatType.CritDamage));
        public float ManaEfficiencyCurr { get; private set; } = 1f;

        public bool isCasting; // 코드 시전 중
        public bool isControlled; // 행동 불가 상태
        /// <summary>남은 행동 불가 <b>턴</b> 수. 자기 턴이 올 때마다 1씩 줄고 그 턴의 행동을 건너뛴다.</summary>
        public int controlTurns;

        /// <summary>일반행동이 지금 노리고 있는 대상. 우선도가 같으면 이 대상을 계속 친다.</summary>
        public Unit currentNormalTarget;

        // 타겟팅 우선도 (-3 ~ 3, 높을수록 먼저 노려진다)
        [SerializeField] private int priority;
        public int Priority
        {
            get
            {
                // 양 진영 모두 중앙에 가까운 열(|x| == 1)이 전열이다. 전열에 선 것 자체가 우선도 +1이다.
                int frontline = currentCell != null && currentCell.yPos > 0 && Mathf.Abs(currentCell.xPos) == 1 ? 1 : 0;
                int modifier = frontline + ActiveEffectObjects().Sum(effect => effect.TargetPriorityAdditiveModifier(this));
                return Mathf.Clamp(priority + modifier, -3, 3);
            }
            set => priority = Mathf.Clamp(value, -3, 3);
        }

        // ── 코드 · 상태 ─────────────────────────────────────────────
        protected readonly UnitStatusController StatusController = new();
        /// <summary>[0] = 고유 특성. 이후는 사건 · 보상으로 얻은 영구 패시브.</summary>
        protected List<PassiveCode> PassiveCodes = new();
        protected NormalCode NormalCode;
        protected SpecialCode SpecialCode;
        protected UltimateCode UltimateCode;
        [SerializeField] private List<int> grantedPassiveCodeIds = new();

        public NormalCode ActiveNormalCode => NormalCode;
        public SpecialCode ActiveSpecialCode => SpecialCode;
        public UltimateCode ActiveUltimateCode => UltimateCode;
        public bool HasSpecialAction => SpecialCode != null;
        /// <summary>상태 효과에 의해 이 유닛을 소환수로 간주하는가.</summary>
        public bool CountsAsSummon => ActiveEffectObjects().Any(effect => effect.CountsAsSummon(this));
        public IReadOnlyList<PassiveCode> ActivePassiveCodes => PassiveCodes;
        public IReadOnlyList<int> GrantedPassiveCodeIds => grantedPassiveCodeIds;
        /// <summary>현재 걸린 상태(버프/디버프) 목록. HUD가 읽는다.</summary>
        public IReadOnlyList<Status.UnitStatus> ActiveStatuses => StatusController.GetLive();

        // ── 원소 ─────────────────────────────────────────────────
        private readonly HashSet<BaseEnums.UnitElement> _combatElements = new();
        /// <summary>실제로 부착되지는 않았지만 <b>판정만</b> 받는 원소(유닛의 고유 원소 등).</summary>
        private readonly HashSet<BaseEnums.UnitElement> _judgementElements = new();
        private readonly Dictionary<BaseEnums.UnitElement, int> _temporaryElementDurations = new();

        // ── 고유 게이지(전투 자원) ─────────────────────────────────
        // 세이의 '창세의 결정'처럼 코드가 쓰는 이름 붙은 중첩. 카드가 궁극기 링 옆에 그린다.
        private readonly Dictionary<string, int> _combatResources = new();
        private readonly Dictionary<string, int> _combatResourceMaximums = new();
        // Dictionary의 열거 순서는 보장되지 않는다. "대표 자원 하나"를 뽑는 UI가 흔들리지 않게 순서를 따로 든다.
        private readonly List<string> _combatResourceOrder = new();
        public IReadOnlyList<string> CombatResourceIds => _combatResourceOrder;

        private int _baseNormalCodeId;
        private int _baseUltimateCodeId;

        // 이벤트
        private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict = new();

        /// <summary>
        /// 초상화 스프라이트를 가리키는 값. <b>해석에 성공하면 전체 경로, 실패하면 키 그대로</b>다.
        /// 어느 쪽이든 <see cref="SpriteResource.LoadPortrait(string)"/>에 그대로 넘길 수 있다.
        /// </summary>
        public string PortraitPath;

        private void Awake()
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            InitializeStatusController();
        }

        private void InitializeStatusController()
        {
            stats.Initialize(this);
            StatusController.Initialize(this, AttributesUpdate, NotifyBeneficialEffectReceived, NotifyNegativeStatusGranted);
        }

        // ══════════════════════════════════════════════════════
        // 생성
        // ══════════════════════════════════════════════════════

        public virtual void Spawn(Cell cell, bool _isEnemy, int _id)
        {
            ActivateUnit();
            InitializeUnit(_isEnemy, _id);
            Invoke(BaseEnums.UnitEventType.OnSpawn, new EventContext(this));
        }

        public virtual void InitializeUnit(bool _isEnemy, int _id)
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            InitializeStatusController();
            PassiveCodes = new List<PassiveCode>();
            grantedPassiveCodeIds = new List<int>();
            unitTags = new List<string>();
            archetypeTags = new List<string>();
            unitTier = "";
            vfxColor = "";
            vfxClass = "";
            CounterUsesBase = 0;
            _combatElements.Clear();
            _judgementElements.Clear();
            _temporaryElementDurations.Clear();
            _combatResources.Clear();
            _combatResourceMaximums.Clear();
            _combatResourceOrder.Clear();
            untargetableSourceCount = 0;
            ID = _id;
            IsEnemy = _isEnemy;
            if (_id == 0) return;

            LoadData(_isEnemy, _id);
            AttributesUpdate();
            HpCurr = HpMax;
            currentCell?.InitializeHpBar();

            ManaCurr = ultimateResourceType == BaseEnums.UltimateResourceType.Passive ? ManaMax : 0;
            ShieldMax = 0;
            ShieldCurr = 0;
            RefreshView();
            AddListener<EventContext>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, _ => RecoverMana(ManaPerNormalAction));
        }

        protected virtual void LoadData(bool _isEnemy, int _id)
        {
            CombatantData data;
            if (_isEnemy)
            {
                EnemyData enemyData = GameManager.Instance.dataManager.FetchEnemyDataList()?.enemies?
                    .FirstOrDefault(e => e.id == _id);
                if (enemyData == null)
                {
                    Debug.LogError($"Enemy data not found for ID: {_id}");
                    return;
                }
                data = enemyData;
                Level = GameManager.Instance?.RoundManager?.EnemyLevel ?? 1;
                unitTier = enemyData.tier ?? "";
            }
            else
            {
                UnitData unitData = GameManager.Instance.unitDataList.units.FirstOrDefault(e => e.id == _id);
                if (unitData == null)
                {
                    Debug.LogError($"Unit data not found for ID: {_id}");
                    return;
                }
                data = unitData;
                Level = 1;
                archetypeTags = unitData.archetypeTags != null ? new List<string>(unitData.archetypeTags) : new List<string>();
                CounterUsesBase = Mathf.Max(0, unitData.counterUsesBase);
            }

            LoadSprite(data.portrait);
            UnitName = data.name;
            Element = string.IsNullOrWhiteSpace(data.element) ? "None" : data.element;
            vfxColor = data.vfxColor ?? "";
            vfxClass = data.vfxClass ?? "";
            ResetCombatElements();
            unitTags = data.tags != null ? new List<string>(data.tags) : new List<string>();
            stats.Load(data);
            manaMaxBase = data.manaMax > 0 ? data.manaMax : DefaultManaMax;
            ultimateResourceType = Enum.TryParse(data.ultimateResourceType, true,
                out BaseEnums.UltimateResourceType parsedResourceType)
                ? parsedResourceType
                : BaseEnums.UltimateResourceType.Mana;
            ultimateResourceName = string.IsNullOrWhiteSpace(data.ultimateResourceName)
                ? (ultimateResourceType == BaseEnums.UltimateResourceType.Stack ? "중첩" :
                    ultimateResourceType == BaseEnums.UltimateResourceType.Passive ? "상시" : "마나")
                : data.ultimateResourceName.Trim();

            isCasting = false;
            isControlled = false;
            controlTurns = 0;
            currentNormalTarget = null;

            PassiveCode innate = CodeFactory.CreatePassiveCode(data.CodeId("passive"), new PassiveCodeContext { Caster = this });
            if (innate != null) PassiveCodes.Add(innate);
            _baseNormalCodeId = data.CodeId("normal");
            _baseUltimateCodeId = data.CodeId("ultimate");
            NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
            SpecialCode = CodeFactory.CreateSpecialCode(data.CodeId("special"), new SpecialCodeContext { Caster = this });
            UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
        }

        protected virtual void LoadSprite(string key)
        {
            Sprite portrait = SpriteResource.LoadPortrait(key, out string path);
            PortraitPath = path;
            currentCell?.SetPortrait(portrait);
        }

        // ══════════════════════════════════════════════════════
        // 스탯 계산
        // ══════════════════════════════════════════════════════

        protected IEnumerable<BaseEffect> ActiveEffectObjects() => StatusController.ActiveEffectObjects();

        /// <summary>
        /// 최종 스탯을 다시 계산한다. 상태 · 레벨이 바뀔 때만 부른다.
        /// <b>체력 비율을 보존한다</b> — 최대 체력 배율이 붙으면 현재 체력도 같이 늘어난다.
        /// </summary>
        protected virtual void AttributesUpdate()
        {
            List<BaseEffect> effects = ActiveEffectObjects().ToList();
            for (int i = 0; i < UnitStats.StatCount; i++)
            {
                var stat = (BaseEnums.StatType)i;
                float baseValue = stats.LeveledBase(stat);
                float percent = 0f;
                float flat = 0f;
                foreach (BaseEffect effect in effects)
                {
                    percent += effect.StatPercentModifier(this, stat);
                    flat += effect.StatFlatModifier(this, stat);
                }

                _finalStats[i] = UnitStats.IsPercentScalable(stat)
                    ? Mathf.Max(0f, baseValue * (1f + percent) + flat)
                    : Mathf.Max(0f, baseValue + flat);
            }

            float healthRatio = HpMax > 0 ? (float)HpCurr / HpMax : 1f;
            HpMax = Mathf.Max(1, Mathf.RoundToInt(_finalStats[(int)BaseEnums.StatType.MaxHp]));
            HpCurr = Mathf.RoundToInt(HpMax * healthRatio);
            ManaMax = Mathf.Max(1, manaMaxBase);

            float manaMultiplier = 1f;
            foreach (BaseEffect effect in effects) manaMultiplier *= effect.ManaRecoveryMultiplierModifier(this);
            ManaEfficiencyCurr = Mathf.Max(0f, manaMultiplier);
            RefreshView();
        }

        /// <summary>파생 스탯 캐시를 즉시 갱신한다. 코드가 들고 있는 동적 수치가 바뀌었을 때 부른다.</summary>
        public void RefreshAttributes() => AttributesUpdate();

        // ══════════════════════════════════════════════════════
        // 레벨
        // ══════════════════════════════════════════════════════

        /// <summary>다음 레벨까지 필요한 EXP.</summary>
        public static int RequiredExpForLevel(int currentLevel)
            => ExpBaseRequirement + ExpRequirementPerLevel * Mathf.Max(0, currentLevel - 1);

        public int ExpToNextLevel => RequiredExpForLevel(Level);

        /// <summary>EXP를 지급하고 필요량을 넘으면 레벨을 올린다. 적은 스테이지가 레벨을 정하므로 받지 않는다.</summary>
        public int AddExp(int amount)
        {
            if (IsEnemy || amount <= 0) return 0;

            exp += amount;
            int gained = 0;
            while (exp >= ExpToNextLevel)
            {
                exp -= ExpToNextLevel;
                Level += 1;
                gained++;
            }

            if (gained > 0) ApplyLevelChange(Level - gained, "EXP");
            RefreshView();
            return gained;
        }

        /// <summary>EXP를 건너뛰고 레벨을 직접 올린다. 모아 둔 EXP는 그대로 둔다.</summary>
        public int GrantLevels(int levels)
        {
            if (IsEnemy || levels <= 0) return 0;
            int before = Level;
            Level += levels;
            ApplyLevelChange(before, "보상");
            return Level - before;
        }

        /// <summary>레벨이 바뀌었다. 늘어난 최대 체력만큼 현재 체력도 함께 올려 준다.</summary>
        private void ApplyLevelChange(int previousLevel, string reason)
        {
            int previousHpMax = HpMax;
            int previousHp = HpCurr;
            AttributesUpdate();
            HpCurr = Mathf.Clamp(previousHp + Mathf.Max(0, HpMax - previousHpMax), 1, HpMax);
            RefreshView();
            Debug.Log($"[레벨업] {UnitName} Lv.{previousLevel} → Lv.{Level} ({reason})");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>디버그 전용 — 레벨을 원하는 값으로 바로 맞춘다.</summary>
        public void DebugSetLevel(int targetLevel)
        {
            Core.DebugMode.BeginSession();
            int before = Level;
            Level = targetLevel;
            exp = 0;
            ApplyLevelChange(before, "디버그");
        }

        public void DebugResetCombatState()
        {
            StopAllCoroutines();
            NormalCode?.StopCode();
            SpecialCode?.StopCode();
            UltimateCode?.StopCode();
            Invoke(BaseEnums.UnitEventType.OnRoundEnd, new EventContext(this));
            foreach (PassiveCode code in PassiveCodes.ToList()) code?.StopCode();
            isCasting = false;
            isControlled = false;
            controlTurns = 0;
            currentNormalTarget = null;
        }

        public void DebugClearStatuses() => StatusController.ClearAll();
#endif

        public bool HasUnitTag(string tag)
            => !string.IsNullOrWhiteSpace(tag) && unitTags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));

        // ══════════════════════════════════════════════════════
        // 패시브
        // ══════════════════════════════════════════════════════

        /// <summary>사건 · 보상으로 얻은 런 영구 패시브를 추가한다. 고유 특성은 받을 수 없다.</summary>
        public bool GrantPermanentPassive(int codeId)
        {
            if (codeId <= 0 || grantedPassiveCodeIds.Contains(codeId)) return false;
            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = this });
            if (code == null || code.IsUniquePassive) return false;
            PassiveCodes.Add(code);
            grantedPassiveCodeIds.Add(codeId);
            return true;
        }

        public bool HasLearnedPassiveCode(int codeId) => PassiveCodes.Any(code => code != null && code.CatalogId == codeId);

        public virtual void CastPassiveCode()
        {
            foreach (PassiveCode passiveCode in PassiveCodes)
            {
                if (passiveCode == null) continue;
                if (passiveCode.SupersededByCodeId > 0 && HasLearnedPassiveCode(passiveCode.SupersededByCodeId))
                {
                    Debug.Log($"{UnitName}의 {passiveCode.CodeName}은(는) 강화 등급 코드에 대체되어 발동하지 않는다");
                    continue;
                }
                passiveCode.CastCode();
            }
            Invoke(BaseEnums.UnitEventType.OnPassiveActivates, new EventContext(this));
        }

        // ══════════════════════════════════════════════════════
        // 행동
        // ══════════════════════════════════════════════════════

        public virtual void CastNormalCode()
        {
            NormalCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnNormalActivates, new EventContext(this));
        }

        public virtual void CastUltimateCode()
        {
            // 예약과 실행 사이에 앞선 행동이 마지막 적을 지웠을 수 있다. 자원을 먼저 태우면 한 번을 통째로 잃는다.
            if (UltimateCode == null) return;
            if (!UltimateCode.HasValidTarget())
            {
                Debug.Log($"{UnitName}의 궁극기 {UltimateCode.CodeName}은(는) 대상이 없어 자원을 유지한 채 취소한다");
                return;
            }

            if (ultimateResourceType != BaseEnums.UltimateResourceType.Passive) ManaCurr = 0;
            RefreshView();
            UltimateCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnUltimateActivates, new EventContext(this));
        }

        public virtual void CastSpecialCode()
        {
            if (SpecialCode == null || !SpecialCode.HasValidTarget())
            {
                Combat.SpecialAction.NotifyResolved(this);
                return;
            }

            SpecialCode.CastCode();
        }

        public bool CanCastUltimateCode()
            => ultimateResourceType != BaseEnums.UltimateResourceType.Passive && ManaCurr >= ManaMax;

        /// <summary>일반행동을 막는 효과가 있는가. 스케줄러가 일반행동 예약 전에 본다.</summary>
        public bool IsNormalAttackBlocked => ActiveEffectObjects().Any(effect => effect.BlocksNormalAttack(this));

        /// <summary>마나를 회복한다. 효과의 회복 배율을 받는다.</summary>
        public virtual void RecoverMana(int amount)
        {
            // 스택형 자원은 고유 특성의 명시적 AddUltimateResource만 받는다.
            // 공용 행동·피격·처치 마나가 섞이면 7중첩 계약이 일반행동 한 번에 끝나 버린다.
            if (ultimateResourceType != BaseEnums.UltimateResourceType.Mana || amount <= 0 || !isActive) return;
            AddUltimateResource(Mathf.Max(0, Mathf.RoundToInt(amount * ManaEfficiencyCurr)));
        }

        /// <summary>마나를 배율 없이 그대로 더한다(음수면 깎는다).</summary>
        public virtual void AddUltimateResource(int amount)
        {
            if (ultimateResourceType == BaseEnums.UltimateResourceType.Passive)
            {
                ManaCurr = ManaMax;
                RefreshView();
                return;
            }
            int before = ManaCurr;
            ManaCurr = Mathf.Clamp(ManaCurr + amount, 0, ManaMax);
            foreach (BaseEffect effect in ActiveEffectObjects().ToList()) effect.OnUltimateResourceChanged(this);
            if (before < ManaMax && ManaCurr >= ManaMax) AudioManager.PlayCue("common.ultimate.ready");
            RefreshView();
        }

        public void FillUltimateResource() => AddUltimateResource(Mathf.Max(0, ManaMax - ManaCurr));

        /// <summary>전투 밖의 긴 휴식이 궁극기 자원을 완전히 되돌린다.</summary>
        public void RestoreUltimateResourceOutsideCombat()
        {
            // 긴 휴식은 마나만 채운다. 조건을 달성해 쌓는 고유 중첩은 새로 주지 않는다.
            ManaCurr = ultimateResourceType == BaseEnums.UltimateResourceType.Passive ? ManaMax :
                ultimateResourceType == BaseEnums.UltimateResourceType.Mana ? ManaMax : 0;
            RefreshView();
        }

        /// <summary>마나를 비운다. 테마(라운드)가 바뀔 때 궁극기를 새로 모으게 한다.</summary>
        public void ClearUltimateResource() => AddUltimateResource(-ManaCurr);

        // ══════════════════════════════════════════════════════
        // 피해
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피해를 받는다. <c>OnBeforeDamageTaken → OnTakingDamage(경감 · 보호막 · 체력) → OnAfterDamageTaken</c>.
        /// </summary>
        public virtual void TakeDamage(DamageContext context)
        {
            if (context == null) return;
            // 광역 공격은 같은 컨텍스트를 여러 대상에게 재사용하므로 대상마다 초기화한다.
            context.ResolvedDamage = 0;
            if (!isActive || IsUntargetable)
            {
                context.IsCancelled = true;
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 디버그 무적. 지속피해 · 고정피해까지 한 곳에서 막으려면 입구에서 잘라야 한다.
            if (IsEnemy ? Core.DebugMode.EnemyInvincible : Core.DebugMode.AllyInvincible)
            {
                context.IsCancelled = true;
                return;
            }
#endif
            AnyDamageIncoming?.Invoke(this, context);
            Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, new EventContext(this, context.Attacker, context));
            Invoke(BaseEnums.UnitEventType.OnTakingDamage, new EventContext(this, null, context));
            Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, new EventContext(this, context.Attacker, context));
        }

        protected void DefaultTakeDamageEvent(EventContext context)
        {
            DamageContext dmgCtx = context.DmgCtx;
            if (dmgCtx == null || dmgCtx.IsCancelled || !isActive)
            {
                RefreshView();
                return;
            }

            int damage = CalculateFinalDamage(dmgCtx);
            int hpBefore = HpCurr;
            int shieldBefore = ShieldCurr;

            // 지속피해와 관통 공격은 보호막을 건너뛴다.
            bool bypassShield = dmgCtx.CodeType == BaseEnums.CodeType.Effect ||
                                dmgCtx.HasTag(DamageTag.ShieldPenetration);
            if (ShieldCurr > 0 && !bypassShield)
            {
                int absorbed = Mathf.Min(ShieldCurr, damage);
                RemoveShield(absorbed);
                damage -= absorbed;
            }
            HpCurr -= damage;

            if (HpCurr <= 0)
            {
                foreach (BaseEffect effect in ActiveEffectObjects().ToList())
                {
                    if (!effect.TryPreventDeath(this, dmgCtx.Attacker)) continue;
                    HpCurr = Mathf.Max(1, HpCurr);
                    break;
                }
            }

            RefreshView();

            int hpLost = Mathf.Max(0, hpBefore - HpCurr);
            int shieldLost = Mathf.Max(0, shieldBefore - ShieldCurr);
            int damageDealt = hpLost + shieldLost;
            dmgCtx.ResolvedDamage = damageDealt;

            // 맞은 쪽의 반응(숫자 · 카드 흔들림)은 전부 여기 한 곳에서 난다.
            Effects.CombatFeedback.PlayDamage(this, dmgCtx, hpLost, shieldLost);

            // 맞는 것도 궁극기를 채운다. 지속피해는 행동이 아니고, 덧붙은 피해는 원래 한 방의 일부라 세지 않는다.
            if (damageDealt > 0 && dmgCtx.CodeType != BaseEnums.CodeType.Effect && !dmgCtx.HasTag(DamageTag.BonusDamage))
                RecoverMana(ManaPerHitTaken);

            if (dmgCtx.Attacker != null && damageDealt > 0)
            {
                dmgCtx.Attacker.RoundDamageDealt += damageDealt;
                dmgCtx.Attacker.LastResolvedDamage = damageDealt;
                dmgCtx.Attacker.LastResolvedTarget = this;
                var resolved = new DamageResolvedContext(dmgCtx.Attacker, this, dmgCtx, damageDealt);
                dmgCtx.Attacker.Invoke(BaseEnums.UnitEventType.OnDamageDealt, resolved);
                AnyDamageDealt?.Invoke(resolved);
            }

            if (HpCurr <= 0) Die(dmgCtx.Attacker);
        }

        /// <summary>
        /// 최종 피해량.
        ///   1) 공격자의 주는 피해 보정
        ///   2) 경감 — K / (K + 방어 스탯), K는 공격자 레벨의 기준값
        ///      방어 스탯은 피해의 성격이 고른다(<see cref="Combat.DamageMitigation"/>)
        ///   3) 피격자의 받는 피해 보정
        /// </summary>
        private int CalculateFinalDamage(DamageContext dmgCtx)
        {
            float outgoing = Mathf.Max(0f, dmgCtx.OutgoingDamageMultiplier);
            float defenseMultiplier = Mathf.Max(0f, dmgCtx.DefenseStatMultiplier);

            if (dmgCtx.Attacker != null)
            {
                outgoing *= Combat.Battlefield.OutgoingMultiplier(dmgCtx.Attacker);
                foreach (BaseEffect effect in dmgCtx.Attacker.ActiveEffectObjects())
                {
                    outgoing *= effect.OutgoingDamageModifier(dmgCtx.Attacker, this, dmgCtx);
                    defenseMultiplier *= effect.DefenseStatMultiplierModifier(dmgCtx.Attacker, this, dmgCtx);
                }
            }

            float receiving = 1f;
            foreach (BaseEffect effect in ActiveEffectObjects())
            {
                receiving *= effect.ReceivingDamageModifier(this, dmgCtx);
                defenseMultiplier *= effect.OwnedDefenseStatMultiplierModifier(this, dmgCtx);
            }

            float mitigation = 1f;
            if (Combat.DamageMitigation.TryGetDefenseStat(dmgCtx, out BaseEnums.StatType defenseStat))
            {
                float defense = StatValue(defenseStat) * defenseMultiplier;
                int attackerLevel = dmgCtx.Attacker != null ? dmgCtx.Attacker.Level : Level;
                mitigation = UnitStats.MitigationMultiplier(defense, UnitStats.ArmorConstant(attackerLevel));
            }

            float scaled = dmgCtx.Damage * outgoing * mitigation * receiving;
            if (scaled <= 0f) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        /// <summary>
        /// 쓰러진다. 사망 이벤트 → 처치 보상 → 칸 비우기 순서다.
        /// 카드가 부서지는 연출은 칸이 비워지기 <b>전에</b> 떠 두어야 하므로 여기서 부른다.
        /// </summary>
        public virtual void Die(Unit attacker)
        {
            if (!isActive) return;

            Effects.Vfx.CardLifecycleFx.PlayDestroy(this);
            if (UltimateCode?.IsPersistent == true) UltimateCode.StopCode();
            Invoke(BaseEnums.UnitEventType.OnDeath, new EventContext(this, attacker));
            AnyUnitDied?.Invoke(this, attacker);
            if (attacker != null && attacker != this)
            {
                attacker.Invoke(BaseEnums.UnitEventType.OnKill, new EventContext(attacker, this));
                attacker.RecoverMana(ManaPerKill);
            }
            if (isEnemy) GameManager.Instance?.OnKillEnemy(this);
            DeactivateUnit();
        }

        // ══════════════════════════════════════════════════════
        // 치유 · 보호막
        // ══════════════════════════════════════════════════════

        /// <summary>체력을 회복한다. 받는 치유 · 주는 치유 배율을 탄다.</summary>
        public void Heal(int amount, Unit source = null)
        {
            if (amount <= 0 || !isActive) return;

            float multiplier = GetOutgoingSupportMultiplier(source, false);
            foreach (BaseEffect effect in ActiveEffectObjects()) multiplier *= effect.HealingReceivedMultiplierModifier(this);
            int healing = Mathf.Max(0, Mathf.RoundToInt(amount * multiplier));
            int before = HpCurr;
            HpCurr = Mathf.Clamp(HpCurr + healing, 0, HpMax);
            int effective = HpCurr - before;
            if (effective > 0)
            {
                Effects.CombatFeedback.PlayHeal(this, effective);
                Effects.Vfx.StatusAffectedVfx.PlaySupport(this, source, "hit.support.heal");
            }
            RefreshView();
        }

        /// <summary>
        /// 체력을 직접 맞춘다. 올리면 치유로, 내리면 소모로 본다.
        /// 옛 호출부와의 호환을 위해 남겨 둔다 — 새 코드는 <see cref="Heal"/>을 쓴다.
        /// </summary>
        public void ModifyHp(int newHp, Unit source = null)
        {
            if (newHp > HpCurr) Heal(newHp - HpCurr, source);
            else
            {
                HpCurr = Mathf.Clamp(newHp, 0, HpMax);
                RefreshView();
            }
        }

        /// <summary>전투 밖의 회복(휴식 · 보상). 전투용 배율을 적용하지 않아 약속한 수치를 정확히 회복한다.</summary>
        public int RestoreHpOutsideCombat(int amount)
        {
            if (amount <= 0 || HpCurr <= 0) return 0;
            int before = HpCurr;
            HpCurr = Mathf.Clamp(HpCurr + amount, 0, HpMax);
            int restored = HpCurr - before;
            if (restored > 0) Effects.CombatFeedback.PlayHeal(this, restored);
            RefreshView();
            return restored;
        }

        public virtual void AddShield(int amount, Unit source = null)
        {
            amount = ScaleShield(amount, source);
            if (amount <= 0) return;
            ShieldMax += amount;
            ShieldCurr += amount;
            AudioManager.PlayCue("common.shield");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("shield");
#endif
            RefreshView();
        }

        /// <summary>보호막을 이 값으로 덮어쓴다.</summary>
        public virtual void SetShield(int amount, Unit source = null)
        {
            amount = ScaleShield(amount, source);
            ShieldMax = amount;
            ShieldCurr = amount;
            if (amount > 0) AudioManager.PlayCue("common.shield");
            RefreshView();
        }

        public virtual void RemoveShield(int amount)
        {
            ShieldCurr = Mathf.Max(0, ShieldCurr - Mathf.Max(0, amount));
            if (ShieldCurr == 0) ShieldMax = 0;
            RefreshView();
        }

        private int ScaleShield(int amount, Unit source)
        {
            float multiplier = GetOutgoingSupportMultiplier(source, true);
            foreach (BaseEffect effect in ActiveEffectObjects()) multiplier *= effect.ShieldReceivedMultiplierModifier(this);
            return Mathf.Max(0, Mathf.RoundToInt(amount * multiplier));
        }

        private float GetOutgoingSupportMultiplier(Unit source, bool shield)
        {
            if (source == null) return 1f;
            float multiplier = 1f;
            foreach (BaseEffect effect in source.ActiveEffectObjects())
            {
                multiplier *= shield
                    ? effect.OutgoingShieldMultiplierModifier(source, this)
                    : effect.OutgoingHealingMultiplierModifier(source, this);
            }
            return Mathf.Max(0f, multiplier);
        }

        // ══════════════════════════════════════════════════════
        // 제어
        // ══════════════════════════════════════════════════════

        public virtual void ControlStarts(ControlContext context)
        {
            isControlled = true;
            controlTurns = Mathf.Max(1, context.Duration);
            Invoke(BaseEnums.UnitEventType.OnControlStarts, new EventContext(this, context.Attacker));
        }

        public virtual void ControlEnds()
        {
            isControlled = false;
            controlTurns = 0;
            Invoke(BaseEnums.UnitEventType.OnControlEnds, new EventContext(this));
        }

        /// <summary>행동 불가를 건다. 이미 걸려 있으면 남은 턴과 새 턴 중 긴 쪽을 남긴다.</summary>
        public void ApplyControlAtLeast(Unit attacker, int turns)
        {
            if (turns <= 0) return;
            if (isControlled && controlTurns >= turns) return;
            ControlStarts(new ControlContext(attacker, turns));
        }

        /// <summary>이번 라운드에 제어에 걸린 횟수와 마지막으로 걸린 턴. 제어 분쇄 판정이 쓴다.</summary>
        public int ControlAppliedCount { get; internal set; }
        public int LastControlledTurn { get; internal set; } = -100000;

        internal bool ResistsNegativeStatus(Status.UnitStatus status)
        {
            if (status == null || status.Category != BaseEnums.StatusCategory.Negative) return false;
            float chance = 0f;
            foreach (BaseEffect effect in ActiveEffectObjects())
                chance += Mathf.Max(0f, effect.NegativeStatusResistanceChanceModifier(this, status));
            return chance > 0f && UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        public void AddUntargetableSource()
        {
            untargetableSourceCount++;
            currentNormalTarget = null;
        }

        public void RemoveUntargetableSource() => untargetableSourceCount = Mathf.Max(0, untargetableSourceCount - 1);

        // ══════════════════════════════════════════════════════
        // 원소
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전투 시작 시 원소 상태를 초기화한다.
        /// <b>유닛의 고유 원소는 부착이 아니라 판정 전용이다.</b> 원신처럼 캐릭터의 속성과
        /// 실제로 걸린 원소 부착은 별개의 축이다.
        /// </summary>
        public void ResetCombatElements()
        {
            _combatElements.Clear();
            _judgementElements.Clear();
            _temporaryElementDurations.Clear();
            if (Enum.TryParse(Element, true, out BaseEnums.UnitElement innate) && innate != BaseEnums.UnitElement.None)
                _judgementElements.Add(innate);
        }

        /// <summary>원소를 부착한다. 부착 직후 원소 반응을 검사한다.</summary>
        /// <param name="source">부착을 일으킨 유닛. 반응 피해가 이 유닛의 특수공격력에 비례한다.</param>
        /// <param name="suppressReaction">반응 판정을 건너뛴다. 확산이 뿌리는 부착의 무한 연쇄를 막는다.</param>
        public void GrantCombatElement(BaseEnums.UnitElement elementToGrant, int duration = CommonElementAuraDuration,
            Unit source = null, bool suppressReaction = false)
        {
            if (elementToGrant == BaseEnums.UnitElement.None || !isActive) return;

            bool added = _combatElements.Add(elementToGrant);
            _temporaryElementDurations[elementToGrant] = Mathf.Max(1, duration > 0 ? duration : CommonElementAuraDuration);
            RefreshView();
            AnyCombatElementGranted?.Invoke(source, this, elementToGrant);

            // added == false는 같은 원소가 이미 붙어 있었다는 뜻이다 — 같은 원소 반응의 재료가 된다.
            if (!suppressReaction) Effects.Negative.ElementalReaction.TryResolve(this, elementToGrant, source, !added);
        }

        /// <summary>부착된 원소를 걷어낸다. 고유 원소는 부착이 아니므로 여기서 사라지지 않는다.</summary>
        public void RemoveCombatElement(BaseEnums.UnitElement elementToRemove)
        {
            if (!_combatElements.Remove(elementToRemove)) return;
            _temporaryElementDurations.Remove(elementToRemove);
            RefreshView();
        }

        internal static void NotifyElementalReaction(Unit source, Unit target, string reactionName)
            => AnyElementalReaction?.Invoke(source, target, reactionName);

        /// <summary>이 원소를 가진 것으로 <b>판정</b>되는가. 실제 부착과 고유 원소를 함께 본다.</summary>
        public bool HasCombatElement(BaseEnums.UnitElement elementToCheck)
            => _combatElements.Contains(elementToCheck) || _judgementElements.Contains(elementToCheck);

        /// <summary>실제로 <b>부착된</b> 원소인가. 원소 반응은 이쪽만 재료로 쓴다.</summary>
        public bool HasAttachedElement(BaseEnums.UnitElement elementToCheck) => _combatElements.Contains(elementToCheck);

        public int GetAttachedElementRemainingTurns(BaseEnums.UnitElement elementToCheck)
            => _temporaryElementDurations.TryGetValue(elementToCheck, out int remaining) ? remaining : 0;

        public bool HasAnyCombatElement => _combatElements.Count > 0;

        /// <summary>
        /// 가장 최근에 이 유닛이 해결한 피해와 그 대상.
        /// 원소 부착은 언제나 피해 <b>뒤</b>에 오므로, 증폭 반응이 "방금 그 공격"을 되짚는 데 쓴다.
        /// </summary>
        public int LastResolvedDamage { get; private set; }
        public Unit LastResolvedTarget { get; private set; }

        /// <summary>원소 반응 재발동 내부 쿨다운 — 반응 ID → 마지막으로 일어난 자기 턴.</summary>
        private readonly Dictionary<int, int> _reactionCooldowns = new();

        public bool IsReactionReady(int reactionId, int intervalTurns)
            => intervalTurns <= 0 || !_reactionCooldowns.TryGetValue(reactionId, out int last) || TurnCount - last >= intervalTurns;

        public void MarkReactionOccurred(int reactionId) => _reactionCooldowns[reactionId] = TurnCount;

        public IEnumerable<BaseEnums.UnitElement> AttachedElements => _combatElements;

        public string GetCombatElementDisplay() => _combatElements.Count == 0 ? "None" : string.Join(", ", _combatElements);

        private void TickTemporaryCombatElements()
        {
            if (_temporaryElementDurations.Count == 0) return;

            bool changed = false;
            foreach (BaseEnums.UnitElement elementType in _temporaryElementDurations.Keys.ToList())
            {
                int remaining = _temporaryElementDurations[elementType] - 1;
                if (remaining > 0)
                {
                    _temporaryElementDurations[elementType] = remaining;
                    continue;
                }
                _temporaryElementDurations.Remove(elementType);
                _combatElements.Remove(elementType);
                changed = true;
            }
            if (changed) RefreshView();
        }

        // ══════════════════════════════════════════════════════
        // 고유 게이지
        // ══════════════════════════════════════════════════════

        public void SetCombatResourceMaximum(string resourceId, int maximum, bool resetCurrent = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            if (!_combatResourceMaximums.ContainsKey(resourceId)) _combatResourceOrder.Add(resourceId);
            _combatResourceMaximums[resourceId] = Mathf.Max(0, maximum);
            _combatResources[resourceId] = resetCurrent || !_combatResources.ContainsKey(resourceId)
                ? 0
                : Mathf.Min(_combatResources[resourceId], _combatResourceMaximums[resourceId]);
            RefreshView();
        }

        public int GetCombatResource(string resourceId)
            => !string.IsNullOrWhiteSpace(resourceId) && _combatResources.TryGetValue(resourceId, out int value) ? value : 0;

        public int GetCombatResourceMaximum(string resourceId)
            => !string.IsNullOrWhiteSpace(resourceId) && _combatResourceMaximums.TryGetValue(resourceId, out int value) ? value : 0;

        public int AddCombatResource(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount == 0) return GetCombatResource(resourceId);
            int next = Mathf.Clamp(GetCombatResource(resourceId) + amount, 0, GetCombatResourceMaximum(resourceId));
            _combatResources[resourceId] = next;
            RefreshView();
            return next;
        }

        public bool TryConsumeCombatResource(string resourceId, int amount)
        {
            if (amount <= 0) return true;
            int current = GetCombatResource(resourceId);
            if (current < amount) return false;
            _combatResources[resourceId] = current - amount;
            RefreshView();
            return true;
        }

        // ══════════════════════════════════════════════════════
        // 턴 · 라운드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 이 유닛이 이번 라운드에 행동한 횟수. <b>지속시간 · 주기 · 내부 쿨다운의 유일한 시간 축이다.</b>
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>
        /// 자기 턴을 연다. <see cref="ActionScheduler"/>가 행동 직전에 한 번만 부른다.
        /// 상태 지속시간이 줄고 지속피해 · 재생 · 주기형 효과가 진행된다.
        /// </summary>
        public void BeginTurn()
        {
            TurnCount++;
            if (ControlAppliedCount > 0 &&
                TurnCount - LastControlledTurn >= Effects.Negative.ControlStatuses.DiminishResetTurns)
            {
                ControlAppliedCount = 0;
            }

            if (isControlled && --controlTurns <= 0) ControlEnds();
            TickTemporaryCombatElements();
            StatusController.TickTurn();
            Combat.Battlefield.OnAnchorTurn(this);
            Invoke(BaseEnums.UnitEventType.OnTurnStart, new EventContext(this));
        }

        /// <summary>자기 턴의 행동이 모두 끝났을 때 스케줄러가 부른다.</summary>
        public void EndTurn() => Invoke(BaseEnums.UnitEventType.OnTurnEnd, new EventContext(this));

        internal void PrepareRoundResources() => TurnCount = 0;

        protected void DefaultRoundStartEvent(EventContext context)
        {
            RoundDamageDealt = 0;
            TurnCount = 0;
            ControlAppliedCount = 0;
            LastControlledTurn = -100000;
            LastResolvedDamage = 0;
            LastResolvedTarget = null;
            _reactionCooldowns.Clear();
            ResetCombatElements();
            CastPassiveCode();
            if (UltimateCode?.IsPersistent == true) UltimateCode.CastCode();
        }

        protected void DefaultRoundEndEvent(EventContext context)
        {
            // 상태는 라운드가 끝나면 모두 정리한다(OnRemove가 리스너를 해제한다).
            StatusController.ClearAll();
            SpecialCode?.StopCode();
            if (UltimateCode?.IsPersistent == true) UltimateCode.StopCode();
            foreach (PassiveCode code in PassiveCodes) code?.StopCode();
            ResetCombatElements();
            _combatResources.Clear();
            _combatResourceMaximums.Clear();
            _combatResourceOrder.Clear();
            ShieldMax = 0;
            ShieldCurr = 0;
            isControlled = false;
            controlTurns = 0;
            AttributesUpdate();
        }

        // ══════════════════════════════════════════════════════
        // 런 저장 · 복원
        // ══════════════════════════════════════════════════════

        public void RestoreRunState(Core.UnitSaveData saveData)
        {
            if (saveData == null) return;

            Level = saveData.level;
            Exp = Mathf.Max(0, saveData.exp);
            AttributesUpdate();
            HpCurr = Mathf.Clamp(saveData.currentHP, 0, HpMax);
            ManaCurr = Mathf.Clamp(saveData.ultimateResource, 0, ManaMax);
            RefreshView();
        }

        /// <summary>전투 밖에서 파생 스탯을 다시 돌린다. 체력 비율을 보존한다.</summary>
        public void RefreshDerivedAttributes() => AttributesUpdate();

        // ══════════════════════════════════════════════════════
        // 활성 · 표시
        // ══════════════════════════════════════════════════════

        public void ActivateUnit()
        {
            isActive = true;
            if (currentCell == null) return;
            currentCell.isOccupied = true;
            currentCell.SetOccupiedUnit(this);
        }

        public void DeactivateUnit()
        {
            isActive = false;
            untargetableSourceCount = 0;
            if (ID > 0) LastActiveId = ID;
            ID = 0;
            currentNormalTarget = null;
            if (currentCell == null) return;

            currentCell.isOccupied = false;
            currentCell.SetPortrait(null);
            currentCell.SetOccupiedUnit(null);
            currentCell.reservedTime = 2f;
        }

        /// <summary>마지막으로 전장에 서 있을 때의 유닛 ID. <see cref="DeactivateUnit"/>이 ID를 지우기 직전 값이다.</summary>
        public int LastActiveId { get; private set; }

        /// <summary>전투가 끝난 뒤 쓰러진 아군을 제자리에 다시 세운다.</summary>
        public bool ReviveAfterBattle(float hpRatio = 1f)
        {
            if (isActive || currentCell == null || LastActiveId <= 0) return false;

            ID = LastActiveId;
            ActivateUnit();
            LoadSprite(PortraitPath);
            HpCurr = Mathf.Clamp(Mathf.CeilToInt(HpMax * Mathf.Clamp01(hpRatio)), 1, HpMax);
            RefreshView();
            return true;
        }

        public void RefreshView() => currentCell?.UpdateUI();

        /// <summary>로그에 쓸 위치 표기.</summary>
        public string FieldPositionLabel() => currentCell != null ? $"({currentCell.xPos}, {currentCell.yPos})" : "";

        public void NotifyBeneficialEffectReceived(Unit grantor)
        {
            AudioManager.PlayCue("common.status.positive");
            Invoke(BaseEnums.UnitEventType.OnBeneficialEffectReceived, new EventContext(this, grantor));
            grantor?.Invoke(BaseEnums.UnitEventType.OnBeneficialEffectGranted, new EventContext(grantor, this));
        }

        private void NotifyNegativeStatusGranted(Unit grantor, Status.UnitStatus status)
        {
            AudioManager.PlayCue("common.status.negative");
            AnyNegativeStatusGranted?.Invoke(grantor, this, status);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 리스너
        // ══════════════════════════════════════════════════════
        // 각 UnitEventType은 하나의 컨텍스트 타입(Action<T>)만 쓴다. 섞이면 오류 로그를 남기고 무시한다.

        public void AddListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (action == null) return;
            if (!_eventDict.TryGetValue(eventType, out Delegate existing) || existing == null)
            {
                _eventDict[eventType] = action;
                return;
            }

            if (existing is Action<T> typed) _eventDict[eventType] = typed + action;
            else Debug.LogError($"[Unit] 이벤트 {eventType}에 이미 {existing.GetType()} 리스너가 있어 {typeof(Action<T>)}를 추가할 수 없습니다.");
        }

        public void RemoveListener<T>(BaseEnums.UnitEventType eventType, Action<T> action)
        {
            if (action == null) return;
            if (_eventDict.TryGetValue(eventType, out Delegate existing) && existing is Action<T> typed)
                _eventDict[eventType] = typed - action;
        }

        public void Invoke<T>(BaseEnums.UnitEventType eventType, T context)
        {
            if (!_eventDict.TryGetValue(eventType, out Delegate value) || value == null) return;
            if (value is Action<T> typed) typed.Invoke(context);
            else Debug.LogError($"[Unit] 이벤트 {eventType} 발행 타입 {typeof(T)}이(가) 등록된 리스너 타입 {value.GetType()}과(와) 다릅니다.");
        }

        // ══════════════════════════════════════════════════════
        // 상태 (UnitStatusController 위임)
        // ══════════════════════════════════════════════════════

        public void AddStatus(Status.UnitStatus status) => StatusController.Add(status);
        public void RemoveStatus(int statusId) => StatusController.Remove(statusId);
        public bool HasStatus(int statusId) => StatusController.Has(statusId);
        public bool HasStatusKey(string key) => StatusController.HasKey(key);
        public void RemoveStatusByKey(string key) => StatusController.RemoveByKey(key);
        public Status.UnitStatus GetStatus(int statusId) => StatusController.Get(statusId);
        public List<Status.UnitStatus> GetAllStatuses(int statusId) => StatusController.GetAll(statusId);
        public List<Status.UnitStatus> GetAllStatuses() => StatusController.GetAll();
        public List<Status.UnitStatus> GetStatuses() => StatusController.GetLive();

        public bool HasNegativeStatus()
            => GetAllStatuses().Any(status => status != null && status.Category == BaseEnums.StatusCategory.Negative);

        public bool HasDamageOverTimeStatus() => ActiveEffectObjects().Any(effect => effect.IsDamageOverTime);

        public void RemoveAllNegativeStatuses(Unit source = null)
        {
            List<Status.UnitStatus> removed = GetAllStatuses()
                .Where(s => s.Category == BaseEnums.StatusCategory.Negative).ToList();
            foreach (Status.UnitStatus status in removed)
                RemoveStatus(status.StatusId);
            // 제어는 별도 타이머도 쓴다. 상태만 지우면 정화 뒤에도 행동 불가가 남는다.
            if (isControlled) ControlEnds();
            if (removed.Count > 0)
                Effects.Vfx.StatusAffectedVfx.PlaySupport(this, source, "hit.support.cleanse");
        }
    }
}
