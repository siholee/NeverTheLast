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
        // Singleton instance
        public static GridManager Instance { get; private set; }

        [Header("References")]
        public GameManager gameManager;

        [Header("Prefabs")]
        public GameObject cellPrefab;
        public GameObject heroPrefab;
        public GameObject enemyPrefab;
        
        [Header("Grid Settings")]
        public int xMin = -4;
        public int xMax = 4;
        public int yMin = 0; 
        public int yMax = 3;
        
        [Header("Bench Grid Settings")]
        public int benchXMin = -4;
        public int benchXMax = 4;
        public int benchYMin = -1;
        public int benchYMax = -1;

        private Cell[,] _fieldCellManager; // Cell management array
        private Cell[,] _benchCellManager; // Cell management array
        public List<Unit> heroList;
        public List<Unit> enemyList;

        public Transform benchParent;
        public Transform fieldParent;
        
        private void Awake()
        {
            // Initialize singleton instance
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Persist through scene changes
            }
            else
            {
                Destroy(gameObject); // Remove if instance already exists
                return;
            }
        }

        public void InitializeComponent()
        {
            heroList = new List<Unit>();
            enemyList = new List<Unit>();
            SetGrid();
        }

        private void SetGrid()
        {
            // _fieldCellManager 배열 초기화
            int rows = yMax - yMin + 1;
            int columns = xMax - xMin + 1;
            _fieldCellManager = new Cell[columns, rows];
            
            // _benchCellManager 배열 초기화
            int benchRows = benchYMax - benchYMin + 1;
            int benchColumns = benchXMax - benchXMin + 1;
            _benchCellManager = new Cell[benchColumns, benchRows];
            
            // fieldParent의 자식 오브젝트들을 순회하며 Cell 정보 설정
            foreach (Transform child in fieldParent)
            {
                // 오브젝트 이름에서 좌표 추출 (Cell_{x}_{y} 형식)
                string childName = child.name;
                if (childName.StartsWith("Cell_"))
                {
                    string[] parts = childName.Split('_');
                    if (parts.Length == 3)
                    {
                        if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                        {
                            // Cell 컴포넌트 가져오기 또는 추가
                            Cell cell = child.GetComponent<Cell>();
                            if (cell == null)
                            {
                                cell = child.gameObject.AddComponent<Cell>();
                            }
                            
                            // Cell 속성 설정
                            cell.xPos = x;
                            cell.yPos = y;
                            cell.isOccupied = false;
                            cell.reservedTime = 0f;
                            
                            // 2차원 배열 인덱스 계산 및 할당
                            int adjustedX = x - xMin;
                            int adjustedY = y - yMin;
                            
                            // 범위 체크
                            if (adjustedX >= 0 && adjustedX < columns && adjustedY >= 0 && adjustedY < rows)
                            {
                                _fieldCellManager[adjustedX, adjustedY] = cell;
                                Debug.Log($"Field Cell assigned to array: {childName} at index [{adjustedX}, {adjustedY}]");
                            }
                            else
                            {
                                Debug.LogWarning($"Field Cell coordinates out of range: {childName} (x: {x}, y: {y})");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to parse coordinates from object name: {childName}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid Cell object name format: {childName}. Expected format: Cell_x_y");
                    }
                }
            }
            
            // benchParent의 자식 오브젝트들을 순회하며 Cell 정보 설정
            foreach (Transform child in benchParent)
            {
                // 오브젝트 이름에서 좌표 추출 (Cell_{x}_{y} 형식)
                string childName = child.name;
                if (childName.StartsWith("Cell_"))
                {
                    string[] parts = childName.Split('_');
                    if (parts.Length == 3)
                    {
                        if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                        {
                            // Cell 컴포넌트 가져오기 또는 추가
                            Cell cell = child.GetComponent<Cell>();
                            if (cell == null)
                            {
                                cell = child.gameObject.AddComponent<Cell>();
                            }
                            
                            // Cell 속성 설정
                            cell.xPos = x;
                            cell.yPos = y;
                            cell.isOccupied = false;
                            cell.reservedTime = 0f;
                            
                            // 2차원 배열 인덱스 계산 및 할당
                            int adjustedX = x - benchXMin;
                            int adjustedY = y - benchYMin;
                            
                            // 범위 체크
                            if (adjustedX >= 0 && adjustedX < benchColumns && adjustedY >= 0 && adjustedY < benchRows)
                            {
                                _benchCellManager[adjustedX, adjustedY] = cell;
                                Debug.Log($"Bench Cell assigned to array: {childName} at index [{adjustedX}, {adjustedY}]");
                            }
                            else
                            {
                                Debug.LogWarning($"Bench Cell coordinates out of range: {childName} (x: {x}, y: {y})");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to parse coordinates from bench object name: {childName}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid Bench Cell object name format: {childName}. Expected format: Cell_x_y");
                    }
                }
            }
        }

        public bool IsCellAvailable(int xPos, int yPos)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;

            if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
            {
                Cell cell = _fieldCellManager[adjustedX, adjustedY];
                return cell != null && !cell.isOccupied && cell.reservedTime <= 0f;
            }

            return false; // Cell doesn't exist or is occupied
        }

        public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;

            if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
            {
                Cell cell = _fieldCellManager[adjustedX, adjustedY];
                if (cell)
                {
                    // Create unit
                    GameObject unitObj;
                    if (isEnemy)
                    {
                        unitObj = Instantiate(enemyPrefab, cell.transform.position, Quaternion.identity);
                        unitObj.name = $"Enemy_{unitId}";
                    }
                    else
                    {
                        unitObj = Instantiate(heroPrefab, cell.transform.position, Quaternion.identity);
                        unitObj.name = $"Hero_{unitId}";
                    }
                    
                    // Assign unit to cell
                    cell.isOccupied = true;
                    cell.unit = unitObj;
                    
                    // Initialize unit
                    Unit unitComponent = unitObj.GetComponent<Unit>();
                    if (unitComponent != null)
                    {
                        // Add to appropriate list
                        if (isEnemy)
                        {
                            enemyList.Add(unitComponent);
                        }
                        else
                        {
                            heroList.Add(unitComponent);
                        }
                        
                        unitComponent.currentCell = cell;
                        unitComponent.Spawn(cell, isEnemy, unitId);
                        Debug.Log($"Spawned {(isEnemy ? "enemy" : "hero")} unit {unitComponent.UnitName} at ({xPos}, {yPos})");
                    }
                    else
                    {
                        Debug.LogError($"Failed to get Unit component from spawned object at ({xPos}, {yPos})");
                    }
                }
            }
        }
        
        public void SelectUnit(int xPos, int yPos)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;
            
            if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
            {
                Cell cell = _fieldCellManager[adjustedX, adjustedY];
                if (cell == null) return;
                if (cell.isOccupied && cell.unit != null)
                {
                    Unit selectedUnit = cell.unit.GetComponent<Unit>();
                    if (selectedUnit != null && selectedUnit.isActive)
                    {
                        Debug.Log($"Selected unit: {selectedUnit.UnitName} at ({xPos}, {yPos})");
                    }
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
                foreach (Unit enemy in enemyList)
                {
                    if (enemy.isActive && enemy.currentCell.yPos > 0) enemyCandidates.Add(enemy);
                }
            }
            else
            {
                foreach (Unit hero in heroList)
                {
                    if (hero.isActive && hero.currentCell.yPos > 0) enemyCandidates.Add(hero);
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
                return enemyList.Where(e => e.isActive && e.currentCell.yPos > 0).ToList();
            }
            else
            {
                return heroList.Where(h => h.isActive && h.currentCell.yPos > 0).ToList();
            }
        }

        public List<Unit> TargetAllAllies(Unit caster)
        {
            if (!caster.IsEnemy)
            {
                return heroList.Where(e => e.isActive && e.currentCell.yPos > 0).ToList();
            }
            else
            {
                return enemyList.Where(h => h.isActive && h.currentCell.yPos > 0).ToList();
            }
        }
        
        // Get unit at specific position
        public Unit GetUnitAtPosition(int xPos, int yPos)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;
            
            if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
            {
                Cell cell = _fieldCellManager[adjustedX, adjustedY];
                if (cell != null && cell.isOccupied && cell.unit != null)
                {
                    return cell.unit.GetComponent<Unit>();
                }
            }
            
            return null;
        }
    }
}

