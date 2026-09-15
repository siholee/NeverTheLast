using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 육성 런 동안 유지되는 훈련 상태. 우마무스메의 훈련 화면이 들고 있는 값들이다.
    ///
    ///   · 체력  — 훈련하면 줄고 휴식하면 찬다. 낮을수록 실패율이 오른다.
    ///   · 훈련 레벨 — 같은 훈련을 반복하면 오르고, 오를수록 기본 상승치가 커진다.
    ///   · 스킬 Pt — 훈련마다 쌓인다.
    ///   · 컨디션 — 훈련 결과에 배율로 곱해진다.
    ///
    ///   · 서포트 배치 — 이번 턴에 어떤 서포트가 어느 훈련에 앉았는지.
    ///
    /// 런 범위 상태이므로 <see cref="Managers.RunManager"/>가 소유하고 세이브에 함께 실린다.
    /// </summary>
    public class TrainingState
    {
        public const int MaxEnergy = 100;
        public const int MinTrainingLevel = 1;
        public const int MaxTrainingLevel = 5;

        /// <summary>좋은 쪽이 앞이다. 인덱스가 클수록 나쁘다.</summary>
        public static readonly string[] ConditionNames = { "최상", "좋음", "보통", "나쁨", "최악" };
        public static readonly float[] ConditionMultipliers = { 1.10f, 1.05f, 1.00f, 0.95f, 0.90f };

        /// <summary>기본 컨디션("보통")의 인덱스. 세이브 기본값으로도 쓴다.</summary>
        public const int NormalConditionIndex = 2;

        private readonly Dictionary<BaseEnums.PrimaryStat, int> _levels = new();

        /// <summary>
        /// 이번 훈련 턴에 각 서포트가 앉은 훈련. 우마무스메처럼 <b>턴마다 다시 배치된다</b>.
        ///
        /// 값이 <see cref="Absent"/>면 이번 턴에 나오지 않은 것이다. "굴리지 않았다"와
        /// "굴렸는데 안 나왔다"를 구분해야 하므로, 자리를 못 받은 서포트도 목록에 남는다.
        /// 화면을 닫았다 다시 열어도 자리가 바뀌면 안 되므로 굴린 결과를 여기 보관한다.
        /// </summary>
        private readonly Dictionary<int, int> _placements = new();

        /// <summary>배치표에서 "이번 턴에는 나오지 않음"을 뜻하는 값.</summary>
        public const int Absent = -1;

        /// <summary>이번 턴 배치를 이미 굴렸는지. 훈련이나 휴식으로 턴이 지나면 false로 돌아간다.</summary>
        public bool PlacementReady { get; private set; }

        public int Energy { get; private set; } = MaxEnergy;
        public int SkillPoints { get; private set; }
        public int ConditionIndex { get; private set; } = NormalConditionIndex;

        public string ConditionName => ConditionNames[Mathf.Clamp(ConditionIndex, 0, ConditionNames.Length - 1)];
        public float ConditionMultiplier => ConditionMultipliers[Mathf.Clamp(ConditionIndex, 0, ConditionMultipliers.Length - 1)];

        /// <summary>해당 훈련의 레벨. 한 번도 하지 않았으면 1이다.</summary>
        public int GetLevel(BaseEnums.PrimaryStat stat)
        {
            return _levels.TryGetValue(stat, out int level)
                ? Mathf.Clamp(level, MinTrainingLevel, MaxTrainingLevel)
                : MinTrainingLevel;
        }

        public void RaiseLevel(BaseEnums.PrimaryStat stat)
        {
            _levels[stat] = Mathf.Min(MaxTrainingLevel, GetLevel(stat) + 1);
        }

        /// <summary>체력을 쓴다. 음수를 넣으면 회복이다(지능 훈련).</summary>
        public void SpendEnergy(int amount)
        {
            Energy = Mathf.Clamp(Energy - amount, 0, MaxEnergy);
        }

        public void RestoreEnergy(int amount) => SpendEnergy(-Mathf.Abs(amount));

        public void GainSkillPoints(int amount)
        {
            SkillPoints = Mathf.Max(0, SkillPoints + amount);
        }

        /// <summary>
        /// 스킬 Pt를 치른다. 모자라면 아무것도 쓰지 않고 false.
        /// 유일한 소비처는 스킬 힌트 습득이다(<see cref="Managers.TrainingManager.TryLearnSkill"/>).
        /// </summary>
        public bool TrySpendSkillPoints(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (SkillPoints < amount) return false;

            SkillPoints -= amount;
            return true;
        }

        /// <summary>
        /// 훈련이 끝날 때마다 컨디션이 한 칸 흔들린다.
        /// 체력이 낮으면 나빠지는 쪽으로 기운다 — 무리해서 굴리면 대가가 따른다.
        ///
        /// <b>실패는 흔들지 않고 확정으로 한 칸 떨어뜨린다.</b> 체력을 바닥까지 끌어 쓴 대가가
        /// 그 턴 안에서 끝나면, 실패율 60%를 감수하고 굴리는 쪽이 늘 이득이 된다.
        /// </summary>
        public void DriftCondition(bool failed = false)
        {
            if (failed)
            {
                WorsenCondition();
                return;
            }

            int roll = Random.Range(0, 100);
            if (roll < 55) return;

            bool worsen = Energy < 40 ? roll < 85 : roll < 78;
            ConditionIndex = Mathf.Clamp(ConditionIndex + (worsen ? 1 : -1), 0, ConditionNames.Length - 1);
        }

        /// <summary>
        /// 컨디션을 한 칸 끌어올린다. <b>휴식만 이걸 할 수 있다</b> —
        /// 훈련은 흔들기만 하므로, 나빠진 컨디션을 되돌릴 확실한 수단이 하나는 있어야 한다.
        /// </summary>
        public void ImproveCondition() => ConditionIndex = Mathf.Max(0, ConditionIndex - 1);

        public void WorsenCondition() =>
            ConditionIndex = Mathf.Min(ConditionNames.Length - 1, ConditionIndex + 1);

        /// <summary>이 서포트가 이번 턴에 앉은 훈련. 나오지 않았거나 아직 안 굴렸으면 false.</summary>
        public bool TryGetPlacement(int unitId, out BaseEnums.PrimaryStat stat)
        {
            stat = BaseEnums.PrimaryStat.STR;
            if (!_placements.TryGetValue(unitId, out int value) || value == Absent) return false;

            stat = (BaseEnums.PrimaryStat)value;
            return true;
        }

        /// <summary>이번 턴에 이 서포트의 자리를 이미 굴렸는지(자리를 못 받았어도 true).</summary>
        public bool WasPlacementRolled(int unitId) => _placements.ContainsKey(unitId);

        /// <summary>이 서포트를 해당 훈련에 앉힌다.</summary>
        public void SetPlacement(int unitId, BaseEnums.PrimaryStat stat) => _placements[unitId] = (int)stat;

        /// <summary>이번 턴에 나오지 않은 서포트로 기록한다(자리 없음).</summary>
        public void SetAbsent(int unitId) => _placements[unitId] = Absent;

        /// <summary>배치 한 판을 다 굴렸다고 표시한다.</summary>
        public void MarkPlacementReady() => PlacementReady = true;

        /// <summary>턴이 지났다. 다음에 훈련 화면을 열면 배치를 새로 굴린다.</summary>
        public void InvalidatePlacement()
        {
            _placements.Clear();
            PlacementReady = false;
        }

        public void Reset()
        {
            _levels.Clear();
            _placements.Clear();
            PlacementReady = false;
            Energy = MaxEnergy;
            SkillPoints = 0;
            ConditionIndex = NormalConditionIndex;
        }

        // ── 저장 · 복원 ──────────────────────────────────────────────

        public TrainingSaveData BuildSaveData()
        {
            var data = new TrainingSaveData
            {
                energy = Energy,
                skillPoints = SkillPoints,
                conditionIndex = ConditionIndex,
                levels = new List<TrainingLevelSaveData>(),
                placementReady = PlacementReady,
                placements = new List<TrainingPlacementSaveData>(),
            };

            foreach (KeyValuePair<int, int> pair in _placements)
            {
                data.placements.Add(new TrainingPlacementSaveData
                {
                    unitId = pair.Key,
                    stat = pair.Value,
                });
            }

            foreach (KeyValuePair<BaseEnums.PrimaryStat, int> pair in _levels)
            {
                data.levels.Add(new TrainingLevelSaveData
                {
                    stat = (int)pair.Key,
                    level = Mathf.Clamp(pair.Value, MinTrainingLevel, MaxTrainingLevel),
                });
            }

            return data;
        }

        /// <summary>
        /// 저장본에서 되돌린다. 훈련 상태가 없던 저장본(v4 이전에 만든 것)이면
        /// <c>saved</c>가 비어 있으므로 기본값으로 시작한다.
        /// </summary>
        public void Restore(TrainingSaveData saved)
        {
            Reset();
            if (saved == null) return;

            Energy = Mathf.Clamp(saved.energy, 0, MaxEnergy);
            SkillPoints = Mathf.Max(0, saved.skillPoints);
            ConditionIndex = Mathf.Clamp(saved.conditionIndex, 0, ConditionNames.Length - 1);

            PlacementReady = saved.placementReady;
            if (saved.placements != null)
            {
                foreach (TrainingPlacementSaveData placement in saved.placements)
                {
                    if (placement == null) continue;
                    if (placement.stat != Absent
                        && !System.Enum.IsDefined(typeof(BaseEnums.PrimaryStat), placement.stat)) continue;

                    _placements[placement.unitId] = placement.stat;
                }
            }

            if (saved.levels == null) return;

            foreach (TrainingLevelSaveData level in saved.levels)
            {
                if (level == null) continue;
                if (!System.Enum.IsDefined(typeof(BaseEnums.PrimaryStat), level.stat)) continue;

                _levels[(BaseEnums.PrimaryStat)level.stat] =
                    Mathf.Clamp(level.level, MinTrainingLevel, MaxTrainingLevel);
            }
        }
    }
}
