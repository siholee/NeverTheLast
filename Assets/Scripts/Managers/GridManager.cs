using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

public class GridManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static GridManager Instance { get; private set; }

    public GameObject cellPrefab;
    public GameObject HeroPrefab;
    public GameObject EnemyPrefab;
    public int xMin = -4;
    public int xMax = 4;
    public int yMin = 1;
    public int yMax = 3;
    public float tileSpacing = 2.1f;

    private Cell[,] cellManager; // 셀 관리 배열
    private List<Hero> heroList;
    private List<Enemy> enemyList;

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

    private void Start()
    {
        heroList = new List<Hero>();
        enemyList = new List<Enemy>();
        CreateGrid();
    }

    private void CreateGrid()
    {
        int rows = yMax - yMin + 1;
        int columns = xMax - xMin + 1;
        cellManager = new Cell[columns, rows];

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
                cellObject.name = $"Cell_{x}_{y}";

                Cell cellComponent = cellObject.GetComponent<Cell>();
                if (cellComponent != null)
                {
                    cellComponent.xPos = x;
                    cellComponent.yPos = y;
                    cellComponent.isOccupied = false; // 초기화
                    cellComponent.portraitRenderer.sprite = null; // 초기화
                }

                // 셀의 위치에 따라 아군과 적 투명 캐릭터 생성
                if (x < 0)
                {
                    GameObject heroObj = Instantiate(HeroPrefab, position, Quaternion.identity);
                    heroObj.transform.SetParent(cellObject.transform);
                    heroObj.transform.localPosition = Vector3.zero;
                    cellComponent.unit = heroObj;
                    Hero hero = heroObj.GetComponent<Hero>();
                    hero.currentCell = cellComponent;
                    hero.isActive = false;
                    heroList.Add(hero);
                }
                else if (x > 0)
                {
                    GameObject enemyObj = Instantiate(EnemyPrefab, position, Quaternion.identity);
                    enemyObj.transform.SetParent(cellObject.transform);
                    enemyObj.transform.localPosition = Vector3.zero;
                    cellComponent.unit = enemyObj;
                    Enemy enemy = enemyObj.GetComponent<Enemy>();
                    enemy.currentCell = cellComponent;
                    enemy.isActive = false;
                    enemyList.Add(enemy);
                }
                else
                {
                    cellComponent.GetComponent<SpriteRenderer>().sprite = null;
                }

                // 배열에 셀 저장
                cellManager[x - xMin, y - yMin] = cellComponent;
            }
        }
    }

    public bool IsCellAvailable(int xPos, int yPos)
    {
        int adjustedX = xPos - xMin;
        int adjustedY = yPos - yMin;

        if (adjustedX >= 0 && adjustedX < cellManager.GetLength(0) &&
            adjustedY >= 0 && adjustedY < cellManager.GetLength(1))
        {
            Cell cell = cellManager[adjustedX, adjustedY];
            return cell != null && !cell.isOccupied;
        }

        return false; // 셀이 없거나 이미 점유됨
    }

    public void SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId)
    {
        int adjustedX = xPos - xMin;
        int adjustedY = yPos - yMin;
        Debug.LogWarning($"adjustedX: {adjustedX}, adjustedY: {adjustedY}");

        if (adjustedX >= 0 && adjustedX < cellManager.GetLength(0) &&
            adjustedY >= 0 && adjustedY < cellManager.GetLength(1))
        {
            Cell cell = cellManager[adjustedX, adjustedY];
            if (cell != null)
            {
                Debug.Log($"Spawning {(isEnemy ? "enemy" : "hero")} {unitId} at Cell ({xPos}, {yPos}).");
                cell.isOccupied = true; // 셀을 점유 상태로 변경
                if (isEnemy)
                {
                    cell.unit.GetComponent<Enemy>().ActivateUnit(isEnemy, unitId);
                }
                else
                {
                    cell.unit.GetComponent<Hero>().ActivateUnit(isEnemy, unitId);
                }
                // Enemy enemyUnit = poolManager.ActivateUnit(false).GetComponent<Enemy>();
                // enemyUnit.currentCell = cell;
                // enemyUnit.InitProcess(false, enemyId);
            }
        }
    }

    public List<Unit> TargetNearestEnemy(Unit caster)
    {
        List<Unit> enemyCandidates = new List<Unit>();

        if (caster is Hero)
        {
            // Assuming GridManager maintains a list of enemies
            foreach (Enemy enemy in enemyList)
            {
                if (enemy.isActive) enemyCandidates.Add(enemy);
            }

        }
        else if (caster is Enemy)
        {
            // Assuming GridManager maintains a list of heroes
            foreach (Hero hero in heroList)
            {
                if (hero.isActive) enemyCandidates.Add(hero);
            }
        }

        List<Unit> nearestUnit = new List<Unit>();
        float minDistance = float.MaxValue;
        Vector2 casterPos = new Vector2(caster.currentCell.xPos, caster.currentCell.yPos);

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
        if (caster is Hero)
        {
            return enemyList.Where(e => e.isActive).Cast<Unit>().ToList();
        }
        else
        {
            return heroList.Where(h => h.isActive).Cast<Unit>().ToList();
        }
    }

    public List<Unit> TargetAllAllies(Unit caster)
    {
        if (caster is Hero)
        {
            return heroList.Where(e => e.isActive).Cast<Unit>().ToList();
        }
        else
        {
            return enemyList.Where(h => h.isActive).Cast<Unit>().ToList();
        }
    }
}
