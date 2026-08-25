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
    /// 런 범위 상태이므로 <see cref="Managers.RunManager"/>가 소유한다.
    /// (아직 세이브에는 실리지 않는다 — 저장한 런을 불러오면 체력과 훈련 레벨이 초기화된다.)
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
        /// 훈련이 끝날 때마다 컨디션이 한 칸 흔들린다.
        /// 체력이 낮으면 나빠지는 쪽으로 기운다 — 무리해서 굴리면 대가가 따른다.
        /// </summary>
        public void DriftCondition()
        {
            int roll = Random.Range(0, 100);
            if (roll < 55) return;

            bool worsen = Energy < 40 ? roll < 85 : roll < 78;
            ConditionIndex = Mathf.Clamp(ConditionIndex + (worsen ? 1 : -1), 0, ConditionNames.Length - 1);
        }

        public void Reset()
        {
            _levels.Clear();
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
            };

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
