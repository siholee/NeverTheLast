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
                    // 위치 계산
                    float xOffset = (x - xMin) * tileSpacing;
                    float yOffset = (y - yMin) * tileSpacing;
                    Vector3 position = new Vector3(xOffset, yOffset, 0);

                    // Cell 생성 및 초기화
                    GameObject cellObject = Instantiate(cellPrefab, position, Quaternion.identity, transform);
                    cellObject.name = $"Cell ({x}, {y})";

                    Cell cellComponent = cellObject.GetComponent<Cell>();
                    if (cellComponent != null)
                    {
                        cellComponent.xPos = x;
                        cellComponent.yPos = y;
                        cellComponent.isOccupied = false; // 초기화
                        cellComponent.portraitRenderer.sprite = null; // 초기화
                        cellComponent.reservedTime = 0f; // 초기화
                    }

                    // 셀의 위치에 따라 아군과 적 투명 캐릭터 생성
                    if (x < 0)
                    {
                        GameObject heroObj = Instantiate(heroPrefab, position, Quaternion.identity);
                        heroObj.transform.SetParent(cellObject.transform);
                        heroObj.transform.localPosition = Vector3.zero;
                        cellComponent.unit = heroObj;
                        Unit hero = heroObj.GetComponent<Unit>();
                        hero.currentCell = cellComponent;
                        hero.isActive = false;
                        heroList.Add(hero);
                    }
                    else if (x > 0)
                    {
                        GameObject enemyObj = Instantiate(enemyPrefab, position, Quaternion.identity);
                        enemyObj.transform.SetParent(cellObject.transform);
                        enemyObj.transform.localPosition = Vector3.zero;
                        cellComponent.unit = enemyObj;
                        Unit enemy = enemyObj.GetComponent<Unit>();
                        enemy.currentCell = cellComponent;
                        enemy.isActive = false;
                        enemyList.Add(enemy);
                    }
                    else
                    {
                        cellComponent.GetComponent<SpriteRenderer>().sprite = null;
                    }

                    // 배열에 셀 저장
                    _cellManager[x - xMin, y - yMin] = cellComponent;
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
            // Debug.LogWarning($"adjustedX: {adjustedX}, adjustedY: {adjustedY}");

            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
                if (cell != null)
                {
                    cell.isOccupied = true; // 셀을 점유 상태로 변경
                    cell.unit.GetComponent<Unit>().Spawn(cell, isEnemy, unitId);
                    Debug.Log($"{(isEnemy ? "적군" : "아군")} 유닛 {cell.unit.GetComponent<Unit>().UnitName}을 ({xPos}, {yPos})에 소환");
                    // Enemy enemyUnit = poolManager.ActivateUnit(false).GetComponent<Enemy>();
                    // enemyUnit.currentCell = cell;
                    // enemyUnit.InitProcess(false, enemyId);
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
