using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 런 범위 상태: 스킬 힌트. 우마무스메의 힌트를 그대로 옮겨 온 것이다.
    ///
    /// 훈련에서 서포트가 <b>힌트</b>를 흘리면 그 패시브가 여기 쌓이고, 준비 페이즈에서
    /// 스킬 Pt를 치러야 비로소 메인이 배운다. 예전에는 훈련이 패시브를 <b>즉시 전수</b>했는데,
    /// 그러면 플레이어가 개입할 여지가 없고 스킬 Pt는 쌓이기만 하는 죽은 자원이었다.
    ///
    /// 같은 힌트를 또 받으면 레벨이 오르고 값이 싸진다 — 한 서포트를 계속 같은 훈련에
    /// 앉히는 선택에 보상이 붙는다.
    /// </summary>
    public class SkillHintState
    {
        /// <summary>힌트 레벨 상한. 여기서 값이 가장 싸진다.</summary>
        public const int MaxLevel = 3;

        /// <summary>레벨별 비용 배율. 인덱스는 레벨 − 1이다.</summary>
        public static readonly float[] CostMultipliers = { 1.00f, 0.85f, 0.70f };

        public class Hint
        {
            public int CodeId;
            public int Level;

            /// <summary>힌트를 준 쪽이 가지고 있던 코드 단계. 배울 때 그대로 쓴다.</summary>
            public int Stage;

            /// <summary>맨 처음 힌트를 준 서포트의 이름. 화면에만 쓴다.</summary>
            public string SourceName;
        }

        private readonly List<Hint> _hints = new();

        /// <summary>
        /// 힌트 천장. 성공한 훈련이 이만큼 연달아 힌트 없이 끝나면 다음 성공 훈련은 힌트를 반드시 준다.
        ///
        /// 첫 런의 힌트 기대치는 훈련 1회당 약 0.11개라, 훈련 7번에 하나도 못 받는 경우가 절반에 가깝다.
        /// 스킬 Pt만 쌓이고 쓸 곳이 없는 채로 첫 보스에 닿는다는 QA가 있어 바닥을 깔았다.
        /// </summary>
        public const int PityTrainings = 4;

        /// <summary>마지막 힌트 이후 힌트 없이 끝난 성공 훈련 수.</summary>
        public int DryTrainings { get; private set; }

        /// <summary>다음 성공 훈련이 힌트를 반드시 주는가.</summary>
        public bool PityReady => DryTrainings >= PityTrainings;

        /// <summary>힌트 보장까지 남은 성공 훈련 수(이번 것 포함). 화면 표시용.</summary>
        public int TrainingsUntilPity => Mathf.Max(1, PityTrainings + 1 - DryTrainings);

        /// <summary>훈련 한 번이 끝났다. 힌트를 받았으면 천장을 되돌리고, 아니면 한 칸 쌓는다.</summary>
        public void RecordTraining(bool gotHint) => DryTrainings = gotHint ? 0 : DryTrainings + 1;

        public void RestoreDryTrainings(int value) => DryTrainings = Mathf.Max(0, value);

        public IReadOnlyList<Hint> Hints => _hints;

        public bool HasAny => _hints.Count > 0;

        public Hint Get(int codeId) => _hints.Find(hint => hint.CodeId == codeId);

        public int LevelOf(int codeId) => Get(codeId)?.Level ?? 0;

        /// <summary>해당 힌트의 비용 배율. 힌트가 없으면 할인도 없다.</summary>
        public float CostMultiplier(int codeId)
        {
            int level = Mathf.Clamp(LevelOf(codeId), 1, MaxLevel);
            return CostMultipliers[level - 1];
        }

        /// <summary>
        /// 힌트를 하나 얹는다. 이미 있으면 레벨이 오른다(상한 <see cref="MaxLevel"/>).
        /// 새로 생겼거나 레벨이 올랐으면 true를 돌려준다 — 결과 화면이 이걸로 줄을 낸다.
        /// </summary>
        public bool Add(int codeId, int stage, string sourceName)
        {
            if (codeId <= 0) return false;

            Hint existing = Get(codeId);
            if (existing == null)
            {
                _hints.Add(new Hint
                {
                    CodeId = codeId,
                    Level = 1,
                    Stage = Mathf.Max(1, stage),
                    SourceName = sourceName,
                });
                return true;
            }

            if (existing.Level >= MaxLevel) return false;

            existing.Level += 1;
            return true;
        }

        /// <summary>배우고 나면 목록에서 뺀다. 이미 가진 코드는 코덱스가 보여 준다.</summary>
        public void Remove(int codeId) => _hints.RemoveAll(hint => hint.CodeId == codeId);

        public void Clear()
        {
            _hints.Clear();
            DryTrainings = 0;
        }

        public List<SkillHintSaveData> BuildSaveData()
        {
            var result = new List<SkillHintSaveData>();
            foreach (Hint hint in _hints)
            {
                result.Add(new SkillHintSaveData
                {
                    codeId = hint.CodeId,
                    level = hint.Level,
                    stage = hint.Stage,
                    sourceName = hint.SourceName,
                });
            }

            return result;
        }

        public void Restore(List<SkillHintSaveData> saved)
        {
            _hints.Clear();
            if (saved == null) return;

            foreach (SkillHintSaveData data in saved)
            {
                if (data == null || data.codeId <= 0) continue;
                _hints.Add(new Hint
                {
                    CodeId = data.codeId,
                    Level = Mathf.Clamp(data.level, 1, MaxLevel),
                    Stage = Mathf.Max(1, data.stage),
                    SourceName = data.sourceName,
                });
            }
        }
    }
}
