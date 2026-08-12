using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
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
        public int xMin = -2;
        public int xMax = 2;
        public int yMin = 1; 
        public int yMax = 4;
        
        [Header("Bench Grid Settings")]
        public int benchSize = 5; // 대기 슬롯 개수

        private Cell[,] _fieldCellManager; // Cell management array
        private Cell[] _benchCellManager; // Cell management array (1차원)
        public List<Unit> heroList;
        public List<Unit> enemyList;

        // 그리드 부모 오브젝트들
        public Transform Field;  // 게임 필드 (HMSon 대체)
        public Transform Bench;  // 대기석 (JSPark 대체)
        
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

        // 에디터에서 수동으로 그리드를 재생성할 수 있는 퍼블릭 메서드
        [ContextMenu("Regenerate Grid")]
        public void RegenerateGrid()
        {
            SetGrid();
        }
        
        // 에디터에서 기존 셀들을 정리할 수 있는 퍼블릭 메서드
        public void ClearExistingCells()
        {
            // Field 하위의 기존 셀들 제거
            if (Field != null)
            {
                for (int i = Field.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(Field.GetChild(i).gameObject);
                    }
                    else
                    {
                        Destroy(Field.GetChild(i).gameObject);
                    }
#else
                    Destroy(Field.GetChild(i).gameObject);
#endif
                }
            }
            
            // Bench 하위의 기존 셀들 제거
            if (Bench != null)
            {
                for (int i = Bench.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(Bench.GetChild(i).gameObject);
                    }
                    else
                    {
                        Destroy(Bench.GetChild(i).gameObject);
                    }
#else
                    Destroy(Bench.GetChild(i).gameObject);
#endif
                }
            }
        }

        private void SetGrid()
        {
            // _fieldCellManager 배열 초기화
            int rows = yMax - yMin + 1;
            int columns = xMax - xMin + 1;
            _fieldCellManager = new Cell[columns, rows];
            
            // _benchCellManager 배열 초기화
            _benchCellManager = new Cell[benchSize];
            
            // 기존에 하드코딩된 셀들이 있다면 먼저 정리
            ClearExistingCells();
            
            // 새로운 필드 셀들 생성
            CreateFieldCells();
            
            // 새로운 벤치 셀들 생성
            CreateBenchCells();

            // 만들어진 셀에 맞춰 카메라를 잡는다.
            FrameCamera();
        }
        
        private void CreateFieldCells()
        {
            if (cellPrefab == null)
            {
                Debug.LogError("Cell Prefab이 할당되지 않았습니다!");
                return;
            }
            
            if (Field == null)
            {
                Debug.LogError("Field가 할당되지 않았습니다!");
                return;
            }
            
            // 필드 셀들 생성: 아군 x=-2~-1, 적군 x=1~2, y=1~4
            for (int x = xMin; x <= xMax; x++)
            {
                if (x == 0) continue;

                for (int y = yMin; y <= yMax; y++)
                {
                    // 셀 프리팹에서 인스턴스 생성
                    GameObject cellObj = Instantiate(cellPrefab, Field);
                    
                    // 셀 이름 설정
                    cellObj.name = $"Cell_{x}_{y}";
                    
                    // 위치 계산 및 설정
                    Vector3 cellPosition = CalculateFieldCellPosition(x, y);
                    cellObj.transform.position = cellPosition;
                    
                    // Cell 컴포넌트 가져오기 또는 추가
                    Cell cell = cellObj.GetComponent<Cell>();
                    if (cell == null)
                    {
                        cell = cellObj.AddComponent<Cell>();
                    }
                    
                    // Cell 속성 설정
                    cell.xPos = x;
                    cell.yPos = y;
                    cell.isOccupied = false;
                    cell.reservedTime = 0f;
                    
                    // UI GameObject 기본 비활성화
                    if (cell.uiObject != null)
                    {
                        cell.uiObject.SetActive(false);
                    }
                    
                    // 2차원 배열 인덱스 계산 및 할당
                    int adjustedX = x - xMin;
                    int adjustedY = y - yMin;
                    
                    // 범위 체크 및 배열에 등록
                    if (adjustedX >= 0 && adjustedX < _fieldCellManager.GetLength(0) &&
                        adjustedY >= 0 && adjustedY < _fieldCellManager.GetLength(1))
                    {
                        _fieldCellManager[adjustedX, adjustedY] = cell;
                        // Field Cell created: Cell_{x}_{y} at position {cellPosition}
                    }
                    else
                    {
                        Debug.LogWarning($"Field Cell coordinates out of range: Cell_{x}_{y}");
                    }
                }
            }
        }
        
        private void CreateBenchCells()
        {
            if (cellPrefab == null)
            {
                Debug.LogError("Cell Prefab이 할당되지 않았습니다!");
                return;
            }
            
            if (Bench == null)
            {
                Debug.LogError("Bench가 할당되지 않았습니다!");
                return;
            }
            
            // 벤치 셀들 생성 (x: -2~2, y: 0)
            int benchIndex = 0;
            for (int x = xMin; x <= xMax; x++)
            {
                if (benchIndex >= benchSize) break; // benchSize 제한 확인
                
                // 셀 프리팹에서 인스턴스 생성
                GameObject cellObj = Instantiate(cellPrefab, Bench);
                
                // 셀 이름 설정
                cellObj.name = $"Cell_{x}_0";
                
                // 위치 계산 및 설정
                Vector3 cellPosition = CalculateBenchCellPosition(x);
                cellObj.transform.position = cellPosition;
                
                // Cell 컴포넌트 가져오기 또는 추가
                Cell cell = cellObj.GetComponent<Cell>();
                if (cell == null)
                {
                    cell = cellObj.AddComponent<Cell>();
                }
                
                // Cell 속성 설정
                cell.xPos = x;
                cell.yPos = 0;
                cell.isOccupied = false;
                cell.reservedTime = 0f;
                
                // UI GameObject 기본 비활성화
                if (cell.uiObject != null)
                {
                    cell.uiObject.SetActive(false);
                }
                
                // 1차원 배열에 등록
                _benchCellManager[benchIndex] = cell;
                // Bench Cell created: Cell_{x}_0 at position {cellPosition}
                benchIndex++;
            }
        }
        
        // ── 배치 상수 (세븐나이츠식 전열/후열) ──────────────────────
        // 전열과 후열은 크게 벌리고, 같은 열의 4명은 촘촘히 세로로 세운다.
        // 진영 사이는 더 크게 벌려 전선이 마주 보는 형태가 되게 한다.

        /// <summary>양 진영 전열 사이의 간격(전열끼리의 거리).</summary>
        private const float FrontLineGap = 26f;

        /// <summary>같은 진영에서 전열과 후열 사이의 간격.</summary>
        private const float RowDepthGap = 16f;

        /// <summary>한 열 안에서 위아래로 늘어선 4명의 간격.</summary>
        private const float SlotSpacing = 10f;

        /// <summary>전장 최하단에서 대기석까지 내려가는 거리.</summary>
        private const float BenchOffsetY = 14f;

        private Vector3 CalculateFieldCellPosition(int x, int y)
        {
            // x = -2(아군 후열) / -1(아군 전열) / 1(적 전열) / 2(적 후열)
            //   전열은 중앙에서 FrontLineGap/2 만큼, 후열은 거기서 RowDepthGap 만큼 더 뒤로 뺀다.
            int side = x < 0 ? -1 : 1;             // 아군 -1, 적 +1
            bool isFront = Mathf.Abs(x) == 1;
            float depth = FrontLineGap * 0.5f + (isFront ? 0f : RowDepthGap);
            float posX = side * depth;

            // y = 1~4를 세로로 중앙 정렬한다. (1.5, 0.5, -0.5, -1.5) × 간격
            float center = (yMin + yMax) * 0.5f;
            float posY = (center - y) * SlotSpacing;

            return new Vector3(posX, posY, 0f);
        }
        
        /// <summary>대기석은 전장 아래에 가로로 늘어놓는다.</summary>
        private Vector3 CalculateBenchCellPosition(int x)
        {
            float fieldBottom = (yMin + yMax) * 0.5f - yMax;      // 전장 최하단 슬롯의 y 계수
            float posY = fieldBottom * SlotSpacing - BenchOffsetY;
            return new Vector3(x * SlotSpacing, posY, 0f);
        }

        /// <summary>칸 하나의 반지름(월드 단위). 셀 테두리 스프라이트가 10.16이다.</summary>
        private const float CellExtent = 5.08f;

        /// <summary>전장 바깥에 남길 여백(월드 단위).</summary>
        private const float CameraMargin = 2f;

        /// <summary>
        /// 화면 아래 HUD(준비 페이즈 바)에 가리지 않도록 아래쪽에만 더 주는 여유.
        /// 화면 높이 대비 비율이며, 대기석이 HUD 뒤로 숨지 않을 만큼 잡는다.
        /// </summary>
        private const float CameraBottomHudFraction = 0.16f;

        /// <summary>
        /// 전장 전체가 화면에 들어오도록 카메라를 맞춘다.
        ///
        /// 배치 상수를 바꿀 때마다 씬의 카메라를 손으로 옮기면 금방 어긋난다.
        /// 실제로 만들어진 셀 좌표에서 경계를 구해 그 중심에 카메라를 두고 배율을 잡으면,
        /// 전열/후열 간격이나 대기석 위치를 바꿔도 프레이밍이 따라온다.
        /// </summary>
        private void FrameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic) return;

            bool any = false;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            void Include(Cell cell)
            {
                if (cell == null) return;
                Vector3 position = cell.transform.position;
                if (!any)
                {
                    minX = maxX = position.x;
                    minY = maxY = position.y;
                    any = true;
                    return;
                }
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            if (_fieldCellManager != null) foreach (Cell cell in _fieldCellManager) Include(cell);
            if (_benchCellManager != null) foreach (Cell cell in _benchCellManager) Include(cell);
            if (!any) return;

            // 셀 중심 좌표를 모았으니 반 칸씩 넓히고 여백을 더한다.
            float pad = CellExtent + CameraMargin;
            minX -= pad; maxX += pad;
            minY -= pad; maxY += pad;

            // 아래쪽 HUD가 대기석을 덮지 않도록 아래로만 더 벌린다.
            minY -= (maxY - minY) * CameraBottomHudFraction;

            float width = maxX - minX;
            float height = maxY - minY;
            float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;

            camera.orthographicSize = Mathf.Max(height * 0.5f, width * 0.5f / aspect);
            camera.transform.position = new Vector3(
                (minX + maxX) * 0.5f, (minY + maxY) * 0.5f, camera.transform.position.z);
        }

        public bool IsCellAvailable(int xPos, int yPos)
        {
            if (!IsValidFieldPosition(xPos, yPos)) return false;

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

        public bool IsValidFieldPosition(int xPos, int yPos)
        {
            return xPos >= xMin && xPos <= xMax && xPos != 0 && yPos >= yMin && yPos <= yMax;
        }

        public bool IsAllyFieldPosition(int xPos, int yPos)
        {
            return IsValidFieldPosition(xPos, yPos) && xPos < 0;
        }

        public bool IsEnemyFieldPosition(int xPos, int yPos)
        {
            return IsValidFieldPosition(xPos, yPos) && xPos > 0;
        }

        public int GetFrontColumn(bool isEnemy)
        {
            return isEnemy ? 1 : -1;
        }

        public int GetRearColumn(bool isEnemy)
        {
            return isEnemy ? 2 : -2;
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
                if ((isEnemy && !IsEnemyFieldPosition(xPos, yPos)) || (!isEnemy && !IsAllyFieldPosition(xPos, yPos)))
                {
                    Debug.LogWarning($"진영에 맞지 않는 셀입니다: {(isEnemy ? "적" : "아군")} -> ({xPos}, {yPos})");
                    return;
                }

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
                    // 벤치 유닛은 Bench를 부모로 설정
                    unitObj.transform.SetParent(Bench);
                }
                else
                {
                    // 필드 유닛은 Field를 부모로 설정
                    unitObj.transform.SetParent(Field);
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
                UnlockStarterIfJoinedDuringRun(isEnemy, unitId);
                // Debug.Log($"Spawned {(isEnemy ? "enemy" : "hero")} unit {unitComponent.UnitName} at {(isBench ? "bench" : $"({xPos}, {yPos})")}");
            }
            else
            {
                Debug.LogError($"Failed to get Unit component from spawned object");
            }
        }

        private static void UnlockStarterIfJoinedDuringRun(bool isEnemy, int unitId)
        {
            if (isEnemy || unitId <= 0 || GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentMode != BaseClasses.BaseEnums.GameMode.Training) return;

            bool selectedAtStart = CharacterSelectionManager.Instance?.Lineup
                .Any(entry => entry.UnitId == unitId) ?? false;
            if (selectedAtStart) return;

            SaveSystem.AddStarterUnlock(unitId);
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
            Debug.Log("[GridManager] OnRoundStart 호출됨");
            
            Debug.Log($"[GridManager] 아군 수: {heroList.Count}");
            foreach (Unit hero in heroList)
            {
                if (hero.isActive)
                {
                    Debug.Log($"[GridManager] 아군 {hero.UnitName}에게 OnRoundStart 이벤트 발송");
                    EventContext context = new EventContext(hero);
                    hero.Invoke(BaseEnums.UnitEventType.OnRoundStart, context);
                }
            }

            Debug.Log($"[GridManager] 적군 수: {enemyList.Count}");
            foreach (Unit enemy in enemyList)
            {
                if (enemy.isActive)
                {
                    Debug.Log($"[GridManager] 적군 {enemy.UnitName}에게 OnRoundStart 이벤트 발송");
                    EventContext context = new EventContext(enemy);
                    enemy.Invoke(BaseEnums.UnitEventType.OnRoundStart, context);
                }
            }
        }

        public List<Unit> TargetNearestEnemy(Unit caster)
        {
            List<Unit> enemyCandidates = new List<Unit>();

            if (!caster.IsEnemy)
            {
                foreach (Unit enemy in enemyList)
                {
                    if (enemy.isActive && !enemy.IsUntargetable && enemy.currentCell.yPos > 0) enemyCandidates.Add(enemy);
                }
            }
            else
            {
                foreach (Unit hero in heroList)
                {
                    if (hero.isActive && !hero.IsUntargetable && hero.currentCell.yPos > 0) enemyCandidates.Add(hero);
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
                return enemyList.Where(e => e.isActive && !e.IsUntargetable && e.currentCell.yPos > 0).ToList();
            }
            else
            {
                return heroList.Where(h => h.isActive && !h.IsUntargetable && h.currentCell.yPos > 0).ToList();
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
                    
                    // 유닛의 부모를 벤치로 설정
                    unit.transform.SetParent(Bench);
                    
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
            // 적 측 셀이 모두 비어있는지 확인
            for (int x = 1; x <= 2; x++)
            {
                for (int y = yMin; y <= yMax; y++)
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

        public void OnRoundEnd()
        {
            foreach (Unit unit in heroList.Concat(enemyList).Where(unit => unit != null && unit.isActive).ToList())
            {
                unit.Invoke(BaseEnums.UnitEventType.OnRoundEnd, new EventContext(unit));
            }
        }

        public void ClearActiveEnemies()
        {
            foreach (Unit enemy in enemyList.ToList())
            {
                if (enemy != null && enemy.isActive && enemy.IsEnemy)
                {
                    enemy.DeactivateUnit();
                }
            }
        }
    }
}
