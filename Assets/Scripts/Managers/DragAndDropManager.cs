using UnityEngine;
using Entities;

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
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
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
        
        public void StartDrag(Cell cell)
        {
            if (cell == null || !cell.isOccupied || cell.unit == null) return;
            
            Unit unit = cell.unit.GetComponent<Unit>();
            if (unit == null || !unit.isActive) return;
            
            isDragging = true;
            draggedUnit = unit;
            sourceCell = cell;
            
            // 드래그 프리뷰 설정 및 활성화
            SetupDragPreview(unit);
            
            // 원본 유닛을 반투명하게 만들기
            SetUnitAlpha(unit, dragAlpha);
            
            Debug.Log($"드래그 시작: {unit.UnitName} at ({cell.xPos}, {cell.yPos})");
        }
        
        public void EndDrag()
        {
            if (!isDragging || draggedUnit == null) return;
            
            Vector2 mousePosition = Input.mousePosition;
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, -mainCamera.transform.position.z));
            
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
        }
        
        private void Update()
        {
            // 드래그 중일 때 프리뷰를 마우스 위치로 이동
            if (isDragging && dragPreview != null)
            {
                Vector3 mousePosition = Input.mousePosition;
                mousePosition.z = -mainCamera.transform.position.z; // 카메라로부터의 거리 설정
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
                dragPreview.transform.position = worldPosition;
            }
        }
        
        private void HandleDrop(Cell targetCell)
        {
            // 유효한 이동인지 확인 (필드의 x좌표가 음수인 셀이거나 벤치의 셀)
            bool isValidMove = IsValidDropTarget(targetCell);
            
            if (!isValidMove)
            {
                Debug.Log("유효하지 않은 이동 위치입니다.");
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
            // 필드의 x좌표가 음수인 셀인지 확인
            bool isNegativeXField = targetCell.xPos < 0;
            
            // 벤치 셀인지 확인 (벤치 셀은 보통 특별한 표시가 있거나 특정 위치에 있음)
            // 여기서는 GridManager의 벤치 셀 배열을 통해 확인
            bool isBenchCell = IsBenchCell(targetCell);
            
            return isNegativeXField || isBenchCell;
        }
        
        private bool IsBenchCell(Cell cell)
        {
            // GridManager의 IsBenchCell 메서드 사용
            return GridManager.Instance != null && GridManager.Instance.IsBenchCell(cell);
        }
        
        private void SwapUnits(Cell sourceCell, Cell targetCell)
        {
            Unit sourceUnit = sourceCell.unit.GetComponent<Unit>();
            Unit targetUnit = targetCell.unit.GetComponent<Unit>();
            
            if (sourceUnit == null || targetUnit == null) return;
            
            // 두 유닛의 정보 저장
            int sourceUnitId = sourceUnit.ID;
            bool sourceIsEnemy = sourceUnit.IsEnemy;
            int targetUnitId = targetUnit.ID;
            bool targetIsEnemy = targetUnit.IsEnemy;
            
            // 두 유닛 모두 비활성화
            sourceUnit.DeactivateUnit();
            targetUnit.DeactivateUnit();
            
            // 위치 교환하여 새롭게 스폰
            bool sourceIsBench = IsBenchCell(sourceCell);
            bool targetIsBench = IsBenchCell(targetCell);
            
            GridManager.Instance.SpawnUnit(targetCell.xPos, targetCell.yPos, sourceIsEnemy, sourceUnitId, targetIsBench);
            GridManager.Instance.SpawnUnit(sourceCell.xPos, sourceCell.yPos, targetIsEnemy, targetUnitId, sourceIsBench);
            
            Debug.Log($"유닛 위치 교환: {sourceUnit.UnitName} <-> {targetUnit.UnitName}");
        }
        
        private void MoveUnit(Cell sourceCell, Cell targetCell)
        {
            Unit unit = sourceCell.unit.GetComponent<Unit>();
            if (unit == null) return;
            
            // 유닛 정보 저장
            int unitId = unit.ID;
            bool isEnemy = unit.IsEnemy;
            
            // 기존 유닛 비활성화
            unit.DeactivateUnit();
            
            // 새 위치에 스폰
            bool isBench = IsBenchCell(targetCell);
            GridManager.Instance.SpawnUnit(targetCell.xPos, targetCell.yPos, isEnemy, unitId, isBench);
            
            Debug.Log($"유닛 이동: {unit.UnitName} -> ({targetCell.xPos}, {targetCell.yPos})");
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
            
            // 스케일 약간 작게 설정 (선택사항)
            dragPreview.transform.localScale = Vector3.one * 0.9f;
            
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
