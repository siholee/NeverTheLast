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
    
    // 클릭 · 드래그 · 호버 판정은 Managers.FieldPointerInput이 한 곳에서 한다.
    // 칸마다 OnMouse* 메시지를 받던 방식은 UI 위 판정이 한 프레임 늦어 버튼 클릭이 카드로 새어 들어왔다.

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
    public const float PortraitFitSize = CardSize * 1.03f;

    public const float StandingHeight = PortraitFitSize;
    public const float StandingWidth = PortraitFitSize;

    /// <summary>바닥 타일을 눕히는 비율. 1이면 정사각형 칸 그대로다.</summary>
    public const float GroundPadFlatten = 0.18f;

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
    private Color _groundPadBase;

    /// <summary>
    /// 덱 구성 모드. 켜져 있으면 아군이 놓일 수 있는 빈 칸과 벤치가 앰버로 숨쉰다.
    /// 예전에는 [덱 구성]을 눌러도 하단 문구만 바뀌어, 어디에 무엇을 끌어다 놓는지 알 수 없다는 QA가 있었다.
    /// 끌고 있는 동안에도 같은 표시가 켜진다.
    /// </summary>
    public static bool PlacementModeActive;
    private UnitCardView _card;
    private float _featureScale = 1f;

    /// <summary>카드 배율. 카메라가 가장 큰 카드 기준으로 여백을 잡을 때 읽는다.</summary>
    public float FeatureScale => _featureScale;

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
        // 빈 칸은 검은 사각형이 아니라 얇은 청록 레인 표식으로만 남긴다.
        _groundPad.color = new Color(0.18f, 0.48f, 0.49f, 0.22f);
        _groundPadBase = _groundPad.color;
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

        UpdatePlacementGlow();
    }

    /// <summary>
    /// 카드에 마우스를 올리면 그 유닛의 상태를 하나씩 풀어 적는다.
    ///
    /// 카드 위 아이콘은 같은 모양끼리 한 칸에 모이고 칸 수도 제한되어 있어, 아이콘만으로는
    /// 무엇이 걸렸는지 다 알 수 없다는 QA가 있었다. 이름 · 중첩 · 남은 턴과 설명을 여기서 보여 준다.
    /// 라부아지에는 실험대 정보가 여기에 함께 붙는다.
    /// </summary>
    public void ShowHoverTooltip()
    {
        Unit hovered = isOccupied && unit != null ? unit.GetComponent<Unit>() : null;
        if (hovered == null || !hovered.isActive)
        {
            Managers.UI.Core.UITooltip.Hide(this);
            return;
        }

        var lines = new System.Collections.Generic.List<Managers.UI.Core.UITooltip.Line>();
        Color muted = Managers.UI.Theme.UITheme.TextMuted;

        // 체력은 카드의 바가 이미 보여 주므로 적지 않는다. 대신 "누구인가"를 먼저 소개한다 —
        // 원소 / 숙련 · 주스탯 · 부스탯 / 한 줄 소개. 그 아래로 걸린 상태가 이어진다.
        Color primary = Managers.UI.Theme.UITheme.TextPrimary;
        lines.Add(new Managers.UI.Core.UITooltip.Line("원소",
            Managers.UI.Core.UnitInfoText.ElementName(hovered.Element), primary));
        if (!hovered.IsEnemy)
            lines.Add(new Managers.UI.Core.UITooltip.Line("숙련",
                Managers.UI.Core.UnitInfoText.Proficiencies(hovered), primary));
        string stats = Managers.UI.Core.UnitInfoText.Stats(hovered);
        if (stats.Length > 0) lines.Add(new Managers.UI.Core.UITooltip.Line("스탯", stats, primary));
        string tagline = Managers.UI.Core.UnitInfoText.Tagline(hovered);
        if (!string.IsNullOrWhiteSpace(tagline))
            lines.Add(Managers.UI.Core.UITooltip.Line.Note(tagline.Trim(), Managers.UI.Theme.UITheme.TextSecondary));

        var groups = new System.Collections.Generic.List<(Entities.Status.UnitStatus Status, int Count)>();
        foreach (Entities.Status.UnitStatus status in hovered.ActiveStatuses)
        {
            if (status == null) continue;
            int index = groups.FindIndex(g => g.Status.Key == status.Key);
            if (index >= 0) groups[index] = (groups[index].Status, groups[index].Count + 1);
            else groups.Add((status, 1));
        }

        foreach ((Entities.Status.UnitStatus status, int count) in groups)
        {
            string turns = status.Duration > 0 ? $"{status.RemainingTurns}턴" : "지속";
            string stacks = count > 1 ? $"{count}중첩 · " : "";
            lines.Add(new Managers.UI.Core.UITooltip.Line(status.StatusName, stacks + turns,
                Managers.UI.Theme.StatusIcons.Tint(status)));
            if (!string.IsNullOrWhiteSpace(status.StatusDescription))
                lines.Add(Managers.UI.Core.UITooltip.Line.Note("   " + status.StatusDescription.Trim(), muted));
        }

        var chemistry = hovered.Chemistry;
        if (chemistry != null && chemistry.Active)
        {
            lines.Add(Managers.UI.Core.UITooltip.Line.Note(chemistry.Summary, Color.white));
            lines.Add(Managers.UI.Core.UITooltip.Line.Note(chemistry.WaitingReason, Managers.UI.Theme.UITheme.Accent));
            lines.Add(Managers.UI.Core.UITooltip.Line.Note($"현재 배합량 {chemistry.Reagents.Batch:0.##}\n예상 기본 피해 {chemistry.PreviewDamage:0}\n치명타·방어·피해 보정 전, 적 1명 기준", Color.white));
            lines.Add(Managers.UI.Core.UITooltip.Line.Note("치유 → 연료 / 보호막 → 안정제 / 버프 → 촉매\n일반행동 2회마다 가장 적은 시약을 합성", Managers.UI.Theme.UITheme.TextSecondary));
        }

        Managers.UI.Core.UITooltip.Show(this, $"{hovered.UnitName}  Lv.{hovered.Level}", lines,
            hovered.IsEnemy ? Managers.UI.Theme.UITheme.Enemy : Managers.UI.Theme.UITheme.Accent);
    }

    public void HideHoverTooltip() => Managers.UI.Core.UITooltip.Hide(this);
    private void OnDisable() => Managers.UI.Core.UITooltip.Hide(this);
    
    /// <summary>
    /// 셀에 유닛 배치 시 호출
    /// </summary>
    /// <param name="unit">배치할 유닛</param>
    public void SetOccupiedUnit(Unit unit)
    {
        occupiedUnit = unit;
        isOccupied = unit != null;
        // 기본 준비 화면에서는 빈 칸 GameObject 자체를 꺼 둔다. 그 자리에 새 유닛이 오면
        // 카드·콜라이더·입력이 함께 돌아오도록 점유 설정이 직접 다시 켠다.
        if (unit != null && !gameObject.activeSelf) gameObject.SetActive(true);

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
    private void UpdatePlacementGlow()
    {
        if (_groundPad == null || !_groundPad.enabled) return;

        bool dragging = DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging();
        bool allySlot = xPos < 0 || (GridManager.Instance != null && GridManager.Instance.IsBenchCell(this));
        if ((PlacementModeActive || dragging) && allySlot)
        {
            float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            _groundPad.color = Color.Lerp(_groundPadBase, Managers.UI.Theme.UITheme.Accent, pulse);
        }
        else if (_groundPad.color != _groundPadBase)
        {
            _groundPad.color = _groundPadBase;
        }
    }

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
