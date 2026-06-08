using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 전열/후열 슬롯 기반 그리드 관리자.<br/>
    /// • 아군/적군 각각 전열 4슬롯 + 후열 4슬롯 (아군 최대 5명)<br/>
    /// • 타겟 선택: 주목도(EffectiveThreat) 기반 — 전열 유닛 +1 보너스<br/>
    /// • DontDestroyOnLoad 제거: Game.unity 재로드 시 새로 생성
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("References")]
        public GameManager gameManager;

        [Header("Prefabs")]
        public GameObject cellPrefab;
        public GameObject heroPrefab;
        public GameObject enemyPrefab;

        [Header("Grid Layout")]
        public float tileSpacingX = 2.1f;   // 슬롯 수평 간격
        public float frontlineY   = 1.05f;  // 전열 Y 위치
        public float backlineY    = -1.05f; // 후열 Y 위치
        public float sideGap      = 0.6f;   // 양 진영 사이 여백 (x=0 기준)

        // ── 슬롯 배열 ─────────────────────────────────────────────────────────
        private Cell[] _heroFront;    // 아군 전열 [0..3]
        private Cell[] _heroBack;     // 아군 후열 [0..3]
        private Cell[] _enemyFront;   // 적 전열 [0..3]
        private Cell[] _enemyBack;    // 적 후열 [0..3]

        public const int SlotsPerLine   = 4;
        public const int MaxHeroesTotal = 5; // 아군 최대 (전열+후열)

        public List<Unit> heroList  = new();
        public List<Unit> enemyList = new();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            // DontDestroyOnLoad 없음: Game 씬 재로드 시 새 인스턴스 생성
        }

        // ── 초기화 ────────────────────────────────────────────────────────────

        public void InitializeComponent()
        {
            heroList  = new List<Unit>();
            enemyList = new List<Unit>();
            CreateGrid();
        }

        private void CreateGrid()
        {
            // 아군 전열: x 음수 (왼쪽), 전열 Y
            _heroFront  = CreateLine(isHeroSide: true,  isFrontline: true);
            // 아군 후열: x 음수 (왼쪽), 후열 Y
            _heroBack   = CreateLine(isHeroSide: true,  isFrontline: false);
            // 적 전열: x 양수 (오른쪽), 전열 Y
            _enemyFront = CreateLine(isHeroSide: false, isFrontline: true);
            // 적 후열: x 양수 (오른쪽), 후열 Y
            _enemyBack  = CreateLine(isHeroSide: false, isFrontline: false);
        }

        private Cell[] CreateLine(bool isHeroSide, bool isFrontline)
        {
            var line = new Cell[SlotsPerLine];
            float y  = isFrontline ? frontlineY : backlineY;

            for (int i = 0; i < SlotsPerLine; i++)
            {
                // 슬롯 0 = 중앙에 가장 가까운 위치
                // 아군(isHeroSide): x 음수, 슬롯 0이 가장 오른쪽
                // 적(isHeroSide=false): x 양수, 슬롯 0이 가장 왼쪽
                float x = isHeroSide
                    ? -(sideGap + (i + 0.5f) * tileSpacingX)
                    :  (sideGap + (i + 0.5f) * tileSpacingX);

                var go = Instantiate(cellPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
                go.name = $"Cell_{(isHeroSide ? "Hero" : "Enemy")}_{(isFrontline ? "Front" : "Back")}_S{i}";

                var cell = go.GetComponent<Cell>() ?? go.AddComponent<Cell>();

                if (!go.TryGetComponent<BoxCollider2D>(out _))
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(2.0f, 2.0f);
                }

                cell.slotIndex   = i;
                cell.isHeroSide  = isHeroSide;
                cell.isFrontline = isFrontline;
                cell.isOccupied  = false;
                line[i] = cell;
            }
            return line;
        }

        // ── 소환 ──────────────────────────────────────────────────────────────

        /// <summary>아군 소환. CharacterSelectionManager에서 호출.</summary>
        public void SpawnHero(int unitId, bool isFrontline, int slotIndex)
        {
            Cell[] line = isFrontline ? _heroFront : _heroBack;
            if (!IsValidSlot(slotIndex)) return;
            SpawnInCell(line[slotIndex], isEnemy: false, unitId);
        }

        /// <summary>적 소환. RoundManager에서 호출.</summary>
        public void SpawnEnemy(int unitId, bool isFrontline, int slotIndex)
        {
            Cell[] line = isFrontline ? _enemyFront : _enemyBack;
            if (!IsValidSlot(slotIndex)) return;
            if (line[slotIndex].isOccupied)
            {
                Debug.LogWarning($"[GridManager] 적 소환 실패 — 슬롯 점유됨 ({(isFrontline?"전열":"후열")} {slotIndex})");
                return;
            }
            SpawnInCell(line[slotIndex], isEnemy: true, unitId);
        }

        /// <summary>비어있는 적 슬롯 인덱스 반환. 없으면 -1.</summary>
        public int FindAvailableEnemySlot(bool isFrontline)
        {
            Cell[] line = isFrontline ? _enemyFront : _enemyBack;
            for (int i = 0; i < SlotsPerLine; i++)
            {
                if (!line[i].isOccupied) return i;
            }
            return -1;
        }

        private void SpawnInCell(Cell cell, bool isEnemy, int unitId)
        {
            if (cell.isOccupied)
            {
                Debug.LogWarning($"[GridManager] SpawnInCell: 셀 이미 점유됨 ({cell.name})");
                return;
            }

            var prefab  = isEnemy ? enemyPrefab : heroPrefab;
            var unitObj = Instantiate(prefab, cell.transform.position, Quaternion.identity);
            unitObj.name = $"{(isEnemy ? "Enemy" : "Hero")}_{unitId}";

            cell.isOccupied = true;
            cell.unit       = unitObj;

            var u = unitObj.GetComponent<Unit>();
            if (u == null) { Debug.LogError($"[GridManager] Unit 컴포넌트 없음: {unitObj.name}"); return; }

            if (isEnemy) enemyList.Add(u);
            else         heroList.Add(u);

            u.currentCell = cell;
            u.CurrentLine = cell.isFrontline ? BaseEnums.GridLine.Frontline : BaseEnums.GridLine.Backline;

            u.Spawn(cell, isEnemy, unitId);
            Debug.Log($"[GridManager] {(isEnemy ? "적" : "아군")} {u.UnitName} 소환 → {(cell.isFrontline ? "전열" : "후열")} 슬롯 {cell.slotIndex}");

            if (GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress
                && BattleManager.Instance != null)
            {
                BattleManager.Instance.RegisterUnit(u);
            }
        }

        // ── 타겟 선택 ────────────────────────────────────────────────────────

        /// <summary>
        /// 단일 타겟 후보 반환 (주목도 기반).<br/>
        /// autoTarget: 동률 없이 결정된 1명 / tiedCandidates: 동률 시 선택 필요한 목록.
        /// </summary>
        public (List<Unit> autoTarget, List<Unit> tiedCandidates) ResolveSingleTarget(Unit caster)
        {
            var opponents = GetOpponents(caster);
            if (opponents.Count == 0)
                return (new List<Unit>(), new List<Unit>());

            int maxThreat = opponents.Max(u => u.EffectiveThreat);
            var tied      = opponents.Where(u => u.EffectiveThreat == maxThreat).ToList();

            return tied.Count == 1
                ? (tied, new List<Unit>())       // 단독 최고 주목도 → 자동 선택
                : (new List<Unit>(), tied);      // 동률 → 선택 필요
        }

        /// <summary>
        /// 단일 타겟 자동 선택 (AI / 다중히트 리타겟용).<br/>
        /// 동률 시 무작위.
        /// </summary>
        public List<Unit> GetAutoSingleTarget(Unit caster)
        {
            var opponents = GetOpponents(caster);
            if (opponents.Count == 0) return new List<Unit>();

            int maxThreat = opponents.Max(u => u.EffectiveThreat);
            var tied      = opponents.Where(u => u.EffectiveThreat == maxThreat).ToList();
            return new List<Unit> { tied[Random.Range(0, tied.Count)] };
        }

        /// <summary>범위 타겟: 지정 진영의 모든 활성 유닛.</summary>
        public List<Unit> ResolveRangeTarget(Unit caster, bool targetEnemySide)
        {
            // caster 시점에서 "적 진영"이 어디인지 결정
            bool wantEnemies = caster.IsEnemy ? !targetEnemySide : targetEnemySide;
            return wantEnemies
                ? enemyList.Where(u => u.isActive).ToList()
                : heroList.Where(u => u.isActive).ToList();
        }

        /// <summary>광역 타겟: 필드 위 모든 활성 유닛.</summary>
        public List<Unit> ResolveAoETarget()
            => heroList.Concat(enemyList).Where(u => u.isActive).ToList();

        /// <summary>본인 타겟.</summary>
        public List<Unit> ResolveSelf(Unit caster)
            => new List<Unit> { caster };

        // ── 레거시 호환 타겟 메서드 (기존 코드가 아직 호출하는 경우) ──────────
        public List<Unit> TargetNearestEnemy(Unit caster) => GetAutoSingleTarget(caster);
        public List<Unit> TargetAllEnemies(Unit caster)   => ResolveRangeTarget(caster, true);
        public List<Unit> TargetAllAllies(Unit caster)    => ResolveRangeTarget(caster, false);

        // ── 이벤트 ────────────────────────────────────────────────────────────

        public void OnRoundStart()
        {
            foreach (var hero  in heroList)  hero.Invoke(BaseEnums.UnitEventType.OnRoundStart, hero);
            foreach (var enemy in enemyList) enemy.Invoke(BaseEnums.UnitEventType.OnRoundStart, enemy);
        }

        // ── 레거시 호환 유틸리티 ──────────────────────────────────────────────

        /// <summary>xPos/yPos 기반 슬롯 가용성 체크 (레거시 호환).</summary>
        public bool IsCellAvailable(int xPos, int yPos)
        {
            TryGetLineAndSlot(xPos, yPos, out var line, out int slotIndex);
            if (line == null || !IsValidSlot(slotIndex)) return false;
            return !line[slotIndex].isOccupied;
        }

        public void SelectUnit(int xPos, int yPos)
        {
            var unit = GetUnitAtPosition(xPos, yPos);
            if (unit != null && unit.isActive)
                Debug.Log($"[GridManager] 선택: {unit.UnitName}");
        }

        public Unit GetUnitAtPosition(int xPos, int yPos)
        {
            TryGetLineAndSlot(xPos, yPos, out var line, out int slotIndex);
            if (line == null || !IsValidSlot(slotIndex)) return null;
            var cell = line[slotIndex];
            return cell.isOccupied ? cell.unit?.GetComponent<Unit>() : null;
        }

        /// <summary>레거시 SpawnUnit 호환 (xPos 부호로 진영, yPos 1=전열/2=후열).</summary>
        public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId)
        {
            bool isFrontline = (yPos != 2);
            int  slotIndex   = Mathf.Abs(xPos) - 1;
            if (isEnemy) SpawnEnemy(unitId, isFrontline, slotIndex);
            else         SpawnHero(unitId, isFrontline, slotIndex);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        private List<Unit> GetOpponents(Unit caster)
        {
            var pool = caster.IsEnemy ? heroList : enemyList;
            return pool.Where(u => u.isActive).ToList();
        }

        private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotsPerLine;

        private void TryGetLineAndSlot(int xPos, int yPos, out Cell[] line, out int slotIndex)
        {
            bool isHeroSide  = xPos < 0;
            bool isFrontline = yPos != 2;
            slotIndex        = Mathf.Abs(xPos) - 1;

            if (isHeroSide)
                line = isFrontline ? _heroFront : _heroBack;
            else
                line = isFrontline ? _enemyFront : _enemyBack;
        }
    }
}
