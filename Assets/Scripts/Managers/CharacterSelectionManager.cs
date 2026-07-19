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
                Debug.LogWarning("[CharSel] 육성 모드 메인 캐릭터는 아탈란테만 선택할 수 있습니다.");
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
                CharacterRole.Main => data.canStartAsMain || SaveSystem.IsStarterUnlocked(unitId) || SaveSystem.IsCharacterTrained(unitId),
                CharacterRole.Support => data.canStartAsSupport || SaveSystem.IsStarterUnlocked(unitId) || SaveSystem.IsCharacterTrained(unitId),
                _ => false,
            };
        }

        private static UnitData GetUnitData(int unitId)
        {
            return GameManager.Instance?.unitDataList?.units?.FirstOrDefault(unit => unit.id == unitId);
        }

        private static void ClearExistingHeroes()
        {
            foreach (var hero in GridManager.Instance.heroList.ToList())
            {
                if (hero != null && hero.isActive)
                {
                    hero.DeactivateUnit();
                }
            }
        }
    }
}
