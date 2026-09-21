using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Core;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 육성(트레이닝) 시스템. 우마무스메의 훈련 화면을 기준으로 삼되,
    /// <b>훈련 하나는 대응하는 스탯 하나만</b> 올린다.
    ///
    /// 한 번의 훈련은 이렇게 계산된다:
    ///   상승 = floor( (훈련 레벨 기본치 + 서포트 보너스) × 컨디션 배율 × (1 + 트레이닝 노트) )
    ///   체력 = 훈련별 소모(지능만 회복) + 실패 시 추가 소모
    ///   <b>훈련이 치르는 것은 체력뿐이다.</b> 컨디션은 훈련으로 오르내리지 않는다.
    ///   실패율 = 체력이 60 미만부터 오르고 30 미만에서 급증
    ///
    /// 메인 캐릭터가 육성 페이즈마다 하나의 5스탯을 집중 훈련하여 성장한다.
    /// 서포트 캐릭터는 훈련 효과를 증폭시킨다(우마무스메 서포트 카드 역할).
    /// 훈련 시 메인의 트레이닝 레벨이 올라 레벨 해금 패시브(#2/#3)가 해금된다.
    ///
    /// 서포트는 <b>턴마다 다섯 훈련 중 한 곳에 새로 배치된다</b>(우마무스메와 같다).
    /// 특기 훈련에 앉을 확률이 특기율이고, 실패하면 다른 훈련으로 흩어지거나 아예 나오지 않는다.
    /// 보너스는 <b>고른 훈련에 앉아 있는 서포트만</b> 준다 — 그래서 "어느 훈련에 누가 앉았나"가
    /// 이 화면의 핵심 선택이 된다.
    ///
    /// 배치는 <see cref="TrainingState"/>가 들고 있어 화면을 닫았다 열어도 바뀌지 않고,
    /// 훈련이나 휴식으로 턴이 지나면 <see cref="InvalidateSupportPlacement"/>로 무효화된다.
    /// </summary>
    public static class TrainingManager
    {
        // 서포트 1명당 추가되는 집중 스탯 강화량(항상 적용).
        public const int SupportStatBonusPerUnit = 1;
        // 집중 스탯이 서포트의 특기(클래스 주 스탯)와 일치할 때 서포트 1명당 추가 강화량.
        public const int AffinityBonusPerUnit = 1;
        // 육성 페이즈마다 오르는 트레이닝 레벨.
        public const int TrainingLevelGain = 1;
        /// <summary>특기 훈련에 앉지 못했을 때, 그래도 다른 훈련 어딘가에 앉을 확률(%).</summary>
        private const int OffSpecialtyAppearanceRate = 35;

        /// <summary>서포트 카드가 없는 동료의 특기율(%). 카드가 붙으면 카드 값을 쓴다.</summary>
        private const int DefaultSpecialtyRate = 45;
        private const int MaxBond = SupportBondState.MaxBond;
        /// <summary>훈련에 실패하면 체력을 이만큼 더 잃는다.</summary>
        public const int FailureEnergyPenalty = 10;

        /// <summary>
        /// 휴식(우마무스메의 휴식+외출)이 컨디션을 한 칸 올릴 확률(%).
        /// 체력을 채우는 것이 본업이라 컨디션은 <b>덤</b>으로 낮게 둔다.
        /// </summary>
        public const int RestConditionUpChance = 20;

        /// <summary>
        /// 스테이지가 넘어갈 때 컨디션이 사건으로 움직일 확률(%). 오를지 내릴지는 반반이다.
        /// 우마무스메의 랜덤 이벤트처럼 <b>매우 드물게</b> 일어난다 — 100스테이지에 세 번 안팎.
        /// </summary>
        public const int StageConditionEventChance = 3;

        /// <summary>
        /// 훈련 하나는 <b>대응하는 스탯 하나만</b> 올린다.
        /// 우마무스메처럼 한 훈련이 여러 스탯을 함께 올리지 않는다.
        /// </summary>
        public readonly struct TrainingOption
        {
            public readonly BaseEnums.PrimaryStat Stat;
            public readonly string Name;
            public readonly string Effect;

            /// <summary>훈련에 드는 체력. 음수면 오히려 회복한다(지능).</summary>
            public readonly int EnergyCost;

            public readonly int SkillPoints;

            private readonly int[] _gains;

            public TrainingOption(BaseEnums.PrimaryStat stat, string name, string effect,
                int energyCost, int skillPoints, int[] gains)
            {
                Stat = stat;
                Name = name;
                Effect = effect;
                EnergyCost = energyCost;
                SkillPoints = skillPoints;
                _gains = gains;
            }

            /// <summary>훈련 레벨(1~5)에 따른 기본 상승치.</summary>
            public int BaseGain(int level)
            {
                if (_gains == null || _gains.Length == 0) return 1;
                return _gains[Mathf.Clamp(level - 1, 0, _gains.Length - 1)];
            }
        }

        /// <summary>
        /// 훈련 5종. 지능만 체력을 회복한다.
        ///
        /// 근력·체력·행운은 부 스탯을 함께 올리고(<see cref="SecondaryShares"/>), 부 스탯이 없는
        /// 민첩·지능은 그 대신 스킬 Pt를 두 배로 준다 — 한 훈련이 스탯 총량과 스킬 Pt 중
        /// 하나를 고르게 하는 구조다.
        /// </summary>
        public static readonly TrainingOption[] Options =
        {
            new(BaseEnums.PrimaryStat.STR, "근력", "방어력 · 장비 중량 한도", 20, 2, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.DEX, "민첩", "행동 속도", 18, 4, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.CON, "체력", "최대 체력", 16, 2, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.INT, "지능", "마나 획득 효율", -5, 4, new[] { 2, 3, 4, 5, 6 }),
            new(BaseEnums.PrimaryStat.LUK, "행운", "치명타 확률", 14, 2, new[] { 3, 4, 5, 6, 8 }),
        };

        /// <summary>훈련이 함께 올린 부 스탯 하나.</summary>
        public struct SecondaryGain
        {
            public BaseEnums.PrimaryStat Stat;
            public int Amount;
        }

        /// <summary>
        /// 훈련이 함께 올리는 부 스탯과 그 몫. 우마무스메의 스피드 훈련이 파워를 함께 올리는 것과 같다.
        ///
        ///   근력 → CON 50%        체력 → INT 25% · LUK 25%        행운 → DEX 25% · CON 25%
        ///
        /// 몫은 <b>순수 상승량</b>(훈련 레벨 기본치 + 서포트 보너스)에 곱한다. 그 뒤 스탯마다
        /// <b>그 스탯의</b> 훈련 효율(서포트 코드·메인 코드)과 컨디션을 따로 곱한다 —
        /// CON 훈련의 INT 몫에는 INT 효율이, LUK 몫에는 LUK 효율이 붙는다.
        /// </summary>
        private static readonly SecondaryGain[] NoSecondary = System.Array.Empty<SecondaryGain>();

        public static (BaseEnums.PrimaryStat Stat, float Share)[] SecondaryShares(BaseEnums.PrimaryStat focus)
            => focus switch
            {
                BaseEnums.PrimaryStat.STR => new[] { (BaseEnums.PrimaryStat.CON, 0.50f) },
                BaseEnums.PrimaryStat.CON => new[] { (BaseEnums.PrimaryStat.INT, 0.25f), (BaseEnums.PrimaryStat.LUK, 0.25f) },
                BaseEnums.PrimaryStat.LUK => new[] { (BaseEnums.PrimaryStat.DEX, 0.25f), (BaseEnums.PrimaryStat.CON, 0.25f) },
                _ => System.Array.Empty<(BaseEnums.PrimaryStat, float)>(),
            };

        /// <summary>"CON +3 · INT +1" 꼴. 없으면 빈 문자열.</summary>
        public static string FormatSecondary(IReadOnlyList<SecondaryGain> gains)
            => gains == null ? "" : string.Join(" · ", gains.Where(g => g.Amount > 0).Select(g => $"{g.Stat} +{g.Amount}"));

        public static TrainingOption GetOption(BaseEnums.PrimaryStat stat)
        {
            foreach (TrainingOption option in Options)
            {
                if (option.Stat == stat) return option;
            }

            return Options[0];
        }

        // 훈련 체력·레벨·스킬 Pt도 런 범위 상태다. RunManager가 없을 때를 위한 폴백.
        private static readonly TrainingState FallbackTraining = new();

        public static TrainingState State =>
            RunManager.Instance != null ? RunManager.Instance.Training : FallbackTraining;

        // 우정도는 런 범위 상태이므로 RunManager가 소유한다.
        // RunManager가 아직 없을 때(에디터 진입 직후 등)를 대비한 폴백.
        private static readonly SupportBondState FallbackBonds = new();
        private static SupportBondState Bonds =>
            RunManager.Instance != null ? RunManager.Instance.SupportBonds : FallbackBonds;

        /// <summary>훈련 결과 화면이 서포트 한 명을 그리는 데 필요한 것들.</summary>
        public struct SupportOutcome
        {
            public string Name;
            public string PortraitPath;
            public int StatBonus;
            public int BondBefore;
            public int BondAfter;
            public bool Friendship;

            /// <summary>이번 훈련에서 흘린 스킬 힌트. 없으면 null.</summary>
            public string HintedName;
            public int HintedLevel;
        }

        public struct TrainingResult
        {
            public BaseEnums.PrimaryStat Focus;

            /// <summary>훈련 이름("근력" 등).</summary>
            public string FocusName;
            public int StatGain;
            /// <summary>함께 오른 부 스탯. 민첩·지능은 비어 있다.</summary>
            public SecondaryGain[] SecondaryGains;

            /// <summary>"CON +3 · INT +1" 꼴. 없으면 빈 문자열.</summary>
            public string SecondaryText => FormatSecondary(SecondaryGains);
            public int SupportBonus;
            public int AffinityBonus;
            public int NewTrainingLevel;
            public bool Failed;
            public int FailureRate;
            public int EnergySpent;
            public int EnergyAfter;
            public int SkillPointsGained;
            public int NewFocusTrainingLevel;
            public List<string> SupportMessages;

            /// <summary>이번 훈련에서 새로 얻거나 레벨이 오른 힌트의 코드 ID.</summary>
            public List<int> HintedCodeIds;

            /// <summary>훈련 전후 컨디션 이름. 훈련은 컨디션을 바꾸지 않으므로 늘 같다(결과 화면 호환용).</summary>
            public string ConditionBefore;
            public string ConditionAfter;

            /// <summary>이번 훈련이 쓴 트레이닝 노트 보너스(%). 없으면 0.</summary>
            public int NoteBonusUsed;

            /// <summary>이 훈련에 참여한 서포트들. 다른 자리에 앉은 서포트는 들어오지 않는다.</summary>
            public List<SupportOutcome> Supports;
        }

        private class SupportTrainingRoll
        {
            public Unit Support;
            public SupportCardSaveData Card;
            public TrainedCharacterRecord Record;
            public bool Appeared;
            public bool SpecialtyMatch;
            public bool FriendshipTraining;
            public int StatBonus;
            public int PreviousBond;
            public int NewBond;
            /// <summary>이번에 흘린 힌트의 코드 ID. 없으면 0이다.</summary>
            public int HintedCodeId;

            /// <summary>힌트받은 패시브의 이름과 오른 레벨. 결과 화면이 그대로 쓴다.</summary>
            public string HintedName;
            public int HintedLevel;
        }

        /// <summary>현재 살아있는 메인 유닛 인스턴스를 찾는다. 없으면 첫 활성 아군을 반환.</summary>
        public static Unit GetMainUnit()
        {
            if (GridManager.Instance == null) return null;

            var heroes = GridManager.Instance.heroList;
            int mainId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;

            Unit main = null;
            if (mainId > 0)
            {
                main = heroes.FirstOrDefault(hero =>
                    hero != null && hero.isActive && !hero.IsEnemy && hero.ID == mainId);
            }

            return main ?? heroes.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
        }

        /// <summary>메인을 제외한 활성 서포트 유닛 목록.</summary>
        public static List<Unit> GetSupportUnits()
        {
            if (GridManager.Instance == null) return new List<Unit>();

            Unit main = GetMainUnit();
            return GridManager.Instance.heroList
                .Where(hero => hero != null && hero.isActive && !hero.IsEnemy && hero != main)
                .ToList();
        }

        /// <summary>서포트 수.</summary>
        public static int GetSupportCount() => GetSupportUnits().Count;

        // ── 서포트 배치 ──────────────────────────────────────────────

        /// <summary>
        /// 이번 턴의 서포트 배치를 굴린다. 이미 굴렸으면 그대로 두되,
        /// 그 뒤에 합류한 서포트만 새로 자리를 잡아 준다.
        ///
        /// 훈련 화면을 열 때마다 호출해도 안전하다 — 그렇지 않으면 화면을 여닫는 것만으로
        /// 자리를 다시 굴릴 수 있어 배치가 선택이 아니게 된다.
        /// </summary>
        public static void EnsureSupportPlacement()
        {
            TrainingState state = State;
            bool fresh = !state.PlacementReady;

            foreach (Unit support in GetSupportUnits())
            {
                if (!fresh && state.WasPlacementRolled(support.ID)) continue;

                RollPlacement(state, support);
            }

            state.MarkPlacementReady();
        }

        private static void RollPlacement(TrainingState state, Unit support)
        {
            BaseEnums.PrimaryStat specialty = GetSupportSpecialty(support);
            SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
            int specialtyRate = HasSupportCard(card)
                ? Mathf.Clamp(card.specialtyRate, 0, 100)
                : DefaultSpecialtyRate;

            if (Random.Range(0, 100) < specialtyRate)
            {
                state.SetPlacement(support.ID, specialty);
                return;
            }

            // 특기에 앉지 못했다. 남은 네 훈련 중 하나로 흩어지거나, 이번 턴은 쉰다.
            if (Random.Range(0, 100) >= OffSpecialtyAppearanceRate)
            {
                state.SetAbsent(support.ID);
                return;
            }

            var others = Options
                .Select(option => option.Stat)
                .Where(stat => stat != specialty)
                .ToList();
            state.SetPlacement(support.ID, others[Random.Range(0, others.Count)]);
        }

        /// <summary>턴이 지났다. 다음에 훈련 화면을 열면 배치를 새로 굴린다.</summary>
        public static void InvalidateSupportPlacement() => State.InvalidatePlacement();

        /// <summary>이 서포트가 이번 턴에 앉은 훈련. 나오지 않았으면 null.</summary>
        public static BaseEnums.PrimaryStat? GetPlacement(Unit support)
        {
            if (support == null) return null;
            return State.TryGetPlacement(support.ID, out BaseEnums.PrimaryStat stat)
                ? stat
                : (BaseEnums.PrimaryStat?)null;
        }

        /// <summary>이번 턴에 해당 훈련에 앉아 있는 서포트들.</summary>
        public static List<Unit> GetSupportsOn(BaseEnums.PrimaryStat focus)
        {
            return GetSupportUnits()
                .Where(support => GetPlacement(support) == focus)
                .ToList();
        }

        /// <summary>이 훈련에 앉은 서포트들의 기본 보너스 합(특기 보너스 제외).</summary>
        public static int GetBaseSupportBonus(BaseEnums.PrimaryStat focus)
        {
            int bonus = 0;
            foreach (Unit support in GetSupportsOn(focus))
            {
                SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
                bonus += HasSupportCard(card)
                    ? Mathf.Max(0, card.trainingBonus)
                    : SupportStatBonusPerUnit;
            }

            return bonus;
        }

        /// <summary>
        /// 이 훈련을 고르면 받게 되는 서포트 보너스.
        /// <b>이번 턴에 그 훈련에 앉아 있는 서포트만</b> 센다 — 다른 자리에 앉은 서포트는
        /// 아무것도 주지 않는다.
        /// </summary>
        public static int GetSupportBonus(BaseEnums.PrimaryStat focus)
        {
            int bonus = 0;
            foreach (Unit support in GetSupportsOn(focus))
            {
                SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
                if (HasSupportCard(card))
                {
                    bonus += Mathf.Max(0, card.trainingBonus);
                    if (IsCardSpecialty(card, focus))
                    {
                        bonus += Mathf.Max(0, card.specialtyBonus);
                        if (GetSupportBond(support) >= 75)
                        {
                            bonus += Mathf.Max(0, card.friendshipBonus);
                        }
                    }
                    continue;
                }

                bonus += SupportStatBonusPerUnit;
                if (GetHighestPrimaryStat(support) == focus)
                {
                    bonus += AffinityBonusPerUnit;
                }
            }
            return bonus;
        }

        private static bool HasSupportCard(SupportCardSaveData card)
        {
            return card != null && card.sourceUnitId > 0 && !string.IsNullOrWhiteSpace(card.specialtyTraining);
        }

        private static bool IsCardSpecialty(SupportCardSaveData card, BaseEnums.PrimaryStat focus)
        {
            return HasSupportCard(card)
                && System.Enum.TryParse(card.specialtyTraining, true, out BaseEnums.PrimaryStat specialty)
                && specialty == focus;
        }

        /// <summary>우정 훈련이 붙기 시작하는 우정도.</summary>
        public const int FriendshipBondThreshold = 75;

        /// <summary>
        /// 이 서포트의 특기 훈련. 서포트 카드가 있으면 카드의 값을,
        /// 없으면 현재 가장 높은 5스탯을 특기로 본다.
        /// </summary>
        public static BaseEnums.PrimaryStat GetSupportSpecialty(Unit support)
        {
            if (support == null) return BaseEnums.PrimaryStat.STR;

            SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
            if (HasSupportCard(card)
                && System.Enum.TryParse(card.specialtyTraining, true, out BaseEnums.PrimaryStat specialty))
            {
                return specialty;
            }

            return GetHighestPrimaryStat(support);
        }

        /// <summary>이번 훈련이 이 서포트의 특기와 맞는지.</summary>
        public static bool IsSupportSpecialty(Unit support, BaseEnums.PrimaryStat focus)
        {
            return support != null && GetSupportSpecialty(support) == focus;
        }

        public static int GetSupportBond(Unit support)
        {
            if (support == null) return 0;
            if (Bonds.TryGet(support.ID, out int bond))
            {
                return bond;
            }

            SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
            int initialBond = HasSupportCard(card) ? Mathf.Clamp(card.initialBond, 0, MaxBond) : 0;
            Bonds.Set(support.ID, initialBond);
            return initialBond;
        }

        /// <summary>
        /// 이번 훈련으로 오를 강화량.
        ///
        ///   (훈련 레벨 기본치 + 서포트 보너스) × 컨디션 × (1 + 트레이닝 노트)
        ///
        /// <b>주/부 스탯 배율은 여기서 곱하지 않는다.</b>
        /// <see cref="Entities.UnitStats"/>가 최종 스탯을 낼 때 이미 곱하고 있어서
        /// 여기서 또 곱하면 두 번 적용된다. 화면에는 <see cref="GetSpecialtyMultiplier"/>를
        /// "최종 스탯에 붙는 배율"로 따로 보여 준다.
        /// </summary>
        public static int GetProjectedGain(BaseEnums.PrimaryStat focus)
        {
            int raw = GetOption(focus).BaseGain(State.GetLevel(focus)) + GetSupportBonus(focus);
            return Mathf.Max(1, Mathf.FloorToInt(
                raw * State.TrainingMultiplier * GetPersonalTrainingMultiplier(GetMainUnit(), focus) *
                GetSupportTrainingEfficiencyMultiplier(focus)));
        }

        private static float GetSupportTrainingEfficiencyMultiplier(BaseEnums.PrimaryStat focus)
            => EfficiencyFrom(GetSupportsOn(focus), focus);

        private static float GetSupportTrainingEfficiencyMultiplier(
            BaseEnums.PrimaryStat focus, IEnumerable<SupportTrainingRoll> rolls)
            => EfficiencyFrom(rolls.Where(roll => roll.Appeared).Select(roll => roll.Support), focus);

        /// <summary>
        /// 훈련에 앉은 서포트가 <paramref name="stat"/> 상승량에 주는 효율.
        ///
        /// 훈련 코드는 <b>어느 훈련에 앉든</b> 자기 스탯에 붙는다(<see cref="Codes.Base.PassiveCode.SupportTrainingBonus"/>).
        /// 예전처럼 "LUK 훈련에 배치될 때만"이 아니다 — 행운아(16)를 든 서포트가 체력 훈련에 앉으면
        /// 그 훈련의 LUK 몫이 오른다. 숙련된 조교(14)처럼 스탯을 가리지 않는 코드는 모든 몫에 붙는다.
        /// 은색 코드는 상위 금색을 함께 배웠으면 세지 않는다(행운아 → 사랑 신의 가호).
        /// </summary>
        private static float EfficiencyFrom(IEnumerable<Unit> supports, BaseEnums.PrimaryStat stat)
        {
            float bonus = 0f;
            foreach (Unit support in supports)
            {
                if (support == null) continue;
                bonus += CodeTrainingBonus(support, code => code.SupportTrainingBonus(stat));
            }
            return 1f + bonus;
        }

        private static float CodeTrainingBonus(Unit owner, System.Func<Codes.Base.PassiveCode, float> read)
        {
            float bonus = 0f;
            foreach (Codes.Base.PassiveCode code in owner.ActivePassiveCodes)
            {
                if (code == null) continue;
                if (code.SupersededByCodeId > 0 && owner.HasLearnedPassiveCode(code.SupersededByCodeId)) continue;
                bonus += read(code);
            }
            return bonus;
        }

        /// <summary>이번 훈련이 함께 올릴 부 스탯 예측. 화면·봇이 쓴다.</summary>
        public static SecondaryGain[] GetProjectedSecondaryGains(BaseEnums.PrimaryStat focus)
        {
            int raw = GetOption(focus).BaseGain(State.GetLevel(focus)) + GetSupportBonus(focus);
            return SecondaryGainsFor(focus, raw, GetSupportsOn(focus).ToList());
        }

        /// <summary>부 스탯 상승 합계. 봇이 훈련 가치를 잴 때 쓴다.</summary>
        public static int GetProjectedSecondaryGain(BaseEnums.PrimaryStat focus)
            => GetProjectedSecondaryGains(focus).Sum(gain => gain.Amount);

        private static SecondaryGain[] SecondaryGainsFor(BaseEnums.PrimaryStat focus, int raw,
            List<Unit> supportsOnTraining)
        {
            var shares = SecondaryShares(focus);
            if (shares.Length == 0) return NoSecondary;

            Unit main = GetMainUnit();
            var gains = new SecondaryGain[shares.Length];
            for (int i = 0; i < shares.Length; i++)
            {
                var (stat, share) = shares[i];
                gains[i] = new SecondaryGain
                {
                    Stat = stat,
                    Amount = Mathf.Max(0, Mathf.FloorToInt(
                        raw * share * State.TrainingMultiplier *
                        GetPersonalTrainingMultiplier(main, stat) * EfficiencyFrom(supportsOnTraining, stat))),
                };
            }
            return gains;
        }

        /// <summary>
        /// 카피바라(109) — 필드의 아군이 들고 있으면 우정도 획득이 늘어난다.
        /// 여러 명이 들어도 가장 큰 하나만 센다.
        /// </summary>
        private static float BondGainMultiplier()
            => 1f + FieldPassiveBonus(code => code is Codes.Passive.ChandraCapybara,
                Codes.Passive.ChandraCapybara.BondBonus);

        /// <summary>
        /// 봉우(108)와 그 상위 미트라(113) — 우정 훈련이 터졌을 때의 보너스를 키운다.
        /// 둘을 함께 들어도 <b>높은 쪽 하나만</b> 센다.
        /// </summary>
        private static float FriendshipBonusMultiplier()
            => 1f + Mathf.Max(
                FieldPassiveBonus(code => code is Codes.Passive.SuryaMitra,
                    Codes.Passive.SuryaMitra.FriendshipBonus),
                FieldPassiveBonus(code => code is Codes.Passive.ChandraSwornFriend,
                    Codes.Passive.ChandraSwornFriend.FriendshipBonus));

        private static float FieldPassiveBonus(System.Func<Codes.Base.PassiveCode, bool> match, float bonus)
        {
            var grid = GridManager.Instance;
            if (grid?.heroList == null) return 0f;

            foreach (Unit hero in grid.heroList)
            {
                if (hero == null || !hero.isActive) continue;
                if (hero.ActivePassiveCodes.Any(code => code != null && match(code))) return bonus;
            }
            return 0f;
        }

        /// <summary>
        /// 메인 본인이 든 훈련 코드(직감·대도·광신도)가 <paramref name="stat"/> 상승량에 주는 배율.
        /// 어느 훈련에서 오르든 그 스탯이면 붙는다.
        /// </summary>
        private static float GetPersonalTrainingMultiplier(Unit main, BaseEnums.PrimaryStat stat)
            => main == null ? 1f : 1f + CodeTrainingBonus(main, code => code.MainTrainingBonus(stat));

        /// <summary>예전 이름. 화면 코드가 쓰던 진입점이라 남겨 둔다.</summary>
        public static int GetFocusStatGain(BaseEnums.PrimaryStat focus) => GetProjectedGain(focus);

        /// <summary>이 훈련에 드는 체력. 음수면 회복이다.</summary>
        public static int GetEnergyCost(BaseEnums.PrimaryStat focus) => GetOption(focus).EnergyCost;

        /// <summary>성공률(%). 화면에는 실패율보다 이쪽을 크게 보여 준다.</summary>
        public static int GetSuccessRate(BaseEnums.PrimaryStat focus) => 100 - GetFailureRate(focus);

        /// <summary>
        /// 실패율(%). 체력이 낮을수록 가파르게 오른다.
        /// 체력을 회복하는 훈련(지능)은 실패하지 않는다.
        /// </summary>
        public static int GetFailureRate(BaseEnums.PrimaryStat focus)
        {
            if (GetOption(focus).EnergyCost <= 0) return 0;

            int energy = State.Energy;
            if (energy >= 60) return 0;
            if (energy >= 30) return Mathf.RoundToInt((60 - energy) * 0.6f);

            return Mathf.Min(85, Mathf.RoundToInt(18 + (30 - energy) * 1.6f));
        }

        /// <summary>
        /// 이 스탯이 메인 캐릭터의 주/부 스탯이라 최종 스탯에 붙는 배율.
        /// 10_units.yaml의 mainStatTrainingBonus · subStatTrainingBonus를 그대로 읽는다.
        /// </summary>
        public static float GetSpecialtyMultiplier(BaseEnums.PrimaryStat focus)
        {
            Unit main = GetMainUnit();
            if (main == null) return 1f;

            float multiplier = 1f;
            if (MatchesStat(main.MainStat, focus)) multiplier += main.MainStatTrainingBonus;

            foreach (string sub in main.SubStats)
            {
                if (!MatchesStat(sub, focus)) continue;

                multiplier += main.SubStatTrainingBonus;
                break;
            }

            return multiplier;
        }

        private static bool MatchesStat(string statName, BaseEnums.PrimaryStat stat)
        {
            return !string.IsNullOrWhiteSpace(statName)
                && System.Enum.TryParse(statName, true, out BaseEnums.PrimaryStat parsed)
                && parsed == stat;
        }

        // ── 컨디션 변동 ──────────────────────────────────────────────
        //
        // 훈련은 컨디션을 건드리지 않는다. 우마무스메처럼 드문 일이 있을 때만 움직인다.

        /// <summary>
        /// 휴식(휴식+외출) 한 번의 컨디션 판정. <see cref="RestConditionUpChance"/>% 확률로 한 칸 오른다.
        /// 이미 최상이면 굴리지 않는다. 올랐으면 true.
        /// </summary>
        public static bool RollRestConditionUp()
        {
            TrainingState state = State;
            if (state.IsConditionBest) return false;
            if (Random.Range(0, 100) >= RestConditionUpChance) return false;

            state.ImproveCondition();
            return true;
        }

        /// <summary>
        /// 스테이지가 넘어갈 때 굴리는 드문 사건. 컨디션이 한 칸 오르거나 내린다(반반).
        /// 이미 끝에 닿아 그 방향으로 갈 수 없으면 일어나지 않은 것으로 친다.
        /// 움직였으면 준비 화면에 띄울 문장을, 아니면 null을 돌려준다.
        /// </summary>
        public static string RollStageConditionEvent()
        {
            if (Random.Range(0, 100) >= StageConditionEventChance) return null;

            TrainingState state = State;
            if (Random.value < 0.5f)
            {
                if (state.IsConditionBest) return null;
                state.ImproveCondition();
                return $"뜻밖의 좋은 일이 있었습니다. 컨디션이 좋아졌습니다 → {state.ConditionName}";
            }

            if (state.ConditionIndex >= TrainingState.ConditionNames.Length - 1) return null;
            state.WorsenCondition();
            return $"몸이 무거운 하루였습니다. 컨디션이 나빠졌습니다 → {state.ConditionName}";
        }

        // 메인이 이번 전투에서 쓰러졌는가. Unit.Die가 알리고, 전투가 끝날 때 한 번만 정산한다.
        // 전투 중에 되살아나도 쓰러진 것은 쓰러진 것이라, 끝나는 순간의 생사를 보지 않고 사건으로 센다.
        private static bool _mainFellInBattle;

        /// <summary>새 전투가 시작됐다. 지난 전투의 기록을 비운다.</summary>
        public static void BeginBattleConditionWatch() => _mainFellInBattle = false;

        /// <summary>아군이 쓰러졌다. 메인이면 이번 전투의 기록에 남긴다(육성 모드에서만).</summary>
        public static void NoteAllyDeath(Unit unit)
        {
            if (unit == null || unit.IsEnemy || unit.IsSummon) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentMode != BaseEnums.GameMode.Training) return;

            int mainId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;
            if (mainId > 0 && unit.ID == mainId) _mainFellInBattle = true;
        }

        /// <summary>
        /// 전투가 끝났다. 메인이 쓰러졌었다면 컨디션을 한 칸 떨어뜨리고 결과 화면에 띄울 문장을 돌려준다.
        /// 쓰러지지 않았으면 null이다.
        /// </summary>
        public static string ResolveBattleCondition()
        {
            if (!_mainFellInBattle) return null;
            _mainFellInBattle = false;

            TrainingState state = State;
            if (state.ConditionIndex >= TrainingState.ConditionNames.Length - 1)
                return "메인이 쓰러졌습니다. 컨디션은 이미 최악입니다.";

            state.WorsenCondition();
            return $"메인이 쓰러져 컨디션이 나빠졌습니다 → {state.ConditionName}";
        }

        /// <summary>
        /// 이 소모품을 지금 쓸 수 있는가. 쓸 수 없으면 사유를, 쓸 수 있으면 null을 돌려준다.
        /// 상점은 사기 전에, 보상 풀은 후보를 고를 때 묻는다 — 써도 아무 일이 없는 것을 사거나 받지 않게.
        /// </summary>
        public static string TrainingSupplyBlockReason(RewardDef supply)
        {
            if (supply == null || !supply.IsTrainingSupply) return null;
            if (GameManager.Instance == null || GameManager.Instance.CurrentMode != BaseEnums.GameMode.Training)
                return "육성 전용";

            TrainingState state = State;
            if (supply.conditionUp > 0 && state.IsConditionBest) return "컨디션 최상";
            if (supply.energyRestore > 0 && state.Energy >= TrainingState.MaxEnergy) return "체력이 가득 참";
            if (supply.trainingNotePercent > 0 && !state.CanAddNote) return "노트가 가득 참";
            return null;
        }

        /// <summary>
        /// 육성 소모품의 효과를 건다. 컨디션·체력은 곧바로 바뀌고, 노트는 다음 훈련을 기다린다.
        /// 쓸 수 없으면 아무것도 바꾸지 않고 false다.
        /// </summary>
        public static bool ApplyTrainingSupply(RewardDef supply)
        {
            // 육성 소모품이 아니면 막을 사유도 없어 BlockReason이 null이다 — 여기서 따로 걸러낸다.
            if (supply == null || !supply.IsTrainingSupply) return false;
            if (TrainingSupplyBlockReason(supply) != null) return false;

            TrainingState state = State;
            if (supply.conditionUp > 0) state.ImproveCondition(supply.conditionUp);
            if (supply.energyRestore > 0) state.RestoreEnergy(supply.energyRestore);
            if (supply.trainingNotePercent > 0) state.AddNote(supply.trainingNotePercent);

            Debug.Log($"[육성] {supply.displayName} — 체력 {state.Energy} · 컨디션 {state.ConditionName} · 노트 +{state.NotePercent}%");
            return true;
        }

        /// <summary>
        /// 집중 스탯 훈련을 메인 캐릭터에 적용한다.
        /// 집중 스탯 강화 + 트레이닝 레벨 상승(레벨 해금 패시브 갱신)을 수행한다.
        /// </summary>
        public static TrainingResult ApplyTraining(BaseEnums.PrimaryStat focus)
        {
            TrainingOption option = GetOption(focus);
            TrainingState state = State;

            List<SupportTrainingRoll> rolls = RollSupportTraining(focus);
            int baseSupport = rolls
                .Where(roll => roll.Appeared)
                .Sum(roll => roll.Card != null ? Mathf.Max(0, roll.Card.trainingBonus) : SupportStatBonusPerUnit);
            int totalSupport = rolls.Where(roll => roll.Appeared).Sum(roll => roll.StatBonus);
            int affinity = Mathf.Max(0, totalSupport - baseSupport);

            int pureGain = option.BaseGain(state.GetLevel(focus)) + totalSupport;
            int gain = Mathf.Max(1, Mathf.FloorToInt(
                pureGain * state.TrainingMultiplier *
                GetPersonalTrainingMultiplier(GetMainUnit(), focus) *
                GetSupportTrainingEfficiencyMultiplier(focus, rolls)));
            // 레벨을 올리기 전에 잰다 — 화면에 보여 준 값과 같아야 한다.
            SecondaryGain[] secondary = SecondaryGainsFor(focus, pureGain,
                rolls.Where(roll => roll.Appeared).Select(roll => roll.Support).ToList());

            // 실패 판정은 체력을 쓰기 전 값으로 한다. 화면에 보여 준 확률과 같아야 한다.
            int failureRate = GetFailureRate(focus);
            bool failed = Random.Range(0, 100) < failureRate;

            int energySpent = option.EnergyCost + (failed ? FailureEnergyPenalty : 0);
            state.SpendEnergy(energySpent);

            Unit main = GetMainUnit();
            if (failed)
            {
                gain = 0;
                secondary = NoSecondary;
            }
            else
            {
                state.RaiseLevel(focus);
                state.GainSkillPoints(option.SkillPoints);

                if (main != null)
                {
                    main.AddStatUpgrade(focus, gain);
                    foreach (SecondaryGain extra in secondary)
                    {
                        if (extra.Amount > 0) main.AddStatUpgrade(extra.Stat, extra.Amount);
                    }
                    main.GainTrainingLevel(TrainingLevelGain);
                    ApplySkillHints(main, rolls);
                }
            }

            // 훈련은 컨디션을 건드리지 않는다. 치른 것은 체력뿐이다(실패의 추가 체력 −10 포함).
            // 트레이닝 노트는 성공이든 실패든 이 훈련에서 쓰인 것으로 친다 — 상승량은 위에서 이미 쟀다.
            int noteUsed = state.ConsumeNote();

            // 턴이 지났다. 다음 훈련 화면은 배치를 새로 굴린다.
            InvalidateSupportPlacement();

            return new TrainingResult
            {
                Focus = focus,
                FocusName = option.Name,
                ConditionBefore = state.ConditionName,
                ConditionAfter = state.ConditionName,
                NoteBonusUsed = noteUsed,
                Supports = BuildSupportOutcomes(rolls),
                StatGain = gain,
                SecondaryGains = secondary,
                SupportBonus = totalSupport,
                AffinityBonus = affinity,
                NewTrainingLevel = main != null ? main.TrainingLevel : 0,
                Failed = failed,
                FailureRate = failureRate,
                EnergySpent = energySpent,
                EnergyAfter = state.Energy,
                SkillPointsGained = failed ? 0 : option.SkillPoints,
                NewFocusTrainingLevel = state.GetLevel(focus),
                SupportMessages = BuildSupportMessages(rolls),
                HintedCodeIds = rolls
                    .Where(roll => roll.HintedCodeId > 0)
                    .Select(roll => roll.HintedCodeId)
                    .ToList(),
            };
        }

        private static List<SupportTrainingRoll> RollSupportTraining(BaseEnums.PrimaryStat focus)
        {
            var rolls = new List<SupportTrainingRoll>();
            foreach (Unit support in GetSupportUnits())
            {
                TrainedCharacterRecord record = SaveSystem.GetTrainedCharacterRecord(support.ID);
                SupportCardSaveData card = record?.supportCard;
                bool hasCard = HasSupportCard(card);
                bool specialtyMatch = hasCard && IsCardSpecialty(card, focus);

                // 참여 여부는 여기서 굴리지 않는다. 이번 턴 배치는 화면을 열 때 이미 정해졌고,
                // 플레이어는 그 배치를 보고 훈련을 골랐다. 지금 다시 굴리면 화면에 보여 준
                // 보너스와 실제 결과가 어긋난다.
                bool appeared = GetPlacement(support) == focus;
                int previousBond = hasCard ? GetSupportBond(support) : 0;
                int newBond = previousBond;
                int statBonus = 0;

                if (appeared)
                {
                    if (hasCard)
                    {
                        statBonus += Mathf.Max(0, card.trainingBonus);
                        if (specialtyMatch)
                        {
                            statBonus += Mathf.Max(0, card.specialtyBonus);
                            if (previousBond >= 75)
                            {
                                // 봉우 — 우정 훈련이 실제로 터질 때만 그 몫을 키운다.
                                statBonus += Mathf.Max(0, Mathf.RoundToInt(
                                    card.friendshipBonus * FriendshipBonusMultiplier()));
                            }
                        }

                        // 카피바라 — 쌓이는 우정도 자체를 키운다.
                        int bondGain = Mathf.Max(1, Mathf.RoundToInt(
                            card.bondGainRate * BondGainMultiplier()));
                        if (specialtyMatch) bondGain += 1;
                        newBond = Mathf.Clamp(previousBond + bondGain, 0, MaxBond);
                        Bonds.Set(support.ID, newBond);
                    }
                    else
                    {
                        statBonus += SupportStatBonusPerUnit;
                        if (GetHighestPrimaryStat(support) == focus)
                        {
                            statBonus += AffinityBonusPerUnit;
                        }
                    }
                }

                rolls.Add(new SupportTrainingRoll
                {
                    Support = support,
                    Card = card,
                    Record = record,
                    Appeared = appeared,
                    SpecialtyMatch = specialtyMatch,
                    FriendshipTraining = appeared && specialtyMatch && previousBond >= 75,
                    StatBonus = statBonus,
                    PreviousBond = previousBond,
                    NewBond = newBond,
                });
            }

            return rolls;
        }

        private static BaseEnums.PrimaryStat GetHighestPrimaryStat(Unit unit)
        {
            if (unit == null) return BaseEnums.PrimaryStat.STR;

            BaseEnums.PrimaryStat highest = BaseEnums.PrimaryStat.STR;
            int highestValue = unit.GetBaseStr();

            Consider(BaseEnums.PrimaryStat.DEX, unit.GetBaseDex());
            Consider(BaseEnums.PrimaryStat.CON, unit.GetBaseCon());
            Consider(BaseEnums.PrimaryStat.INT, unit.GetBaseInt());
            Consider(BaseEnums.PrimaryStat.LUK, unit.GetBaseLuk());
            return highest;

            void Consider(BaseEnums.PrimaryStat stat, int value)
            {
                if (value <= highestValue) return;

                highest = stat;
                highestValue = value;
            }
        }

        // ── 스킬 힌트 ────────────────────────────────────────────────

        /// <summary>특기 훈련에 앉은 서포트가 힌트를 흘릴 확률에 더해지는 값(%p).</summary>
        public const int HintSpecialtyBonus = 15;

        /// <summary>
        /// 서포트 카드가 없는 동료가 힌트를 흘릴 확률(%).
        ///
        /// <b>카드는 편성이 아니라 완주 기록이 만든다.</b> 메인 외 넷은 언제나 서포트로 참여하지만,
        /// 그 넷의 <see cref="SupportCardSaveData"/>는 그 캐릭터가 육성을 완주했을 때만 생긴다.
        /// 그래서 완주 기록이 하나도 없는 동안에는 네 명 전부 이 확률을 쓴다 — 카드가 붙기 전까지
        /// 힌트가 아예 흐르지 않으면 첫 런에서는 스킬 화면이 늘 비어 있게 된다.
        /// </summary>
        public const int CardlessHintRate = 20;

        /// <summary>
        /// 힌트가 떴을 때 그것이 <b>금 코드</b>일 확률(%).
        ///
        /// 금은 은을 배운 뒤에야 배울 수 있으므로, 힌트까지 흔하면 쓸 수 없는 목록만 길어진다.
        /// 낮게 두어 금 힌트 하나가 사건이 되게 한다.
        /// </summary>
        public const int EnhancedHintChance = 20;

        /// <summary>
        /// 등장한 서포트마다 힌트를 한 번 굴린다.
        ///
        /// 예전에는 여기서 패시브를 <b>즉시 전수</b>했다. 그 경로에는 문제가 둘 있었다 —
        /// 플레이어가 개입할 여지가 없었고, 배운 코드가 <c>grantedPassiveCodeIds</c>에 들어가지
        /// 않아 <b>라운드가 끝나 유닛을 스냅샷에서 다시 세우는 순간 사라졌다</b>.
        /// 지금은 힌트만 남기고, 습득은 <see cref="TryLearnSkill"/>이 영구 경로로 처리한다.
        /// </summary>
        private static void ApplySkillHints(Unit main, List<SupportTrainingRoll> rolls)
        {
            if (main == null) return;

            SkillHintState hints = Hints;
            bool gotHint = false;
            foreach (SupportTrainingRoll roll in rolls)
            {
                if (!roll.Appeared || roll.Support == null) continue;

                bool hasCard = HasSupportCard(roll.Card);
                int rate = hasCard ? Mathf.Clamp(roll.Card.skillTransferRate, 0, 100) : CardlessHintRate;
                if (roll.SpecialtyMatch) rate += HintSpecialtyBonus;
                if (roll.Support.ActivePassiveCodes.Any(code => code is Codes.Passive.Inspiration))
                    rate = Mathf.RoundToInt(rate * (1f + Codes.Passive.Inspiration.HintRateBonus));
                if (Random.Range(0, 100) >= rate) continue;

                gotHint |= TryGiveHint(main, roll);
            }

            // 천장 — 힌트 없는 훈련이 쌓였으면 이번에는 반드시 하나 준다. 이번 훈련에 앉은 서포트가
            // 먼저, 줄 것이 없으면 파티의 다른 서포트가 준다(훈련 배치 운까지 겹쳐 다시 비는 일이 없게).
            if (!gotHint && hints.PityReady)
            {
                foreach (SupportTrainingRoll roll in rolls.OrderByDescending(roll => roll.Appeared))
                {
                    if (roll.Support == null) continue;
                    if (TryGiveHint(main, roll)) { gotHint = true; break; }
                }
            }

            hints.RecordTraining(gotHint);
        }

        private static bool TryGiveHint(Unit main, SupportTrainingRoll roll)
        {
            int codeId = PickHintCandidate(main, roll);
            if (codeId <= 0) return false;
            if (!Hints.Add(codeId, HintStage(roll, codeId), roll.Support.UnitName)) return false;

            roll.HintedCodeId = codeId;
            roll.HintedName = PassiveCatalog.Get(codeId, main).Name;
            roll.HintedLevel = Hints.LevelOf(codeId);
            return true;
        }

        /// <summary>
        /// 이 서포트가 흘릴 수 있는 코드 하나를 고른다.
        /// 금과 은을 먼저 갈라 놓고 <see cref="EnhancedHintChance"/>로 금 쪽을 뽑는다.
        /// 후보를 한 통에 섞으면 금이 몇 개냐에 따라 금 힌트 빈도가 제멋대로 흔들린다.
        /// </summary>
        private static int PickHintCandidate(Unit main, SupportTrainingRoll roll)
        {
            var silver = new List<int>();
            var gold = new List<int>();

            foreach (int codeId in HintSourceCodeIds(roll))
            {
                if (codeId < PassiveCatalog.MinSharedId || codeId > PassiveCatalog.MaxSharedId) continue;
                if (main.HasLearnedPassiveCode(codeId)) continue;
                // 이미 최대 레벨인 힌트는 더 받아도 달라지는 것이 없다.
                if (Hints.LevelOf(codeId) >= SkillHintState.MaxLevel) continue;

                PassiveCatalog.Entry entry = PassiveCatalog.Get(codeId, main);
                if (!entry.CanBeHinted) continue;

                if (entry.Grade == BaseEnums.CodeGrade.Enhanced) gold.Add(codeId);
                else silver.Add(codeId);
            }

            bool takeGold = gold.Count > 0 && (silver.Count == 0 || Random.Range(0, 100) < EnhancedHintChance);
            List<int> pool = takeGold ? gold : silver;
            return pool.Count == 0 ? 0 : pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// 힌트가 나올 수 있는 코드 목록.
        /// 카드가 있으면 그 카드가 남긴 패시브에서, 없으면 그 동료의 해금 패시브 정의에서 나온다.
        /// 둘 다 결국 <b>그 동료가 가진 해금 패시브</b>다 — 카드는 목록을 바꾸는 것이 아니라
        /// 확률과 보너스를 얹는다.
        /// </summary>
        private static IEnumerable<int> HintSourceCodeIds(SupportTrainingRoll roll)
        {
            if (HasSupportCard(roll.Card) && roll.Record?.ownedPassiveCodes != null)
            {
                return roll.Record.ownedPassiveCodes
                    .Where(passive => passive != null && passive.transferable && passive.codeId > 0)
                    .Select(passive => passive.codeId);
            }

            return GetUnitDefinition(roll.Support)?.levelPassives?
                .Where(passive => passive != null && passive.codeId > 0)
                .Select(passive => passive.codeId) ?? Enumerable.Empty<int>();
        }

        /// <summary>힌트가 가리키는 코드 단계. 카드가 들고 있던 단계를 그대로 물려준다.</summary>
        private static int HintStage(SupportTrainingRoll roll, int codeId)
        {
            LearnedPassiveSaveData owned = roll.Record?.ownedPassiveCodes?
                .FirstOrDefault(passive => passive != null && passive.codeId == codeId);
            if (owned != null) return Mathf.Max(1, owned.stage);

            LevelPassiveData level = GetUnitDefinition(roll.Support)?.levelPassives?
                .FirstOrDefault(passive => passive != null && passive.codeId == codeId);
            return Mathf.Max(1, level?.stage ?? 1);
        }

        private static UnitData GetUnitDefinition(Unit unit)
        {
            if (unit == null) return null;
            return GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(definition => definition != null && definition.id == unit.ID);
        }

        // ── 스킬 습득 ────────────────────────────────────────────────

        /// <summary>일반(은) 등급 패시브 하나의 스킬 Pt 값.</summary>
        public const int SilverSkillCost = 20;

        /// <summary>강화(금) 등급 패시브 하나의 스킬 Pt 값. 은 셋과 맞먹는다.</summary>
        public const int EnhancedSkillCost = 60;

        private static readonly SkillHintState FallbackHints = new();

        /// <summary>힌트도 런 범위 상태다. RunManager가 없을 때를 위한 폴백.</summary>
        public static SkillHintState Hints =>
            RunManager.Instance != null ? RunManager.Instance.SkillHints : FallbackHints;

        /// <summary>스킬 화면 한 줄. 값과 잠김 사유를 함께 들고 다닌다.</summary>
        public readonly struct SkillOffer
        {
            public readonly int CodeId;
            public readonly string Name;
            public readonly BaseEnums.CodeGrade Grade;
            public readonly int HintLevel;
            public readonly int Cost;

            /// <summary>배울 수 없는 이유. 배울 수 있으면 null이다.</summary>
            public readonly string BlockedReason;

            public SkillOffer(int codeId, string name, BaseEnums.CodeGrade grade, int hintLevel,
                int cost, string blockedReason)
            {
                CodeId = codeId;
                Name = name;
                Grade = grade;
                HintLevel = hintLevel;
                Cost = cost;
                BlockedReason = blockedReason;
            }

            public bool CanLearn => BlockedReason == null;
        }

        /// <summary>기본 비용에 힌트 할인을 먹인 값.</summary>
        public static int GetSkillCost(int codeId)
        {
            PassiveCatalog.Entry entry = PassiveCatalog.Get(codeId, GetMainUnit());
            int baseCost = entry.Grade == BaseEnums.CodeGrade.Enhanced ? EnhancedSkillCost : SilverSkillCost;
            return Mathf.Max(1, Mathf.CeilToInt(baseCost * Hints.CostMultiplier(codeId)));
        }

        /// <summary>
        /// 스킬 화면에 세울 목록. 힌트를 받은 코드만 오른다 — 힌트 없이는 어떤 코드도 살 수 없다.
        /// 금이 앞에 오고, 같은 등급이면 싼 것이 앞이다.
        /// </summary>
        public static List<SkillOffer> GetSkillOffers()
        {
            var offers = new List<SkillOffer>();
            Unit main = GetMainUnit();
            if (main == null) return offers;

            foreach (SkillHintState.Hint hint in Hints.Hints)
            {
                PassiveCatalog.Entry entry = PassiveCatalog.Get(hint.CodeId, main);
                if (!entry.Exists) continue;

                offers.Add(new SkillOffer(hint.CodeId, entry.Name, entry.Grade, hint.Level,
                    GetSkillCost(hint.CodeId), DescribeSkillBlock(main, hint.CodeId, entry)));
            }

            return offers
                .OrderByDescending(offer => offer.Grade == BaseEnums.CodeGrade.Enhanced)
                .ThenBy(offer => offer.Cost)
                .ToList();
        }

        /// <summary>배울 수 없는 이유. 배울 수 있으면 null.</summary>
        private static string DescribeSkillBlock(Unit main, int codeId, PassiveCatalog.Entry entry)
        {
            if (!entry.CanBeHinted) return "습득 불가";
            if (main.HasLearnedPassiveCode(codeId)) return "이미 보유";

            // 금은 은을 밟고 올라간다. 선행 은 코드가 없으면 배울 수 없다.
            int required = PassiveCatalog.RequiredCodeIdFor(codeId, main);
            if (required > 0 && !main.HasLearnedPassiveCode(required))
            {
                return $"선행 필요: {PassiveCatalog.Get(required, main).Name}";
            }

            if (State.SkillPoints < GetSkillCost(codeId)) return "스킬 Pt 부족";
            return null;
        }

        /// <summary>
        /// 힌트받은 스킬을 스킬 Pt로 배운다. 준비 페이즈에서만 부른다.
        ///
        /// <see cref="Unit.GrantPermanentPassive"/>를 쓴다. 그 경로만 저장 스냅샷에 실려
        /// 라운드가 바뀌어도 남는다.
        /// </summary>
        public static bool TryLearnSkill(int codeId, out string reason)
        {
            reason = null;
            Unit main = GetMainUnit();
            if (main == null)
            {
                reason = "메인 캐릭터가 없습니다.";
                return false;
            }

            if (Hints.LevelOf(codeId) <= 0)
            {
                reason = "힌트를 받지 않은 스킬입니다.";
                return false;
            }

            PassiveCatalog.Entry entry = PassiveCatalog.Get(codeId, main);
            reason = DescribeSkillBlock(main, codeId, entry);
            if (reason != null) return false;

            int cost = GetSkillCost(codeId);
            if (!State.TrySpendSkillPoints(cost))
            {
                reason = "스킬 Pt 부족";
                return false;
            }

            int stage = Hints.Get(codeId)?.Stage ?? 1;
            if (!main.GrantPermanentPassive(codeId, stage))
            {
                State.GainSkillPoints(cost);
                reason = "코드를 붙이지 못했습니다.";
                return false;
            }

            Hints.Remove(codeId);
            Debug.Log($"[스킬] {main.UnitName}이(가) {entry.Name}을(를) 스킬 Pt {cost}로 습득했습니다.");
            return true;
        }

        /// <summary>결과 화면용 서포트 목록. 이번 훈련에 실제로 앉아 있던 서포트만 담는다.</summary>
        private static List<SupportOutcome> BuildSupportOutcomes(List<SupportTrainingRoll> rolls)
        {
            var outcomes = new List<SupportOutcome>();
            foreach (SupportTrainingRoll roll in rolls)
            {
                if (roll.Support == null || !roll.Appeared) continue;

                outcomes.Add(new SupportOutcome
                {
                    Name = roll.Support.UnitName,
                    PortraitPath = roll.Support.PortraitPath,
                    StatBonus = roll.StatBonus,
                    BondBefore = roll.PreviousBond,
                    BondAfter = roll.NewBond,
                    Friendship = roll.FriendshipTraining,
                    HintedName = roll.HintedName,
                    HintedLevel = roll.HintedLevel,
                });
            }

            return outcomes;
        }

        private static List<string> BuildSupportMessages(List<SupportTrainingRoll> rolls)
        {
            var messages = new List<string>();
            foreach (SupportTrainingRoll roll in rolls)
            {
                if (roll.Support == null || !HasSupportCard(roll.Card)) continue;

                string appearance = roll.Appeared ? "참여" : "다른 훈련";
                string bond = roll.Appeared ? $"우정 {roll.PreviousBond}->{roll.NewBond}" : $"우정 {roll.PreviousBond}";
                string friendship = roll.FriendshipTraining ? " / 우정 훈련" : "";
                string hint = roll.HintedCodeId > 0
                    ? $" / 힌트 {roll.HintedName} Lv.{roll.HintedLevel}"
                    : "";
                messages.Add($"{roll.Support.UnitName}: {appearance}, +{roll.StatBonus}, {bond}{friendship}{hint}");
            }

            return messages;
        }
    }
}
