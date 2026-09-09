using System.Collections.Generic;
using System.Linq;
using BaseClasses;
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
    ///   상승 = floor( (훈련 레벨 기본치 + 서포트 보너스) × 컨디션 배율 )
    ///   체력 = 훈련별 소모(지능만 회복) + 실패 시 추가 소모
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
        /// 훈련 5종. 지능만 체력을 회복하고 스킬 Pt를 많이 준다 — 우마무스메의 지능 훈련 역할이다.
        /// </summary>
        public static readonly TrainingOption[] Options =
        {
            new(BaseEnums.PrimaryStat.STR, "근력", "방어력 · 장비 중량 한도", 20, 2, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.DEX, "민첩", "행동 속도", 18, 2, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.CON, "체력", "최대 체력", 16, 2, new[] { 3, 4, 5, 6, 8 }),
            new(BaseEnums.PrimaryStat.INT, "지능", "마나 획득 효율", -5, 4, new[] { 2, 3, 4, 5, 6 }),
            new(BaseEnums.PrimaryStat.LUK, "행운", "치명타 확률", 14, 3, new[] { 3, 4, 5, 6, 8 }),
        };

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

            /// <summary>이번 훈련에서 전수받은 패시브. 없으면 null.</summary>
            public string TransferredName;
        }

        public struct TrainingResult
        {
            public BaseEnums.PrimaryStat Focus;

            /// <summary>훈련 이름("근력" 등).</summary>
            public string FocusName;
            public int StatGain;
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
            public List<int> TransferredPassiveIds;

            /// <summary>훈련 전후 컨디션 이름. 결과 화면이 변화를 보여 준다.</summary>
            public string ConditionBefore;
            public string ConditionAfter;

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
            public LearnedPassiveSaveData TransferredPassive;

            /// <summary>전수된 패시브의 이름. 유닛에 붙은 코드 인스턴스에서 읽어 온다.</summary>
            public string TransferredName;
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
        ///   (훈련 레벨 기본치 + 서포트 보너스) × 컨디션
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
                raw * State.ConditionMultiplier * GetPersonalTrainingMultiplier(GetMainUnit(), focus) *
                GetSupportTrainingEfficiencyMultiplier(focus)));
        }

        private static float GetSupportTrainingEfficiencyMultiplier(BaseEnums.PrimaryStat focus)
        {
            int bonusCount = GetSupportsOn(focus).Count(support =>
                HasTrainingEfficiencyPassive(support, focus));
            return 1f + bonusCount * 0.10f;
        }

        private static float GetSupportTrainingEfficiencyMultiplier(
            BaseEnums.PrimaryStat focus, IEnumerable<SupportTrainingRoll> rolls)
        {
            int bonusCount = rolls.Count(roll => roll.Appeared && roll.Support != null &&
                HasTrainingEfficiencyPassive(roll.Support, focus));
            return 1f + bonusCount * 0.10f;
        }

        private static bool HasTrainingEfficiencyPassive(Unit support, BaseEnums.PrimaryStat focus)
        {
            if (support == null) return false;
            return focus switch
            {
                BaseEnums.PrimaryStat.INT => support.ActivePassiveCodes.Any(code =>
                    code is Codes.Passive.ScholarshipPassive),
                BaseEnums.PrimaryStat.DEX => support.ActivePassiveCodes.Any(code =>
                    code is Codes.Passive.MarieOfficer),
                _ => false,
            };
        }

        private static float GetPersonalTrainingMultiplier(Unit main, BaseEnums.PrimaryStat focus)
        {
            if (main == null) return 1f;
            if (focus == BaseEnums.PrimaryStat.LUK &&
                main.ActivePassiveCodes.Any(code => code is Codes.Passive.BastetMasterThief)) return 1.10f;
            if (focus == BaseEnums.PrimaryStat.DEX &&
                main.ActivePassiveCodes.Any(code => code is Codes.Passive.SabahDexterity)) return 1.10f;
            return 1f;
        }

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

            int gain = Mathf.Max(1, Mathf.FloorToInt(
                (option.BaseGain(state.GetLevel(focus)) + totalSupport) * state.ConditionMultiplier *
                GetPersonalTrainingMultiplier(GetMainUnit(), focus) *
                GetSupportTrainingEfficiencyMultiplier(focus, rolls)));

            // 실패 판정은 체력을 쓰기 전 값으로 한다. 화면에 보여 준 확률과 같아야 한다.
            int failureRate = GetFailureRate(focus);
            bool failed = Random.Range(0, 100) < failureRate;

            int energySpent = option.EnergyCost + (failed ? FailureEnergyPenalty : 0);
            state.SpendEnergy(energySpent);

            Unit main = GetMainUnit();
            if (failed)
            {
                gain = 0;
            }
            else
            {
                state.RaiseLevel(focus);
                state.GainSkillPoints(option.SkillPoints);

                if (main != null)
                {
                    main.AddStatUpgrade(focus, gain);
                    main.GainTrainingLevel(TrainingLevelGain);
                    ApplySkillTransfers(main, rolls);
                }
            }

            string conditionBefore = state.ConditionName;
            state.DriftCondition();

            // 턴이 지났다. 다음 훈련 화면은 배치를 새로 굴린다.
            InvalidateSupportPlacement();

            return new TrainingResult
            {
                Focus = focus,
                FocusName = option.Name,
                ConditionBefore = conditionBefore,
                ConditionAfter = state.ConditionName,
                Supports = BuildSupportOutcomes(rolls),
                StatGain = gain,
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
                TransferredPassiveIds = rolls
                    .Where(roll => roll.TransferredPassive != null)
                    .Select(roll => roll.TransferredPassive.codeId)
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
                                statBonus += Mathf.Max(0, card.friendshipBonus);
                            }
                        }

                        int bondGain = Mathf.Max(1, card.bondGainRate);
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

        private static void ApplySkillTransfers(Unit main, List<SupportTrainingRoll> rolls)
        {
            foreach (SupportTrainingRoll roll in rolls)
            {
                if (!roll.Appeared || !roll.SpecialtyMatch || !HasSupportCard(roll.Card) || roll.Record?.ownedPassiveCodes == null)
                {
                    continue;
                }

                int transferRate = Mathf.Clamp(roll.Card.skillTransferRate, 0, 100);
                if (Random.Range(0, 100) >= transferRate)
                {
                    continue;
                }

                List<LearnedPassiveSaveData> candidates = roll.Record.ownedPassiveCodes
                    .Where(passive => passive != null && passive.transferable && passive.codeId > 0)
                    .Where(passive => !main.LearnedPassiveRecords.Any(known => known != null && known.codeId == passive.codeId))
                    .ToList();
                if (candidates.Count == 0)
                {
                    continue;
                }

                LearnedPassiveSaveData selected = candidates[Random.Range(0, candidates.Count)];
                // 코드 인스턴스에는 원본 ID가 없다. 배우기 전후를 비교해 새로 붙은 것을 집는다.
                var before = new HashSet<Codes.Base.PassiveCode>(main.ActivePassiveCodes);
                if (main.LearnTransferredPassive(selected.codeId, selected.stage))
                {
                    roll.TransferredPassive = selected;
                    roll.TransferredName = main.ActivePassiveCodes
                        .FirstOrDefault(code => code != null && !before.Contains(code))?.CodeName
                        ?? $"패시브 #{selected.codeId}";
                }
            }
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
                    TransferredName = roll.TransferredName,
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
                string transfer = roll.TransferredPassive != null ? $" / 패시브 {roll.TransferredPassive.codeId} 전수" : "";
                messages.Add($"{roll.Support.UnitName}: {appearance}, +{roll.StatBonus}, {bond}{friendship}{transfer}");
            }

            return messages;
        }
    }
}
