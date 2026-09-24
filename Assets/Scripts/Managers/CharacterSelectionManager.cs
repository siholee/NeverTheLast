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
        /// 라부아지에와 야마는 예외다. 사건이 아니라 각각 <b>첫 육성 완주</b>,
        /// <b>아무 런 1회 종료</b>가 해금 조건이라 경로가 있다.
        /// </summary>
        public static bool HasNoUnlockPath(UnitData data)
        {
            if (data == null || data.characterType != "Locked") return false;
            // 기획상 미해금으로 되돌린 둘은 영입 경로가 붙기 전에도 후보에 노출하지 않는다.
            if (data.id is 23 or 121) return false;
            if (data.id == Entities.LavoisierChemistry.UnitId || data.id == RunManager.YamaUnitId) return false;
            if (GameManager.Instance?.unitDataList?.units?.Any(source =>
                    source?.unlocksUnitIdsOnClear?.Contains(data.id) == true) == true)
                return false;
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
        /// 아직 <b>서포터 칸에만</b> 오르는 초기 서포트 카드(characterType: Support)인가.
        ///
        /// 기본적으로 이 부류는 메인 후보가 되지 않는다. 육성 완료 기록이 남아도 마찬가지다 —
        /// 서포트는 메인이 될 수 없어 기록이 곧 자격이 되면 앞뒤가 맞지 않기 때문이다.
        ///
        /// <b>예외는 스스로 얻어 낸 스타팅 해금뿐이다.</b> <c>unlocksAsStarterOnClear</c>가 붙은
        /// 일부 서포트(라이트·니콜·프레이아·마리)는 서포트로 한 런을 완주시키면 스타팅 후보로 열리고
        /// (<see cref="RunManager"/>), 그 뒤로는 메인 격자에 선다.
        /// </summary>
        /// <summary>
        /// 해금 조건이 적혀 있는데 아직 못 얻은 유닛인가. 선택 화면이 미해금 타일로 띄운다.
        /// 한 번이라도 육성을 끝냈거나 스타팅으로 열렸으면 더는 잠긴 것이 아니다.
        /// </summary>
        public static bool IsPendingUnlock(UnitData data)
            => data != null && !string.IsNullOrWhiteSpace(data.unlockCondition) &&
               !SaveSystem.IsStarterUnlocked(data.id) && !SaveSystem.IsCharacterTrained(data.id);

        public static bool IsPendingUnlock(int unitId)
            => IsPendingUnlock(GetUnitData(unitId));

        /// <summary>미해금 타일에 띄울 조건 문구. 잠겨 있지 않으면 빈 문자열.</summary>
        public static string UnlockConditionText(UnitData data)
            => IsPendingUnlock(data) ? data.unlockCondition : "";

        public static bool IsSupportOnly(UnitData data)
            => data != null && !data.canStartAsMain && data.characterType == "Support" &&
               !SaveSystem.IsStarterUnlocked(data.id);

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
            public string SupportId;
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
            return AddHero(unitId, SaveSystem.GetSupportCard(unitId)?.supportId);
        }

        /// <summary>
        /// 캐릭터와 그 캐릭터의 육성 카드 한 장을 편성한다. 동일 유닛은 카드가 달라도 두 번 넣지 않는다.
        /// </summary>
        public bool AddHero(int unitId, string supportId)
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
                !CanUseInInfinite(unitId))
            {
                Debug.Log($"[CharSel] 무한 모드는 육성 완료 캐릭터만 선택 가능: {unitId}");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(supportId) &&
                !SaveSystem.GetTrainedCharacterRecords(unitId)
                    .Any(record => record.supportCard?.supportId == supportId))
            {
                Debug.LogWarning($"[CharSel] {unitId}의 카드가 아닌 supportId: {supportId}");
                return false;
            }

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Infinite &&
                string.IsNullOrWhiteSpace(supportId))
            {
                Debug.LogWarning($"[CharSel] 무한 모드는 육성 카드 한 장을 지정해야 합니다: {unitId}");
                return false;
            }

            if (!TryFindOpenAllyPosition(out int xPos, out int yPos))
            {
                Debug.LogWarning("[CharSel] 배치 가능한 아군 좌표가 없습니다.");
                return false;
            }

            _lineup.Add(new LineupEntry
            {
                UnitId = unitId,
                SupportId = supportId ?? "",
                XPos = xPos,
                YPos = yPos,
                Role = role,
            });
            Debug.Log($"[CharSel] {role} {unitId} 추가 -> ({xPos}, {yPos}) / 카드 {supportId ?? "기본"}");
            return true;
        }

        public bool SetSelectedSupportCard(int unitId, string supportId)
        {
            LineupEntry entry = _lineup.FirstOrDefault(candidate => candidate.UnitId == unitId);
            if (entry == null) return false;
            if (!string.IsNullOrWhiteSpace(supportId) &&
                !SaveSystem.GetTrainedCharacterRecords(unitId)
                    .Any(record => record.supportCard?.supportId == supportId)) return false;
            entry.SupportId = supportId ?? "";
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
                var spawned = GridManager.Instance.SpawnUnit(entry.XPos, entry.YPos, false, entry.UnitId);
                spawned?.SelectSupportCard(entry.SupportId);
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
            var selections = _lineup.Select(entry => (entry.UnitId, entry.SupportId)).ToList();
            _lineup.Clear();
            foreach ((int unitId, string supportId) in selections)
            {
                AddHero(unitId, supportId);
            }
        }

        /// <summary>무한 모드를 여는 데 필요한 육성 완료 카드 수. 무한 모드는 서포트 카드 5장으로만 편성한다.</summary>
        public const int InfiniteRequiredCards = MaxSelection;

        private static bool CanUseInInfinite(int unitId) => IsInfiniteEligible(GetUnitData(unitId));

        /// <summary>
        /// 무한 모드에 서포트 카드로 설 수 있는가 — 분류가 허락하고 <b>육성을 마쳤을 때</b>.
        ///
        /// Locked는 해금되면 Starter처럼 메인으로 키울 수 있으므로 무한 모드에도 선다.
        /// 예전에는 데이터의 <c>canUseInInfinite</c>만 봐서, 해금한 로키나 데모에서 열린 시를
        /// 끝까지 키워도 무한 모드 카드로 세지 않았다. Support 유형은 메인이 될 수 없어 육성 기록이 생기지 않는다.
        /// </summary>
        public static bool IsInfiniteEligible(UnitData data) =>
            data != null &&
            (data.canUseInInfinite || data.characterType == "Locked" ||
             SaveSystem.IsStarterUnlocked(data.id)) &&
            SaveSystem.GetTrainedCharacterRecords(data.id).Count > 0;

        /// <summary>무한 모드에 쓸 수 있는 육성 완료 카드 수. 메인 메뉴가 무한 모드를 열지 정할 때 묻는다.</summary>
        public static int CountInfiniteEligibleCards(IEnumerable<UnitData> units) =>
            units?.Count(IsInfiniteEligible) ?? 0;

        private static bool CanSelectForRole(int unitId, CharacterRole role)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == BaseClasses.BaseEnums.GameMode.Infinite)
            {
                return CanUseInInfinite(unitId);
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
