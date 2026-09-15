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

        public void Clear() => _hints.Clear();

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
