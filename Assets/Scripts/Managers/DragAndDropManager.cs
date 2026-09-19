using UnityEngine;
using Entities;
using BaseClasses;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class DragAndDropManager : MonoBehaviour
    {
        public static DragAndDropManager Instance { get; private set; }
        
        [Header("Drag & Drop Settings")]
        public LayerMask cellLayerMask = -1; // 모든 레이어
        
        [Header("Visual Feedback")]
        public float dragAlpha = 0.6f; // 드래그 중인 스프라이트 투명도
        
        private bool isDragging = false;
        private Unit draggedUnit = null;
        private Cell sourceCell = null;
        private Camera mainCamera;
        
        // 드래그 중인 유닛의 시각적 표현
        private GameObject dragPreview = null;
        private SpriteRenderer dragPreviewRenderer = null;
        private Vector2 dragScreenPosition;
        private bool hasExternalPointer;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                // 셀·카메라 참조는 Game 씬과 함께 수명을 끝낸다.
                
                // 드래그 프리뷰를 미리 생성 (비활성화 상태로)
                CreateDragPreview();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        
        public void StartDrag(Cell cell)
        {
            StartDrag(cell, default, false);
        }

        /// <summary>터치 등 외부 포인터의 현재 화면 좌표로 드래그를 시작한다.</summary>
        public void StartDrag(Cell cell, Vector2 screenPosition)
        {
            StartDrag(cell, screenPosition, true);
        }

        private void StartDrag(Cell cell, Vector2 screenPosition, bool externalPointer)
        {
            if (cell == null || !cell.isOccupied || cell.unit == null) return;
            
            Unit unit = cell.unit.GetComponent<Unit>();
            if (unit == null || !unit.isActive) return;
            
            // 라운드 진행 중에는 벤치에 있는 유닛만 드래그 가능
            if (GameManager.Instance != null && GameManager.Instance.gameState == GameState.RoundInProgress)
            {
                // GridManager를 통해 벤치 셀인지 확인
                if (!GridManager.Instance.IsBenchCell(cell))
                {
                    Debug.Log($"라운드 진행 중에는 벤치에 있는 유닛만 이동할 수 있습니다.");
                    return;
                }
            }
            
            isDragging = true;
            draggedUnit = unit;
            sourceCell = cell;
            hasExternalPointer = externalPointer;
            if (externalPointer) dragScreenPosition = screenPosition;
            
            // 드래그 프리뷰 설정 및 활성화
            SetupDragPreview(unit);
            
            // 원본 유닛을 반투명하게 만들기
            SetUnitAlpha(unit, dragAlpha);
        }
        
        public void EndDrag()
        {
            EndDrag(CurrentPointerPosition());
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (!isDragging || draggedUnit == null) return;

            dragScreenPosition = screenPosition;
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));
            
            // 레이캐스트로 드롭 대상 셀 찾기
            RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero, Mathf.Infinity, cellLayerMask);
            
            if (hit.collider != null)
            {
                Cell targetCell = hit.collider.GetComponent<Cell>();
                if (targetCell != null)
                {
                    HandleDrop(targetCell);
                }
            }
            
            FinishDrag();
        }

        /// <summary>OS가 터치를 취소했거나 모달 UI 위에서 놓았을 때 이동 없이 원상 복구한다.</summary>
        public void CancelDrag()
        {
            if (!isDragging) return;
            FinishDrag();
        }

        public void UpdateDragPointer(Vector2 screenPosition)
        {
            if (!isDragging) return;
            hasExternalPointer = true;
            dragScreenPosition = screenPosition;
            MoveDragPreview(screenPosition);
        }

        private void FinishDrag()
        {
            // 드래그 프리뷰 숨기기 및 원본 유닛 복원
            HideDragPreview();
            if (draggedUnit != null)
            {
                SetUnitAlpha(draggedUnit, 1.0f);
            }
            
            // 드래그 상태 초기화
            isDragging = false;
            draggedUnit = null;
            sourceCell = null;
            hasExternalPointer = false;
        }
        
        private void Update()
        {
            // 드래그 중일 때 프리뷰를 마우스 위치로 이동
            if (isDragging && dragPreview != null)
            {
                MoveDragPreview(CurrentPointerPosition());
            }
        }

        private void MoveDragPreview(Vector2 screenPosition)
        {
            if (mainCamera == null || dragPreview == null) return;
            Vector3 point = new(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z);
            dragPreview.transform.position = mainCamera.ScreenToWorldPoint(point);
        }

        private Vector2 CurrentPointerPosition()
        {
            if (hasExternalPointer) return dragScreenPosition;
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
        
        private void HandleDrop(Cell targetCell)
        {
            // 유효한 이동인지 확인 (라운드 진행 중에는 벤치만, 다른 상태에서는 벤치 또는 필드)
            bool isValidMove = IsValidDropTarget(targetCell);
            
            if (!isValidMove)
            {
                if (GameManager.Instance != null && GameManager.Instance.gameState == GameState.RoundInProgress)
                {
                    Debug.Log("라운드 진행 중에는 벤치에서 벤치로만 유닛을 이동할 수 있습니다.");
                }
                else
                {
                    Debug.Log("유효하지 않은 이동 위치입니다.");
                }
                return;
            }
            
            // 같은 셀에 드롭한 경우
            if (targetCell == sourceCell)
            {
                Debug.Log("같은 위치로 이동했습니다.");
                return;
            }
            
            // 대상 셀에 다른 유닛이 있는 경우 위치 교환
            if (targetCell.isOccupied && targetCell.unit != null)
            {
                SwapUnits(sourceCell, targetCell);
            }
            else
            {
                // 단순 이동
                MoveUnit(sourceCell, targetCell);
            }
        }
        
        private bool IsValidDropTarget(Cell targetCell)
        {
            // 라운드 진행 중에는 벤치만 유효한 드롭 대상
            if (GameManager.Instance != null && GameManager.Instance.gameState == GameState.RoundInProgress)
            {
                return IsBenchCell(targetCell);
            }
            
            // 다른 상태에서는 기존 로직 유지 (필드의 x좌표가 음수인 셀이거나 벤치의 셀)
            bool isNegativeXField = targetCell.xPos < 0;
            bool isBenchCell = IsBenchCell(targetCell);
            
            return isNegativeXField || isBenchCell;
        }
        
        private bool IsBenchCell(Cell cell)
        {
            // GridManager의 IsBenchCell 메서드 사용
            return GridManager.Instance != null && GridManager.Instance.IsBenchCell(cell);
        }
        
        // 이동과 교환은 같은 Unit 오브젝트를 옮긴다. 예전처럼 끄고 새로 스폰하면
        // 레벨 · 훈련 · 장비 · 현재 체력이 데이터 초기값으로 돌아갔다(GridManager.RelocateUnit).
        private void SwapUnits(Cell sourceCell, Cell targetCell)
        {
            Unit sourceUnit = sourceCell.unit.GetComponent<Unit>();
            if (sourceUnit == null || targetCell.unit.GetComponent<Unit>() == null) return;
            // 끄는 동안 흐리게 만든 초상화는 원래 칸의 렌더러다. 옮기기 전에 되돌려야 그 칸에 남지 않는다.
            SetUnitAlpha(sourceUnit, 1f);
            GridManager.Instance.RelocateUnit(sourceUnit, targetCell);
        }

        private void MoveUnit(Cell sourceCell, Cell targetCell)
        {
            Unit unit = sourceCell.unit.GetComponent<Unit>();
            if (unit == null) return;
            SetUnitAlpha(unit, 1f);
            GridManager.Instance.RelocateUnit(unit, targetCell);
        }

        private void CreateDragPreview()
        {
            // 드래그 프리뷰 오브젝트 생성
            dragPreview = new GameObject("DragPreview");
            dragPreviewRenderer = dragPreview.AddComponent<SpriteRenderer>();
            
            // 비활성화 상태로 시작
            dragPreview.SetActive(false);
        }
        
        private void SetupDragPreview(Unit unit)
        {
            if (unit == null || dragPreviewRenderer == null) return;
            
            // 원본 유닛의 스프라이트 복사
            SpriteRenderer originalRenderer = unit.currentCell.portraitRenderer;
            if (originalRenderer != null)
            {
                dragPreviewRenderer.sprite = originalRenderer.sprite;
                dragPreviewRenderer.color = originalRenderer.color;
                dragPreviewRenderer.sortingLayerName = "UI"; // UI 레이어에 표시
                dragPreviewRenderer.sortingOrder = 100; // 가장 앞에 표시
                
                // 투명도 설정
                Color previewColor = dragPreviewRenderer.color;
                previewColor.a = dragAlpha;
                dragPreviewRenderer.color = previewColor;
            }
            
            // 칸에 맞춘 배율을 그대로 따르고, 들고 있는 느낌이 나도록 조금만 줄인다.
            float fit = Cell.PortraitScaleFor(dragPreviewRenderer.sprite);
            dragPreview.transform.localScale = Vector3.one * (fit * 0.9f);
            
            // 드래그 프리뷰 활성화
            dragPreview.SetActive(true);
        }
        
        private void HideDragPreview()
        {
            if (dragPreview != null)
            {
                dragPreview.SetActive(false);
            }
        }
        
        private void SetUnitAlpha(Unit unit, float alpha)
        {
            if (unit == null) return;
            
            SpriteRenderer renderer = unit.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
            
            // Cell의 portrait 렌더러도 함께 변경
            if (unit.currentCell != null && unit.currentCell.portraitRenderer != null)
            {
                Color portraitColor = unit.currentCell.portraitRenderer.color;
                portraitColor.a = alpha;
                unit.currentCell.portraitRenderer.color = portraitColor;
            }
        }
        
        public bool IsDragging()
        {
            return isDragging;
        }
    }
}
