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
                // 셀과 유닛은 Game 씬 소유다. 메뉴로 나갈 때 함께 정리한다.
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
            // 전장 카드의 클릭 · 드래그 · 호버는 칸이 아니라 이 한 곳에서 판정한다.
            FieldPointerInput.Ensure();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        /// <summary>
        /// 같은 진영 두 열 사이의 간격. 카드(8.94) 사이에 <b>소환수 소형 카드가 들어갈 틈</b>을 남긴다.
        ///
        /// 예전(11.6)에는 틈이 2.7뿐이라 소환수 카드를 소환자 카드 위에 겹쳐 세웠고,
        /// 5인 편성에서 라이트의 소환수가 라이트의 초상화·이름을 가렸다(QA).
        /// 이제 소환수는 소환자 카드의 바깥쪽 틈에 선다(<see cref="Entities.View.SummonCardView"/>).
        /// </summary>
        private const float ColumnSpacing = 13.2f;

        /// <summary>
        /// 한 열 안에서 위아래로 늘어선 4명의 간격.
        ///
        /// 카드 아래로 체력 · 행동 게이지 · 상태 아이콘이 카드 한 변의 32%만큼 붙고, 위로 궁극기
        /// 게이지가 5% 걸친다. 카드 한 장의 세로 영역은 12.2다. 예전 11.6은 그보다 작아
        /// 4명이 선 열에서 윗사람의 상태 아이콘이 아랫사람의 게이지를 덮었다.
        /// </summary>
        private const float SlotSpacing = 12.8f;

        /// <summary>양 진영 사이(아군 전열 ↔ 적 전열)에 추가로 벌리는 거리.</summary>
        private const float CenterGap = 7f;

        /// <summary>대기석 칸 사이 간격. 대기석 카드는 전투 HUD가 없어 전장보다 촘촘해도 된다.</summary>
        private const float BenchSpacing = 11.6f;

        /// <summary>전장 최하단에서 대기석까지 내려가는 거리. 최하단 카드의 상태 아이콘 줄을 비켜야 한다.</summary>
        private const float BenchOffsetY = 14.5f;

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
            return new Vector3(x * BenchSpacing, fieldBottom - BenchOffsetY, 0f);
        }

        /// <summary>칸 하나의 반지름(월드 단위). 셀 테두리 스프라이트가 10.16이다.</summary>
        private const float CellExtent = 5.08f;

        /// <summary>전장 바깥에 남길 여백(월드 단위).</summary>
        private const float CameraMargin = 1.2f;

        // ── HUD가 차지하는 화면 영역(1920×1080 기준 px) ─────────────
        // 예전에는 "전장 높이의 7% · 16%"처럼 전장 크기에 비례해 여유를 줬다. HUD는 전장과 무관하게
        // 픽셀로 고정된 크기라, 인원이 늘어 전장이 커질수록 계산이 어긋났다. 5대5에서 맨 아래 카드의
        // 체력·상태 줄이 화면 밖으로, 맨 위 적 카드가 상단 체력 띠 아래로 들어간 원인이다.
        // 이제 HUD를 픽셀로 비워 두고 남은 영역에 전장을 맞춘다.

        /// <summary>상단 — 자원 띠(60) · 우상단 바(76) · 엘리트 체력 띠(112) 아래까지.</summary>
        private const float TopHudPixels = 172f;

        /// <summary>하단 — 전투 중에는 가장자리 여백만.</summary>
        private const float BottomHudPixelsBattle = 28f;

        /// <summary>하단 — 준비 페이즈 바(<see cref="UI.Screens.PreparationScreen"/>)와 그 위 여백.</summary>
        private static float BottomHudPixelsPreparation =>
            UI.Screens.PreparationScreen.PanelBottom + UI.Screens.PreparationScreen.PanelHeight + 14f;

        /// <summary>
        /// 좌우 — 228px 행동 서열과 12px 안전 간격을 확보한다.
        /// 4:3에서는 기존 160px 예약으로 아군 후열과 행동 카드 사이가 20px 남짓까지 붙고,
        /// 반대편 대형 적도 화면 끝에서 잘렸으므로 실제 HUD 폭을 기준으로 잡는다.
        /// </summary>
        private const float SideHudPixels = 240f;

        /// <summary>소환수 소형 카드가 소환자 카드 바깥으로 걸치는 폭(월드 단위).</summary>
        private static float SummonOverhang => Cell.CardSize * Entities.View.SummonCardView.ScaleRatio + 0.4f;

        /// <summary>대기석이 지금 화면에 나와 있는지.</summary>
        private bool _benchVisible = true;

        /// <summary>이번 프레임에 열 정렬을 다시 해야 하는지.</summary>
        private bool _layoutDirty = true;

        /// <summary>배치가 바뀌었음을 알린다. 실제 정렬은 프레임 끝에 한 번만 돈다.</summary>
        public void RequestFieldLayoutRefresh() => _layoutDirty = true;

        /// <summary>마지막으로 정렬할 때 빈 칸을 보여 주고 있었는지.</summary>
        private bool _lastShowEmpty = true;

        /// <summary>마지막으로 카메라를 맞춘 화면 크기. 창 크기를 바꾸면 HUD 비율이 달라져 다시 맞춘다.</summary>
        private Vector2Int _framedScreen;
        private float _framedHudScale = 1f;
        private BaseEnums.GameState _lastLayoutState = (BaseEnums.GameState)(-1);

        // 전투 중 사망으로 점유 칸이 줄어도 카메라가 매번 당겨지지 않도록 시작 경계를 고정한다.
        private bool _battleBoundsValid;
        private float _battleMinX, _battleMaxX, _battleMinY, _battleMaxY;
        private float _battleFeatureScale = 1f;

        private void LateUpdate()
        {
            // 게임 상태가 바뀌면(준비 ↔ 전투) 빈 칸을 보여 줄지가 달라지므로 그때도 다시 세운다.
            bool showEmpty = ShouldShowEmptyCells();
            BaseEnums.GameState state = GameManager.Instance != null
                ? GameManager.Instance.gameState
                : BaseEnums.GameState.Preparation;
            if (state != _lastLayoutState)
            {
                _lastLayoutState = state;
                _battleBoundsValid = false;
                _layoutDirty = true;
            }
            var screen = new Vector2Int(Screen.width, Screen.height);
            float hudScale = UI.Theme.UITheme.HudScale;
            if (screen != _framedScreen || !Mathf.Approximately(hudScale, _framedHudScale))
            {
                _framedScreen = screen;
                _framedHudScale = hudScale;
                _layoutDirty = true;
            }
            if (!_layoutDirty && showEmpty == _lastShowEmpty) return;

            _layoutDirty = false;
            _lastShowEmpty = showEmpty;
            RefreshFieldLayout();
        }

        /// <summary>배치를 만질 수 있는 동안에만 빈 칸(=놓을 자리)을 보여 준다.</summary>
        private static bool ShouldShowEmptyCells()
        {
            return Cell.PlacementModeActive &&
                   (GameManager.Instance == null ||
                    GameManager.Instance.gameState == BaseEnums.GameState.Preparation);
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
            ApplyBenchVisibility(_benchVisible && showEmpty);

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
                if (!cell.gameObject.activeSelf) cell.gameObject.SetActive(true);
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
                // 보이지 않는 빈 칸의 Collider2D가 모바일 드롭 대상으로 남지 않게 함께 끈다.
                cell.gameObject.SetActive(false);
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

            ApplyBenchVisibility(visible && ShouldShowEmptyCells());

            FrameCamera();
        }

        private void ApplyBenchVisibility(bool visible)
        {
            if (_benchCellManager == null) return;
            foreach (Cell cell in _benchCellManager)
                if (cell != null) cell.gameObject.SetActive(visible);
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

            // 배경은 카메라에 붙어 따라다닌다. 프레이밍을 잡는 자리에서 함께 세운다.
            Effects.BattlefieldBackdrop.Ensure();

            bool any = false;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
            bool includeSlots = ShouldShowEmptyCells();
            bool inBattle = GameManager.Instance != null &&
                            GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress;
            bool hasAlly = false, hasEnemy = false;

            void Include(Cell cell)
            {
                // 전투 중 지워진 빈 칸은 프레이밍에서 뺀다.
                if (cell == null || !cell.IsLaidOut || (!includeSlots && !cell.isOccupied)) return;
                Vector3 position = cell.transform.position;
                if (cell.isOccupied)
                {
                    hasAlly |= cell.xPos < 0;
                    hasEnemy |= cell.xPos > 0;
                }
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

            _largestFeatureScale = 1f;
            if (inBattle && _battleBoundsValid)
            {
                any = true;
                minX = _battleMinX; maxX = _battleMaxX;
                minY = _battleMinY; maxY = _battleMaxY;
                _largestFeatureScale = _battleFeatureScale;
            }
            else if (_fieldCellManager != null)
            {
                foreach (Cell cell in _fieldCellManager)
                {
                    Include(cell);
                    if (cell != null && cell.IsLaidOut && cell.isOccupied)
                        _largestFeatureScale = Mathf.Max(_largestFeatureScale, cell.FeatureScale);
                }
            }
            if (_benchVisible && includeSlots && _benchCellManager != null)
            {
                foreach (Cell cell in _benchCellManager) Include(cell);
            }
            if (!any) return;

            if (inBattle && !_battleBoundsValid && hasAlly && hasEnemy)
            {
                _battleMinX = minX; _battleMaxX = maxX;
                _battleMinY = minY; _battleMaxY = maxY;
                _battleFeatureScale = _largestFeatureScale;
                _battleBoundsValid = true;
            }

            // 전투/편성 화면은 양 진영 축을 고정한다. 준비 기본 화면은 실제 아군만 가운데로 당겨
            // 빈 적 진영 때문에 캐릭터가 절반 크기로 줄지 않게 한다.
            float cameraX;
            if (inBattle || includeSlots)
            {
                float halfWidth = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
                minX = -halfWidth;
                maxX = halfWidth;
                cameraX = 0f;
            }
            else
            {
                cameraX = (minX + maxX) * 0.5f;
            }
            minX -= CellExtent + SummonOverhang + CameraMargin;
            maxX += CellExtent + SummonOverhang + CameraMargin;

            // 셀 중심 좌표를 모았으니 카드가 실제로 덮는 세로 영역으로 넓힌다.
            // 위로는 궁극기 게이지가 걸치고, 아래로는 체력 · 행동 · 상태 아이콘 줄이 붙는다.
            // 보스 단독 편성은 카드가 커지므로(FeatureScale) 가장 큰 카드 기준으로 잰다.
            float card = Cell.CardSize * _largestFeatureScale;
            maxY += card * 0.5f + Entities.View.UnitCardView.HudAbove(card) + CameraMargin;
            minY -= card * 0.5f + Entities.View.UnitCardView.HudBelow(card) + CameraMargin;

            // HUD가 덮는 픽셀을 화면 비율로 바꾼다. CanvasScaler(0.5 매칭)와 같은 식이어야
            // 해상도·화면비가 달라도 HUD 가장자리와 전장 가장자리가 맞는다.
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            float canvasScale = Mathf.Sqrt(screenWidth / UI.Theme.UITheme.ReferenceResolution.x *
                                           (screenHeight / UI.Theme.UITheme.ReferenceResolution.y));
            // HUD 크기 설정만큼 HUD가 커지므로 비워 둘 자리도 같이 커진다.
            canvasScale *= UI.Theme.UITheme.HudScale;
            float top = TopHudPixels * canvasScale / screenHeight;
            bool preparation = GameManager.Instance != null &&
                               GameManager.Instance.gameState == BaseEnums.GameState.Preparation;
            float bottom = (preparation ? BottomHudPixelsPreparation : BottomHudPixelsBattle) * canvasScale / screenHeight;
            float side = SideHudPixels * canvasScale / screenWidth;

            float usableHeight = Mathf.Max(0.3f, 1f - top - bottom);
            float usableWidth = Mathf.Max(0.3f, 1f - side * 2f);
            float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;

            float viewHeight = Mathf.Max((maxY - minY) / usableHeight, (maxX - minX) / usableWidth / aspect);
            camera.orthographicSize = viewHeight * 0.5f;

            // 전장을 HUD 사이 빈 영역의 가운데에 둔다. 화면 아래 bottom 비율만큼 비우고,
            // 남은 영역에서 전장이 세로 가운데에 오도록 카메라 중심을 잡는다.
            float freeCenter = (bottom + (1f - top)) * 0.5f;          // 빈 영역 중심(화면 비율)
            float contentCenter = (minY + maxY) * 0.5f;
            float cameraY = contentCenter + (0.5f - freeCenter) * viewHeight;
            camera.transform.position = new Vector3(cameraX, cameraY, camera.transform.position.z);
        }

        /// <summary>지금 전장에 선 칸 중 가장 큰 카드 배율. 보스 단독 편성에서 1.5가 된다.</summary>
        private float _largestFeatureScale = 1f;

        /// <summary>
        /// 한 진영이 화면에서 차지하는 영역(월드 좌표). 시전자와 떨어진 지점에서
        /// 무언가를 터뜨리거나 쏠 때(허공의 차원문 등) 그 진영 안쪽 좌표를 고르는 데 쓴다.
        ///
        /// 전투 중 비어서 지워진 칸(<see cref="Cell.IsLaidOut"/> == false)은 화면에 없으므로 제외한다.
        /// </summary>
        /// <param name="side">아군 -1, 적 +1. 칸의 x 부호와 같다.</param>
        public bool TryGetSideBounds(int side, out Bounds bounds)
        {
            bounds = new Bounds();
            if (_fieldCellManager == null) return false;

            bool any = false;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            foreach (Cell cell in _fieldCellManager)
            {
                if (cell == null || !cell.IsLaidOut) continue;
                if ((cell.xPos < 0 ? -1 : 1) != (side < 0 ? -1 : 1)) continue;

                Vector3 position = cell.transform.position;
                if (!any)
                {
                    minX = maxX = position.x;
                    minY = maxY = position.y;
                    any = true;
                    continue;
                }
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            if (!any) return false;

            // 모은 것은 칸의 중심 좌표다. 반 칸씩 넓혀 실제로 칸이 덮는 영역으로 만든다.
            minX -= CellExtent; maxX += CellExtent;
            minY -= CellExtent; maxY += CellExtent;

            bounds = new Bounds(
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                new Vector3(maxX - minX, maxY - minY, 0f));
            return true;
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

        /// <summary>유닛을 스폰하고 그 인스턴스를 돌려준다. 자리를 못 잡으면 null이다.</summary>
        public Unit SpawnUnit(int xPos, int yPos, bool isEnemy, int unitId, bool isBench = false)
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
                    return null;
                }
            }
            else
            {
                // 필드에 유닛 스폰
                if ((isEnemy && !IsEnemyFieldPosition(xPos, yPos)) || (!isEnemy && !IsAllyFieldPosition(xPos, yPos)))
                {
                    Debug.LogWarning($"진영에 맞지 않는 셀입니다: {(isEnemy ? "적" : "아군")} -> ({xPos}, {yPos})");
                    return null;
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
                    return null;
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
            // 기본 준비 화면에서는 빈 셀 GameObject가 꺼져 있을 수 있다. 그 아래에 잠깐이라도
            // 붙이면 새 유닛이 OnDisable/OnEnable을 왕복하므로, 실제 수명 부모에 바로 붙인다.
            unitObj.transform.SetParent(isBench ? Bench : Field);
            
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
                // Debug.Log($"Spawned {(isEnemy ? "enemy" : "hero")} unit {unitComponent.UnitName} at {(isBench ? "bench" : $"({xPos}, {yPos})")}");
                return unitComponent;
            }

            Debug.LogError($"Failed to get Unit component from spawned object");
            return null;
        }

        // 예전에는 여기서 "런 도중 합류한 아군"을 스타팅으로 해금했다. 없앤 이유는 둘이다.
        //
        //   1. <b>합류 판정이 틀렸다.</b> "처음부터 편성됐는가"를 CharacterSelectionManager.Lineup으로
        //      물었는데, 세이브를 불러오면 그 편성 기록이 비어 있다. 그래서 불러온 런에서는
        //      전투가 끝나 아군 필드를 복원할 때마다(GameManager.RestoreAllyFieldState)
        //      <b>파티 전원이 영구 해금</b>됐다. 완주 해금이 통째로 무의미해지는 구멍이었다.
        //   2. <b>필요가 없다.</b> 영입 사건의 해금은 GameManager.GrantRecruitUnlock이
        //      사건이 내민 전원에게 확정으로 준다. 만났다는 사실이 조건이므로 스폰을 볼 이유가 없다.
        //
        // 해금 경로는 이제 셋뿐이다 — 영입 사건 · 계정 첫 완주(라부아지에) ·
        // 서포트로 완주(RunManager.GrantSupportStarterUnlocks).
        
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
            // 목록 순서와 무관하게 아군 개전 오라보다 수신기를 먼저 연다.
            foreach (Unit unit in heroList.Concat(enemyList).Where(unit => unit != null && unit.isActive && !unit.IsBench))
                unit.PrepareRoundResources();
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
                return enemyList.Where(e => e.IsOnField && !e.IsUntargetable).ToList();
            }
            else
            {
                return heroList.Where(h => h.IsOnField && !h.IsUntargetable).ToList();
            }
        }

        public List<Unit> TargetAllAllies(Unit caster)
        {
            if (!caster.IsEnemy)
            {
                return heroList.Where(e => e.IsOnField).ToList();
            }
            else
            {
                return enemyList.Where(h => h.IsOnField).ToList();
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

        /// <summary>
        /// 아군을 하나 더 받을 자리가 있는가. 대기석을 먼저 보고, 없으면 아군 필드의 빈 칸을 본다.
        ///
        /// 영입 사건이 <b>선택지를 보여 주기 전에</b> 물어야 하는 질문이다.
        /// 합류를 고른 뒤에야 자리가 없다는 걸 알면 플레이어는 아무 일도 일어나지 않았다고 읽는다.
        /// </summary>
        public bool HasAvailableAllySlot()
        {
            if (HasAvailableBenchSlot()) return true;

            for (int x = GetRearColumn(false); x <= GetFrontColumn(false); x++)
            {
                if (x == 0) continue;
                for (int y = yMin; y <= yMax; y++)
                {
                    if (IsCellAvailable(x, y)) return true;
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
                    // 빈 벤치 셀은 숨겨져 있을 수 있으므로 셀 자식으로 잠깐 붙이지 않는다.
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

        /// <summary>
        /// 유닛을 <b>같은 오브젝트 그대로</b> 다른 칸으로 옮긴다. 대상 칸이 차 있으면 두 유닛을 맞바꾼다.
        ///
        /// 예전 배치 이동은 원래 유닛을 끄고 같은 ID로 새로 스폰했다. 스폰은 데이터 파일에서 유닛을
        /// 처음부터 다시 만들므로 레벨 · 훈련 · 장비 · 현재 체력이 모두 초기값으로 돌아갔다 —
        /// 전열에서 후열로 옮기면 체력이 줄어 보이던 원인이다. 전열/후열 규칙은 체력을 건드리지 않는다.
        /// </summary>
        public bool RelocateUnit(Unit unit, Cell target)
        {
            if (unit == null || target == null) return false;
            Cell source = unit.currentCell;
            if (source == null || source == target) return false;

            Unit other = target.isOccupied && target.unit != null ? target.unit.GetComponent<Unit>() : null;
            Sprite unitPortrait = source.portraitRenderer != null ? source.portraitRenderer.sprite : null;
            Sprite otherPortrait = target.portraitRenderer != null ? target.portraitRenderer.sprite : null;

            Vacate(source);
            Vacate(target);

            Occupy(target, unit, unitPortrait);
            if (other != null) Occupy(source, other, otherPortrait);

            RequestFieldLayoutRefresh();
            return true;
        }

        private static void Vacate(Cell cell)
        {
            cell.isOccupied = false;
            cell.unit = null;
            cell.SetPortrait(null);
            cell.SetOccupiedUnit(null);
        }

        private void Occupy(Cell cell, Unit unit, Sprite portrait)
        {
            unit.transform.SetParent(IsBenchCell(cell) ? Bench : Field);
            unit.transform.position = cell.transform.position;

            cell.isOccupied = true;
            cell.unit = unit.gameObject;
            unit.currentCell = cell;
            cell.SetPortrait(portrait);
            cell.SetOccupiedUnit(unit);
        }

        /// <summary>
        /// 모든 전장 칸의 카드 배율을 보통 크기로 되돌린다.
        ///
        /// 배율은 칸에 붙어 있고 유닛이 죽거나 판이 바뀌어도 풀리지 않았다. 그래서 한 번 엘리트 단독
        /// 판을 치른 칸은 이후 그 자리에 서는 잡졸까지 크게 그렸다(메히코의 독수리 전사 하나만 커지던 원인).
        /// 적 편성을 새로 깔 때마다 먼저 부른다.
        /// </summary>
        public void ResetFeatureScales()
        {
            if (_fieldCellManager == null) return;
            foreach (Cell cell in _fieldCellManager)
            {
                if (cell != null) cell.SetFeatureScale(1f);
            }
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

        /// <summary>
        /// 칸을 차지하지 않는 소환수를 전장에 세운다.
        ///
        /// 격자에 자리를 잡지 않으므로 <see cref="SpawnUnit"/>과 달리 <see cref="Cell"/>을 거치지 않는다.
        /// 진영 목록에만 올려 두면 대상 지정(<c>Target</c>)과 행동 순서(<c>ActionScheduler</c>)가
        /// <see cref="Unit.IsOnField"/>를 통해 자동으로 집어 간다.
        /// </summary>
        public Unit SpawnSummon(Unit owner, Combat.SummonSpec spec)
        {
            if (owner == null || spec == null || !owner.IsOnField) return null;

            GameObject prefab = owner.IsEnemy ? enemyPrefab : heroPrefab;
            if (prefab == null) return null;

            Vector3 origin = owner.currentCell != null ? owner.currentCell.transform.position : Vector3.zero;
            GameObject summonObj = Instantiate(prefab, origin, Quaternion.identity);
            summonObj.name = $"Summon_{spec.Name}";
            summonObj.transform.SetParent(owner.IsEnemy ? Field : Field);

            Unit summon = summonObj.GetComponent<Unit>();
            if (summon == null)
            {
                Debug.LogError($"[GridManager] 소환수 프리팹에 Unit이 없습니다: {spec.Name}");
                Destroy(summonObj);
                return null;
            }

            if (owner.IsEnemy) enemyList.Add(summon);
            else heroList.Add(summon);

            summon.currentCell = null;   // 칸을 차지하지 않는다
            summon.SpawnAsSummon(owner, spec);
            if (!owner.IsEnemy && GameManager.Instance?.gameState == BaseEnums.GameState.RoundInProgress)
                Core.SaveSystem.RecordCombatSummon();
            Entities.View.SummonCardView.Attach(summon, owner);
            Debug.Log($"[소환] {owner.UnitName}이(가) {spec.Name}을(를) 불러냈다");
            return summon;
        }

        public void OnRoundEnd()
        {
            foreach (Unit unit in heroList.Concat(enemyList).Where(unit => unit != null))
                unit.Chemistry?.EndRound();
            // 판에 깔린 상태는 라운드를 넘기지 않는다.
            Combat.Battlefield.Clear();
            // 공명도 같은 성질이다. 진영마다 한 자리에만 적혀 있으므로 여기서 함께 걷는다.
            Codes.Passive.OathBond.Clear();

            foreach (Unit unit in heroList.Concat(enemyList).Where(unit => unit != null && unit.isActive).ToList())
            {
                unit.Invoke(BaseEnums.UnitEventType.OnRoundEnd, new EventContext(unit));
            }

            // 소환수는 라운드를 넘기지 않는다. 소환자가 살아남아도 함께 걷는다.
            foreach (Unit unit in heroList.Concat(enemyList).Where(unit => unit != null).ToList())
            {
                unit.DismissSummons();
            }

            // 죽은 자리의 예약을 푼다.
            //
            // 예약은 '전투 중에 죽은 자리로 곧바로 다시 소환되는 것'을 막으려는 장치라,
            // 라운드 경계를 넘길 이유가 없다. 그대로 두면 시간으로만 풀리므로
            // 라운드 종료 직후 같은 프레임에 배치하는 경로(스테이지 점프·다음 라운드 적 배치)에서
            // <b>그 칸이 통째로 비어 적이 사양보다 적게 선다.</b>
            if (_fieldCellManager != null)
            {
                foreach (Cell cell in _fieldCellManager)
                {
                    if (cell != null) cell.reservedTime = 0f;
                }
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

            // 소환수는 칸이 없다. 칸 유무로 거르면 영영 비활성화되지 않는다.
            if (unit.isActive)
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

        /// <summary>
        /// 아군 전원의 파생 스탯을 다시 돌린다. 강화제처럼 <b>전투 밖에서</b> 파티 전체의
        /// 스탯을 건드리는 효과가 걸리거나 풀린 직후에 부른다.
        /// CON이 바뀌면 최대 체력이 바뀌므로, 체력 비율을 보존하는 AttributesUpdate를 거쳐야 한다.
        /// </summary>
        public void RefreshAllyAttributes()
        {
            foreach (Unit hero in heroList)
            {
                if (hero == null || hero.IsEnemy) continue;
                hero.RefreshDerivedAttributes();
            }
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
