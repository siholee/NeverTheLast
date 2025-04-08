using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Managers
{
    public class GridManager : MonoBehaviour
    {
        // 싱글톤 인스턴스
        public static GridManager Instance { get; private set; }

        public GameManager gameManager;

        public GameObject cellPrefab;
        public GameObject heroPrefab;
        public GameObject enemyPrefab;
        public int xMin = -4;
        public int xMax = 4;
        public int yMin = 1;
        public int yMax = 3;
        public float tileSpacing = 2.1f;

        private Cell[,] _cellManager; // 셀 관리 배열
        public List<Unit> heroList;
        public List<Unit> enemyList;

        private void Awake()
        {
            // 싱글톤 인스턴스 초기화
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // 씬 변경 시에도 유지
            }
            else
            {
                Destroy(gameObject); // 이미 인스턴스가 존재하면 제거
            }
        }

        public void InitializeComponent()
        {
            heroList = new List<Unit>();
            enemyList = new List<Unit>();
            CreateGrid();
        }

        private void CreateGrid()
{
    int rows = yMax - yMin + 1;
    int columns = xMax - xMin + 1;
    _cellManager = new Cell[columns, rows];

    for (int x = xMin; x <= xMax; x++)
    {
        for (int y = yMin; y <= yMax; y++)
        {
            Vector3 position = new Vector3(x * tileSpacing, y * tileSpacing, 0);
            GameObject cellObject = Instantiate(cellPrefab, position, Quaternion.identity);
            cellObject.name = $"Cell_{x}_{y}";
            cellObject.transform.parent = transform;

            Cell cell = cellObject.GetComponent<Cell>();
            if (cell == null)
            {
                cell = cellObject.AddComponent<Cell>();
            }
            
            // BoxCollider2D 추가
            BoxCollider2D boxCollider = cellObject.GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = cellObject.AddComponent<BoxCollider2D>();
                boxCollider.size = new Vector2(2.0f, 2.0f); // 셀 크기에 맞게 조정
            }

            cell.xPos = x;
            cell.yPos = y;
            cell.isOccupied = false;
            cell.reservedTime = 0f;

            int adjustedX = x - xMin;
            int adjustedY = y - yMin;
            _cellManager[adjustedX, adjustedY] = cell;
        }
    }
}

        public bool IsCellAvailable(int xPos, int yPos)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;

            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
                return cell != null && !cell.isOccupied && cell.reservedTime <= 0f;
            }

            return false; // 셀이 없거나 이미 점유됨
        }

        public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;

            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
                if (cell != null)
                {
                    cell.isOccupied = true; // 셀을 점유 상태로 변경
                    cell.unit.GetComponent<Unit>().Spawn(cell, isEnemy, unitId);
                    Debug.Log($"{(isEnemy ? "적군" : "아군")} 유닛 {cell.unit.GetComponent<Unit>().UnitName}을 ({xPos}, {yPos})에 소환");
                }
            }
        }

        public void OnRoundStart()
        {
            foreach (Unit hero in heroList)
            {
                hero.Invoke(BaseEnums.UnitEventType.OnRoundStart, (hero));
            }

            foreach (Unit enemy in enemyList)
            {
                enemy.Invoke(BaseEnums.UnitEventType.OnRoundStart, (enemy));
            }
        }

        public List<Unit> TargetNearestEnemy(Unit caster)
        {
            List<Unit> enemyCandidates = new List<Unit>();

            if (!caster.IsEnemy)
            {
                // Assuming GridManager maintains a list of enemies
                foreach (Unit enemy in enemyList)
                {
                    if (enemy.isActive) enemyCandidates.Add(enemy);
                }

            }
            else
            {
                // Assuming GridManager maintains a list of heroes
                foreach (Unit hero in heroList)
                {
                    if (hero.isActive) enemyCandidates.Add(hero);
                }
            }

            List<Unit> nearestUnit = new List<Unit>();
            float minDistance = float.MaxValue;
            Vector2 casterPos = new(caster.currentCell.xPos, caster.currentCell.yPos);

            foreach (Unit unit in enemyCandidates)
            {
                Vector2 targetPos = new Vector2(unit.currentCell.xPos, unit.currentCell.yPos);
                float distance = Vector2.Distance(casterPos, targetPos);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestUnit = new List<Unit> { unit };
                }
            }

            return nearestUnit;
        }

        public List<Unit> TargetAllEnemies(Unit caster)
        {
            if (!caster.IsEnemy)
            {
                return enemyList.Where(e => e.isActive).ToList();
            }
            else
            {
                return heroList.Where(h => h.isActive).ToList();
            }
        }

        public List<Unit> TargetAllAllies(Unit caster)
        {
            if (!caster.IsEnemy)
            {
                return heroList.Where(e => e.isActive).ToList();
            }
            else
            {
                return enemyList.Where(h => h.isActive).ToList();
            }
        }
    }
}
