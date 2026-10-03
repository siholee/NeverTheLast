using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 편성 규칙 — 1~5명, 중복 없음.
    ///
    /// 게임 모드는 무한 모드 하나뿐이고 메인 · 서포트 구분이 없다. 캐릭터는 <b>해금 / 미해금</b>으로만 나뉘며
    /// 해금된 캐릭터면 누구나 편성에 오른다. 해금은 데이터(<c>characterType: Unlocked</c>)가 처음부터 주거나
    /// 계정 해금(<see cref="SaveSystem.IsStarterUnlocked"/> — 영입 사건 등)이 준다.
    /// 편성 첫 자리는 <b>대표</b>로, 준비 화면의 레벨 표기에 쓰일 뿐 규칙상 특별하지 않다.
    /// </summary>
    public class CharacterSelectionManager : MonoBehaviour
    {
        public static CharacterSelectionManager Instance { get; private set; }

        public const int MaxSelection = 5;
        private const int MinSelection = 1;

        /// <summary>처음부터 열려 있는 캐릭터의 분류 값. 그 밖의 값(Locked)은 계정 해금이 있어야 쓴다.</summary>
        public const string UnlockedType = "Unlocked";

        /// <summary>지금 이 계정이 쓸 수 있는 캐릭터인가.</summary>
        public static bool IsUnlocked(UnitData data)
            => data != null && (data.characterType == UnlockedType || SaveSystem.IsStarterUnlocked(data.id));

        /// <summary>해금 조건이 적혀 있는데 아직 못 얻은 유닛인가. 선택 화면이 미해금 타일로 띄운다.</summary>
        public static bool IsPendingUnlock(UnitData data)
            => data != null && !IsUnlocked(data) && !string.IsNullOrWhiteSpace(data.unlockCondition);

        public static bool IsPendingUnlock(int unitId) => IsPendingUnlock(GetUnitData(unitId));

        /// <summary>미해금 타일에 띄울 조건 문구. 잠겨 있지 않으면 빈 문자열.</summary>
        public static string UnlockConditionText(UnitData data) => IsPendingUnlock(data) ? data.unlockCondition : "";

        public static void DestroyInstance()
        {
            if (Instance == null) return;
            var instance = Instance;
            Instance = null;
            Destroy(instance.gameObject);
        }

        public class LineupEntry
        {
            public int UnitId;
            public int XPos;
            public int YPos;
        }

        private readonly List<LineupEntry> _lineup = new();
        public IReadOnlyList<LineupEntry> Lineup => _lineup;

        /// <summary>편성 첫 자리(대표). 비어 있으면 0.</summary>
        public int LeadUnitId => _lineup.FirstOrDefault()?.UnitId ?? 0;

        /// <summary>
        /// 전장에 서 있는 대표 캐릭터. 편성 기록이 없으면(불러온 런) 첫 활성 아군으로 대신한다.
        /// </summary>
        public static Entities.Unit FindLeadUnit()
        {
            if (GridManager.Instance == null) return null;
            var heroes = GridManager.Instance.heroList;
            int leadId = Instance?.LeadUnitId ?? 0;
            Entities.Unit lead = leadId > 0
                ? heroes.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == leadId)
                : null;
            return lead ?? heroes.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>편성에 한 명을 더한다.</summary>
        public bool AddHero(int unitId)
        {
            if (_lineup.Count >= MaxSelection)
            {
                Debug.Log("[CharSel] 최대 편성 인원 초과");
                return false;
            }

            if (_lineup.Any(entry => entry.UnitId == unitId))
            {
                Debug.Log($"[CharSel] 이미 선택된 영웅: {unitId}");
                return false;
            }

            if (!IsUnlocked(GetUnitData(unitId)))
            {
                Debug.LogWarning($"[CharSel] 해금되지 않은 영웅: {unitId}");
                return false;
            }

            if (!TryFindOpenAllyPosition(out int xPos, out int yPos))
            {
                Debug.LogWarning("[CharSel] 배치 가능한 아군 좌표가 없습니다.");
                return false;
            }

            _lineup.Add(new LineupEntry { UnitId = unitId, XPos = xPos, YPos = yPos });
            Debug.Log($"[CharSel] {unitId} 추가 -> ({xPos}, {yPos})");
            return true;
        }

        public void RemoveHero(int unitId)
        {
            int index = _lineup.FindIndex(entry => entry.UnitId == unitId);
            if (index < 0) return;
            _lineup.RemoveAt(index);
            RebuildPositions();
        }

        public void ClearLineup() => _lineup.Clear();

        public void ConfirmSelection()
        {
            if (_lineup.Count == 0)
            {
                Debug.LogWarning("[CharSel] 선택된 영웅 없음 - 기본 팀으로 시작");
                UseDefaultLineup();
            }

            if (_lineup.Count < MinSelection)
            {
                Debug.LogWarning($"[CharSel] 편성 인원이 부족합니다. 현재 {_lineup.Count}/{MinSelection}");
                return;
            }

            ClearExistingHeroes();

            // 런의 첫 편성은 카드가 한 장씩 차례로 떨어져 내려앉는다.
            float delay = 0f;
            foreach (var entry in _lineup)
            {
                GridManager.Instance.SpawnUnit(entry.XPos, entry.YPos, false, entry.UnitId, playEntry: true, entryDelay: delay);
                delay += 0.12f;
            }

            GameManager.Instance.uiManager?.HideCharacterSelection();
            GameManager.Instance.BeginRunAfterCharacterSelection();
        }

        /// <summary>편성 없이 시작하면(에디터 직행 등) 해금된 캐릭터를 ID순으로 최대 다섯 명 세운다.</summary>
        public void UseDefaultLineup()
        {
            _lineup.Clear();
            var units = GameManager.Instance.unitDataList?.units;
            if (units == null || units.Count == 0)
            {
                Debug.LogError("[CharSel] unitDataList 비어있음 - 기본 팀 구성 불가");
                return;
            }

            foreach (UnitData unit in units.Where(IsUnlocked).OrderBy(unit => unit.id).Take(MaxSelection))
                AddHero(unit.id);
        }

        private bool TryFindOpenAllyPosition(out int xPos, out int yPos)
        {
            (int x, int y)[] preferred = { (-1, 2), (-2, 2), (-1, 3), (-2, 3), (-1, 1), (-2, 1), (-1, 4), (-2, 4) };
            foreach (var position in preferred)
            {
                if (_lineup.Any(entry => entry.XPos == position.x && entry.YPos == position.y)) continue;
                xPos = position.x;
                yPos = position.y;
                return true;
            }

            xPos = 0;
            yPos = 0;
            return false;
        }

        private void RebuildPositions()
        {
            var ids = _lineup.Select(entry => entry.UnitId).ToList();
            _lineup.Clear();
            foreach (int unitId in ids) AddHero(unitId);
        }

        private static UnitData GetUnitData(int unitId)
            => GameManager.Instance?.unitDataList?.units?.FirstOrDefault(unit => unit.id == unitId);

        private static void ClearExistingHeroes()
        {
            foreach (var hero in GridManager.Instance.heroList.ToList()) GridManager.Instance.RetireUnit(hero);
            GridManager.Instance.PruneUnitLists();
        }
    }
}
