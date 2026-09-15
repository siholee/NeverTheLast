using UnityEngine;
using Entities;
using Entities.View;
using Managers;

public class Cell : MonoBehaviour
{
    public int xPos;
    public int yPos;

    /// <summary>
    /// 정렬된 뒤 이 칸이 실제로 서 있는 세로 자리. 열 중앙이 0이고 아래로 갈수록 커진다.
    ///
    /// 빈 칸을 지우고 남은 유닛만 중앙에 모으므로 <see cref="yPos"/>와 화면상의 거리가 어긋난다.
    /// 사거리·근접 판정은 눈에 보이는 거리를 따라야 하므로 yPos 대신 이 값을 쓴다.
    /// </summary>
    public float DisplaySlot;

    /// <summary>지금 화면에 자리를 받은 칸인지. 전투 중 빈 칸은 false가 된다.</summary>
    public bool IsLaidOut = true;
    public float reservedTime;
    public bool isOccupied = false;
    public GameObject unit;
    public SpriteRenderer portraitRenderer;
    public GameObject uiObject;     
    
    // ── 예전 프리팹 바 (미사용) ─────────────────────────────────────
    // UnitCardView가 대신 그린다. 프리팹이 아직 참조하고 있어 필드는 남겨 두지만
    // 코드에서는 쓰지 않는다. 프리팹을 정리할 때 함께 지우면 된다.
    [Header("HP Bar Components - Sprite Mask (미사용)")]
    public SpriteRenderer hpBarBackground;  // HP 바 배경
    public SpriteRenderer hpBarFill;        // HP 바 채움 (SpriteMask로 제어됨)
    public SpriteMask hpBarMask;            // HP 바 마스크
    
    [Header("MP Bar Components - Sprite Mask (미사용)")]
    public SpriteRenderer mpBarBackground;  // MP 바 배경
    public SpriteRenderer mpBarFill;        // MP 바 채움 (SpriteMask로 제어됨)
    public SpriteMask mpBarMask;            // MP 바 마스크
    
    [Header("Shield Bar Components - Sprite Mask (미사용)")]
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

    /// <summary>칸 한 변(월드 단위). 칸 테두리 스프라이트의 크기다.</summary>
    public const float CellSize = 10.16f;

    /// <summary>카드 한 변. 칸보다 조금 작게 잡아야 격자 사이가 벌어져 보인다.</summary>
    public const float CardSize = CellSize * UnitCardView.CardRatio;

    /// <summary>
    /// 초상화가 차지할 한 변(월드 단위).
    ///
    /// 초상화는 SpriteRenderer라 잘라낼 수 없으므로 카드 <b>안쪽에 맞춰</b> 줄인다(contain).
    /// 초상화가 정사각이면 카드를 거의 꽉 채우고, 세로로 긴 그림은 좌우에 여백이 남는다.
    /// </summary>
    public const float PortraitFitSize = CardSize * 0.86f;

    public const float StandingHeight = PortraitFitSize;
    public const float StandingWidth = PortraitFitSize;

    /// <summary>바닥 타일을 눕히는 비율. 1이면 정사각형 칸 그대로다.</summary>
    public const float GroundPadFlatten = 1f;

    /// <summary>같은 행 안에서 렌더링 순서를 나누는 간격.</summary>
    private const int DepthSortingStep = 10;

    /// <summary>
    /// 엘리트·보스 단독 편성에서 카드와 초상화를 키우는 배율.
    ///
    /// 한 칸에 하나씩 서는 판에서 보스가 잡졸과 같은 크기로 나오면 무게가 실리지 않는다.
    /// 옆 칸이 비어 있을 때만 켜지므로 커진 카드가 다른 유닛을 가리지 않는다.
    /// </summary>
    public const float FeatureCardScale = 1.5f;

    private SpriteRenderer _groundPad;
    private UnitCardView _card;
    private float _featureScale = 1f;

    /// <summary>이 칸의 렌더링 순서 기준값. 칸 위에 얹는 카드가 이 값에서 출발한다.</summary>
    public int DepthOrder { get; private set; }

    /// <summary>이 칸이 쓰는 정렬 레이어.</summary>
    public int DepthLayerId { get; private set; }

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
        BuildGroundPad();
        EnsureCard();

        // 체력 · 마나 · 방어막 바는 이제 카드(UnitCardView)가 들고 있다.
        // 프리팹에 남아 있는 옛 바 오브젝트는 카드와 겹쳐 보이므로 통째로 꺼 둔다.
        if (uiObject != null) uiObject.SetActive(false);
    }

    /// <summary>
    /// 이 칸의 카드·초상화 배율을 바꾼다. 1이면 보통 크기다.
    /// 편성이 확정된 뒤 <see cref="Managers.GridManager"/>가 한 번 부른다.
    /// </summary>
    public void SetFeatureScale(float scale)
    {
        float next = Mathf.Max(0.1f, scale);
        if (Mathf.Approximately(_featureScale, next)) return;

        _featureScale = next;
        EnsureCard();
        _card.Resize(CardSize * _featureScale);

        // 초상화는 카드와 별개의 렌더러라 같은 배율을 직접 다시 먹여야 한다.
        if (portraitRenderer != null) SetPortrait(portraitRenderer.sprite);
    }

    private void EnsureCard()
    {
        if (_card != null) return;

        _card = UnitCardView.Attach(transform, CellSize);
        // 초상화는 칸의 자식이라 카드 루트를 흔들어도 따라오지 않는다. 직접 물려 준다.
        _card.BindPortrait(portraitRenderer);
    }

    /// <summary>
    /// 칸 테두리를 눕힌 바닥 타일로 바꾼다.
    ///
    /// 테두리 스프라이트는 <b>셀 루트</b>에 붙어 있어서 루트를 눌러 봐야 소용이 없다.
    /// 루트 스케일은 <see cref="Managers.GridManager"/>가 원근 배율로 덮어쓰고,
    /// 무엇보다 그 아래 캐릭터까지 같이 눌린다.
    /// 그래서 테두리를 전용 자식으로 옮기고 그 자식만 납작하게 만든다.
    /// </summary>
    private void BuildGroundPad()
    {
        if (_groundPad != null) return;

        SpriteRenderer rootFrame = GetComponent<SpriteRenderer>();
        if (rootFrame == null) return;

        var padObject = new GameObject("GroundPad");
        padObject.transform.SetParent(transform, false);
        padObject.transform.localPosition = Vector3.zero;
        padObject.transform.localScale = new Vector3(1f, GroundPadFlatten, 1f);

        _groundPad = padObject.AddComponent<SpriteRenderer>();
        _groundPad.sprite = rootFrame.sprite;
        _groundPad.color = rootFrame.color;
        _groundPad.sortingLayerID = rootFrame.sortingLayerID;
        _groundPad.sortingOrder = rootFrame.sortingOrder;

        // 원본은 꺼 둔다. 둘 다 그리면 눕히지 않은 사각형이 그대로 남는다.
        rootFrame.enabled = false;
    }

    /// <summary>
    /// 앞줄이 뒷줄을 가리도록 렌더링 순서를 정한다.
    /// <paramref name="rowFromBack"/>이 클수록 화면 앞쪽(카메라에 가까움)이다.
    /// </summary>
    public void ApplyDepth(int rowFromBack)
    {
        int order = rowFromBack * DepthSortingStep;

        BuildGroundPad();
        EnsureCard();

        int layer = _groundPad != null ? _groundPad.sortingLayerID
            : portraitRenderer != null ? portraitRenderer.sortingLayerID : 0;

        // 칸 위에 얹히는 것들(소환수 카드)이 같은 기준을 읽을 수 있게 남겨 둔다.
        // 루트 SpriteRenderer는 바닥 타일로 옮긴 뒤 꺼 두므로 그쪽 값은 낡아 있다.
        DepthOrder = order;
        DepthLayerId = layer;

        if (_groundPad != null) _groundPad.sortingOrder = order;
        // 카드 프레임은 order, 초상화는 그 위(order + 1), HUD는 다시 그 위(order + 5).
        _card?.ApplyDepth(order, layer);
        if (portraitRenderer != null)
        {
            portraitRenderer.sortingLayerID = layer;
            portraitRenderer.sortingOrder = order + 1;
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

        float scale = PortraitScaleFor(sprite) * _featureScale;
        portraitRenderer.transform.localScale = new Vector3(scale, scale, 1f);

        // 칸 한가운데에 세운다. 예전처럼 발밑을 타일 중심에 맞추면 캐릭터가 칸 위로 솟아
        // 윗줄과 겹쳤다. 칸 안에 들어가는 크기이므로 중앙 정렬이 가장 깔끔하다.
        portraitRenderer.transform.localPosition = Vector3.zero;

        // 배율이 다시 잡혔으니 카드가 흔들 때 쓸 기준값도 갱신한다.
        _card?.RefreshPortraitBase();
    }

    /// <summary>
    /// 해당 스프라이트를 칸 안에 넣는 배율.
    /// 세로뿐 아니라 가로도 함께 제한해야 넓은 그림이 옆 칸을 침범하지 않는다.
    /// </summary>
    public static float PortraitScaleFor(Sprite sprite)
    {
        if (sprite == null) return 1f;

        float height = sprite.bounds.size.y;
        float width = sprite.bounds.size.x;
        if (height <= 0.0001f || width <= 0.0001f) return 1f;

        return Mathf.Min(StandingHeight / height, StandingWidth / width);
    }

    private void Update()
    {
        reservedTime = Mathf.Max(0, reservedTime - Time.deltaTime);

        // 카드가 살아 있는 동안 체력 · 방어막 · 행동 게이지를 계속 따라간다.
        if (occupiedUnit != null) UpdateUI();
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

        // 점유가 바뀌면 열 정렬이 달라진다(빈 칸은 자리를 차지하지 않는다).
        GridManager.Instance?.RequestFieldLayoutRefresh();
        
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
    /// 유닛이 들어오면 카드를 세운다.
    /// 대기석(yPos == 0)은 전투에 참여하지 않으므로 전투 HUD 없이 이름표만 보여 준다.
    /// </summary>
    private void ActivateUI()
    {
        EnsureCard();
        _card.Bind(occupiedUnit, yPos > 0);

        // 카드가 칸을 덮으므로 바닥 타일은 감춘다. 빈 칸에서만 타일이 보인다.
        SetGroundPadVisible(false);
    }

    private void DeactivateUI()
    {
        EnsureCard();
        _card.Bind(null, false);
        SetGroundPadVisible(true);
    }

    /// <summary>
    /// 바닥 타일(배치 슬롯)을 보이거나 감춘다.
    /// 빈 칸을 아예 지우는 정렬을 <see cref="Managers.GridManager"/>가 이걸로 처리한다.
    /// </summary>
    public void SetGroundPadVisible(bool visible)
    {
        BuildGroundPad();
        if (_groundPad != null) _groundPad.enabled = visible;
    }

    /// <summary>체력 · 방어막 · 행동 게이지 · 궁극기 링 · 상태 점을 한 번에 갱신한다.</summary>
    public void UpdateUI()
    {
        if (occupiedUnit == null) return;

        EnsureCard();
        _card.Tick();
    }

    /// <summary>투사체 착탄·근접 타격 시점에 카드가 적 방향으로 짧게 전진한다.</summary>
    public void PlayAttackReaction(float strength = 1f)
    {
        EnsureCard();
        _card?.PlayAttackReaction(strength);
    }

    /// <summary>피해가 들어간 순간의 카드 반응. 물리면 떨림까지 함께 친다.</summary>
    public void PlayHitReaction(float strength, bool physical, bool crit)
    {
        EnsureCard();
        _card?.PlayHitReaction(strength, physical, crit);
    }

    // ── 예전 프리팹 바 진입점 ────────────────────────────────────────
    // 체력 · 마나 · 방어막 바는 UnitCardView로 옮겼다. Unit이 소환 직후 부르는
    // 자리라 이름은 남겨 두고, 카드 바인딩만 확인한다.

    public void InitializeHpBar()
    {
        if (occupiedUnit != null) ActivateUI();
    }

    public void InitializeMpBar()
    {
    }

    public void InitializeShieldBar()
    {
    }
}
