using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;

namespace Managers
{
    public class CharacterSelectionManager : MonoBehaviour
    {
        public static CharacterSelectionManager Instance { get; private set; }

        private const int MaxSelection = 5;
        private const int MinSelection = 1;
        private const int MainSelection = 1;

        // 테스트용 임시 플래그. Locked 캐릭터를 영구 해금 데이터 변경 없이 메인 후보에 노출한다.
        public const bool UnlockLockedCharactersForTesting = false;

        public static bool IsTemporarilyUnlocked(UnitData data)
            => UnlockLockedCharactersForTesting && data != null && data.characterType == "Locked";

        /// <summary>
        /// <b>해금할 방법이 없는</b> Locked 유닛인가. 그런 유닛은 편성에 올린다.
        ///
        /// Locked는 "영입 사건을 만나면 열린다"는 뜻인데, 아직 그 사건이 없는 유닛까지
        /// 잠가 두면 <b>영영 쓸 수 없는 데이터</b>가 된다. 분류는 Locked로 남기고
        /// 자격만 열어 둔다 — 사건이 생기면 <c>80_stages.yaml</c>이 바뀌는 것만으로
        /// 저절로 다시 잠긴다. 코드에 이름을 적지 않는 이유다.
        ///
        /// 라부아지에는 예외다. 사건이 아니라 <b>첫 완주</b>가 해금 조건이라 경로가 있다.
        /// </summary>
        public static bool HasNoUnlockPath(UnitData data)
        {
            if (data == null || data.characterType != "Locked") return false;
            if (data.id == Entities.LavoisierChemistry.UnitId) return false;
            return !RecruitableUnitIds.Contains(data.id);
        }

        private static HashSet<int> _recruitableUnitIds;

        /// <summary>영입 사건이 제안하는 유닛 ID 전부. 사건 데이터에서 읽는다.</summary>
        private static HashSet<int> RecruitableUnitIds
        {
            get
            {
                if (_recruitableUnitIds != null) return _recruitableUnitIds;

                _recruitableUnitIds = new HashSet<int>();
                var events = GameManager.Instance?.dataManager?.FetchStageThemeDataList()?.events;
                if (events == null) return _recruitableUnitIds;

                foreach (StageEventData stageEvent in events)
                {
                    foreach (int unitId in stageEvent?.RecruitUnitIds ?? System.Linq.Enumerable.Empty<int>())
                    {
                        _recruitableUnitIds.Add(unitId);
                    }
                }

                return _recruitableUnitIds;
            }
        }

        /// <summary>
        /// 초기 서포트 카드(characterType: Support)인가.
        ///
        /// 이 부류는 <b>서포터 칸에만</b> 오른다. 육성으로 해금되거나 스타터 해금 기록이
        /// 남더라도 메인 캐릭터 후보에는 절대 넣지 않는다 — 미해금(Locked)을 테스트용으로
        /// 열어 둔 것과는 다른 이야기다.
        /// </summary>
        public static bool IsSupportOnly(UnitData data)
            => data != null && !data.canStartAsMain && data.characterType == "Support";

        public static void DestroyInstance()
        {
            if (Instance == null) return;

            var instance = Instance;
            Instance = null;
            Destroy(instance.gameObject);
        }

        public enum CharacterRole
        {
            Main,
            Support,
        }

        public class LineupEntry
        {
            public int UnitId;
            public int XPos;
            public int YPos;
            public CharacterRole Role;
        }

        private readonly List<LineupEntry> _lineup = new();
        public IReadOnlyList<LineupEntry> Lineup => _lineup;
        public int MainUnitId => _lineup.FirstOrDefault(entry => entry.Role == CharacterRole.Main)?.UnitId ?? 0;
        public IReadOnlyList<int> SupportUnitIds => _lineup
            .Where(entry => entry.Role == CharacterRole.Support)
            .Select(entry => entry.UnitId)
            .ToList();

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
            if (Instance == this)
            {
                Instance = null;
            }
        }

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

            CharacterRole role = _lineup.Count < MainSelection ? CharacterRole.Main : CharacterRole.Support;
            if (!CanSelectForRole(unitId, role))
            {
                Debug.LogWarning($"[CharSel] {role} 슬롯에 선택할 수 없는 영웅: {unitId}");
                return false;
            }

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Infinite &&
                (!CanUseInInfinite(unitId) || !SaveSystem.IsCharacterTrained(unitId)))
            {
                Debug.Log($"[CharSel] 무한 모드는 육성 완료 캐릭터만 선택 가능: {unitId}");
                return false;
            }

            if (!TryFindOpenAllyPosition(out int xPos, out int yPos))
            {
                Debug.LogWarning("[CharSel] 배치 가능한 아군 좌표가 없습니다.");
                return false;
            }

            _lineup.Add(new LineupEntry { UnitId = unitId, XPos = xPos, YPos = yPos, Role = role });
            Debug.Log($"[CharSel] {role} {unitId} 추가 -> ({xPos}, {yPos})");
            return true;
        }

        public bool AddMainHero(int unitId)
        {
            if (_lineup.Any(entry => entry.Role == CharacterRole.Main))
            {
                Debug.Log("[CharSel] 메인 캐릭터는 이미 선택되었습니다.");
                return false;
            }

            return AddHero(unitId);
        }

        public bool AddSupportHero(int unitId)
        {
            if (!_lineup.Any(entry => entry.Role == CharacterRole.Main))
            {
                Debug.Log("[CharSel] 메인 캐릭터를 먼저 선택해야 합니다.");
                return false;
            }

            if (_lineup.Count(entry => entry.Role == CharacterRole.Support) >= MaxSelection - MainSelection)
            {
                Debug.Log("[CharSel] 서포트 캐릭터 최대 인원 초과");
                return false;
            }

            return AddHero(unitId);
        }

        public void RemoveHero(int unitId)
        {
            int index = _lineup.FindIndex(entry => entry.UnitId == unitId);
            if (index < 0) return;

            _lineup.RemoveAt(index);
            RebuildPositions();
        }

        public void ClearLineup()
        {
            _lineup.Clear();
        }

        public void ConfirmSelection()
        {
            if (_lineup.Count == 0)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Training)
                {
                    Debug.LogWarning("[CharSel] 선택된 영웅 없음 - 기본 팀으로 시작");
                    UseDefaultLineup();
                }
                else
                {
                    Debug.LogWarning("[CharSel] 무한 모드는 육성 완료 캐릭터 5명이 필요합니다.");
                    return;
                }
            }

            bool infiniteMode = GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Infinite;
            int requiredSelection = infiniteMode ? MaxSelection : MinSelection;

            if (_lineup.Count < requiredSelection)
            {
                Debug.LogWarning($"[CharSel] 편성 인원이 부족합니다. 현재 {_lineup.Count}/{requiredSelection}");
                return;
            }

            if (!infiniteMode && !CanSelectForRole(MainUnitId, CharacterRole.Main))
            {
                Debug.LogWarning("[CharSel] 육성 모드 메인으로 시작할 수 없는 캐릭터입니다.");
                return;
            }

            ClearExistingHeroes();

            foreach (var entry in _lineup)
            {
                GridManager.Instance.SpawnUnit(entry.XPos, entry.YPos, false, entry.UnitId);
            }

            GameManager.Instance.uiManager?.HideCharacterSelection();
            GameManager.Instance.BeginRunAfterCharacterSelection();
        }

        public void UseDefaultLineup()
        {
            _lineup.Clear();

            var units = GameManager.Instance.unitDataList?.units;
            if (units == null || units.Count == 0)
            {
                Debug.LogError("[CharSel] unitDataList 비어있음 - 기본 팀 구성 불가");
                return;
            }

            UnitData main = units.FirstOrDefault(unit => unit.canStartAsMain);
            if (main != null)
            {
                AddHero(main.id);
            }

            foreach (var support in units.Where(unit => unit.canStartAsSupport).Take(MaxSelection - MainSelection))
            {
                AddHero(support.id);
            }
        }

        private bool TryFindOpenAllyPosition(out int xPos, out int yPos)
        {
            (int x, int y)[] preferredPositions =
            {
                (-1, 2),
                (-2, 2),
                (-1, 3),
                (-2, 3),
                (-1, 1),
                (-2, 1),
                (-1, 4),
                (-2, 4),
            };

            foreach (var position in preferredPositions)
            {
                bool alreadyReserved = _lineup.Any(entry => entry.XPos == position.x && entry.YPos == position.y);
                if (!alreadyReserved)
                {
                    xPos = position.x;
                    yPos = position.y;
                    return true;
                }
            }

            xPos = 0;
            yPos = 0;
            return false;
        }

        private void RebuildPositions()
        {
            var unitIds = _lineup.Select(entry => entry.UnitId).ToList();
            _lineup.Clear();
            foreach (int unitId in unitIds)
            {
                AddHero(unitId);
            }
        }

        private static bool CanUseInInfinite(int unitId)
        {
            UnitData data = GetUnitData(unitId);
            return data != null && data.canUseInInfinite;
        }

        private static bool CanSelectForRole(int unitId, CharacterRole role)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Infinite)
            {
                return CanUseInInfinite(unitId) && SaveSystem.IsCharacterTrained(unitId);
            }

            UnitData data = GetUnitData(unitId);
            if (data == null) return false;

            return role switch
            {
                CharacterRole.Main => !IsSupportOnly(data) &&
                    (data.canStartAsMain || IsTemporarilyUnlocked(data) || HasNoUnlockPath(data) ||
                     SaveSystem.IsStarterUnlocked(unitId) || SaveSystem.IsCharacterTrained(unitId)),
                CharacterRole.Support => data.canStartAsSupport || HasNoUnlockPath(data) ||
                    SaveSystem.IsStarterUnlocked(unitId) || SaveSystem.IsCharacterTrained(unitId),
                _ => false,
            };
        }

        private static UnitData GetUnitData(int unitId)
        {
            return GameManager.Instance?.unitDataList?.units?.FirstOrDefault(unit => unit.id == unitId);
        }

        private static void ClearExistingHeroes()
        {
            // 리스트에 사본이 남지 않도록 완전히 물린다.
            foreach (var hero in GridManager.Instance.heroList.ToList())
            {
                GridManager.Instance.RetireUnit(hero);
            }
            GridManager.Instance.PruneUnitLists();
        }
    }
}
