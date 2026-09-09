using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Core;
using Helpers;
using Managers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Entities
{
    public class Unit : MonoBehaviour
    {
        /// <summary>모든 진영의 사망을 관찰해야 하는 필드 패시브용 전역 전투 이벤트.</summary>
        public static event Action<Unit, Unit> AnyUnitDied;
        /// <summary>해로운 상태가 실제로 적용되거나 지속시간이 갱신된 순간.</summary>
        public static event Action<Unit, Unit, Status.UnitStatus> AnyNegativeStatusGranted;
        /// <summary>어느 유닛이든 실제 피해를 가한 순간.</summary>
        public static event Action<DamageResolvedContext> AnyDamageDealt;
        /// <summary>피해 판정이 아닌 효과나 공격 비용으로 체력을 실제 소비한 순간.</summary>
        public static event Action<Unit, int> AnyHpSpent;

        public bool isActive = false;

        // 기본 식별정보
        [SerializeField] private int id;
        [SerializeField] private bool isEnemy;
        [SerializeField] private string unitName;
        [SerializeField] private string element;
        [SerializeField] private string mainStat;
        [SerializeField] private string subStat;
        [SerializeField] private List<string> subStats = new();
        [SerializeField] private float mainStatTrainingBonus = 0.2f;
        [SerializeField] private float subStatTrainingBonus = 0.1f;
        [SerializeField] private List<string> startingProficiencies = new();
        [SerializeField] private List<string> unitTags = new();
        // 적 등급(normal / elite / boss). 아군은 비어 있다.
        [SerializeField] private string unitTier = "";
        [SerializeField] private List<int> equippedItemIds = new();
        [SerializeField] private List<int> carriedItemIds = new();
        [SerializeField] private List<LearnedPassiveSaveData> learnedPassiveRecords = new();
        [SerializeField] private List<int> grantedPassiveCodeIds = new();
        [SerializeField] private int level;
        // 아군의 누적 경험치. 적은 사용하지 않는다(레벨을 스테이지가 정한다).
        [SerializeField] private int exp;
        // 이번 라운드에 이 유닛이 실제로 가한 누적 피해. 라운드 시작 시 0으로 초기화된다.
        [SerializeField] private int roundDamageDealt;
        // 육성(트레이닝) 레벨. 육성 페이즈에서 메인 캐릭터가 훈련할 때마다 증가한다.
        // 서포트 카드 산정과 저장/복원에 쓰인다. 스탯 성장은 Level이 담당한다.
        [SerializeField] private int trainingLevel;
        [SerializeField] private int untargetableSourceCount;
        public int ID { get => id; protected set => id = value; }
        public bool IsEnemy { get => isEnemy; protected set => isEnemy = value; }
        public string UnitName { get => unitName; protected set => unitName = value; }
        public string Element { get => element; protected set => element = value; }
        public string MainStat { get => mainStat; protected set => mainStat = value; }
        public string SubStat { get => subStat; protected set => subStat = value; }
        public IReadOnlyList<string> SubStats => subStats;
        public float MainStatTrainingBonus => mainStatTrainingBonus;
        public float SubStatTrainingBonus => subStatTrainingBonus;
        public IReadOnlyList<int> EquippedItemIds => equippedItemIds;
        public IReadOnlyList<int> CarriedItemIds => carriedItemIds;
        public IReadOnlyList<LearnedPassiveSaveData> LearnedPassiveRecords => learnedPassiveRecords;
        public IReadOnlyList<int> GrantedPassiveCodeIds => grantedPassiveCodeIds;
        // 유닛 레벨. 적은 현재 스테이지가, 아군은 누적 EXP가 결정한다.
        public int Level { get => level; protected set => level = value; }
        // 레벨업 EXP 곡선: Lv N → N+1 에 필요한 EXP = Base + PerLevel × (N − 1)
        public const int ExpBaseRequirement = 100;
        public const int ExpRequirementPerLevel = 28;
        // 육성 레벨. 육성 페이즈에서만 증가. 서포트 카드 산정과 저장/복원에 쓰인다.
        public int TrainingLevel { get => trainingLevel; protected set => trainingLevel = value; }
        // 레벨 해금 패시브 판정 기준. 성장 축이 Level로 단일화되어 Level을 그대로 쓴다.
        public int PassiveUnlockLevel => Level;
        public bool IsUntargetable => untargetableSourceCount > 0;

        public Cell currentCell; // 위치중인 셀. 소환수는 칸을 차지하지 않으므로 null이다.

        // ── 소환수 ────────────────────────────────────────────────
        //
        // 소환수는 붕괴: 스타레일의 '기억 정령'과 같은 취급이다.
        // 칸은 차지하지 않지만 <b>피격 대상이 되고 체력을 가지며</b> 소환자에게 종속된다.
        // 행동 게이지(DEX)와 궁극기 자원(INT)은 일반 유닛과 똑같이 굴러간다.
        private readonly List<Unit> _summons = new();

        /// <summary>이 유닛을 불러낸 소환자. 소환수가 아니면 null이다.</summary>
        public Unit SummonOwner { get; private set; }

        /// <summary>칸 없이 소환자에 종속된 개체인가.</summary>
        public bool IsSummon => SummonOwner != null;

        /// <summary>이 유닛이 거느린 살아 있는 소환수.</summary>
        public IReadOnlyList<Unit> ActiveSummons => _summons;

        /// <summary>
        /// <b>전장에 서 있는가.</b> 대상 지정·행동 순서·필드 판정이 모두 이 하나를 본다.
        ///
        /// 일반 유닛은 칸을 차지하고 있어야 하고, 소환수는 칸이 없는 대신
        /// <b>소환자가 전장에 살아 있는 동안</b>만 존재한다.
        /// 대상 지정 가능 여부(<see cref="IsUntargetable"/>)는 여기서 보지 않는다 —
        /// 필드에 있으면서 잠시 지정 불가인 상태가 따로 있기 때문이다.
        /// </summary>
        public bool IsOnField
        {
            get
            {
                if (!isActive) return false;
                if (IsSummon) return SummonOwner.isActive && SummonOwner.IsOnField;
                return currentCell != null && currentCell.yPos > 0 && currentCell.isOccupied;
            }
        }

        /// <summary>소환수를 이 유닛에 묶는다. <see cref="GridManager"/>의 소환 경로만 부른다.</summary>
        internal void RegisterSummon(Unit summon)
        {
            if (summon == null || summon == this || _summons.Contains(summon)) return;
            summon.SummonOwner = this;
            _summons.Add(summon);
        }

        /// <summary>소환수를 목록에서 뗀다. 소환수가 쓰러졌을 때 부른다.</summary>
        internal void UnregisterSummon(Unit summon)
        {
            if (summon == null) return;
            _summons.Remove(summon);
        }

        /// <summary>거느린 소환수를 전부 거둔다. 소환자가 쓰러지거나 라운드가 끝날 때 부른다.</summary>
        public void DismissSummons()
        {
            if (_summons.Count == 0) return;
            // 소환수의 사망 처리가 목록을 건드리므로 사본으로 돈다.
            foreach (Unit summon in _summons.ToList())
            {
                if (summon != null && summon.isActive) summon.Die(null);
            }
            _summons.Clear();
        }

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
        [SerializeField] private int defCurr;
        [SerializeField] private float critChanceCurr;
        [SerializeField] private float excessCritChanceCurr;
        [SerializeField] private float critDamageCurr;
        [SerializeField] private float codeAcceleration;
        [FormerlySerializedAs("attackSpeedCurr")]
        [SerializeField] private float actionSpeedCurr;
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
        /// <summary>이번 라운드에 이 유닛이 가한 누적 피해.</summary>
        public int RoundDamageDealt { get => roundDamageDealt; private set => roundDamageDealt = value; }

        /// <summary>
        /// 이번 라운드에 이 유닛이 <b>부여자로서</b> 실제로 채운 체력의 합(순수치유량).
        /// 과다치유(오버힐)와 보호막은 세지 않는다. 프레이아 궁극기 '풍요의 산물'이 자원으로 쓴다.
        /// </summary>
        public int RoundEffectiveHealingDone { get; private set; }

        /// <summary>순수치유량 기록을 0으로 되돌린다.</summary>
        public void ResetEffectiveHealingRecord() => RoundEffectiveHealingDone = 0;

        // ── 강인도 (붕괴: 스타레일식 가상 체력) ─────────────────────
        // 체력과 병렬로 깎이며 피해를 흡수하지 않는다. 0이 되면 사라지고 처치 판정만 발행한다.
        [SerializeField] private int toughnessCurr;
        [SerializeField] private int toughnessMax;

        public int ToughnessCurr => toughnessCurr;
        public int ToughnessMax => toughnessMax;
        public bool HasToughness => toughnessCurr > 0;

        /// <summary>방어력. STR 파생. 롤 방식으로 받는 피해를 비율 감소시킨다.</summary>
        public int DefCurr { get => defCurr; protected set => defCurr = value; }

        /// <summary>현재 방어력이 만드는 받는 피해 배율(1 = 감소 없음).</summary>
        public float DamageTakenMultiplierFromArmor => ArmorMultiplier(DefCurr, stats.GetArmorConstant());

        /// <summary>
        /// 롤 방어력 공식. 방어력이 음수면 대칭적으로 피해가 증폭된다.
        /// </summary>
        public static float ArmorMultiplier(float armor, float armorConstant)
        {
            armorConstant = Mathf.Max(1f, armorConstant);
            return armor >= 0f
                ? armorConstant / (armorConstant + armor)
                : 2f - armorConstant / (armorConstant - armor);
        }

        /// <summary>이 유닛의 주스탯. 지정이 없거나 파싱에 실패하면 STR로 본다.</summary>
        public BaseEnums.PrimaryStat MainPrimaryStat =>
            Enum.TryParse(MainStat, true, out BaseEnums.PrimaryStat parsed)
                ? parsed
                : BaseEnums.PrimaryStat.STR;

        /// <summary>
        /// 포켓몬식 피해 산출. 스킬이 가진 고정 위력에 주스탯을 곱한다.
        /// 별도의 공격력 스탯도, 방어력도 없다.
        /// </summary>
        public int SkillDamage(int skillPower) => stats.GetSkillDamage(skillPower, MainPrimaryStat);

        /// <summary>피해 근거 스탯을 직접 지정하는 변형(주스탯이 아닌 스탯으로 때리는 스킬용).</summary>
        public int SkillDamage(int skillPower, BaseEnums.PrimaryStat stat) => stats.GetSkillDamage(skillPower, stat);
        public float CritChanceCurr { get => critChanceCurr; protected set => critChanceCurr = value; }
        public float ExcessCritChanceCurr { get => excessCritChanceCurr; protected set => excessCritChanceCurr = value; }
        public float CritMultiplierCurr { get => critDamageCurr; protected set => critDamageCurr = value; }
        public float CodeAcceleration { get => codeAcceleration; protected set => codeAcceleration = value; }
        public float ActionSpeedCurr { get => actionSpeedCurr; protected set => actionSpeedCurr = value; }
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
        /// <summary>남은 행동 불가 <b>턴</b> 수. 자기 턴이 올 때마다 1씩 줄고 그 턴의 행동을 건너뛴다.</summary>
        public int controlTurns;
        
        // 일반행동 타겟팅
        public Unit currentNormalTarget; // 현재 일반행동 타겟

        // 타겟팅 우선도 (-3 ~ 3, 높을수록 우선순위 높음)
        [SerializeField] private int priority = 0;
        public int Priority
        {
            get
            {
                int modifier = ActiveEffectObjects().Sum(effect => effect.TargetPriorityAdditiveModifier(this));
                return Mathf.Clamp(priority + modifier, -3, 3);
            }
            set => priority = Mathf.Clamp(value, -3, 3);
        }

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

        /// <summary>
        /// 체력 바를 나눠 그릴 칸 수. 0이면 보통의 연속 게이지다.
        /// 효과가 여럿이면 <b>가장 잘게 나누는 값 하나</b>만 쓴다. 표시 전용이다.
        /// </summary>
        public int HpSegmentCount
        {
            get
            {
                int segments = 0;
                foreach (var effect in ActiveEffectObjects())
                {
                    segments = Mathf.Max(segments, effect.HpSegmentCount(this));
                }
                return segments;
            }
        }

        public int UltimateResourceMax { get => ultimateResourceMax; protected set => ultimateResourceMax = value; }
        public int ManaMax { get => manaMax; protected set => manaMax = value; }
        // 공격력/방어력 스탯은 존재하지 않는다. 피해는 SkillDamage(위력)로 그때그때 산출한다.

        // 유닛 상태(버프/디버프) 컨테이너
        protected readonly UnitStatusController StatusController = new();

        // 유닛 코드(스킬) 정보
        // 패시브 목록: [0] = 고유 패시브(항상 활성·용량 미점유), 이후 = 레벨/INT 용량에 따라 해금되는 패시브.
        protected List<PassiveCode> PassiveCodes = new();
        // 아직 해금되지 않은 레벨 패시브 정의. RefreshLevelPassives()에서 Level에 도달하면 PassiveCodes로 승격한다.
        protected List<LevelPassiveData> PendingLevelPassives = new();
        private readonly Dictionary<LevelPassiveData, (PassiveCode code, bool ownsRecord)> levelPassiveInstances = new();
        protected List<PassiveCode> ItemPassiveCodes = new();
        protected NormalCode NormalCode;
        protected UltimateCode UltimateCode;
        public float ultimateCooldown;
        protected EquipmentLoadout EquipmentLoadout;
        /// <summary>원소 부착 기본 지속 <b>턴</b> 수. 부착자가 아니라 <b>부착된 유닛</b>의 턴으로 센다.</summary>
        public const int CommonElementAuraDuration = 5;
        private readonly HashSet<BaseEnums.UnitElement> _combatElements = new();
        /// <summary>
        /// 실제로 부착되지는 않았지만 <b>판정만</b> 받는 원소(스사노오 '뇌신').
        /// 원소 반응의 재료로는 쓰이지 않는다. 부착이 아니므로 반응에 소모되지도 않는다.
        /// </summary>
        private readonly HashSet<BaseEnums.UnitElement> _judgementElements = new();
        private readonly Dictionary<BaseEnums.UnitElement, int> _temporaryElementDurations = new();
        private readonly Dictionary<string, int> _combatResources = new();
        private readonly Dictionary<string, int> _combatResourceMaximums = new();

        // 등록 순서를 따로 들고 있는다. Dictionary의 열거 순서는 보장되는 계약이 아니라
        // "대표 자원 하나"를 뽑는 UI가 갱신마다 다른 자원을 집을 수 있다.
        private readonly List<string> _combatResourceOrder = new();
        private int _baseNormalCodeId;
        private int _baseUltimateCodeId;
        private int _baseNormalCodeStage = 1;
        private int _baseUltimateCodeStage = 1;

        /// <summary>
        /// 중량과 개인 인벤토리를 쓰는가.
        ///
        /// <b>적은 쓰지 않는다.</b> 적은 편성 화면도, 보관함도, 장비를 갈아 끼울 기회도 없다 —
        /// 데이터에 적힌 <c>startingItemIds</c>를 입고 나오는 것이 전부다.
        /// 중량 페널티는 "무엇을 들고 갈지 고르게 만드는" 장치인데 그 선택이 없는 쪽에 걸면
        /// 기획 의도와 무관하게 스탯만 깎인다.
        /// </summary>
        public bool UsesCarryWeight => !IsEnemy;

        /// <summary>중량 페널티 적용 전 STR과 같은 1차 적정 중량.</summary>
        public int CarryWeightFirstCap => Mathf.Max(1, stats.GetUnburdenedBaseStr());
        /// <summary>DEX 페널티 구간의 끝. 초과하면 올스탯 페널티로 전환된다.</summary>
        public int CarryWeightSecondCap => Mathf.CeilToInt(CarryWeightFirstCap * 1.5f);
        /// <summary>3차 중량 기준. 초과 휴대는 가능하지만 장비 정리 전까지 진행 행동이 잠긴다.</summary>
        public int CarryWeightMax => CarryWeightFirstCap * 2;
        public int CarryWeightCurrent => (EquipmentLoadout?.GetTotalWeight() ?? 0) + GetStoredItemWeight();
        public int EncumbranceTier => !UsesCarryWeight
            ? 0
            : CarryWeightCurrent > CarryWeightSecondCap ? 2 : CarryWeightCurrent > CarryWeightFirstCap ? 1 : 0;
        public bool IsOverCarryWeightMax => UsesCarryWeight && CarryWeightCurrent > CarryWeightMax;
        /// <summary>
        /// 배운 코드 수. <b>상한은 없다</b> — 표시 전용이다.
        ///
        /// 예전에는 INT가 코드 용량(<c>max(3, INT)</c>)을 정해 그 수를 넘으면 해금 패시브를
        /// 조용히 배우지 못했다. INT가 낮은 적은 설계된 해금의 절반도 얻지 못했고,
        /// 잘리는 쪽이 항상 고레벨 코드라 <b>가장 강한 것부터 사라졌다.</b>
        /// INT는 이제 마나 효율만 담당한다.
        ///
        /// 일반행동·궁극기는 고정 2칸, 고유 패시브는 별도 슬롯이라 세지 않는다.
        /// </summary>
        public int LearnedCodeCount => 2 + PassiveCodes.Count(
            code => code != null && !code.IsUniquePassive && !code.IgnoresCodeCapacity);

        // ── UI 조회용 읽기 전용 접근자 ──────────────────────────────
        // 코드/장비 컨테이너는 protected로 유지하고, 화면이 필요로 하는 조회만 공개한다.
        public NormalCode ActiveNormalCode => NormalCode;
        public UltimateCode ActiveUltimateCode => UltimateCode;
        public IReadOnlyList<PassiveCode> ActivePassiveCodes => PassiveCodes;
        public IReadOnlyList<PassiveCode> ActiveItemPassiveCodes => ItemPassiveCodes;
        /// <summary>현재 걸린 상태(버프/디버프) 목록. HUD의 Modifier 표시가 읽는다.</summary>
        public IReadOnlyList<Status.UnitStatus> ActiveStatuses => StatusController.GetLive();
        /// <summary>고유 게이지(전투 자원) 식별자 목록. 없으면 비어 있다.</summary>
        public IReadOnlyList<string> CombatResourceIds => _combatResourceOrder;
        public IEnumerable<ItemData> EquippedItems =>
            EquipmentLoadout?.GetEquippedItemData() ?? Enumerable.Empty<ItemData>();

        // 이벤트
        private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict;
        private bool _resolvingDamageDealtEvent;
        // OnDeath 리스너가 사망 칸이 비워진 뒤 실행해야 하는 작업(분열 등)을 한 번만 예약한다.
        private readonly List<Action> _postDeathActions = new();

        /// <summary>
        /// 초상화 스프라이트를 가리키는 값. <b>해석에 성공하면 전체 경로, 실패하면 키 그대로</b>다.
        /// 어느 쪽이든 <see cref="Helpers.SpriteResource.LoadPortrait(string)"/>에 그대로 넘길 수 있으므로,
        /// 읽는 쪽은 <c>Resources.Load</c>를 직접 부르지 않는다 — 분류 폴더를 못 찾아 항상 null이 된다.
        /// </summary>
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
                () => AttributesUpdate(),
                NotifyBeneficialEffectReceived,
                NotifyNegativeStatusGranted);
        }

        public virtual void InitializeUnit(bool _isEnemy, int _id)
        {
            _eventDict = new Dictionary<BaseEnums.UnitEventType, Delegate>();
            InitializeStatusController();
            PassiveCodes = new List<PassiveCode>();
            PendingLevelPassives = new List<LevelPassiveData>();
            levelPassiveInstances.Clear();
            ItemPassiveCodes = new List<PassiveCode>();
            learnedPassiveRecords = new List<LearnedPassiveSaveData>();
            grantedPassiveCodeIds = new List<int>();
            carriedItemIds = new List<int>();
            startingProficiencies = new List<string>();
            unitTags = new List<string>();
            unitTier = "";
            _combatElements.Clear();
            _judgementElements.Clear();
            _temporaryElementDurations.Clear();
            _combatResources.Clear();
            _combatResourceMaximums.Clear();
            _combatResourceOrder.Clear();
            _postDeathActions.Clear();
            untargetableSourceCount = 0;
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
            
            // 칸이 없는 소환수는 칸이 들고 있던 바가 없다. 카드는 소환 경로가 따로 붙인다.
            currentCell?.InitializeHpBar();
            currentCell?.InitializeMpBar();
            
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
                Level = Mathf.Max(1, GameManager.Instance?.RoundManager?.EnemyLevel ?? 1);
                UnitName = enemyData.name;
                Element = string.IsNullOrWhiteSpace(enemyData.element) ? "None" : enemyData.element;
                ResetCombatElements();
                MainStat = enemyData.mainStat ?? "";
                subStats = enemyData.subStats != null && enemyData.subStats.Count > 0
                    ? new List<string>(enemyData.subStats)
                    : string.IsNullOrWhiteSpace(enemyData.subStat)
                        ? new List<string>()
                        : new List<string> { enemyData.subStat };
                SubStat = subStats.FirstOrDefault() ?? "";
                mainStatTrainingBonus = 0.2f;
                subStatTrainingBonus = 0.1f;
                startingProficiencies = enemyData.startingProficiencies != null
                    ? new List<string>(enemyData.startingProficiencies)
                    : new List<string>();
                unitTags = enemyData.tags != null ? new List<string>(enemyData.tags) : new List<string>();
                unitTier = enemyData.tier ?? "";
                LoadStatData(
                    enemyData.strBase, enemyData.strIncrementLvl, enemyData.strIncrementUpgrade,
                    enemyData.dexBase, enemyData.dexIncrementLvl, enemyData.dexIncrementUpgrade,
                    enemyData.conBase, enemyData.conIncrementLvl, enemyData.conIncrementUpgrade,
                    enemyData.intBase, enemyData.intIncrementLvl, enemyData.intIncrementUpgrade,
                    enemyData.lukBase, enemyData.lukIncrementLvl, enemyData.lukIncrementUpgrade);
                ConfigureUltimateResource(enemyData.ultimateResourceType, enemyData.ultimateResourceName, enemyData.ultimateResourceMax);
                CodeAcceleration = 1f;

                isCasting = false;
                castingTime = 0f;
                isControlled = false;
                controlTurns = 0;
                currentNormalTarget = null;

                LoadPassiveCodes(enemyData.codes["passive"], enemyData.levelPassives);
                _baseNormalCodeId = enemyData.codes["normal"];
                _baseUltimateCodeId = enemyData.codes["ultimate"];
                NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
                UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
                ApplyCodeStages(enemyData.codeStages);
                EquipStartingItems(enemyData.startingItemIds);
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
                subStats = data.subStats != null && data.subStats.Count > 0
                    ? new List<string>(data.subStats)
                    : string.IsNullOrWhiteSpace(data.subStat)
                        ? new List<string>()
                        : new List<string> { data.subStat };
                SubStat = subStats.FirstOrDefault() ?? "";
                mainStatTrainingBonus = Mathf.Max(0f, data.mainStatTrainingBonus);
                subStatTrainingBonus = Mathf.Max(0f, data.subStatTrainingBonus);
                startingProficiencies = data.startingProficiencies != null
                    ? new List<string>(data.startingProficiencies)
                    : new List<string>();
                unitTags = data.tags != null ? new List<string>(data.tags) : new List<string>();
                unitTier = "";   // 아군 영웅은 등급이 없다
                LoadStatData(
                    data.strBase, data.strIncrementLvl, data.strIncrementUpgrade,
                    data.dexBase, data.dexIncrementLvl, data.dexIncrementUpgrade,
                    data.conBase, data.conIncrementLvl, data.conIncrementUpgrade,
                    data.intBase, data.intIncrementLvl, data.intIncrementUpgrade,
                    data.lukBase, data.lukIncrementLvl, data.lukIncrementUpgrade);
                ConfigureUltimateResource(data.ultimateResourceType, data.ultimateResourceName, data.ultimateResourceMax);
                CodeAcceleration = 1f;

                isCasting = false;
                castingTime = 0f;
                isControlled = false;
                controlTurns = 0;
                currentNormalTarget = null; // 일반행동 타겟 초기화

                LoadPassiveCodes(data.codes["passive"], data.levelPassives);
                _baseNormalCodeId = data.codes["normal"];
                _baseUltimateCodeId = data.codes["ultimate"];
                NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
                UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
                ApplyCodeStages(data.codeStages);
                EquipStartingItems(data.startingItemIds);
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

            if (!EquipmentLoadout.TryEquip(itemData, out reason))
            {
                return false;
            }

            RefreshEquippedItemIds();
            RefreshEquipmentCodeGrants();
            AttributesUpdate();
            currentCell?.UpdateUI();
            return true;
        }

        /// <summary>보상 등으로 얻은 장비를 이 유닛의 개인 인벤토리에 넣는다.</summary>
        public bool TryStoreItem(int itemId, out string reason)
        {
            reason = null;
            if (!UsesCarryWeight)
            {
                reason = "적 유닛은 개인 인벤토리를 갖지 않습니다.";
                return false;
            }

            ItemData itemData = GetItemData(itemId);
            if (itemData == null)
            {
                reason = $"아이템 데이터를 찾을 수 없습니다: {itemId}";
                return false;
            }

            carriedItemIds ??= new List<int>();
            carriedItemIds.Add(itemId);
            AttributesUpdate();
            currentCell?.UpdateUI();
            return true;
        }

        /// <summary>개인 인벤토리의 장비를 장착하고 밀려난 장비는 다시 개인 인벤토리로 돌린다.</summary>
        public bool TryEquipCarriedItem(int itemId, out string reason)
        {
            reason = null;
            if (carriedItemIds == null || !carriedItemIds.Contains(itemId))
            {
                reason = "이 유닛이 휴대 중인 아이템이 아닙니다.";
                return false;
            }

            ItemData itemData = GetItemData(itemId);
            if (itemData == null)
            {
                reason = $"아이템 데이터를 찾을 수 없습니다: {itemId}";
                return false;
            }

            List<int> displaced = GetDisplacedEquippedItemIds(itemData);
            carriedItemIds.Remove(itemId);
            if (!TryEquipItem(itemId, out reason))
            {
                carriedItemIds.Add(itemId);
                return false;
            }

            carriedItemIds.AddRange(displaced);
            AttributesUpdate();
            currentCell?.UpdateUI();
            return true;
        }

        /// <summary>
        /// 장착 중인 장비를 벗긴다.
        /// <paramref name="storeToCarried"/>가 true면 개인 인벤토리로 들어가고,
        /// false면 그냥 손에서 놓는다(다른 유닛에게 넘길 때 쓴다).
        /// </summary>
        public bool TryUnequip(int itemId, bool storeToCarried)
        {
            if (EquipmentLoadout == null || !EquipmentLoadout.Unequip(itemId)) return false;

            // 적은 보관함이 없다. 벗은 장비는 그대로 사라진다.
            if (storeToCarried && UsesCarryWeight)
            {
                carriedItemIds ??= new List<int>();
                carriedItemIds.Add(itemId);
            }

            RefreshEquippedItemIds();
            RefreshEquipmentCodeGrants();
            AttributesUpdate();
            currentCell?.UpdateUI();
            return true;
        }

        /// <summary>이 유닛이 이 아이템을 장착 중인지.</summary>
        public bool IsEquipped(int itemId)
        {
            if (EquipmentLoadout == null) return false;

            foreach (ItemData equipped in EquipmentLoadout.GetEquippedItemData())
            {
                if (equipped != null && equipped.id == itemId) return true;
            }

            return false;
        }

        public bool RemoveCarriedItem(int itemId)
        {
            bool removed = carriedItemIds != null && carriedItemIds.Remove(itemId);
            if (removed)
            {
                AttributesUpdate();
                currentCell?.UpdateUI();
            }
            return removed;
        }

        private List<int> GetDisplacedEquippedItemIds(ItemData selected)
        {
            var result = new List<int>();
            if (selected == null || !EquipmentLoadout.TryParseSlot(selected.slot, out EquipmentSlot selectedSlot)) return result;

            foreach (ItemData equipped in EquipmentLoadout.GetEquippedItemData())
            {
                if (equipped == null || !EquipmentLoadout.TryParseSlot(equipped.slot, out EquipmentSlot equippedSlot)) continue;
                if (equippedSlot == selectedSlot ||
                    (selectedSlot == EquipmentSlot.MainHand && selected.twoHanded && equippedSlot == EquipmentSlot.OffHand))
                {
                    result.Add(equipped.id);
                }
            }
            return result;
        }

        private int GetStoredItemWeight()
        {
            if (!UsesCarryWeight || carriedItemIds == null || carriedItemIds.Count == 0) return 0;
            int total = 0;
            foreach (int itemId in carriedItemIds)
            {
                ItemData itemData = GetItemData(itemId);
                total += Mathf.Max(0, itemData?.weight ?? 0);
            }
            return total;
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
                if (!CanUseEquipmentEffects(equippedItem)) continue;
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

        private bool HasEquipmentProficiency(EquipmentProficiency proficiency)
        {
            if (proficiency == EquipmentProficiency.None) return true;

            bool hasStartingProficiency = startingProficiencies.Any(value =>
                Enum.TryParse(value, true, out EquipmentProficiency parsed) && parsed == proficiency);
            if (hasStartingProficiency) return true;

            // 모션 문제로 숙련을 부여하는 코드는 전부 삭제했다.
            // 만류귀종(55)은 없어졌고, 궁수(140)·민첩함(141)도 더 이상 숙련을 열지 않는다.
            // 각 캐릭터가 쓸 수 있는 장비는 시작 숙련이 전부다.
            return false;
        }

        /// <summary>숙련 장비 또는 숙련이 필요 없는 의복만 실제 효과를 낸다.</summary>
        public bool CanUseEquipmentEffects(ItemData itemData)
        {
            if (itemData == null) return false;
            return itemData.RequiredProficiency == EquipmentProficiency.None ||
                   HasEquipmentProficiency(itemData.RequiredProficiency);
        }

        public bool HasEquippedProficiency(EquipmentProficiency proficiency)
        {
            return EquipmentLoadout != null && EquipmentLoadout.GetEquippedItemData()
                .Any(item => item != null && item.RequiredProficiency == proficiency && CanUseEquipmentEffects(item));
        }

        public bool HasEquippedBow()
        {
            return EquipmentLoadout != null && EquipmentLoadout.GetEquippedItemData()
                .Any(item => item != null && item.RequiredProficiency.IsBow() && CanUseEquipmentEffects(item));
        }

        /// <summary>숙련이 필요 없는 Armor 슬롯 장비(의복류)를 착용 중인가.</summary>
        public bool HasEquippedClothing()
        {
            return EquipmentLoadout != null && EquipmentLoadout.GetEquippedItemData().Any(item =>
                item != null && item.RequiredProficiency == EquipmentProficiency.None &&
                (string.Equals(item.slot, "Armor", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.slot, "Body", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.slot, "갑옷", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>적 등급. 아군은 빈 문자열이다.</summary>
        public string UnitTier => unitTier ?? "";

        /// <summary>
        /// 처형(즉사) 대상이 될 수 있는가.
        ///
        /// **일반 등급의 적만 처형된다.** 엘리트·보스는 물론이고, 아군 영웅도 처형되지 않는다.
        /// 적이 처형 코드를 들고 있어도 아군을 즉사시킬 수 없다는 뜻이다.
        /// </summary>
        public bool IsExecutable =>
            IsEnemy && string.Equals(unitTier, "normal", StringComparison.OrdinalIgnoreCase);

        public bool HasUnitTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            if (unitTags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase))) return true;
            return ActiveEffectObjects().Any(effect => effect.GrantsUnitTag(this, tag));
        }

        /// <summary>고유 패시브처럼 런타임에 유닛 분류를 추가한다. 동일 태그는 중복되지 않는다.</summary>
        public void GrantUnitTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || HasUnitTag(tag)) return;
            unitTags.Add(tag);
        }

        public bool HasLearnedPassiveCode(int codeId)
        {
            return learnedPassiveRecords.Any(record => record != null && record.codeId == codeId);
        }

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
                    case "일반행동":
                        NormalCode?.StopCode();
                        NormalCode = CodeFactory.CreateNormalCode(codeGrant.codeId, new NormalCodeContext { Caster = this });
                        NormalCode?.SetStage(codeGrant.stage);
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

        /// <summary>
        /// 착용 장비가 제공하는 내구의 합. 받는 피해에서 고정으로 차감된다.
        /// 방어력과 달리 비율이 아니라 절대량이므로, 방어력을 100% 무시당해도 남는다.
        /// </summary>
        public int DurabilityCurr
        {
            get
            {
                if (EquipmentLoadout == null) return 0;

                int total = 0;
                foreach (ItemData itemData in EquipmentLoadout.GetEquippedItemData())
                {
                    if (itemData == null || !CanUseEquipmentEffects(itemData)) continue;
                    float itemMultiplier = 1f;
                    foreach (var effect in ActiveEffectObjects())
                    {
                        itemMultiplier *= effect.EquipmentDurabilityMultiplierModifier(this, itemData);
                    }

                    // 분류가 정한 고정 내구(방어구)와 스탯 칸으로 고른 내구를 함께 센다.
                    // 아이템 단위 배율(금빛 짐승의 가죽 방패 등)은 둘 모두에 걸린다.
                    int itemDurability = itemData.durability + SecondaryDurabilityOf(itemData);
                    total += Mathf.Max(0, Mathf.RoundToInt(itemDurability * itemMultiplier));
                }
                total += GetStatusDurabilityBonus();
                float multiplier = 1f;
                foreach (var effect in ActiveEffectObjects())
                {
                    multiplier *= effect.DurabilityMultiplierModifier(this);
                }
                return Mathf.Max(0, Mathf.RoundToInt(total * multiplier));
            }
        }

        /// <summary>장비 한 점이 스탯 칸으로 고른 내구.</summary>
        private static int SecondaryDurabilityOf(ItemData itemData)
        {
            if (itemData?.statBonuses == null) return 0;

            int total = 0;
            foreach (EquipmentStatBonus bonus in itemData.statBonuses)
            {
                if (bonus != null && bonus.Secondary == EquipmentSecondaryStat.Durability) total += bonus.amount;
            }

            return total;
        }

        /// <summary>상태 효과가 더해 주는 내구.</summary>
        private int GetStatusDurabilityBonus()
        {
            int total = 0;
            foreach (var effect in ActiveEffectObjects())
            {
                total += effect.DurabilityAdditiveModifier(this);
            }
            return total;
        }

        internal int GetEquipmentStatBonus(BaseEnums.PrimaryStat stat)
        {
            if (EquipmentLoadout == null) return 0;

            int total = 0;
            foreach (ItemData itemData in EquipmentLoadout.GetEquippedItemData())
            {
                if (itemData?.statBonuses == null || !CanUseEquipmentEffects(itemData)) continue;

                foreach (EquipmentStatBonus bonus in itemData.statBonuses)
                {
                    if (bonus == null) continue;
                    if (bonus.TryGetPrimary(out BaseEnums.PrimaryStat bonusStat) && bonusStat == stat)
                    {
                        total += bonus.amount;
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// 장비가 5스탯 대신 실은 부가 수치의 합 (치명타 확률·치명타 피해·내구).
        /// 5스탯과 같은 규칙으로 <b>숙련이 없으면 통째로 죽는다.</b>
        /// </summary>
        internal int GetEquipmentSecondaryStatBonus(EquipmentSecondaryStat stat)
        {
            if (EquipmentLoadout == null || stat == EquipmentSecondaryStat.None) return 0;

            int total = 0;
            foreach (ItemData itemData in EquipmentLoadout.GetEquippedItemData())
            {
                if (itemData?.statBonuses == null || !CanUseEquipmentEffects(itemData)) continue;

                foreach (EquipmentStatBonus bonus in itemData.statBonuses)
                {
                    if (bonus != null && bonus.Secondary == stat) total += bonus.amount;
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
            levelPassiveInstances.Clear();
            PassiveCodes.Clear();
            PendingLevelPassives.Clear();

            PassiveCode innate = CodeFactory.CreatePassiveCode(innatePassiveId, new PassiveCodeContext { Caster = this });
            if (innate != null)
            {
                PassiveCodes.Add(innate);
                AddLearnedPassiveRecord(innatePassiveId, innate.CurrentStage, innate.Transferable);
                // 모든 고유 패시브는 전수 불가다. 열화 전수본 제도는 폐지했다.
            }

            // 팔랑크스(183)는 모든 Greek 유닛이 자동으로 가진다.
            // 코드 용량을 차지하지 않아야 하므로 levelPassives가 아니라 여기서 직접 붙인다.
            if (!IsEnemy && HasUnitTag("Greek") &&
                PassiveCodes.All(code => code is not GreekPhalanx))
            {
                PassiveCode phalanx = CodeFactory.CreatePassiveCode(
                    183, new PassiveCodeContext { Caster = this });
                if (phalanx != null) PassiveCodes.Add(phalanx);
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
        /// 현재 Level의 해금 조건을 만족한 패시브를 활성 목록으로 승격한다. 개수 상한은 없다.
        /// 레벨업(예: 육성 페이즈, 업그레이드) 이후 호출한다. 이미 활성화된 패시브는 중복 추가하지 않는다.
        /// </summary>
        protected void RefreshLevelPassives()
        {
            foreach (var pair in levelPassiveInstances.Where(pair => PassiveUnlockLevel < pair.Key.unlockLevel).ToList())
            {
                pair.Value.code.StopCode();
                PassiveCodes.Remove(pair.Value.code);
                if (pair.Value.ownsRecord && !grantedPassiveCodeIds.Contains(pair.Key.codeId))
                    learnedPassiveRecords.RemoveAll(record => record.codeId == pair.Key.codeId);
                PendingLevelPassives.Add(pair.Key);
                levelPassiveInstances.Remove(pair.Key);
            }
            if (PendingLevelPassives.Count == 0) return;

            List<LevelPassiveData> eligible = PendingLevelPassives
                .Where(def => def != null && PassiveUnlockLevel >= def.unlockLevel)
                .OrderBy(def => def.unlockLevel)
                .ToList();

            foreach (LevelPassiveData def in eligible)
            {
                PassiveCode code = CodeFactory.CreatePassiveCode(def.codeId, new PassiveCodeContext { Caster = this });
                if (code != null)
                {
                    if (def.stage > 0) code.SetStage(def.stage);
                    PassiveCodes.Add(code);
                    levelPassiveInstances[def] = (code, !HasLearnedPassiveCode(def.codeId));
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
            if (learnedPassiveRecords.Any(record => record != null && record.codeId == codeId))
            {
                return false;
            }

            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = this });
            if (code == null) return false;
            if (!code.Transferable || code.IsUniquePassive) return false;

            code.SetStage(stage);
            PassiveCodes.Add(code);
            AddLearnedPassiveRecord(codeId, code.CurrentStage, code.Transferable);
            return true;
        }

        /// <summary>사건/보상으로 획득한 런 영구 패시브를 추가한다.</summary>
        public bool GrantPermanentPassive(int codeId, int stage = 1)
        {
            if (codeId <= 0 || grantedPassiveCodeIds.Contains(codeId)) return false;
            PassiveCode code = CodeFactory.CreatePassiveCode(codeId, new PassiveCodeContext { Caster = this });
            if (code == null || code.IsUniquePassive) return false;
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

        // ── 경험치와 레벨업 ────────────────────────────────────────
        // 적의 레벨은 스테이지가 정하지만, 아군은 EXP를 모아 직접 레벨을 올린다.
        // EXP 획득처: 적 처치 / 스테이지 클리어 / 육성 페이즈 (GameManager가 지급).

        /// <summary>다음 레벨까지 필요한 누적 EXP.</summary>
        public static int RequiredExpForLevel(int currentLevel)
            => ExpBaseRequirement + ExpRequirementPerLevel * Mathf.Max(0, currentLevel - 1);

        public int Exp { get => exp; protected set => exp = value; }

        /// <summary>현재 레벨에서 다음 레벨까지 필요한 EXP.</summary>
        public int ExpToNextLevel => RequiredExpForLevel(Level);

        /// <summary>
        /// EXP를 지급하고 필요량을 넘으면 레벨을 올린다. 여러 레벨이 한 번에 오를 수 있다.
        /// 적은 스테이지가 레벨을 정하므로 EXP를 받지 않는다.
        /// </summary>
        public int AddExp(int amount)
        {
            if (IsEnemy || amount <= 0) return 0;

            exp += amount;
            int levelsGained = 0;
            while (exp >= ExpToNextLevel)
            {
                exp -= ExpToNextLevel;
                Level += 1;
                levelsGained++;
            }

            if (levelsGained > 0)
            {
                // 레벨업으로 최대 체력이 늘어난 만큼 현재 체력도 함께 올려 준다.
                int previousHpMax = HpMax;
                RefreshLevelPassives();
                AttributesUpdate();
                HpCurr = Mathf.Clamp(HpCurr + Mathf.Max(0, HpMax - previousHpMax), 0, HpMax);
                Debug.Log($"[레벨업] {UnitName} Lv.{Level - levelsGained} → Lv.{Level}");
            }

            currentCell?.UpdateUI();
            return levelsGained;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 디버그 전용 — 레벨을 원하는 값으로 바로 맞춘다.
        ///
        /// <see cref="AddExp"/>는 올리기만 하므로 "Lv.90 해금 코드를 지금 보고 싶다"를 만들 수 없다.
        /// 레벨을 내릴 때도 <see cref="RefreshLevelPassives"/>가 조건을 다시 재므로 해금이 함께 정리된다.
        /// </summary>
        public void DebugSetLevel(int targetLevel)
        {
            Core.DebugMode.BeginSession();
            bool inBattle = GameManager.Instance?.gameState == BaseEnums.GameState.RoundInProgress;
            DebugResetCombatState();
            int previousHpMax = HpMax;
            Level = Mathf.Max(1, targetLevel);
            exp = 0;
            RefreshLevelPassives();
            AttributesUpdate();
            // 최대 체력이 늘어난 만큼 현재 체력도 따라 올린다(줄어들면 AttributesUpdate가 비율로 맞춘다).
            HpCurr = Mathf.Clamp(HpCurr + Mathf.Max(0, HpMax - previousHpMax), 1, HpMax);
            currentCell?.UpdateUI();
            if (inBattle) CastPassiveCode();
            Debug.Log($"[디버그] {UnitName} 레벨을 {Level}로 맞췄다.");
        }

        public void DebugResetCombatState()
        {
            StopAllCoroutines();
            NormalCode?.StopCode();
            UltimateCode?.StopCode();
            Invoke(BaseEnums.UnitEventType.OnRoundEnd, new EventContext(this));
            foreach (var code in PassiveCodes.Concat(ItemPassiveCodes).ToList()) code?.StopCode();
            isCasting = false;
            isControlled = false;
            controlTurns = 0;
            ultimateCooldown = 0;
            currentNormalTarget = null;
        }
#endif

        private void LoadStatData(
            int dataStrBase, int dataStrIncrementLvl, int dataStrIncrementUpgrade,
            int dataDexBase, int dataDexIncrementLvl, int dataDexIncrementUpgrade,
            int dataConBase, int dataConIncrementLvl, int dataConIncrementUpgrade,
            int dataIntBase, int dataIntIncrementLvl, int dataIntIncrementUpgrade,
            int dataLukBase, int dataLukIncrementLvl, int dataLukIncrementUpgrade)
        {
            stats.Load(
                dataStrBase, dataStrIncrementLvl, dataStrIncrementUpgrade,
                dataDexBase, dataDexIncrementLvl, dataDexIncrementUpgrade,
                dataConBase, dataConIncrementLvl, dataConIncrementUpgrade,
                dataIntBase, dataIntIncrementLvl, dataIntIncrementUpgrade,
                dataLukBase, dataLukIncrementLvl, dataLukIncrementUpgrade);
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
            Sprite portrait = SpriteResource.LoadPortrait(_name, out string path);
            PortraitPath = path;

            // 전장의 유닛은 언제나 초상화 카드다. 전용 인게임 SD 스프라이트 경로는 없앴다 —
            // 해상도와 여백이 제각각이라 카드 안에서 크기가 들쭉날쭉했고,
            // 연출 방향도 스프라이트가 아니라 카드가 반응하고 발사하는 쪽으로 잡았다.
            // 타격·시전 반응은 UnitCardView가 카드 자체를 흔들어 처리한다.
            currentCell?.SetPortrait(portrait);
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
            float manaRecoveryIntContributionMultiplier = 1f;
            float maxHpMultiplier = 1f;

            List<Effects.Base.BaseEffect> activeEffects = ActiveEffectObjects().ToList();
            foreach (var effect in activeEffects)
            {
                critChanceAdd += effect.CritChanceAdditiveModifier(this);
                critMultiplierAdd += effect.CritMultiplierAdditiveModifier(this);
                shieldBonusAdd += effect.ShieldBonusAdditiveModifier(this);
                codeAccelerationAdd += effect.CodeAccelerationAdditiveModifier(this);
                manaRecoveryMultiplier *= effect.ManaRecoveryMultiplierModifier(this);
                manaRecoveryIntContributionMultiplier *= effect.ManaRecoveryIntContributionMultiplierModifier(this);
                maxHpMultiplier *= effect.MaxHpMultiplierModifier(this);
            }
            // hp 비율 저장
            float healthRatio = (HpMax > 0) ? (float)HpCurr / HpMax : 1f;

            HpMax = Mathf.Max(1, Mathf.RoundToInt(GetDerivedHp() * maxHpMultiplier));
            // 공격력은 스탯으로 존재하지 않는다. 피해는 스킬 위력 × 주스탯으로 그때그때 계산한다.
            ManaMax = GetUltimateResourceMax();
            DefCurr = GetDerivedDef();
            // 장비가 실은 치명타 수치는 %p 단위로 적히므로 0.01을 곱해 배율 축으로 옮긴다.
            critChanceAdd += GetEquipmentSecondaryStatBonus(EquipmentSecondaryStat.CritRate) * 0.01f;
            critMultiplierAdd += GetEquipmentSecondaryStatBonus(EquipmentSecondaryStat.CritDamage) * 0.01f;

            float rawCritChance = Mathf.Max(0f, GetDerivedCritChance() + critChanceAdd);
            ExcessCritChanceCurr = Mathf.Max(0f, rawCritChance - 1f);
            CritChanceCurr = Mathf.Clamp01(rawCritChance);
            float excessCritConversion = activeEffects.Sum(effect => effect.ExcessCritChanceConversionMultiplier(this));
            CritMultiplierCurr = Mathf.Max(1f,
                GetDerivedCritDamage() + critMultiplierAdd + ExcessCritChanceCurr * excessCritConversion);
            EvasionChanceCurr = GetDerivedEvasionChance();
            HealingBonusCurr = GetDerivedHealingBonus();
            ShieldBonusCurr = Mathf.Max(0f, GetDerivedShieldBonus() + shieldBonusAdd);
            float baseManaEfficiency = GetDerivedManaEfficiency();
            ManaEfficiencyCurr = (1f + (baseManaEfficiency - 1f) * manaRecoveryIntContributionMultiplier) *
                                 manaRecoveryMultiplier;
            CodeActivationChanceCurr = GetDerivedCodeActivationChance();
            CodeAcceleration = Mathf.Max(0.1f, GetDerivedCodeAcceleration() + CodeAccelerationRunBonus + codeAccelerationAdd);
            // 별도 공격속도 스탯·배율은 없다. 행동 빈도는 최종 DEX에서만 파생된다.
            float actionSpeed = GetDerivedActionSpeed();
            foreach (var effect in activeEffects)
            {
                actionSpeed = effect.ActionSpeedModifier(this, actionSpeed);
            }
            ActionSpeedCurr = Mathf.Max(0.1f, actionSpeed);

            // hp 비율 복구
            HpCurr = Mathf.RoundToInt(HpMax * healthRatio);
            
            // 방어막 바 업데이트
            UpdateShieldBar();
        }

        /// <summary>
        /// 방어막 바의 시각적 표시를 업데이트
        /// </summary>
        /// <summary>동적 패시브 스택이 바뀌었을 때 파생 스탯 캐시를 즉시 갱신한다.</summary>
        public void RefreshAttributes()
        {
            AttributesUpdate();
            currentCell?.UpdateUI();
        }

        protected virtual void UpdateShieldBar()
        {
            // 체력·마나·방어막 바는 카드가 함께 들고 있다. 칸이든 소환수 카드든 한 곳으로 보낸다.
            RefreshView();
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
        /// 소환수로 태어난다. 칸도, <c>10_units.yaml</c> 항목도 없다.
        ///
        /// 능력치는 <b>소환 시점 소환자의 기본 스탯</b>을 비율로 물려받아 레벨 1 고정값으로 굳힌다.
        /// 소환 뒤에 소환자가 강해져도 따라 오르지 않는다 — 스냅샷이 소환수의 정의다.
        /// 행동 게이지(DEX)와 궁극기 자원(INT)은 일반 유닛과 완전히 같은 경로를 탄다.
        /// </summary>
        public void SpawnAsSummon(Unit owner, Combat.SummonSpec spec)
        {
            if (owner == null || spec == null) return;

            isActive = true;
            InitializeUnit(owner.IsEnemy, 0);   // ID 0 — 데이터 파일을 읽지 않는 경로
            owner.RegisterSummon(this);

            Level = 1;
            UnitName = spec.Name;
            Element = spec.ResolveElement(owner);
            ResetCombatElements();
            MainStat = spec.ResolveMainStat(owner);
            SubStat = "";
            subStats = new List<string>();
            mainStatTrainingBonus = 0f;
            subStatTrainingBonus = 0f;
            unitTier = "";
            if (!string.IsNullOrWhiteSpace(spec.Portrait)) LoadSprite(spec.Portrait, owner.IsEnemy);

            spec.ResolveStats(owner, out int str, out int dex, out int con, out int intel, out int luk);
            // 증가분 0 — 소환수는 레벨도 강화도 없다.
            LoadStatData(str, 0, 0, dex, 0, 0, con, 0, 0, intel, 0, 0, luk, 0, 0);
            ConfigureUltimateResource(null, null, 0);   // 마나형 기본값
            CodeAcceleration = 1f;

            isCasting = false;
            castingTime = 0f;
            isControlled = false;
            controlTurns = 0;
            currentNormalTarget = null;

            _baseNormalCodeId = spec.NormalCodeId;
            _baseUltimateCodeId = spec.UltimateCodeId;
            NormalCode = CodeFactory.CreateNormalCode(_baseNormalCodeId, new NormalCodeContext { Caster = this });
            UltimateCode = CodeFactory.CreateUltimateCode(_baseUltimateCodeId, new UltimateCodeContext { Caster = this });
            ultimateCooldown = UltimateCode?.Cooldown ?? 0f;

            AttributesUpdate();
            HpCurr = HpMax;
            ManaCurr = 0;
            ShieldMax = 0;
            ShieldCurr = 0;

            // InitializeUnit은 ID 0에서 조기 반환하므로 기본 리스너가 붙지 않는다.
            // 이걸 빠뜨리면 소환수가 피해를 아예 받지 않는다.
            AddListener<EventContext>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
            AddListener<EventContext>(BaseEnums.UnitEventType.OnUpdate, DefaultUpdateEvent);

            BindSummonLifetime(owner, spec.LifetimeTurns);
            Invoke(BaseEnums.UnitEventType.OnSpawn, new EventContext(this));
        }

        // ── 소환수 수명 ───────────────────────────────────────────
        //
        // '라이트 기준 3턴'처럼 수명은 <b>소환자의 턴</b>으로 센다. 소환수의 DEX가 소환자와
        // 다르므로 자기 턴으로 세면 같은 3턴이 매번 다른 길이가 된다.
        private int _summonTurnsRemaining;
        private Action<EventContext> _summonLifetimeHandler;

        private void BindSummonLifetime(Unit owner, int turns)
        {
            if (owner == null || turns <= 0) return;

            _summonTurnsRemaining = turns;
            _summonLifetimeHandler = _ =>
            {
                if (!isActive) return;
                if (--_summonTurnsRemaining > 0) return;
                Die(null);
            };
            owner.AddListener(BaseEnums.UnitEventType.OnTurnStart, _summonLifetimeHandler);
        }

        private void ReleaseSummonLifetime()
        {
            if (_summonLifetimeHandler == null) return;
            SummonOwner?.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _summonLifetimeHandler);
            _summonLifetimeHandler = null;
            _summonTurnsRemaining = 0;
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

            if (_postDeathActions.Count == 0) return;
            List<Action> actions = _postDeathActions.ToList();
            _postDeathActions.Clear();
            foreach (Action action in actions)
            {
                action?.Invoke();
            }
        }

        /// <summary>사망 이벤트가 끝나고 원래 칸이 비워진 직후 실행할 작업을 예약한다.</summary>
        internal void EnqueuePostDeathAction(Action action)
        {
            if (action != null) _postDeathActions.Add(action);
        }

        /// <summary>
        /// 유닛 피해 처리 이벤트<br/>
        /// 다른 피해 처리 이벤트를 등록하지 않았을 경우 기본 피해 처리 이벤트(DefaultTakeDamageEvent) 호출
        /// </summary>
        /// <param name="context">피해 정보 컨텍스트</param>
        public virtual void TakeDamage(DamageContext context)
        {
            if (context == null) return;
            // 광역 공격은 같은 컨텍스트를 여러 대상에게 재사용할 수 있으므로 대상마다 초기화한다.
            context.ResolvedDamage = 0;
            if (IsUntargetable)
            {
                context.IsCancelled = true;
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 디버그 무적. 지속피해·고정피해까지 한 곳에서 막으려면 입구에서 잘라야 한다.
            if (IsEnemy ? Core.DebugMode.EnemyInvincible : Core.DebugMode.AllyInvincible)
            {
                context.IsCancelled = true;
                return;
            }
#endif
            // 즉시 피해형 스킬도 Slash 태그/근접 병종 판정을 거쳐 타격 VFX를 얻는다.
            // 일반행동 발사 루틴에서 이미 연출한 대상은 DamageContext 표식으로 중복을 막는다.
            GameManager.Instance?.sfxManager?.PlayDamageImpact(this, context);
            Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, new EventContext(this, context.Attacker, context));
            Invoke(BaseEnums.UnitEventType.OnTakingDamage, new EventContext(this, null, context));
            Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, new EventContext(this, context.Attacker, context));
        }

        /// <summary>
        /// 유닛이 행동불능 상태가 되었을 때 호출되는 이벤트
        /// </summary>
        /// <param name="context">공격자와 지속시간을 지닌 제어 정보 컨텍스트</param>
        public virtual void ControlStarts(ControlContext context)
        {
            isControlled = true;
            controlTurns = Mathf.Max(1, Mathf.RoundToInt(context.Duration));
            Invoke(BaseEnums.UnitEventType.OnControlStarts, new EventContext(this, context.Attacker));
        }

        /// <summary>
        /// 유닛이 행동불능 상태에서 벗어났을 때 호출되는 이벤트
        /// </summary>
        public virtual void ControlEnds()
        {
            isControlled = false;
            controlTurns = 0;
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

            // 같은 계열의 강화 등급을 이미 배웠으면 일반 등급은 발동하지 않는다.
            if (passiveCode.SupersededByCodeId > 0 &&
                HasLearnedPassiveCode(passiveCode.SupersededByCodeId))
            {
                Debug.Log($"{UnitName}의 {passiveCode.CodeName}은(는) 강화 등급 코드에 대체되어 발동하지 않는다");
                return;
            }

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
            // 예약과 실행 사이에 앞선 추가행동이 마지막 적을 지웠을 수 있다.
            // 자원을 먼저 태우면 아무 일도 못 하고 궁극기 한 번을 통째로 잃는다.
            if (UltimateCode != null && !UltimateCode.HasValidTarget())
            {
                Debug.Log($"{UnitName}의 궁극기 {UltimateCode.CodeName}은(는) 대상이 없어 자원을 유지한 채 취소한다");
                return;
            }

            ConsumeUltimateResource();
            // Cell의 통합 UI 시스템 사용
            RefreshView();

            UltimateCode.CastCode();
            Invoke(BaseEnums.UnitEventType.OnUltimateActivates, new EventContext(this));
            if (UltimateCode.IsAutoCast) AnyActiveUltimateActivated?.Invoke(this);
        }

        // ── 궁극기 자원 ────────────────────────────────────────
        //
        // 예전에는 일반행동 한 번에 고정량을 얻었다. 그래서 행동이 잦은
        // 고DEX 유닛일수록 궁극기가 빨리 찼고, INT는 곁가지 배율에 그쳤다.
        // 지금은 <b>전투 시간</b>에 비례해 차오르므로 행동 횟수와 무관하다.
        // 느리더라도 INT가 높으면 궁극기로 화력을 내는 빌드가 성립한다.

        /// <summary>전투 시간과 무관하게 항상 붙는 최소 회복량(초당).</summary>
        private const float ManaRegenBase = 1.5f;

        /// <summary>INT 1당 늘어나는 초당 회복량.</summary>
        private const float ManaRegenPerInt = 0.15f;

        /// <summary>
        /// 초당 회복량 상한. 자원 100 기준 <b>전투 시간 5초</b>가 최단 충전 주기다.
        ///
        /// 기본 회복량이 INT에 비례하는데 <see cref="ManaEfficiencyCurr"/>도 INT에 비례해서,
        /// 곱하면 사실상 INT의 제곱으로 자란다. 고레벨 캐스터가 매 턴 궁극기를 쓰는 것을 막는 뚜껑이다.
        /// </summary>
        private const float ManaRegenCap = 20f;

        /// <summary>
        /// 전투 시간 1초당 회복하는 궁극기 자원.
        /// DEX는 관여하지 않는다. INT가 유일한 충전 속도 스탯이다.
        /// </summary>
        public float ManaPerCombatSecond => Mathf.Min(ManaRegenCap,
            (ManaRegenBase + Mathf.Max(0, GetBaseInt()) * ManaRegenPerInt) * ManaEfficiencyCurr);

        /// <summary>흐른 전투 시간만큼 궁극기 자원을 채운다. 소수점은 다음 정산으로 넘긴다.</summary>
        public void AccrueUltimateResource(float combatSeconds)
        {
            if (combatSeconds <= 0f) return;
            if (UltimateResourceType != BaseEnums.UltimateResourceType.Mana) return;
            if (!isActive || ManaCurr >= ManaMax) return;

            _manaCarry += ManaPerCombatSecond * combatSeconds;
            int whole = Mathf.FloorToInt(_manaCarry);
            if (whole <= 0) return;

            _manaCarry -= whole;
            AddUltimateResource(whole);
        }

        private float _manaCarry;

        public virtual void RecoverMana(int amount)
        {
            if (UltimateResourceType != BaseEnums.UltimateResourceType.Mana)
            {
                return;
            }

            int adjustedAmount = Mathf.Max(0, Mathf.RoundToInt(amount * ManaEfficiencyCurr));
            AddUltimateResource(adjustedAmount);
        }

        /// <summary>
        /// 궁극기 자원을 최대치로 채운다.
        /// <paramref name="manaOnly"/>가 true면 마나형 자원을 쓰는 유닛에게만 적용된다
        /// (세이의 '사전준비'는 마나를 쓰지 않는 특수 궁극기에 효과가 없다).
        /// </summary>
        public void FillUltimateResource(bool manaOnly)
        {
            if (manaOnly && UltimateResourceType != BaseEnums.UltimateResourceType.Mana) return;
            AddUltimateResource(Mathf.Max(0, ManaMax - ManaCurr));
        }

        public virtual void AddUltimateResource(int amount)
        {
            ManaCurr = Mathf.Clamp(ManaCurr + amount, 0, ManaMax);
            // Cell의 통합 UI 시스템 사용
            RefreshView();
        }

        /// <summary>
        /// 전투 시작 시 원소 상태를 초기화한다.
        ///
        /// <b>유닛의 고유 원소는 부착이 아니라 판정 전용이다.</b> 원신처럼 캐릭터의 속성과
        /// 실제로 걸린 원소 부착은 별개의 축이다. 바위 유닛이라는 사실만으로 바위가
        /// '부착'되어 있다고 보면, 누가 바위를 걸어 주는 순간 진동이 터져 아군이 기절한다.
        /// 조건 판정(<see cref="HasCombatElement"/>)에는 그대로 잡히고,
        /// 원소 반응의 재료(<see cref="HasAttachedElement"/>)로는 쓰이지 않는다.
        /// </summary>
        public void ResetCombatElements()
        {
            _combatElements.Clear();
            _judgementElements.Clear();
            _temporaryElementDurations.Clear();
            if (Enum.TryParse(Element, true, out BaseEnums.UnitElement innateElement) &&
                innateElement != BaseEnums.UnitElement.None)
            {
                _judgementElements.Add(innateElement);
            }
        }

        /// <summary>원소를 부착한다. 부착 직후 원소 반응을 검사한다.</summary>
        public void GrantCombatElement(BaseEnums.UnitElement elementToGrant, int duration = CommonElementAuraDuration)
            => GrantCombatElement(elementToGrant, duration, null);

        /// <param name="source">부착을 일으킨 유닛. 원소 반응 피해가 이 유닛의 CON에 비례한다.</param>
        /// <param name="suppressReaction">
        /// 반응 판정을 건너뛴다. <b>확산이 뿌리는 부착</b>이 다시 반응을 일으켜
        /// 무한 연쇄가 되는 것을 막는 데 쓴다.
        /// </param>
        public void GrantCombatElement(
            BaseEnums.UnitElement elementToGrant, int duration, Unit source, bool suppressReaction = false)
        {
            if (elementToGrant == BaseEnums.UnitElement.None) return;

            // 자기 고유 원소를 받더라도 다른 원소와 똑같은 '부착'이다. 고유 원소는 판정 축에만
            // 살아 있으므로, 여기서 예외를 두면 지속시간 없는 영구 부착이 생겨 축이 다시 섞인다.
            bool added = _combatElements.Add(elementToGrant);
            _temporaryElementDurations[elementToGrant] = duration > 0 ? duration : CommonElementAuraDuration;
            if (added)
            {
                AttributesUpdate();
                currentCell?.UpdateUI();
            }

            AnyCombatElementGranted?.Invoke(source, this, elementToGrant);

            // 부착 직후 반응 검사. 반응하면 두 원소가 함께 소모된다.
            // added == false는 같은 원소가 이미 붙어 있었다는 뜻이다 — 같은 원소 반응의 재료가 된다.
            if (!suppressReaction)
            {
                Effects.Negative.ElementalReaction.TryResolve(this, elementToGrant, source, !added);
            }
        }

        /// <summary>부착된 원소를 걷어낸다. 고유 원소는 부착이 아니므로 여기서 사라지지 않는다.</summary>
        public void RemoveCombatElement(BaseEnums.UnitElement elementToRemove)
        {
            if (!_combatElements.Remove(elementToRemove)) return;
            _temporaryElementDurations.Remove(elementToRemove);
            AttributesUpdate();
            currentCell?.UpdateUI();
        }

        /// <summary>원소 반응이 일어났음을 알린다. 반응 연계 패시브가 이 신호를 듣는다.</summary>
        public static event Action<Unit, Unit, string> AnyElementalReaction;

        /// <summary>원소가 부착될 때 발행한다. (부여자, 대상, 부여 원소)</summary>
        public static event Action<Unit, Unit, BaseEnums.UnitElement> AnyCombatElementGranted;

        /// <summary>발동형 궁극기가 사용될 때 발행한다. 상시형 궁극기는 제외한다.</summary>
        public static event Action<Unit> AnyActiveUltimateActivated;

        internal static void NotifyElementalReaction(Unit source, Unit target, string reactionName)
            => AnyElementalReaction?.Invoke(source, target, reactionName);

        /// <summary>
        /// 이 원소를 가진 것으로 <b>판정</b>되는가. 실제 부착과 판정 전용 원소를 함께 본다.
        /// 유닛의 고유 원소는 판정 축에 있으므로 여기서는 잡히고, 반응 재료로는 쓰이지 않는다.
        /// 원소 반응 판정에는 <see cref="HasAttachedElement"/>를 써야 한다.
        /// </summary>
        public bool HasCombatElement(BaseEnums.UnitElement elementToCheck)
        {
            return _combatElements.Contains(elementToCheck) || _judgementElements.Contains(elementToCheck);
        }

        /// <summary>
        /// 실제로 <b>부착된</b> 원소인가. 원소 반응은 이쪽만 재료로 쓴다.
        /// 유닛의 고유 원소는 포함되지 않는다 — 속성과 부착은 별개의 축이다.
        /// </summary>
        /// <summary>부착된 원소의 남은 턴. 붙어 있지 않으면 0이다. 값이 클수록 최근에 붙었다.</summary>
        public int GetAttachedElementRemainingTurns(BaseEnums.UnitElement elementToCheck)
            => _temporaryElementDurations.TryGetValue(elementToCheck, out int remaining) ? remaining : 0;

        public bool HasAttachedElement(BaseEnums.UnitElement elementToCheck)
        {
            return _combatElements.Contains(elementToCheck);
        }

        /// <summary>판정 전용 원소를 추가한다. 부착이 아니므로 반응을 일으키지도, 반응에 소모되지도 않는다.</summary>
        public void AddElementJudgement(BaseEnums.UnitElement elementToGrant)
        {
            if (elementToGrant == BaseEnums.UnitElement.None) return;
            if (_judgementElements.Add(elementToGrant))
            {
                AttributesUpdate();
                currentCell?.UpdateUI();
            }
        }

        public string GetCombatElementDisplay()
        {
            return _combatElements.Count == 0 ? "None" : string.Join(", ", _combatElements);
        }

        /// <summary>
        /// <b>부착된</b> 원소가 하나라도 있는가. 고유 원소는 판정 전용이라 여기에 포함되지 않는다 —
        /// 아무도 원소를 걸어 주지 않은 유닛은 속성이 무엇이든 false다.
        /// </summary>
        public bool HasAnyCombatElement => _combatElements.Count > 0;

        /// <summary>부착 원소를 한 턴 소모한다. 부착된 유닛의 턴 기준이다.</summary>
        private void TickTemporaryCombatElements()
        {
            if (_temporaryElementDurations.Count == 0) return;

            List<BaseEnums.UnitElement> expired = null;
            foreach (BaseEnums.UnitElement elementType in _temporaryElementDurations.Keys.ToList())
            {
                int remaining = _temporaryElementDurations[elementType] - 1;
                if (remaining > 0)
                {
                    _temporaryElementDurations[elementType] = remaining;
                    continue;
                }

                expired ??= new List<BaseEnums.UnitElement>();
                expired.Add(elementType);
            }

            if (expired == null) return;
            foreach (BaseEnums.UnitElement elementType in expired)
            {
                _temporaryElementDurations.Remove(elementType);
                _combatElements.Remove(elementType);
            }
            AttributesUpdate();
            currentCell?.UpdateUI();
        }

        public void SetCombatResourceMaximum(string resourceId, int maximum, bool resetCurrent = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            if (!_combatResourceMaximums.ContainsKey(resourceId)) _combatResourceOrder.Add(resourceId);
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

        public void ModifyHp(int newHp, Unit source = null)
        {
            int hpBefore = HpCurr;
            if (newHp > HpCurr)
            {
                float receivedMultiplier = 1f;
                float overhealShieldRatio = 0f;
                bool attackTriggeredSelfHealing = source == this && _resolvingDamageDealtEvent;
                foreach (var effect in ActiveEffectObjects())
                {
                    receivedMultiplier *= effect.HealingReceivedMultiplierModifier(
                        this, source, attackTriggeredSelfHealing);
                    overhealShieldRatio += effect.OverhealShieldConversionModifier(this);
                }
                float outgoingMultiplier = GetOutgoingSupportMultiplier(source, false);
                int healingAmount = Mathf.RoundToInt(
                    (newHp - HpCurr) * (1f + HealingBonusCurr) * receivedMultiplier * outgoingMultiplier);
                healingAmount = ApplyHealingShieldCritical(healingAmount, source);
                int missingHp = Mathf.Max(0, HpMax - HpCurr);
                int overheal = Mathf.Max(0, healingAmount - missingHp);
                HpCurr = Mathf.Clamp(HpCurr + healingAmount, 0, HpMax);
                if (overheal > 0 && overhealShieldRatio > 0f)
                {
                    AddShield(Mathf.RoundToInt(overheal * overhealShieldRatio), source);
                }

                // 순수치유량 — 오버힐을 뺀 실제 회복분만 부여자에게 누적한다.
                int effectiveHealing = Mathf.Max(0, healingAmount - overheal);
                if (source != null && effectiveHealing > 0)
                {
                    source.RoundEffectiveHealingDone += effectiveHealing;
                }

                NotifyHealingOrShieldGranted(source, healingAmount);
            }
            else
            {
                HpCurr = Mathf.Clamp(newHp, 0, HpMax);
            }
            int hpSpent = Mathf.Max(0, hpBefore - HpCurr);
            if (hpSpent > 0) AnyHpSpent?.Invoke(this, hpSpent);
            currentCell?.UpdateUI();
        }

        /// <summary>
        /// 공격 준비 중 최대 체력 비율만큼 체력을 지불한다. 일반 비용은 체력을 1 미만으로
        /// 만들 수 없으며, 부족 시 1까지 소모하는 효과만 예외를 허용한다.
        /// </summary>
        public bool TryConsumeAttackHp(
            float requestedMaxHpRatio,
            bool reduceToOneIfInsufficient,
            out float spentMaxHpRatio)
        {
            spentMaxHpRatio = 0f;
            if (requestedMaxHpRatio <= 0f || HpMax <= 0 || HpCurr <= 0) return false;

            float costMultiplier = 1f;
            foreach (var effect in ActiveEffectObjects())
            {
                costMultiplier *= Mathf.Max(0f, effect.AttackSelfHpCostMultiplier(this));
            }

            int requested = Mathf.Max(1, Mathf.RoundToInt(HpMax * requestedMaxHpRatio * costMultiplier));
            int spent;
            if (HpCurr > requested)
            {
                spent = requested;
            }
            else if (reduceToOneIfInsufficient)
            {
                spent = Mathf.Max(0, HpCurr - 1);
            }
            else
            {
                return false;
            }

            HpCurr = Mathf.Max(1, HpCurr - spent);
            spentMaxHpRatio = (float)spent / HpMax;
            if (spent > 0) AnyHpSpent?.Invoke(this, spent);
            currentCell?.UpdateUI();
            return true;
        }

        internal bool ResistsNegativeStatus(Status.UnitStatus status)
        {
            if (status == null || status.Category != BaseEnums.StatusCategory.Negative) return false;
            float chance = 0f;
            foreach (var effect in ActiveEffectObjects())
            {
                chance += Mathf.Max(0f, effect.NegativeStatusResistanceChanceModifier(this, status));
            }
            return chance > 0f && UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        // ── 제어 상태 조회와 부여 ──────────────────────────────────

        /// <summary>빙결 중인가. 빙결은 행동만 막고 피격·대상 지정은 정상이다.</summary>
        public bool IsFrozen => HasStatus(Effects.Negative.ControlStatuses.FrozenStatusId);

        /// <summary>에어본(공중에 떠 있는) 상태인가. 행동만 막고 피격·대상 지정은 정상이다.</summary>
        public bool IsAirborne => HasStatus(Effects.Negative.ControlStatuses.AirborneStatusId);

        /// <summary>어떤 효과가 일반행동을 막고 있는지.</summary>
        public bool IsNormalAttackBlocked =>
            ActiveEffectObjects().Any(effect => effect.BlocksNormalAttack(this));

        /// <summary>빙결 면역 여부.</summary>
        public bool IsFreezeImmune =>
            ActiveEffectObjects().Any(effect => effect.GrantsFreezeImmunity(this));

        /// <summary>에어본 면역 여부.</summary>
        public bool IsAirborneImmune =>
            ActiveEffectObjects().Any(effect => effect.GrantsAirborneImmunity(this));

        /// <summary>
        /// 행동 불가를 건다. 이미 걸려 있으면 <b>남은 시간과 새 지속시간 중 긴 쪽</b>을 남긴다.
        /// <see cref="ControlStarts"/>가 무조건 덮어쓰는 것과 달리 제어를 짧게 만들지 않는다.
        /// </summary>
        public void ApplyControlAtLeast(Unit attacker, int turns)
        {
            if (turns <= 0) return;
            if (isControlled && controlTurns >= turns) return;
            ControlStarts(new ControlContext(attacker, turns));
        }

        // ── 강인도 ─────────────────────────────────────────────────

        /// <summary>
        /// 강인도(가상 체력)를 부여한다. 이미 가지고 있으면 아무 일도 하지 않는다.
        /// </summary>
        public bool GrantToughness(int amount, Unit source)
        {
            if (amount <= 0 || HasToughness || !isActive) return false;
            toughnessMax = amount;
            toughnessCurr = amount;
            currentCell?.UpdateUI();
            string grantorName = source != null ? source.UnitName : "-";
            Debug.Log($"[강인도] {UnitName}에게 {amount} 부여 (부여자: {grantorName})");
            return true;
        }

        public void ClearToughness()
        {
            toughnessCurr = 0;
            toughnessMax = 0;
        }

        /// <summary>
        /// 실제로 들어간 피해만큼 강인도를 깎는다. 0이 되면 강인도를 없애고
        /// <b>처치 판정 이벤트만</b> 발행한다. 대상은 죽지 않고 골드·EXP도 지급하지 않는다.
        /// </summary>
        private int ReduceToughness(int damageDealt, Unit attacker, DamageContext context)
        {
            if (damageDealt <= 0 || toughnessCurr <= 0 ||
                context?.DamageTags?.Contains(BaseClasses.DamageTag.ToughnessEcho) == true) return 0;

            float efficiencyBonus = 0f;
            if (attacker != null)
            {
                foreach (var effect in attacker.ActiveEffectObjects())
                {
                    efficiencyBonus += Mathf.Max(0f,
                        effect.ToughnessDamageAdditiveModifier(attacker, this, context));
                }
            }

            int before = toughnessCurr;
            int reduction = Mathf.Max(1, Mathf.RoundToInt(damageDealt * (1f + efficiencyBonus)));
            toughnessCurr = Mathf.Max(0, toughnessCurr - reduction);
            int reduced = before - toughnessCurr;
            currentCell?.UpdateUI();
            if (toughnessCurr > 0) return reduced;

            toughnessMax = 0;
            Debug.Log($"[강인도] {UnitName}의 강인도가 파괴되었습니다 — 처치 판정 발행");
            AnyUnitDied?.Invoke(this, attacker);
            if (attacker != null && attacker != this)
            {
                attacker.Invoke(BaseEnums.UnitEventType.OnKill, new EventContext(attacker, this));
            }
            return reduced;
        }

        // ══════════════════════════════════════════════════════
        // 턴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 이 유닛이 이번 라운드에 행동한 횟수. <b>지속시간·주기·내부 쿨다운의 유일한 시간 축이다.</b>
        /// 초 축은 궁극기 자원 충전에만 남아 있다(<see cref="AccrueUltimateResource"/>).
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>
        /// 제어 분쇄용 상태 — 이번 라운드에 제어에 걸린 횟수와 마지막으로 걸린 턴.
        /// 판정 규칙은 <see cref="Effects.Negative.ControlStatuses"/>가 가지고 여기에는 값만 둔다.
        /// </summary>
        public int ControlAppliedCount { get; internal set; }

        /// <summary>마지막으로 제어에 걸렸을 때의 <see cref="TurnCount"/>. 한 번도 없으면 충분히 과거다.</summary>
        public int LastControlledTurn { get; internal set; } = -100000;

        /// <summary>
        /// 가장 최근에 이 유닛이 해결한 피해와 그 대상.
        /// 원소 부착은 언제나 피해 <b>뒤</b>에 오므로, 증폭 반응이 "방금 그 공격"을 되짚는 데 쓴다.
        /// </summary>
        public int LastResolvedDamage { get; private set; }

        public Unit LastResolvedTarget { get; private set; }

        /// <summary>원소 반응 재부착 내부 쿨다운 — 반응 ID → 마지막으로 일어난 자기 턴.</summary>
        private readonly Dictionary<int, int> _reactionCooldowns = new();

        /// <summary>이 반응이 다시 일어날 수 있는지. <paramref name="intervalTurns"/>가 0 이하면 항상 참이다.</summary>
        public bool IsReactionReady(int reactionId, int intervalTurns)
        {
            if (intervalTurns <= 0) return true;
            return !_reactionCooldowns.TryGetValue(reactionId, out int last) ||
                   TurnCount - last >= intervalTurns;
        }

        /// <summary>반응이 실제로 일어났음을 기록한다.</summary>
        public void MarkReactionOccurred(int reactionId) => _reactionCooldowns[reactionId] = TurnCount;

        /// <summary>
        /// 자기 턴을 연다. <see cref="Managers.ActionScheduler"/>가 행동 직전에 한 번만 부른다.
        ///
        /// 이 안에서 상태 지속시간이 줄고, 지속피해·재생·주기형 패시브가 진행된다.
        /// 예전에는 프레임마다 돌아서 누가 행동하는 동안에도 효과만 계속 흘렀다.
        /// </summary>
        public void BeginTurn()
        {
            TurnCount++;

            // 제어 분쇄 회차는 '조회할 때 계산'이 아니라 여기서 실제로 되돌린다.
            // 그러지 않으면 필드에 낡은 회차가 남아 디버그 패널과 외부 조회가 서로 다른 값을 본다.
            if (ControlAppliedCount > 0 &&
                TurnCount - LastControlledTurn >= Effects.Negative.ControlStatuses.DiminishResetTurns)
            {
                ControlAppliedCount = 0;
            }

            TickControlTurn();
            TickTemporaryCombatElements();

            // 궁극기 쿨다운도 턴 단위다. 코드 가속은 턴당 감소량을 키운다.
            if (ultimateCooldown > 0f)
            {
                ultimateCooldown = Mathf.Max(0f, ultimateCooldown - Mathf.Max(1f, CodeAcceleration));
            }

            StatusController.TickTurn();
            Invoke(BaseEnums.UnitEventType.OnTurnStart, new EventContext(this));
        }

        /// <summary>자기 턴의 행동이 모두 끝났을 때 스케줄러가 부른다.</summary>
        public void EndTurn()
        {
            Invoke(BaseEnums.UnitEventType.OnTurnEnd, new EventContext(this));
        }

        /// <summary>행동 불가를 한 턴 소모한다. 0이 되면 제어가 풀린다.</summary>
        private void TickControlTurn()
        {
            if (!isControlled) return;

            controlTurns--;
            if (controlTurns <= 0) ControlEnds();
        }

        public void AddUntargetableSource()
        {
            untargetableSourceCount++;
            currentNormalTarget = null;
        }

        public void RemoveUntargetableSource()
        {
            untargetableSourceCount = Mathf.Max(0, untargetableSourceCount - 1);
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

            Level = Mathf.Max(1, saveData.level);
            Exp = Mathf.Max(0, saveData.exp);
            TrainingLevel = saveData.trainingLevel;
            // 레벨이 복원되면 그에 맞는 레벨 해금 패시브를 다시 활성화한다.
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
            carriedItemIds = saveData.carriedItemIds?.ToList() ?? new List<int>();
            AttributesUpdate();
            ModifyHp(saveData.currentHP);
        }

        /// <summary>
        /// 방어막을 추가하는 메서드
        /// </summary>
        /// <param name="amount">추가할 방어막 양</param>
        public virtual void AddShield(int amount, Unit source = null)
        {
            amount = ApplyShieldBonus(Mathf.RoundToInt(amount * GetOutgoingSupportMultiplier(source, true)));
            amount = ApplyHealingShieldCritical(amount, source);
            int previousShieldMax = ShieldMax;
            int previousShieldCurr = ShieldCurr;
            
            // ShieldMax와 ShieldCurr 둘 다 증가
            ShieldMax += amount;
            ShieldCurr += amount;
            
            AttributesUpdate(); // 상태 효과 반영을 위해 스탯 업데이트
            UpdateShieldBar(); // 방어막 바 시각 업데이트
            Debug.Log($"[AddShield] {UnitName}: Max {previousShieldMax}→{ShieldMax}, Curr {previousShieldCurr}→{ShieldCurr} (HP: {HpCurr}/{HpMax})");
            NotifyHealingOrShieldGranted(source, amount);
        }

        /// <summary>
        /// 방어막을 설정하는 메서드 (기존 방어막을 덮어씀)
        /// </summary>
        /// <param name="amount">설정할 방어막 양</param>
        public virtual void SetShield(int amount, Unit source = null)
        {
            amount = ApplyShieldBonus(Mathf.RoundToInt(amount * GetOutgoingSupportMultiplier(source, true)));
            amount = ApplyHealingShieldCritical(amount, source);
            ShieldMax = amount;
            ShieldCurr = amount;
            AttributesUpdate(); // 상태 효과 반영을 위해 스탯 업데이트
            UpdateShieldBar(); // 방어막 바 시각 업데이트
            Debug.Log($"[SetShield] {UnitName}의 방어막이 {amount}로 설정되었습니다 (Max={ShieldMax}, Curr={ShieldCurr})");
            NotifyHealingOrShieldGranted(source, amount);
        }

        private float GetOutgoingSupportMultiplier(Unit source, bool shield)
        {
            if (source == null) return 1f;
            float multiplier = 1f;
            foreach (var effect in source.ActiveEffectObjects())
            {
                multiplier *= shield
                    ? effect.OutgoingShieldMultiplierModifier(source, this)
                    : effect.OutgoingHealingMultiplierModifier(source, this);
            }
            return Mathf.Max(0f, multiplier);
        }

        private int ApplyHealingShieldCritical(int amount, Unit source)
        {
            if (amount <= 0 || source == null) return Mathf.Max(0, amount);
            bool canCrit = source.ActiveEffectObjects().Any(effect => effect.EnablesHealingShieldCritical(source));
            if (!canCrit || UnityEngine.Random.value > source.CritChanceCurr) return amount;
            return Mathf.Max(0, Mathf.RoundToInt(amount * source.CritMultiplierCurr));
        }

        private void NotifyHealingOrShieldGranted(Unit source, int amount)
        {
            if (source == null || amount <= 0) return;
            foreach (var effect in source.ActiveEffectObjects().ToList())
            {
                effect.OnHealingOrShieldGranted(source, this);
            }
        }

        public void RemoveAllNegativeStatuses()
        {
            foreach (var status in GetAllStatuses()
                         .Where(status => status.Category == BaseEnums.StatusCategory.Negative)
                         .ToList())
            {
                RemoveStatus(status.StatusId);
            }
        }

        private int ApplyShieldBonus(int amount)
        {
            float receivedMultiplier = 1f;
            List<Effects.Base.BaseEffect> activeEffects = ActiveEffectObjects().ToList();
            foreach (var effect in activeEffects)
            {
                receivedMultiplier *= effect.ShieldReceivedMultiplierModifier(this);
            }
            return Mathf.Max(0, Mathf.RoundToInt(amount * (1f + ShieldBonusCurr) * receivedMultiplier));
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

            // 씬을 내리는 동안에도 유닛의 Update는 한동안 더 돈다.
            // 그때 GameManager는 이미 사라져 있어, 검사 없이 참조하면 콘솔이 예외로 뒤덮인다.
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress)
            {
                // 벤치에 있는 유닛은 공격하지 않음
                if (GridManager.Instance && GridManager.Instance.IsBenchCell(currentCell))
                {
                    return;
                }
                
                // 프레임 훅은 연출 전용으로 남긴다.
                // 지속시간·제어·쿨다운·주기 효과는 전부 BeginTurn()이 턴 경계에서 진행한다.
                Invoke(BaseEnums.UnitEventType.OnUpdate, new EventContext(this, null, null, Time.deltaTime));

                // 일반행동에는 쿨타임이 없다. 행동 주기는 DEX가 만드는 행동치(AV)가 전담한다.
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
                receivingDamageModifier *= effect.ReceivingDamageModifier(self, dmgCtx);
            }
            
            bool canEvade = dmgCtx.CodeType != BaseEnums.CodeType.Effect;
            float evasionChance = self.EvasionChanceCurr;
            foreach (var effect in ActiveEffectObjects())
            {
                evasionChance += effect.EvasionChanceAdditiveModifier(self, dmgCtx);
            }
            if (canEvade && UnityEngine.Random.value < evasionChance)
            {
                currentCell?.UpdateUI();
                Debug.Log($"{self.UnitName}이(가) 공격을 회피했습니다. 회피율: {evasionChance * 100f:F1}%");
                return;
            }
            
            int damageReceived = self.CalculateFinalDamage(dmgCtx, receivingDamageModifier);
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

            if (self.HpCurr <= 0)
            {
                foreach (var effect in ActiveEffectObjects())
                {
                    if (!effect.TryPreventDeath(self, dmgCtx.Attacker)) continue;
                    // 즉시 부활 효과가 복구한 체력은 보존한다. 지연 부활만 최소 1을 보장한다.
                    self.HpCurr = Mathf.Max(1, self.HpCurr);
                    break;
                }
            }
            
            // Cell의 통합 UI 업데이트 메서드 사용
            self.RefreshView();

            int damageDealt = Mathf.Max(0, hpBeforeHit - self.HpCurr) + Mathf.Max(0, shieldBeforeHit - self.ShieldCurr);
            dmgCtx.ResolvedDamage = damageDealt;

            // 강인도는 체력과 병렬로 깎인다. 피해를 흡수하지 않으므로 위 계산에는 관여하지 않는다.
            int toughnessReduced = self.ReduceToughness(damageDealt, dmgCtx.Attacker, dmgCtx);

            if (dmgCtx.Attacker != null && damageDealt > 0)
            {
                dmgCtx.Attacker.RoundDamageDealt += damageDealt;
                // 증폭 반응은 부착보다 먼저 끝난 이 피해를 되짚어 키운다.
                dmgCtx.Attacker.LastResolvedDamage = damageDealt;
                dmgCtx.Attacker.LastResolvedTarget = self;
                var resolvedContext = new DamageResolvedContext(dmgCtx.Attacker, self, dmgCtx, damageDealt);
                dmgCtx.Attacker.Invoke(
                    BaseEnums.UnitEventType.OnDamageDealt,
                    resolvedContext);
                AnyDamageDealt?.Invoke(resolvedContext);
            }

            // 강인도 감소량의 일부를 실제 피해로 바꾸는 효과. 전환 피해는 다시 강인도를
            // 깎지 않도록 ToughnessEcho 태그를 붙여 재귀와 이중 정산을 막는다.
            if (dmgCtx.Attacker != null && toughnessReduced > 0 && self.isActive && self.HpCurr > 0)
            {
                float echoRatio = 0f;
                foreach (var effect in dmgCtx.Attacker.ActiveEffectObjects())
                {
                    echoRatio += Mathf.Max(0f,
                        effect.ToughnessEchoDamageRatioModifier(dmgCtx.Attacker, self, dmgCtx));
                }

                int echoDamage = Mathf.RoundToInt(toughnessReduced * echoRatio);
                if (echoDamage > 0)
                {
                    self.TakeDamage(new DamageContext(
                        dmgCtx.Attacker, echoDamage, dmgCtx.CodeType,
                        new List<int>
                        {
                            BaseClasses.DamageTag.SingleTarget,
                            BaseClasses.DamageTag.TrueDamage,
                            BaseClasses.DamageTag.ToughnessEcho,
                        }));
                }
            }
            
            // Debug.Log($"{self.UnitName}은(는) {dmgCtx.Attacker.UnitName}에게 {damageReceived}의 {(dmgCtx.IsCrit ? "치명" : "")}피해를 받았습니다. 체력: {hpBeforeHit} -> {self.HpCurr}");
            if (self.HpCurr <= 0)
            {
                self.Die(dmgCtx.Attacker);
            }
        }

        /// <summary>
        /// 최종 피해량 산출.
        ///   1) 공격자의 주는 피해 보정
        ///   2) 롤 방식 방어력 감쇠 — 관통과 방어 무시 배율을 반영한 뒤 적용
        ///   3) 대상의 받는 피해 보정
        /// </summary>
        private int CalculateFinalDamage(DamageContext dmgCtx, float receivingDamageModifier)
        {
            float outgoingDamageModifier = Mathf.Max(0f, dmgCtx.OutgoingDamageMultiplier);
            float defenseStatMultiplier = dmgCtx.DefenseStatMultiplier;
            int durabilityPenetration = Mathf.Max(0, dmgCtx.DurabilityPenetration);

            // 소환수의 공격은 소환자의 '가하는 피해 증가'를 물려받지 않는다.
            // 대신 소환수 전용 배율(소환사 등)만 적용된다.
            bool isSummonAttack = dmgCtx.DamageTags != null &&
                                  dmgCtx.DamageTags.Contains(BaseClasses.DamageTag.SummonAttack);

            if (dmgCtx.Attacker != null)
            {
                foreach (var effect in dmgCtx.Attacker.ActiveEffectObjects())
                {
                    if (!isSummonAttack)
                    {
                        outgoingDamageModifier *= effect.OutgoingDamageModifier(dmgCtx.Attacker, this, dmgCtx);
                    }
                    defenseStatMultiplier *= effect.DefenseStatMultiplierModifier(dmgCtx.Attacker, this, dmgCtx);
                    durabilityPenetration += Mathf.Max(0,
                        effect.DurabilityPenetrationModifier(dmgCtx.Attacker, this, dmgCtx));
                }
            }

            foreach (var effect in ActiveEffectObjects())
            {
                defenseStatMultiplier *= effect.OwnedDefenseStatMultiplierModifier(this, dmgCtx);
            }

            // 방어 관통은 방어력을 깎고, 방어 무시 배율은 남은 방어력을 비율로 줄인다.
            float effectiveArmor = Mathf.Max(0f, DefCurr - Mathf.Max(0, dmgCtx.Penetration))
                                   * Mathf.Max(0f, defenseStatMultiplier);
            float armorMultiplier = dmgCtx.DamageTags != null &&
                                    dmgCtx.DamageTags.Contains(BaseClasses.DamageTag.TrueDamage)
                ? 1f   // 고정 피해(TrueDamage)는 방어력 감쇠를 받지 않는다
                : ArmorMultiplier(effectiveArmor, stats.GetArmorConstant());

            float scaled = dmgCtx.Damage * outgoingDamageModifier * armorMultiplier * receivingDamageModifier;

            // 내구는 마지막에 고정값으로 깎는다.
            // 방어력을 100% 무시당해도 남으므로, 관통 빌드에 대한 최후의 완충재가 된다.
            // 지속피해(도트)는 틱당 피해가 작아 내구가 곧 무효화가 되므로 제외한다.
            bool ignoresDurability = dmgCtx.CodeType == BaseEnums.CodeType.Effect ||
                                     (dmgCtx.DamageTags != null &&
                                      dmgCtx.DamageTags.Contains(BaseClasses.DamageTag.DurabilityPenetration));
            if (!ignoresDurability)
            {
                scaled -= Mathf.Max(0, DurabilityCurr - durabilityPenetration);
            }

            // 한 방 상한. 감쇠·내구를 전부 통과한 뒤에 자른다 — '알파 개체'처럼
            // 체력이 단계로 끊기는 유닛이 광역 폭딜 한 번에 무너지지 않게 하는 장치다.
            float capRatio = 0f;
            foreach (var effect in ActiveEffectObjects())
            {
                float ratio = effect.IncomingDamageCapRatio(this, dmgCtx);
                if (ratio > 0f && (capRatio <= 0f || ratio < capRatio)) capRatio = ratio;
            }
            if (capRatio > 0f) scaled = Mathf.Min(scaled, HpMax * capRatio);

            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        /// <summary>
        /// 라운드 시작 처리 이벤트
        /// </summary>
        protected void DefaultRoundStartEvent(EventContext context)
        {
            Debug.Log($"[라운드 시작] {UnitName}의 DefaultRoundStartEvent 호출됨");
            RoundDamageDealt = 0;
            RoundEffectiveHealingDone = 0;
            TurnCount = 0;
            ControlAppliedCount = 0;
            LastControlledTurn = -100000;
            LastResolvedDamage = 0;
            LastResolvedTarget = null;
            _reactionCooldowns.Clear();
            _manaCarry = 0f;
            ClearToughness();
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
            _combatResourceOrder.Clear();
            ShieldMax = 0;   // 라운드 종료 시 방어막 최대치 초기화
            ShieldCurr = 0;  // 라운드 종료 시 방어막 현재치 초기화
            ClearToughness();
            AttributesUpdate();
            UpdateShieldBar(); // 방어막 바 UI 업데이트 (비활성화)
        }
        
        /// <summary>
        /// 매 프레임 실행 이벤트.
        ///
        /// 전투 판정은 전부 턴 경계(<see cref="BeginTurn"/>)로 옮겼다.
        /// 여기에는 연출처럼 프레임 단위가 필요한 것만 남긴다.
        /// </summary>
        protected void DefaultUpdateEvent(EventContext context)
        {
        }

        // ===== 스탯 계산 (UnitStats 위임) =====

        public int GetBasePrimaryStat(BaseEnums.PrimaryStat stat) => stats.GetBasePrimaryStat(stat);
        public int GetBaseStr() => stats.GetBaseStr();
        public int GetBaseDex() => stats.GetBaseDex();
        public int GetBaseCon() => stats.GetBaseCon();
        public int GetBaseInt() => stats.GetBaseInt();
        public int GetBaseLuk() => stats.GetBaseLuk();
        public int GetGrowthStatValue(BaseEnums.PrimaryStat stat) => stats.GetGrowthStatValue(stat);
        public int GetLevelGrowthStatValue(BaseEnums.PrimaryStat stat) => stats.GetLevelGrowthStatValue(stat);

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
            return ApplyEncumbranceStatMultiplier(stat, ApplyStatusPrimaryStatMultipliers(stat, value));
        }

        internal int ApplyStatusPrimaryStatMultipliers(BaseEnums.PrimaryStat stat, int value)
        {
            float multiplier = 1f;
            foreach (var effect in ActiveEffectObjects())
            {
                multiplier *= effect.PrimaryStatMultiplierModifier(this, stat);
            }
            return Mathf.Max(0, Mathf.RoundToInt(value * multiplier));
        }

        internal int ApplyEncumbranceStatMultiplier(BaseEnums.PrimaryStat stat, int value)
        {
            int tier = EncumbranceTier;
            if (tier >= 2 || (tier == 1 && stat == BaseEnums.PrimaryStat.DEX))
            {
                return Mathf.Max(0, Mathf.RoundToInt(value * 0.5f));
            }
            return Mathf.Max(0, value);
        }

        public int GetDerivedHp() => stats.GetDerivedHp();
        public int GetDerivedMana() => stats.GetDerivedMana();

        public int GetUltimateResourceMax()
        {
            return UltimateResourceType == BaseEnums.UltimateResourceType.Mana
                ? stats.GetDerivedMana()
                : Mathf.Max(1, UltimateResourceMax);
        }

        public int GetDerivedDef() => stats.GetDerivedDef();
        /// <summary>지정한 스킬 위력과 스탯으로 피해량을 계산한다.</summary>
        public int GetSkillDamage(int skillPower, BaseEnums.PrimaryStat stat) => stats.GetSkillDamage(skillPower, stat);
        public float GetDerivedCritChance() => stats.GetDerivedCritChance();
        public float GetDerivedCritDamage() => stats.GetDerivedCritDamage();
        public float GetDerivedCodeAcceleration() => stats.GetDerivedCodeAcceleration();
        public float GetDerivedActionSpeed() => stats.GetDerivedActionSpeed();
        public float GetDerivedEvasionChance() => stats.GetDerivedEvasionChance();
        public float GetDerivedHealingBonus() => stats.GetDerivedHealingBonus();
        public float GetDerivedShieldBonus() => stats.GetDerivedShieldBonus();
        public float GetDerivedManaEfficiency() => stats.GetDerivedManaEfficiency();
        public float GetDerivedCodeActivationChance() => stats.GetDerivedCodeActivationChance();

        // 유닛 활성화 상태 관리
        public void ActivateUnit()
        {
            isActive = true;
            if (currentCell == null) return;   // 칸 없는 소환수

            currentCell.isOccupied = true;
            // Cell의 통합 UI 관리 사용
            currentCell.SetOccupiedUnit(this);
        }

        public void DeactivateUnit()
        {
            isActive = false;
            untargetableSourceCount = 0;
            if (ID > 0) LastActiveId = ID;
            ID = 0;
            currentNormalTarget = null; // 타겟 초기화

            // 거느리던 소환수는 소환자가 사라지면 함께 거둔다.
            DismissSummons();
            ReleaseSummonLifetime();
            SummonOwner?.UnregisterSummon(this);

            if (currentCell == null)
            {
                SummonView?.Dismiss();
                return;
            }

            currentCell.isOccupied = false;
            currentCell.SetPortrait(null);
            // Cell의 통합 UI 관리 사용
            currentCell.SetOccupiedUnit(null);
            currentCell.reservedTime = 2f;
        }

        /// <summary>
        /// 마지막으로 전장에 서 있을 때의 유닛 ID.
        ///
        /// <see cref="DeactivateUnit"/>은 <c>ID</c>를 0으로 지운다. 되살리는 코드가
        /// 정체성을 되찾을 수 있도록 지우기 직전의 값을 남겨 둔다.
        /// </summary>
        public int LastActiveId { get; private set; }

        /// <summary>
        /// 전투가 끝난 뒤 쓰러진 유닛을 제자리에 다시 세운다.
        ///
        /// 칸이 없는 소환수는 대상이 아니다 — 소환수는 라운드를 넘기지 않는다.
        /// 회복 배율을 타면 과다치유가 보호막으로 새므로 체력은 직접 채운다.
        /// </summary>
        /// <returns>실제로 되살아났으면 true.</returns>
        public bool ReviveAfterBattle()
        {
            if (isActive || IsSummon || currentCell == null || LastActiveId <= 0) return false;

            ID = LastActiveId;
            ActivateUnit();
            HpCurr = HpMax;
            RefreshView();
            return true;
        }

        /// <summary>
        /// 이 유닛의 카드를 다시 그린다. 칸 위의 유닛은 칸이, 소환수는 전용 카드가 받는다.
        /// </summary>
        public void RefreshView()
        {
            if (currentCell != null) currentCell.UpdateUI();
            else SummonView?.Tick();
        }

        /// <summary>소환수 카드. 칸이 없는 유닛만 가진다.</summary>
        public View.SummonCardView SummonView { get; internal set; }

        /// <summary>로그에 쓸 위치 표기. 칸이 없는 소환수는 소환자를 대신 적는다.</summary>
        public string FieldPositionLabel()
            => currentCell != null
                ? $"({currentCell.xPos}, {currentCell.yPos})"
                : IsSummon ? $"({SummonOwner.UnitName}의 소환수)" : "";

        // 유닛 상태효과 관리
        public void NotifyBeneficialEffectReceived(Unit grantor)
        {
            Invoke(BaseEnums.UnitEventType.OnBeneficialEffectReceived, new EventContext(this, grantor));
            if (grantor != null)
            {
                grantor.Invoke(BaseEnums.UnitEventType.OnBeneficialEffectGranted, new EventContext(grantor, this));
            }
        }

        private void NotifyNegativeStatusGranted(Unit grantor, Status.UnitStatus status)
        {
            AnyNegativeStatusGranted?.Invoke(grantor, this, status);
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
                bool previousDamageEventState = _resolvingDamageDealtEvent;
                if (eventType == BaseEnums.UnitEventType.OnDamageDealt)
                {
                    _resolvingDamageDealtEvent = true;
                }
                try
                {
                    typed.Invoke(context);
                }
                finally
                {
                    _resolvingDamageDealtEvent = previousDamageEventState;
                }
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
        }

        /// <summary>이 유닛이 부여하는 유한 턴 상태의 지속시간 가산치를 합산한다.</summary>
        internal int GetGrantedStatusDurationBonus(Status.UnitStatus status)
        {
            return ActiveEffectObjects().Sum(effect =>
                effect.GrantedStatusDurationAdditiveModifier(this, status));
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
        /// <summary>해로운 상태를 하나라도 갖고 있는가.</summary>
        public bool HasNegativeStatus()
            => GetAllStatuses().Any(status =>
                status != null && status.Category == BaseEnums.StatusCategory.Negative);

        /// <summary>지정 숙련을 갖췄는가(외부 조회용).</summary>
        public bool HasProficiency(EquipmentProficiency proficiency)
            => HasEquipmentProficiency(proficiency);

        public bool HasDamageOverTimeStatus()
        {
            return ActiveEffectObjects().Any(effect => effect.IsDamageOverTime);
        }

        /// <summary>
        /// 보유한 지속피해의 종류 수. 같은 Key로 중첩된 화상 등은 여러 스택이어도 1종으로 센다.
        /// </summary>
        public int GetDamageOverTimeStatusCount()
        {
            return GetAllStatuses()
                .Where(status => status.Effects.Any(instance => instance.EffectObject?.IsDamageOverTime == true))
                .Select(status => status.Key)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        /// <summary>현재 지속피해 효과를 1턴간 정산한 예상 피해 총합.</summary>
        public int GetEstimatedDamageOverTimePerTurn()
        {
            return ActiveEffectObjects()
                .Where(effect => effect.IsDamageOverTime)
                .Sum(effect => Mathf.Max(0, effect.EstimateDamagePerTurn()));
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
