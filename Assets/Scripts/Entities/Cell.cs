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

    /// <summary>칸 안에 초상화가 차지할 한 변의 크기(월드 단위). 칸 테두리(10.16)보다 조금 작게 잡는다.</summary>
    public const float PortraitFitSize = 9.6f;

    /// <summary>세워 세울 캐릭터의 높이(월드 단위). 바닥 타일보다 크게 잡아 존재감을 준다.</summary>
    public const float StandingHeight = 13f;

    /// <summary>바닥 타일을 눕히는 비율. 위에서 비스듬히 내려다보는 느낌을 낸다.</summary>
    public const float GroundPadFlatten = 0.34f;

    /// <summary>발밑(바닥 타일 중심)에서 캐릭터가 살짝 떠 보이지 않도록 내리는 보정.</summary>
    private const float GroundSink = 0.6f;

    /// <summary>같은 행 안에서 렌더링 순서를 나누는 간격.</summary>
    private const int DepthSortingStep = 10;

    private SpriteRenderer _groundPad;
    private Vector3 _uiBasePosition;
    private bool _uiBaseCaptured;

    private void Awake()
    {
        SetUpPseudo3D();
    }

    /// <summary>
    /// 바닥 타일을 눕히고, 체력 바를 캐릭터 머리 위로 올린다.
    ///
    /// 정사영 카메라라 진짜 원근은 없다. 바닥은 납작하게, 캐릭터는 그 위에 세워
    /// 비스듬히 내려다보는 판처럼 보이게 만든다.
    /// </summary>
    private void SetUpPseudo3D()
    {
        if (_groundPad == null) _groundPad = GetComponent<SpriteRenderer>();
        if (_groundPad != null)
        {
            Vector3 padScale = _groundPad.transform.localScale;
            _groundPad.transform.localScale = new Vector3(padScale.x, padScale.y * GroundPadFlatten, padScale.z);
        }

        if (uiObject != null)
        {
            if (!_uiBaseCaptured)
            {
                _uiBasePosition = uiObject.transform.localPosition;
                _uiBaseCaptured = true;
            }
            // 체력/마나 바는 세운 캐릭터의 머리 위로.
            uiObject.transform.localPosition = _uiBasePosition + new Vector3(0f, StandingHeight * 0.85f, 0f);
        }
    }

    /// <summary>
    /// 앞줄이 뒷줄을 가리도록 렌더링 순서를 정한다.
    /// <paramref name="rowFromBack"/>이 클수록 화면 앞쪽(카메라에 가까움)이다.
    /// </summary>
    public void ApplyDepth(int rowFromBack)
    {
        int order = rowFromBack * DepthSortingStep;

        if (_groundPad == null) _groundPad = GetComponent<SpriteRenderer>();
        if (_groundPad != null) _groundPad.sortingOrder = order;
        if (portraitRenderer != null) portraitRenderer.sortingOrder = order + 1;

        // 바 종류는 캐릭터보다 항상 위에.
        foreach (SpriteRenderer bar in new[]
                 {
                     hpBarBackground, hpBarFill, mpBarBackground, mpBarFill,
                     shieldBarBackground, shieldBarFill,
                 })
        {
            if (bar != null) bar.sortingOrder = order + 5;
        }
    }

    /// <summary>
    /// 칸에 초상화를 세운다.
    ///
    /// 원본 해상도가 제각각이라(아군 1254px, 적 512px, 일부 1024px) 렌더러 스케일을 고정하면
    /// 아군은 칸을 넘치고 적은 칸의 절반만 채운다. <b>세로 높이</b>를
    /// <see cref="StandingHeight"/>에 맞춰 균일 배율로 키우면 해상도와 무관하게 같은 키가 되고,
    /// 발밑이 바닥 타일에 닿도록 위로 올려 세운다.
    ///
    /// 임시 조치다 — 전신 스탠딩 스프라이트가 준비되면 초상화 대신 그것을 쓴다.
    /// </summary>
    public void SetPortrait(Sprite sprite)
    {
        if (portraitRenderer == null) return;

        portraitRenderer.sprite = sprite;

        if (sprite == null)
        {
            portraitRenderer.transform.localScale = Vector3.one;
            portraitRenderer.transform.localPosition = Vector3.zero;
            return;
        }

        float scale = PortraitScaleFor(sprite);
        portraitRenderer.transform.localScale = new Vector3(scale, scale, 1f);

        // 스프라이트 중심이 원점이므로 절반 높이만큼 올려야 발밑이 바닥에 닿는다.
        float halfHeight = sprite.bounds.size.y * 0.5f * scale;
        portraitRenderer.transform.localPosition = new Vector3(0f, halfHeight - GroundSink, 0f);
    }

    /// <summary>해당 스프라이트를 정해진 키에 맞추는 배율.</summary>
    public static float PortraitScaleFor(Sprite sprite)
    {
        if (sprite == null) return 1f;
        float height = sprite.bounds.size.y;
        return height <= 0.0001f ? 1f : StandingHeight / height;
    }

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
            // 짧은 클릭 처리 - 해당 유닛 기준으로 코덱스(상세 화면)를 연다.
            if (isOccupied && unit != null)
            {
                Unit cellUnit = unit.GetComponent<Unit>();
                if (cellUnit != null && cellUnit.isActive)
                {
                    GameManager.Instance?.uiManager?.ShowUnitDetail(cellUnit);
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
