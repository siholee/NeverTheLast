using UnityEngine;
using Entities;
using Managers;

public class Cell : MonoBehaviour
{
    public int xPos;
    public int yPos;
    public float reservedTime;
    public bool isOccupied = false;
    public GameObject unit;
    public SpriteRenderer portraitRenderer;
    public GameObject uiObject;     
    
    [Header("HP Bar Components - Sprite Mask")]
    public SpriteRenderer hpBarBackground;  // HP 바 배경
    public SpriteRenderer hpBarFill;        // HP 바 채움 (SpriteMask로 제어됨)
    public SpriteMask hpBarMask;            // HP 바 마스크
    
    [Header("MP Bar Components - Sprite Mask")]
    public SpriteRenderer mpBarBackground;  // MP 바 배경
    public SpriteRenderer mpBarFill;        // MP 바 채움 (SpriteMask로 제어됨)
    public SpriteMask mpBarMask;            // MP 바 마스크
    
    [Header("Shield Bar Components - Sprite Mask")]
    public SpriteRenderer shieldBarBackground;  // 방어막 바 배경
    public SpriteRenderer shieldBarFill;        // 방어막 바 채움 (SpriteMask로 제어됨)
    public SpriteMask shieldBarMask;            // 방어막 바 마스크
    
    private Unit occupiedUnit;  // 점유 중인 유닛 참조
    
    // 클릭/드래그 구분용 필드
    private Vector3 mouseDownPosition;
    private float mouseDownTime;
    private bool isDraggingStarted = false;
    
    [Header("Drag Threshold Settings")]
    [Tooltip("드래그로 인식할 최소 마우스 이동 거리 (픽셀)")]
    public float dragDistanceThreshold = 10f;
    [Tooltip("드래그로 인식할 최소 시간 (초)")]
    public float dragTimeThreshold = 0.15f;

    private void Update()
    {
        reservedTime = Mathf.Max(0, reservedTime - Time.deltaTime);
        
        // UI 업데이트 (점유 유닛이 있고 UI가 활성화된 경우에만)
        if (occupiedUnit != null && uiObject != null && uiObject.activeInHierarchy)
        {
            UpdateUI();
        }
    }

    private void OnMouseDown()
    {
        // 유닛이 있는지 확인
        if (isOccupied && unit != null)
        {
            Unit cellUnit = unit.GetComponent<Unit>();
            if (cellUnit != null && cellUnit.isActive)
            {
                // 기본적으로 클릭 처리를 위한 초기값 저장
                mouseDownPosition = Input.mousePosition;
                mouseDownTime = Time.time;
                isDraggingStarted = false;
            }
        }
    }
    
    private void OnMouseDrag()
    {
        // 이미 드래그를 시작했거나 유닛이 없으면 무시
        if (isDraggingStarted || !isOccupied || unit == null) return;
        
        Unit cellUnit = unit.GetComponent<Unit>();
        if (cellUnit == null || !cellUnit.isActive) return;
        
        // 적군은 드래그 불가
        if (cellUnit.IsEnemy) return;
        
        // 아군만 드래그 가능하며, Preparation 상태에서만 가능
        if (GameManager.Instance == null || 
            GameManager.Instance.gameState != BaseClasses.BaseEnums.GameState.Preparation)
        {
            return;
        }
        
        // 거리와 시간 체크
        float distance = Vector3.Distance(Input.mousePosition, mouseDownPosition);
        float holdTime = Time.time - mouseDownTime;
        
        // 하이브리드: 거리 OR 시간 중 하나를 만족하면 드래그 시작
        if (distance > dragDistanceThreshold || holdTime > dragTimeThreshold)
        {
            DragAndDropManager.Instance?.StartDrag(this);
            isDraggingStarted = true;
        }
    }

    private void OnMouseUp()
    {
        if (isDraggingStarted)
        {
            // 드래그 종료
            if (DragAndDropManager.Instance != null && 
                DragAndDropManager.Instance.IsDragging())
            {
                DragAndDropManager.Instance.EndDrag();
            }
        }
        else
        {
            // 짧은 클릭 처리 - InfoTab 토글
            if (isOccupied && unit != null)
            {
                Unit cellUnit = unit.GetComponent<Unit>();
                if (cellUnit != null && cellUnit.isActive)
                {
                    UIManager uiManager = GameManager.Instance?.uiManager;
                    if (uiManager != null && uiManager.infoTab != null)
                    {
                        // 적군은 무조건 InfoTab 표시
                        if (cellUnit.IsEnemy)
                        {
                            uiManager.ShowInfoTab(cellUnit);
                        }
                        else
                        {
                            // 아군도 클릭 시 InfoTab 표시 (이미 열려있어도 정보 갱신)
                            uiManager.ShowInfoTab(cellUnit);
                        }
                    }
                }
            }
        }
        
        // 상태 초기화
        isDraggingStarted = false;
    }
    
    /// <summary>
    /// 셀에 유닛 배치 시 호출
    /// </summary>
    /// <param name="unit">배치할 유닛</param>
    public void SetOccupiedUnit(Unit unit)
    {
        occupiedUnit = unit;
        isOccupied = unit != null;
        
        if (unit != null)
        {
            ActivateUI();
        }
        else
        {
            DeactivateUI();
        }
    }
    
    /// <summary>
    /// UI 활성화 (유닛 점유 시)
    /// </summary>
    private void ActivateUI()
    {
        // 필드에 있는 유닛(yPos > 0)만 UI 활성화, 대기석(yPos = 0)은 제외
        if (uiObject != null && yPos > 0)
        {
            uiObject.SetActive(true);
            InitializeHpBar();
            InitializeMpBar();
            InitializeShieldBar();
        }
    }
    
    /// <summary>
    /// UI 비활성화 (유닛 제거 시)
    /// </summary>
    private void DeactivateUI()
    {
        if (uiObject != null)
        {
            uiObject.SetActive(false);
            DeactivateShieldBarCompletely(); // 방어막 바도 완전 비활성화
        }
    }
    
    /// <summary>
    /// 통합 UI 업데이트 함수 (HP, MP, 방어막 등)
    /// </summary>
    public void UpdateUI()
    {
        if (occupiedUnit == null || uiObject == null || !uiObject.activeInHierarchy) 
            return;
        
        // HP 바 업데이트
        if (occupiedUnit.HpMax > 0)
        {
            float hpRatio = (float)occupiedUnit.HpCurr / occupiedUnit.HpMax;
            UpdateHpBar(hpRatio);
        }
        
        // MP 바 업데이트
        if (occupiedUnit.ManaMax > 0)
        {
            float mpRatio = (float)occupiedUnit.ManaCurr / occupiedUnit.ManaMax;
            UpdateMpBar(mpRatio);
        }
        
        // 방어막 바 업데이트 (ShieldMax 대비 ShieldCurr)
        if (occupiedUnit.ShieldMax > 0)
        {
            float shieldRatio = (float)occupiedUnit.ShieldCurr / occupiedUnit.ShieldMax;
            UpdateShieldBar(shieldRatio);
            ActivateShieldBar(); // 방어막이 있으면 활성화
        }
        else
        {
            DeactivateShieldBarCompletely(); // 방어막이 없으면 완전히 비활성화
        }
    }
    
    /// <summary>
    /// 유닛 초기화 시 HP 바 활성화
    /// </summary>
    public void InitializeHpBar()
    {
        if (hpBarBackground != null && hpBarFill != null && hpBarMask != null)
        {
            // HP 바 컴포넌트들 활성화
            hpBarBackground.gameObject.SetActive(true);
            hpBarFill.gameObject.SetActive(true);
            hpBarMask.gameObject.SetActive(true);
            
            // 2D 환경에서 z값 통일 (중요!)
            Vector3 bgPos = hpBarBackground.transform.localPosition;
            bgPos.z = 0f;
            hpBarBackground.transform.localPosition = bgPos;
            
            Vector3 fillPos = hpBarFill.transform.localPosition;
            fillPos.z = 0f;
            hpBarFill.transform.localPosition = fillPos;
            
            Vector3 maskPos = hpBarMask.transform.localPosition;
            maskPos.z = 0f;
            hpBarMask.transform.localPosition = maskPos;
            
            // 마스크 초기화 - Background와 동일한 스케일로 설정
            Vector3 bgScale = hpBarBackground.transform.localScale;
            hpBarMask.transform.localScale = bgScale; // Background와 동일하게!
            
            // 렌더링 순서 설정 (z축 대신 sortingOrder 활용)
            hpBarFill.sortingOrder = hpBarBackground.sortingOrder + 1;
        }
        else
        {
            Debug.LogWarning($"[Cell] {name} HP 바 컴포넌트가 할당되지 않았습니다! (Background: {hpBarBackground != null}, Fill: {hpBarFill != null}, Mask: {hpBarMask != null})");
        }
    }

    /// <summary>
    /// 유닛 초기화 시 MP 바 활성화
    /// </summary>
    public void InitializeMpBar()
    {
        if (mpBarBackground != null && mpBarFill != null && mpBarMask != null)
        {
            // MP 바 컴포넌트들 활성화
            mpBarBackground.gameObject.SetActive(true);
            mpBarFill.gameObject.SetActive(true);
            mpBarMask.gameObject.SetActive(true);
            
            // 2D 환경에서 z값 통일 (중요!)
            Vector3 bgPos = mpBarBackground.transform.localPosition;
            bgPos.z = 0f;
            mpBarBackground.transform.localPosition = bgPos;
            
            Vector3 fillPos = mpBarFill.transform.localPosition;
            fillPos.z = 0f;
            mpBarFill.transform.localPosition = fillPos;
            
            Vector3 maskPos = mpBarMask.transform.localPosition;
            maskPos.z = 0f;
            mpBarMask.transform.localPosition = maskPos;
            
            // 마스크 초기화 - Background와 동일한 스케일로 설정
            Vector3 bgScale = mpBarBackground.transform.localScale;
            mpBarMask.transform.localScale = bgScale; // Background와 동일하게!
            
            // 렌더링 순서 설정 (z축 대신 sortingOrder 활용)
            mpBarFill.sortingOrder = mpBarBackground.sortingOrder + 1;
        }
        else
        {
            Debug.LogWarning($"[Cell] {name} MP 바 컴포넌트가 할당되지 않았습니다! (Background: {mpBarBackground != null}, Fill: {mpBarFill != null}, Mask: {mpBarMask != null})");
        }
    }

    /// <summary>
    /// 방어막 바 초기화
    /// </summary>
    public void InitializeShieldBar()
    {
        if (shieldBarBackground != null && shieldBarFill != null && shieldBarMask != null)
        {
            // 초기에는 방어막 바를 완전히 비활성화
            shieldBarBackground.gameObject.SetActive(false);
            shieldBarFill.gameObject.SetActive(false);
            shieldBarMask.gameObject.SetActive(false);
            
            // 2D 환경에서 z값 통일 (활성화될 때를 대비)
            Vector3 bgPos = shieldBarBackground.transform.localPosition;
            bgPos.z = 0f;
            shieldBarBackground.transform.localPosition = bgPos;
            
            Vector3 fillPos = shieldBarFill.transform.localPosition;
            fillPos.z = 0f;
            shieldBarFill.transform.localPosition = fillPos;
            
            Vector3 maskPos = shieldBarMask.transform.localPosition;
            maskPos.z = 0f;
            shieldBarMask.transform.localPosition = maskPos;
            
            // 마스크 초기화 - Background와 동일한 스케일로 설정
            Vector3 bgScale = shieldBarBackground.transform.localScale;
            shieldBarMask.transform.localScale = bgScale;
            
            // 렌더링 순서 설정
            if (mpBarFill != null)
            {
                shieldBarFill.sortingOrder = mpBarFill.sortingOrder + 1;
            }
        }
        else
        {
            Debug.LogWarning($"[Cell] {name} 방어막 바 컴포넌트가 할당되지 않았습니다! (Background: {shieldBarBackground != null}, Fill: {shieldBarFill != null}, Mask: {shieldBarMask != null})");
        }
    }

    /// <summary>
    /// 방어막 바 업데이트 - Sprite Mask 방식 (ShieldMax 대비 ShieldCurr)
    /// </summary>
    /// <param name="shieldRatio">방어막 비율 (0.0 ~ 1.0, ShieldCurr / ShieldMax)</param>
    public void UpdateShieldBar(float shieldRatio)
    {
        if (shieldBarMask == null || shieldBarBackground == null) return;
        
        // Background의 현재 스케일을 기준으로 Mask 스케일 계산
        Vector3 bgScale = shieldBarBackground.transform.localScale;
        Vector3 maskScale = new Vector3(bgScale.x * shieldRatio, bgScale.y, bgScale.z);
        shieldBarMask.transform.localScale = maskScale;
        
        // Pivot이 Center인 경우 왼쪽 정렬을 위한 위치 조정
        if (shieldRatio < 1.0f)
        {
            float offsetX = (bgScale.x - maskScale.x) * 0.5f;
            Vector3 maskPos = shieldBarMask.transform.localPosition;
            maskPos.x = -offsetX; // 왼쪽으로 이동
            shieldBarMask.transform.localPosition = maskPos;
        }
        else
        {
            // 100%일 때는 원래 위치
            Vector3 maskPos = shieldBarMask.transform.localPosition;
            maskPos.x = 0f;
            shieldBarMask.transform.localPosition = maskPos;
        }
        
        // 방어막 색상 설정 (파란색 계열)
        if (shieldBarFill != null)
        {
            shieldBarFill.color = Color.cyan;
        }
    }

    /// <summary>
    /// 방어막 바 비활성화
    /// </summary>
    public void DeactivateShieldBar()
    {
        if (shieldBarBackground != null && shieldBarFill != null && shieldBarMask != null)
        {
            // 마스크 스케일을 0으로 설정하여 완전히 숨김
            shieldBarMask.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// HP 바 업데이트 - Sprite Mask 방식
    /// </summary>
    /// <param name="hpRatio">체력 비율 (0.0 ~ 1.0)</param>
    public void UpdateHpBar(float hpRatio)
    {
        if (hpBarMask == null || hpBarBackground == null) return;
        
        // Background의 현재 스케일을 기준으로 Mask 스케일 계산
        Vector3 bgScale = hpBarBackground.transform.localScale;
        Vector3 maskScale = new Vector3(bgScale.x * hpRatio, bgScale.y, bgScale.z);
        hpBarMask.transform.localScale = maskScale;
        
        // Pivot이 Center인 경우 왼쪽 정렬을 위한 위치 조정
        if (hpRatio < 1.0f)
        {
            float offsetX = (bgScale.x - maskScale.x) * 0.5f;
            Vector3 maskPos = hpBarMask.transform.localPosition;
            maskPos.x = -offsetX; // 왼쪽으로 이동
            hpBarMask.transform.localPosition = maskPos;
        }
        else
        {
            // 100%일 때는 원래 위치
            Vector3 maskPos = hpBarMask.transform.localPosition;
            maskPos.x = 0f;
            hpBarMask.transform.localPosition = maskPos;
        }
    }

    /// <summary>
    /// MP 바 업데이트 - Sprite Mask 방식
    /// </summary>
    /// <param name="mpRatio">마나 비율 (0.0 ~ 1.0)</param>
    public void UpdateMpBar(float mpRatio)
    {
        if (mpBarMask == null || mpBarBackground == null) return;
        
        // Background의 현재 스케일을 기준으로 Mask 스케일 계산
        Vector3 bgScale = mpBarBackground.transform.localScale;
        Vector3 maskScale = new Vector3(bgScale.x * mpRatio, bgScale.y, bgScale.z);
        mpBarMask.transform.localScale = maskScale;
        
        // Pivot이 Center인 경우 왼쪽 정렬을 위한 위치 조정
        if (mpRatio < 1.0f)
        {
            float offsetX = (bgScale.x - maskScale.x) * 0.5f;
            Vector3 maskPos = mpBarMask.transform.localPosition;
            maskPos.x = -offsetX; // 왼쪽으로 이동
            mpBarMask.transform.localPosition = maskPos;
        }
        else
        {
            // 100%일 때는 원래 위치
            Vector3 maskPos = mpBarMask.transform.localPosition;
            maskPos.x = 0f;
            mpBarMask.transform.localPosition = maskPos;
        }
    }

    /// <summary>
    /// HP 바 비활성화
    /// </summary>
    public void DeactivateHpBar()
    {
        if (hpBarBackground != null && hpBarFill != null && hpBarMask != null)
        {
            hpBarBackground.gameObject.SetActive(false);
            hpBarFill.gameObject.SetActive(false);
            hpBarMask.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// MP 바 비활성화
    /// </summary>
    public void DeactivateMpBar()
    {
        if (mpBarBackground != null && mpBarFill != null && mpBarMask != null)
        {
            mpBarBackground.gameObject.SetActive(false);
            mpBarFill.gameObject.SetActive(false);
            mpBarMask.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 방어막 바 비활성화 (완전 숨김)
    /// </summary>
    public void DeactivateShieldBarCompletely()
    {
        if (shieldBarBackground != null && shieldBarFill != null && shieldBarMask != null)
        {
            // 아예 GameObject를 비활성화하여 완전히 숨김
            shieldBarBackground.gameObject.SetActive(false);
            shieldBarFill.gameObject.SetActive(false);
            shieldBarMask.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 방어막 바 활성화 (방어막이 있을 때)
    /// </summary>
    public void ActivateShieldBar()
    {
        if (shieldBarBackground != null && shieldBarFill != null && shieldBarMask != null)
        {
            // GameObject 활성화
            shieldBarBackground.gameObject.SetActive(true);
            shieldBarFill.gameObject.SetActive(true);
            shieldBarMask.gameObject.SetActive(true);
        }
    }
}
