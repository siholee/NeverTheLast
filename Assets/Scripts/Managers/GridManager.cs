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
        public int yMin = 1;
        public int yMax = 3;
        public float tileSpacing = 2.1f;

        private Cell[,] _cellManager; // Cell management array
        public List<Unit> heroList;
        public List<Unit> enemyList;
        
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
                    
                    // Add BoxCollider2D
                    BoxCollider2D boxCollider = cellObject.GetComponent<BoxCollider2D>();
                    if (boxCollider == null)
                    {
                        boxCollider = cellObject.AddComponent<BoxCollider2D>();
                        boxCollider.size = new Vector2(2.0f, 2.0f); // Adjust to cell size
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

            return false; // Cell doesn't exist or is occupied
        }

        public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;

            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
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
            
            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
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
                    if (enemy.isActive) enemyCandidates.Add(enemy);
                }
            }
            else
            {
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
        
        // Get unit at specific position
        public Unit GetUnitAtPosition(int xPos, int yPos)
        {
            int adjustedX = xPos - xMin;
            int adjustedY = yPos - yMin;
            
            if (adjustedX >= 0 && adjustedX < _cellManager.GetLength(0) &&
                adjustedY >= 0 && adjustedY < _cellManager.GetLength(1))
            {
                Cell cell = _cellManager[adjustedX, adjustedY];
                if (cell != null && cell.isOccupied && cell.unit != null)
                {
                    return cell.unit.GetComponent<Unit>();
                }
            }
            
            return null;
        }
    }
}