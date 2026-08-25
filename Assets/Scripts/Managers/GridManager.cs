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
            EnforceLayoutBounds();

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

            // 만들어진 셀에 맞춰 카메라를 잡는다. 준비 단계이므로 대기석까지 보여 준다.
            SetBenchVisible(true);
        }
        
        // 전열/후열 구조가 성립하는 유일한 범위. 진영당 2열 × 4행이다.
        private const int LayoutXMin = -2;
        private const int LayoutXMax = 2;
        private const int LayoutYMin = 1;
        private const int LayoutYMax = 4;
        private const int LayoutBenchSize = 5;

        /// <summary>
        /// 씬에 저장된 범위를 전열/후열 구조에 맞게 되돌린다.
        ///
        /// <see cref="CalculateFieldCellPosition"/>은 |x|=1을 전열, 그 외를 후열로 본다.
        /// 따라서 x가 ±2를 넘으면 <b>여러 x가 같은 좌표로 계산되어 셀이 겹쳐 쌓인다</b>
        /// (실제로 씬에 구 레이아웃 값 xMin -3 / xMax 3이 남아 x=-3과 -2가 포개져 있었다).
        /// 인스펙터 값이 조용히 구조를 깨뜨리지 않도록 여기서 바로잡고 알린다.
        /// </summary>
        private void EnforceLayoutBounds()
        {
            if (xMin == LayoutXMin && xMax == LayoutXMax &&
                yMin == LayoutYMin && yMax == LayoutYMax && benchSize == LayoutBenchSize)
            {
                return;
            }

            Debug.LogWarning(
                $"[GridManager] 전장 범위가 전열/후열 구조와 다릅니다. " +
                $"x[{xMin},{xMax}] y[{yMin},{yMax}] 대기석 {benchSize} → " +
                $"x[{LayoutXMin},{LayoutXMax}] y[{LayoutYMin},{LayoutYMax}] 대기석 {LayoutBenchSize}로 보정합니다.");

            xMin = LayoutXMin;
            xMax = LayoutXMax;
            yMin = LayoutYMin;
            yMax = LayoutYMax;
            benchSize = LayoutBenchSize;
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

                    // 칸 크기는 모두 같다. 정렬 순서만 행에 따라 줘서 앞줄이 뒷줄 위로 그려지게 한다.
                    cellObj.transform.localScale = Vector3.one * CellScaleFor(y);
                    
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
                    cell.ApplyDepth(y - yMin);
                    
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
        
        // ── 배치 상수 ───────────────────────────────────────────────
        //
        // 진영마다 정확히 2열 × 4행이다. 유사 3D 원근(뒤쪽 행 축소·수렴·행간 압축)은 걷어냈다.
        // 원근을 넣으면 뒤 행 간격이 SlotSpacing × 0.82까지 줄어드는데, 칸 스프라이트 자체가
        // 10.16 월드 단위라 간격이 칸 크기보다 작아져 **칸과 스탠딩이 서로 겹쳐 보였다.**
        // 지금은 간격을 항상 칸 크기보다 크게 잡아 격자가 또렷하게 떨어진다.

        /// <summary>같은 진영 두 열 사이의 간격. 칸 크기(10.16)보다 커야 겹치지 않는다.</summary>
        private const float ColumnSpacing = 11.6f;

        /// <summary>한 열 안에서 위아래로 늘어선 4명의 간격.</summary>
        private const float SlotSpacing = 11.6f;

        /// <summary>양 진영 사이(아군 전열 ↔ 적 전열)에 추가로 벌리는 거리.</summary>
        private const float CenterGap = 7f;

        /// <summary>전장 최하단에서 대기석까지 내려가는 거리.</summary>
        private const float BenchOffsetY = 14f;

        /// <summary>
        /// 칸 하나의 좌표. |x| = 1이 전열(중앙 쪽), 2가 후열이며 y는 위에서 아래로 1~4다.
        /// 두 축 모두 등간격이라 진영마다 반듯한 2 × 4 격자가 나온다.
        /// </summary>
        private Vector3 CalculateFieldCellPosition(int x, int y)
        {
            int side = x < 0 ? -1 : 1;                        // 아군 -1, 적 +1
            int columnIndex = Mathf.Abs(x) - 1;               // 전열 0, 후열 1

            float posX = side * (CenterGap * 0.5f + (columnIndex + 0.5f) * ColumnSpacing);

            // y가 작을수록 화면 위. 행 중앙을 원점에 두어 전장이 세로로 가운데 정렬된다.
            float rowCenter = (yMin + yMax) * 0.5f;
            float posY = (rowCenter - y) * SlotSpacing;

            return new Vector3(posX, posY, 0f);
        }

        /// <summary>모든 칸의 크기가 같다. 원근 축소를 쓰지 않는다.</summary>
        private float CellScaleFor(int y) => 1f;

        /// <summary>대기석은 전장 아래에 가로로 늘어놓는다.</summary>
        private Vector3 CalculateBenchCellPosition(int x)
        {
            float rowCenter = (yMin + yMax) * 0.5f;
            float fieldBottom = (rowCenter - yMax) * SlotSpacing;   // 최하단 행의 y 좌표
            return new Vector3(x * ColumnSpacing, fieldBottom - BenchOffsetY, 0f);
        }

        /// <summary>칸 하나의 반지름(월드 단위). 셀 테두리 스프라이트가 10.16이다.</summary>
        private const float CellExtent = 5.08f;

        /// <summary>전장 바깥에 남길 여백(월드 단위).</summary>
        private const float CameraMargin = 2f;

        /// <summary>칸 위로 삐져나오는 체력·마나 바의 높이(월드 단위).</summary>
        private const float BarOverhang = 2.5f;

        /// <summary>
        /// 화면 아래 HUD(준비 페이즈 바)에 가리지 않도록 아래쪽에만 더 주는 여유.
        /// 화면 높이 대비 비율이며, 대기석이 HUD 뒤로 숨지 않을 만큼 잡는다.
        /// </summary>
        private const float CameraBottomHudFraction = 0.16f;

        /// <summary>상단 상태바(생명력·골드·스테이지)에 최상단 행이 가리지 않도록 두는 여유.</summary>
        private const float CameraTopHudFraction = 0.07f;

        /// <summary>대기석이 지금 화면에 나와 있는지.</summary>
        private bool _benchVisible = true;

        /// <summary>이번 프레임에 열 정렬을 다시 해야 하는지.</summary>
        private bool _layoutDirty = true;

        /// <summary>배치가 바뀌었음을 알린다. 실제 정렬은 프레임 끝에 한 번만 돈다.</summary>
        public void RequestFieldLayoutRefresh() => _layoutDirty = true;

        /// <summary>마지막으로 정렬할 때 빈 칸을 보여 주고 있었는지.</summary>
        private bool _lastShowEmpty = true;

        private void LateUpdate()
        {
            // 게임 상태가 바뀌면(준비 ↔ 전투) 빈 칸을 보여 줄지가 달라지므로 그때도 다시 세운다.
            bool showEmpty = ShouldShowEmptyCells();
            if (!_layoutDirty && showEmpty == _lastShowEmpty) return;

            _layoutDirty = false;
            _lastShowEmpty = showEmpty;
            RefreshFieldLayout();
        }

        /// <summary>배치를 만질 수 있는 동안에만 빈 칸(=놓을 자리)을 보여 준다.</summary>
        private static bool ShouldShowEmptyCells()
        {
            return GameManager.Instance == null ||
                   GameManager.Instance.gameState == BaseEnums.GameState.Preparation;
        }

        /// <summary>
        /// 진영마다 두 열을 다시 세운다. <b>빈 칸은 자리를 차지하지 않는다</b> —
        /// 배치된 유닛만 열 중앙에 모이고(세븐나이츠식), 빈 칸은 그 아래에 붙는다.
        /// 전투 중에는 빈 칸을 아예 지워 전장이 화면을 더 크게 쓴다.
        ///
        /// 정렬이 끝나면 카메라도 다시 잡는다. 인원이 줄면 그만큼 화면이 당겨진다.
        /// </summary>
        public void RefreshFieldLayout()
        {
            if (_fieldCellManager == null) return;

            bool showEmpty = ShouldShowEmptyCells();
            _lastShowEmpty = showEmpty;

            for (int columnIndex = 0; columnIndex < 2; columnIndex++)
            {
                LayOutColumn(-1, columnIndex, showEmpty);
                LayOutColumn(1, columnIndex, showEmpty);
            }

            FrameCamera();
        }

        /// <summary>한 진영의 한 열(전열 또는 후열)을 세로 중앙 정렬한다.</summary>
        private void LayOutColumn(int side, int columnIndex, bool showEmpty)
        {
            int x = side * (columnIndex + 1);

            var occupied = new List<Cell>();
            var empty = new List<Cell>();
            for (int y = yMin; y <= yMax; y++)
            {
                Cell cell = GetFieldCell(x, y);
                if (cell == null) continue;
                (cell.isOccupied ? occupied : empty).Add(cell);
            }

            var ordered = new List<Cell>(occupied);
            if (showEmpty) ordered.AddRange(empty);

            float posX = side * (CenterGap * 0.5f + (columnIndex + 0.5f) * ColumnSpacing);
            float half = (ordered.Count - 1) * 0.5f;

            for (int i = 0; i < ordered.Count; i++)
            {
                Cell cell = ordered[i];
                float slot = i - half;   // 위가 음수, 아래가 양수 — yPos와 같은 방향이다.

                cell.transform.position = new Vector3(posX, -slot * SlotSpacing, 0f);
                cell.transform.localScale = Vector3.one;

                // 유닛 오브젝트는 Field 밑에 따로 매달려 있어 셀을 옮겨도 따라오지 않는다.
                // 투사체는 유닛의 트랜스폼을 시작점·도착점으로 쓰므로, 여기서 맞춰 주지 않으면
                // 소환 당시 좌표를 향해 날아가 엉뚱한 허공에서 터진다.
                if (cell.unit != null) cell.unit.transform.position = cell.transform.position;

                cell.DisplaySlot = slot;
                cell.IsLaidOut = true;
                cell.ApplyDepth(i);
                // 카드가 칸을 덮으므로 바닥 타일은 빈 칸에서만 보인다.
                cell.SetGroundPadVisible(!cell.isOccupied);
            }

            if (showEmpty) return;

            foreach (Cell cell in empty)
            {
                cell.IsLaidOut = false;
                cell.SetGroundPadVisible(false);
            }
        }

        /// <summary>필드 칸 하나를 좌표로 찾는다. 범위를 벗어나면 null.</summary>
        private Cell GetFieldCell(int x, int y)
        {
            if (_fieldCellManager == null) return null;

            int adjustedX = x - xMin;
            int adjustedY = y - yMin;
            if (adjustedX < 0 || adjustedX >= _fieldCellManager.GetLength(0)) return null;
            if (adjustedY < 0 || adjustedY >= _fieldCellManager.GetLength(1)) return null;

            return _fieldCellManager[adjustedX, adjustedY];
        }

        /// <summary>
        /// 대기석을 보이거나 숨기고, 그에 맞춰 카메라를 다시 잡는다.
        ///
        /// 전투 중에는 유닛을 대기석으로 옮길 수 없으므로 자리만 차지한다.
        /// 숨긴 만큼 전장이 화면을 더 크게 쓰게 되어 전투가 잘 보인다.
        /// </summary>
        public void SetBenchVisible(bool visible)
        {
            _benchVisible = visible;
            RequestFieldLayoutRefresh();

            if (_benchCellManager != null)
            {
                foreach (Cell cell in _benchCellManager)
                {
                    if (cell != null) cell.gameObject.SetActive(visible);
                }
            }

            FrameCamera();
        }

        /// <summary>
        /// 전장 전체가 화면에 들어오도록 카메라를 맞춘다.
        ///
        /// 배치 상수를 바꿀 때마다 씬의 카메라를 손으로 옮기면 금방 어긋난다.
        /// 실제로 만들어진 셀 좌표에서 경계를 구해 그 중심에 카메라를 두고 배율을 잡으면,
        /// 전열/후열 간격이나 대기석 위치를 바꿔도 프레이밍이 따라온다.
        ///
        /// 대기석이 숨어 있으면 계산에서도 빼므로 전장이 그만큼 확대된다.
        /// </summary>
        private void FrameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic) return;

            bool any = false;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            void Include(Cell cell)
            {
                // 전투 중 지워진 빈 칸은 프레이밍에서 뺀다.
                if (cell == null || !cell.IsLaidOut) return;
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
            if (_benchVisible && _benchCellManager != null)
            {
                foreach (Cell cell in _benchCellManager) Include(cell);
            }
            if (!any) return;

            // 전장은 x = 0을 기준으로 좌우 대칭이다. 한쪽 열이 비어 화면에서 지워져도
            // 프레이밍까지 한쪽으로 쏠리면 안 되므로 가로 경계를 대칭으로 되돌린다.
            float halfWidth = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
            minX = -halfWidth;
            maxX = halfWidth;

            // 셀 중심 좌표를 모았으니 반 칸씩 넓히고 여백을 더한다.
            float pad = CellExtent + CameraMargin;
            minX -= pad; maxX += pad;
            minY -= pad;
            // 체력·마나 바는 칸 위로 조금 삐져나온다. 그만큼만 위를 더 연다.
            // 예전에는 캐릭터 키 전체(13)를 더했는데, 이제 캐릭터가 칸 안에 들어가므로
            // 그대로 두면 전장이 화면 아래쪽으로 쏠린다.
            maxY += BarOverhang + CameraMargin;

            // HUD가 판을 덮지 않도록 위아래로 더 벌린다.
            // 상단 상태바는 늘 떠 있고, 준비 페이즈 바는 전투 중에 사라지므로 그때는 아래 여유가 없어도 된다.
            float span = maxY - minY;
            maxY += span * CameraTopHudFraction;
            if (_benchVisible) minY -= span * CameraBottomHudFraction;

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

            // 전투 중에는 대기석을 쓸 수 없다. 숨기고 그만큼 전장을 확대한다.
            SetBenchVisible(false);

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
            // yPos가 아니라 DisplaySlot을 쓴다. 빈 칸을 지우고 중앙 정렬하면
            // 논리 좌표와 화면상의 거리가 어긋나기 때문이다.
            Vector2 casterPos = new(caster.currentCell.xPos, caster.currentCell.DisplaySlot);

            foreach (Unit unit in enemyCandidates)
            {
                Vector2 targetPos = new Vector2(unit.currentCell.xPos, unit.currentCell.DisplaySlot);
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

            // 다시 편성할 수 있도록 대기석을 되돌린다.
            SetBenchVisible(true);
        }

        /// <summary>
        /// 유닛을 전장에서 완전히 물린다.
        ///
        /// <see cref="Unit.DeactivateUnit"/>만 부르면 <c>isActive</c>만 꺼질 뿐
        /// <see cref="heroList"/>·<see cref="enemyList"/>의 항목과 씬의 오브젝트는 그대로 남는다.
        /// 라운드가 끝날 때마다 아군을 비활성화하고 새로 <see cref="SpawnUnit"/>하므로,
        /// 그대로 두면 <b>같은 캐릭터가 라운드 수만큼 리스트에 쌓인다</b>
        /// (TAB 목록에 세이가 네 번 나오던 원인). 오브젝트도 함께 누적된다.
        /// </summary>
        public void RetireUnit(Unit unit)
        {
            if (unit == null) return;

            if (unit.isActive && unit.currentCell != null)
            {
                unit.DeactivateUnit();
            }

            heroList.Remove(unit);
            enemyList.Remove(unit);

            if (unit != null && unit.gameObject != null)
            {
                Destroy(unit.gameObject);
            }
        }

        /// <summary>파괴됐거나 비어 버린 항목을 리스트에서 걷어낸다.</summary>
        public void PruneUnitLists()
        {
            heroList?.RemoveAll(unit => unit == null);
            enemyList?.RemoveAll(unit => unit == null);
        }

        /// <summary>라운드가 끝나면 적을 전부 물린다. 죽어서 이미 비활성인 개체도 함께 정리한다.</summary>
        public void ClearActiveEnemies()
        {
            foreach (Unit enemy in enemyList.ToList())
            {
                RetireUnit(enemy);
            }
            PruneUnitLists();
        }
    }
}
