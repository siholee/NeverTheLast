using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class CharacterSelectionManager : MonoBehaviour
    {
        public static CharacterSelectionManager Instance { get; private set; }

        private const int MaxSelection = 5;

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

        public void ClearLineup()
        {
            _lineup.Clear();
        }

        public void ConfirmSelection()
        {
            if (_lineup.Count == 0)
            {
                Debug.LogWarning("[CharSel] 선택된 영웅 없음 - 기본 팀으로 시작");
                UseDefaultLineup();
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

            var playerUnits = units.Where(unit => unit.id < 100).Take(3).ToList();
            foreach (var unit in playerUnits)
            {
                AddHero(unit.id);
            }
        }

        private bool TryFindOpenAllyPosition(out int xPos, out int yPos)
        {
            int[] allyColumns = { -1, -2, -3, -4 };
            for (int y = 1; y <= 3; y++)
            {
                foreach (int x in allyColumns)
                {
                    bool alreadyReserved = _lineup.Any(entry => entry.XPos == x && entry.YPos == y);
                    if (!alreadyReserved)
                    {
                        xPos = x;
                        yPos = y;
                        return true;
                    }
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
