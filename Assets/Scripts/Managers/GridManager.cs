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
        public int benchSize = 9; // 벤치 슬롯 개수

        private Cell[,] _fieldCellManager; // Cell management array
        private Cell[] _benchCellManager; // Cell management array (1차원)
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
            _benchCellManager = new Cell[benchSize];
            
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
            int benchIndex = 0;
            foreach (Transform child in benchParent)
            {
                if (benchIndex >= benchSize) break;
                
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
                            
                            // 1차원 배열 인덱스 계산 및 할당
                            _benchCellManager[benchIndex] = cell;
                            Debug.Log($"Bench Cell assigned to array: {childName} at index [{benchIndex}]");
                            benchIndex++;
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

        public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId, bool isBench = false)
        {
            Cell cell = null;
            
            if (isBench)
            {
                // 벤치에 유닛 스폰
                for (int i = 0; i < benchSize; i++)
                {
                    if (!_benchCellManager[i] || _benchCellManager[i].isOccupied) continue;
                    cell = _benchCellManager[i];
                    break;
                }
                
                if (!cell)
                {
                    Debug.LogWarning("벤치에 빈 셀이 없습니다.");
                    return;
                }
            }
            else
            {
                // 필드에 유닛 스폰
                int adjustedX = xPos - xMin;
                int adjustedY = yPos - yMin;

                if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                    adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
                {
                    cell = _fieldCellManager[adjustedX, adjustedY];
                }
                
                if (cell == null || cell.isOccupied)
                {
                    Debug.LogWarning($"셀이 없거나 이미 점유되어 있습니다: ({xPos}, {yPos})");
                    return;
                }
            }

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
            unitObj.transform.SetParent(cell.transform);
            
            // Assign unit to cell
            cell.isOccupied = true;
            cell.unit = unitObj;
            
            // Initialize unit
            Unit unitComponent = unitObj.GetComponent<Unit>();
            if (unitComponent != null)
            {
                // Set parent based on location
                if (isBench)
                {
                    unitObj.transform.SetParent(benchParent);
                }
                else
                {
                    unitObj.transform.SetParent(fieldParent);
                }
                
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
                Debug.Log($"Spawned {(isEnemy ? "enemy" : "hero")} unit {unitComponent.UnitName} at {(isBench ? "bench" : $"({xPos}, {yPos})")}");
            }
            else
            {
                Debug.LogError($"Failed to get Unit component from spawned object");
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
                EventContext context = new EventContext(hero);
                hero.Invoke(BaseEnums.UnitEventType.OnRoundStart, context);
            }

            foreach (Unit enemy in enemyList)
            {
                EventContext context = new EventContext(enemy);
                enemy.Invoke(BaseEnums.UnitEventType.OnRoundStart, context);
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

        public bool HasAvailableBenchSlot()
        {
            for (int i = 0; i < benchSize; i++)
            {
                Cell cell = _benchCellManager[i];
                if (cell != null && !cell.isOccupied)
                {
                    return true;
                }
            }
            return false;
        }

        public bool PlaceUnitOnBench(Unit unit)
        {
            if (unit == null) return false;
            
            // 첫 번째 빈 셀 찾기
            for (int i = 0; i < benchSize; i++)
            {
                Cell cell = _benchCellManager[i];
                if (cell != null && !cell.isOccupied)
                {
                    // 셀에 유닛 배치
                    cell.isOccupied = true;
                    cell.unit = unit.gameObject;
                    unit.currentCell = cell;
                    
                    // 유닛의 위치를 셀 위치로 설정
                    unit.transform.position = cell.transform.position;
                    unit.transform.SetParent(cell.transform);
                    
                    // 유닛을 활성화
                    unit.gameObject.SetActive(true);
                    
                    Debug.Log($"유닛 {unit.UnitName}이(가) 벤치 셀 ({cell.xPos}, {cell.yPos})에 배치되었습니다.");
                    return true;
                }
            }
            
            Debug.LogWarning("벤치에 빈 셀이 없습니다.");
            return false;
        }
        
        public bool IsBenchCell(Cell cell)
        {
            if (cell == null || _benchCellManager == null) return false;
            
            for (int i = 0; i < benchSize; i++)
            {
                if (_benchCellManager[i] == cell)
                    return true;
            }
            return false;
        }

        public bool AreAllEnemySideCellsEmpty()
        {
            // 적 측 셀들(y = 1, 2, 3)이 모두 비어있는지 확인
            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = 1; y <= 3; y++) // 적 측은 y = 1, 2, 3
                {
                    int adjustedX = x - xMin;
                    int adjustedY = y - yMin;
                    
                    if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                        adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
                    {
                        Cell cell = _fieldCellManager[adjustedX, adjustedY];
                        if (cell != null && cell.isOccupied)
                        {
                            // 셀에 유닛이 있고, 그 유닛이 적인지 확인
                            if (cell.unit != null)
                            {
                                Unit unit = cell.unit.GetComponent<Unit>();
                                if (unit != null && unit.IsEnemy)
                                {
                                    return false; // 적이 있으면 false 반환
                                }
                            }
                        }
                    }
                }
            }
            return true; // 모든 적 측 셀이 비어있음
        }
    }
}
