using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 육성(트레이닝) 시스템.
    /// 메인 캐릭터가 육성 페이즈마다 하나의 5스탯을 집중 훈련하여 성장한다.
    /// 서포트 캐릭터는 훈련 효과를 증폭시킨다(우마무스메 서포트 카드 역할).
    /// 훈련 시 메인의 트레이닝 레벨이 올라 레벨 해금 패시브(#2/#3)가 해금된다.
    ///
    /// 서포트 보너스는 실제 편성 데이터에서 계산한다:
    ///  - 서포트 1명당 기본 보너스(SupportStatBonusPerUnit).
    ///  - 집중 스탯이 서포트의 현재 최고 5스탯과 일치하면 특기 보너스(AffinityBonusPerUnit) 추가.
    /// </summary>
    public static class TrainingManager
    {
        // 집중 훈련 시 기본 스탯 강화량.
        public const int BaseStatGain = 1;
        // 서포트 1명당 추가되는 집중 스탯 강화량(항상 적용).
        public const int SupportStatBonusPerUnit = 1;
        // 집중 스탯이 서포트의 특기(클래스 주 스탯)와 일치할 때 서포트 1명당 추가 강화량.
        public const int AffinityBonusPerUnit = 1;
        // 육성 페이즈마다 오르는 트레이닝 레벨.
        public const int TrainingLevelGain = 1;
        private const int OffSpecialtyAppearanceRate = 35;
        private const int MaxBond = SupportBondState.MaxBond;

        // 우정도는 런 범위 상태이므로 RunManager가 소유한다.
        // RunManager가 아직 없을 때(에디터 진입 직후 등)를 대비한 폴백.
        private static readonly SupportBondState FallbackBonds = new();
        private static SupportBondState Bonds =>
            RunManager.Instance != null ? RunManager.Instance.SupportBonds : FallbackBonds;

        public struct TrainingResult
        {
            public BaseEnums.PrimaryStat Focus;
            public int StatGain;
            public int SupportBonus;
            public int AffinityBonus;
            public int NewTrainingLevel;
            public List<string> SupportMessages;
            public List<int> TransferredPassiveIds;
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

        /// <summary>집중 스탯과 무관하게 항상 적용되는 서포트 기본 보너스.</summary>
        public static int GetBaseSupportBonus()
        {
            int bonus = 0;
            foreach (Unit support in GetSupportUnits())
            {
                SupportCardSaveData card = SaveSystem.GetSupportCard(support.ID);
                bonus += HasSupportCard(card)
                    ? Mathf.Max(0, card.trainingBonus)
                    : SupportStatBonusPerUnit;
            }

            return bonus;
        }

        /// <summary>지정 집중 스탯에 대한 서포트 보너스(기본 + 특기 일치 보너스).</summary>
        public static int GetSupportBonus(BaseEnums.PrimaryStat focus)
        {
            int bonus = 0;
            foreach (Unit support in GetSupportUnits())
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

        /// <summary>지정 집중 스탯의 총 강화량(기본 + 서포트 보너스).</summary>
        public static int GetFocusStatGain(BaseEnums.PrimaryStat focus) => BaseStatGain + GetSupportBonus(focus);

        /// <summary>
        /// 집중 스탯 훈련을 메인 캐릭터에 적용한다.
        /// 집중 스탯 강화 + 트레이닝 레벨 상승(레벨 해금 패시브 갱신)을 수행한다.
        /// </summary>
        public static TrainingResult ApplyTraining(BaseEnums.PrimaryStat focus)
        {
            List<SupportTrainingRoll> rolls = RollSupportTraining(focus);
            int baseSupport = rolls
                .Where(roll => roll.Appeared)
                .Sum(roll => roll.Card != null ? Mathf.Max(0, roll.Card.trainingBonus) : SupportStatBonusPerUnit);
            int totalSupport = rolls.Where(roll => roll.Appeared).Sum(roll => roll.StatBonus);
            int affinity = Mathf.Max(0, totalSupport - baseSupport);
            int gain = BaseStatGain + totalSupport;

            Unit main = GetMainUnit();
            if (main != null)
            {
                main.AddStatUpgrade(focus, gain);
                main.GainTrainingLevel(TrainingLevelGain);
                ApplySkillTransfers(main, rolls);
            }

            return new TrainingResult
            {
                Focus = focus,
                StatGain = gain,
                SupportBonus = totalSupport,
                AffinityBonus = affinity,
                NewTrainingLevel = main != null ? main.TrainingLevel : 0,
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
                bool appeared = !hasCard || RollAppearance(card, specialtyMatch);
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

        private static bool RollAppearance(SupportCardSaveData card, bool specialtyMatch)
        {
            int rate = specialtyMatch ? card.specialtyRate : OffSpecialtyAppearanceRate;
            return Random.Range(0, 100) < Mathf.Clamp(rate, 0, 100);
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
                if (main.LearnTransferredPassive(selected.codeId, selected.stage))
                {
                    roll.TransferredPassive = selected;
                }
            }
        }

        private static List<string> BuildSupportMessages(List<SupportTrainingRoll> rolls)
        {
            var messages = new List<string>();
            foreach (SupportTrainingRoll roll in rolls)
            {
                if (roll.Support == null || !HasSupportCard(roll.Card)) continue;

                string appearance = roll.Appeared ? "참여" : "불참";
                string bond = roll.Appeared ? $"우정 {roll.PreviousBond}->{roll.NewBond}" : $"우정 {roll.PreviousBond}";
                string friendship = roll.FriendshipTraining ? " / 우정 훈련" : "";
                string transfer = roll.TransferredPassive != null ? $" / 패시브 {roll.TransferredPassive.codeId} 전수" : "";
                messages.Add($"{roll.Support.UnitName}: {appearance}, +{roll.StatBonus}, {bond}{friendship}{transfer}");
            }

            return messages;
        }
    }
}
