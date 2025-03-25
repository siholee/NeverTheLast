using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class UiManager : MonoBehaviour
{
    [Header("캔버스 및 프리팹")]
    // 캔버스는 스크립트가 부착된 오브젝트의 Canvas 컴포넌트를 사용하거나, 에디터에서 직접 지정할 수 있습니다.
    [SerializeField] private Canvas mainCanvas;
    // TopPanelBG: 상단 패널 배경 Prefab, 권장 사이즈 400 x 40, 스프라이트: "UI/TopPanel_BG"
    [SerializeField] private GameObject topPanelBGPrefab;
    
    [Header("상점 관련 프리팹")]
    // ShopPanel: 상점 패널 자체 Prefab (패널 BG 역할 포함), 권장 사이즈 950 x 120, 스프라이트: "UI/ShopPanel_BG"
    [SerializeField] private GameObject shopPanelPrefab;
    // ShopSlot: 상점 슬롯 프리팹, 권장 사이즈 100 x 100, 스프라이트: "UI/ShopSlot_BG"
    [SerializeField] private GameObject shopSlotPrefab;
    // Button Prefab: 권장 사이즈 120 x 40, 스프라이트: "UI/Button_BG"
    [SerializeField] private GameObject buttonPrefab;
    
    [Header("중앙 하단 패널")]
    [SerializeField] private RectTransform bottomPanelContainer;
    [SerializeField] private int activeBottomPanelIndex = 0; // 0: None, 1: Bench, 2: Shop, 3: Unit Info
    private Dictionary<int, GameObject> bottomPanels = new Dictionary<int, GameObject>();
    
    [Header("대기석 패널")]
    [SerializeField] private RectTransform benchPanel;
    
    [Header("유닛 상점")]
    [SerializeField] private RectTransform shopPanel;
    // 상점 슬롯 버튼들을 배열로 관리 (에디터에서 참조할 수 있습니다)
    [SerializeField] private Button[] unitShopSlots;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private TextMeshProUGUI costRangeText;
    
    [Header("상단 정보 패널")]
    [SerializeField] private RectTransform topInfoPanel;
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI goldText;
    
    [Header("시너지 시스템")]
    [SerializeField] private RectTransform synergyPanel;
    [SerializeField] private RectTransform synergyContainer;
    
    [Header("시너지 툴팁")]
    [SerializeField] private RectTransform synergyTooltip;
    [SerializeField] private TextMeshProUGUI synergyTitle;
    [SerializeField] private TextMeshProUGUI synergyDesc;
    [SerializeField] private RectTransform unitPortraitContainer;
    
    [Header("UI 매니저들")]
    [SerializeField] private UiShopManager shopManager;
    [SerializeField] private UiUnitManager unitManager;

    // 시너지 정보 저장용 딕셔너리
    private Dictionary<string, SynergyInfo> synergyInfos = new Dictionary<string, SynergyInfo>();

    private void Start()
    {
        // 캔버스가 에디터에서 지정되지 않았다면, 스크립트가 붙은 오브젝트의 Canvas 컴포넌트를 사용
        if (mainCanvas == null)
            mainCanvas = GetComponent<Canvas>();

        InitializeSynergyData();
        EnsureUIComponents();
        SetupUIPositions();
        UpdateUIVisibility();
        
        // UI 매니저들 초기화
        InitializeUIManagers();
        
        // 기본 패널 표시 (대기석 패널)
        SwitchBottomPanel(1);
    }
    
    private void Update()
    {
        // 키보드 입력 처리
        HandleKeyboardInput();
    }
    
    private void HandleKeyboardInput()
    {
        // 숫자 키 1: 대기석(벤치) 패널 표시
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SwitchBottomPanel(1);
        }
        // 숫자 키 2: 상점 패널 표시
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SwitchBottomPanel(2);
        }
        // 숫자 키 3: 캐릭터 정보 패널 표시
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SwitchBottomPanel(3);
        }
    }
    
    private void SwitchBottomPanel(int panelIndex)
    {
        // 이미 활성화된 패널이면 무시
        if (activeBottomPanelIndex == panelIndex)
            return;
            
        // 모든 패널 비활성화
        foreach (var panel in bottomPanels)
        {
            if (panel.Value != null)
                panel.Value.SetActive(false);
        }
        
        // 선택한 패널 활성화
        if (bottomPanels.TryGetValue(panelIndex, out GameObject selectedPanel) && selectedPanel != null)
        {
            selectedPanel.SetActive(true);
            activeBottomPanelIndex = panelIndex;
            
            // 패널에 따른 추가 처리
            if (panelIndex == 3 && unitManager != null)
            {
                // 선택된 유닛 설정 (현재는 테스트용 임시 코드)
                Unit selectedUnit = FindSelectedUnit();
                unitManager.SetSelectedUnit(selectedUnit);
            }
            
            Debug.Log($"패널 전환: {panelIndex}번 패널");
        }
        else
        {
            activeBottomPanelIndex = 0;
            Debug.LogWarning($"패널 {panelIndex}번을 찾을 수 없습니다");
        }
    }

    private void EnsureUIComponents()
    {
        // mainCanvas는 Start()에서 처리됨
        
        // 중앙 하단 패널 컨테이너 생성
        if (bottomPanelContainer == null)
        {
            GameObject containerObj = new GameObject("BottomPanelContainer");
            containerObj.transform.SetParent(mainCanvas.transform, false);
            bottomPanelContainer = containerObj.AddComponent<RectTransform>();
            Image containerBg = containerObj.AddComponent<Image>();
            containerBg.color = new Color(0.1f, 0.1f, 0.1f, 0.4f);
            bottomPanelContainer.sizeDelta = new Vector2(950f, 120f);
        }
        
        // 대기석(벤치) 패널 생성 - 1번 키
        if (benchPanel == null)
        {
            GameObject benchObj = new GameObject("BenchPanel");
            benchObj.transform.SetParent(bottomPanelContainer, false);
            benchPanel = benchObj.AddComponent<RectTransform>();
            Image benchBg = benchObj.AddComponent<Image>();
            benchBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            benchPanel.anchorMin = Vector2.zero;
            benchPanel.anchorMax = Vector2.one;
            benchPanel.offsetMin = Vector2.zero;
            benchPanel.offsetMax = Vector2.zero;
            
            // 임시 UI 요소: 대기석 타이틀
            GameObject titleObj = new GameObject("BenchTitle");
            titleObj.transform.SetParent(benchPanel, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "대기석 (1번 키)";
            titleText.fontSize = 16;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.8f);
            titleRect.anchorMax = new Vector2(0.2f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            // 대기석 슬롯 추가 (추후 구현)
            float slotWidth = 100f;
            float slotSpacing = 10f;
            float firstSlotX = -310f;
            for (int i = 0; i < 8; i++)
            {
                GameObject slotObj = new GameObject($"BenchSlot_{i}");
                slotObj.transform.SetParent(benchPanel, false);
                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(slotWidth, 100f);
                float xPos = firstSlotX + i * (slotWidth + slotSpacing);
                slotRect.anchoredPosition = new Vector2(xPos, 0f);
                Image slotBg = slotObj.AddComponent<Image>();
                slotBg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            
            // 패널 딕셔너리에 추가
            bottomPanels[1] = benchObj;
            benchObj.SetActive(false);
        }
        
        // 상점 패널 생성 - 2번 키
        if (shopPanel == null)
        {
            GameObject shopObj;
            // shopPanelPrefab를 통해 생성 (패널 BG 역할 포함)
            if (shopPanelPrefab != null)
            {
                shopObj = Instantiate(shopPanelPrefab, bottomPanelContainer);
                shopObj.name = "ShopPanel";
                shopPanel = shopObj.GetComponent<RectTransform>();
            }
            else
            {
                shopObj = new GameObject("ShopPanel");
                shopObj.transform.SetParent(bottomPanelContainer, false);
                shopPanel = shopObj.AddComponent<RectTransform>();
                Image shopBg = shopObj.AddComponent<Image>();
                shopBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
                shopPanel.anchorMin = Vector2.zero;
                shopPanel.anchorMax = Vector2.one;
                shopPanel.offsetMin = Vector2.zero;
                shopPanel.offsetMax = Vector2.zero;
            }
            
            // 상점 타이틀
            GameObject titleObj = new GameObject("ShopTitle");
            titleObj.transform.SetParent(shopPanel, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "상점 (2번 키)";
            titleText.fontSize = 16;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.8f);
            titleRect.anchorMax = new Vector2(0.2f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            // 패널 딕셔너리에 추가
            bottomPanels[2] = shopObj;
            shopObj.SetActive(false);
        }
        
        // 새로고침 버튼 생성
        if (refreshButton == null)
        {
            GameObject refreshObj = null;
            if (buttonPrefab != null)
            {
                refreshObj = Instantiate(buttonPrefab, shopPanel);
                refreshObj.name = "RefreshButton";
            }
            else
            {
                refreshObj = new GameObject("RefreshButton");
                refreshObj.transform.SetParent(shopPanel, false);
            }
            refreshButton = refreshObj.GetComponent<Button>() ?? refreshObj.AddComponent<Button>();
            Image refreshBg = refreshObj.GetComponent<Image>() ?? refreshObj.AddComponent<Image>();
            refreshBg.color = new Color(0.2f, 0.6f, 0.9f);
            refreshButton.targetGraphic = refreshBg;
            RectTransform refreshRect = refreshObj.GetComponent<RectTransform>();
            refreshRect.sizeDelta = new Vector2(120f, 40f);
            refreshRect.anchoredPosition = new Vector2(-390f, -25f);
            
            GameObject refreshTextObj = new GameObject("ButtonText");
            refreshTextObj.transform.SetParent(refreshObj.transform, false);
            TextMeshProUGUI refreshBtnText = refreshTextObj.AddComponent<TextMeshProUGUI>();
            refreshBtnText.text = "Refresh";
            refreshBtnText.color = Color.white;
            refreshBtnText.fontSize = 16;
            refreshBtnText.alignment = TextAlignmentOptions.Center;
            RectTransform refreshTextRect = refreshBtnText.GetComponent<RectTransform>();
            refreshTextRect.anchorMin = Vector2.zero;
            refreshTextRect.anchorMax = Vector2.one;
            refreshTextRect.sizeDelta = Vector2.zero;
            
            refreshButton.onClick.AddListener(OnRefreshButtonClick);
        }
        
        // Up 버튼 생성 (왼쪽 배치)
        if (upButton == null)
        {
            GameObject upObj = null;
            if (buttonPrefab != null)
            {
                upObj = Instantiate(buttonPrefab, shopPanel);
                upObj.name = "UpButton";
            }
            else
            {
                upObj = new GameObject("UpButton");
                upObj.transform.SetParent(shopPanel, false);
            }
            upButton = upObj.GetComponent<Button>() ?? upObj.AddComponent<Button>();
            Image upBg = upObj.GetComponent<Image>() ?? upObj.AddComponent<Image>();
            upBg.color = new Color(0.3f, 0.5f, 0.8f);
            upButton.targetGraphic = upBg;
            RectTransform upRect = upObj.GetComponent<RectTransform>();
            upRect.sizeDelta = new Vector2(60f, 40f);
            upRect.anchoredPosition = new Vector2(-420f, 25f);
            
            GameObject upTextObj = new GameObject("ButtonText");
            upTextObj.transform.SetParent(upObj.transform, false);
            TextMeshProUGUI upBtnText = upTextObj.AddComponent<TextMeshProUGUI>();
            upBtnText.text = "Up";
            upBtnText.color = Color.white;
            upBtnText.fontSize = 16;
            upBtnText.alignment = TextAlignmentOptions.Center;
            RectTransform upTextRect = upBtnText.GetComponent<RectTransform>();
            upTextRect.anchorMin = Vector2.zero;
            upTextRect.anchorMax = Vector2.one;
            upTextRect.sizeDelta = Vector2.zero;
            
            upButton.onClick.AddListener(OnUpButtonClick);
        }
        
        // Down 버튼 생성 (오른쪽 배치)
        if (downButton == null)
        {
            GameObject downObj = null;
            if (buttonPrefab != null)
            {
                downObj = Instantiate(buttonPrefab, shopPanel);
                downObj.name = "DownButton";
            }
            else
            {
                downObj = new GameObject("DownButton");
                downObj.transform.SetParent(shopPanel, false);
            }
            downButton = downObj.GetComponent<Button>() ?? downObj.AddComponent<Button>();
            Image downBg = downObj.GetComponent<Image>() ?? downObj.AddComponent<Image>();
            downBg.color = new Color(0.3f, 0.5f, 0.8f);
            downButton.targetGraphic = downBg;
            RectTransform downRect = downObj.GetComponent<RectTransform>();
            downRect.sizeDelta = new Vector2(60f, 40f);
            downRect.anchoredPosition = new Vector2(-360f, 25f);
            
            GameObject downTextObj = new GameObject("ButtonText");
            downTextObj.transform.SetParent(downObj.transform, false);
            TextMeshProUGUI downBtnText = downTextObj.AddComponent<TextMeshProUGUI>();
            downBtnText.text = "Down";
            downBtnText.color = Color.white;
            downBtnText.fontSize = 16;
            downBtnText.alignment = TextAlignmentOptions.Center;
            RectTransform downTextRect = downBtnText.GetComponent<RectTransform>();
            downTextRect.anchorMin = Vector2.zero;
            downTextRect.anchorMax = Vector2.one;
            downTextRect.sizeDelta = Vector2.zero;
            
            downButton.onClick.AddListener(OnDownButtonClick);
        }
        
        // 코스트 범위 텍스트 생성
        if (costRangeText == null)
        {
            GameObject costRangeObj = new GameObject("CostRangeText");
            costRangeObj.transform.SetParent(shopPanel, false);
            costRangeText = costRangeObj.AddComponent<TextMeshProUGUI>();
            costRangeText.text = "Cost <size=26>1</size>~<size=26>3</size>";
            costRangeText.fontSize = 18;
            costRangeText.color = Color.white;
            costRangeText.alignment = TextAlignmentOptions.Left;
            costRangeText.richText = true;
            RectTransform costRangeRect = costRangeText.GetComponent<RectTransform>();
            costRangeRect.sizeDelta = new Vector2(120f, 40f);
            costRangeRect.anchoredPosition = new Vector2(-240f, -25f);
        }
        
        // 유닛 슬롯 생성 (상점 슬롯 프리팹 사용)
        if (shopSlotPrefab != null)
        {
            unitShopSlots = new Button[5];
            float slotSpacing = 10f;
            float firstSlotX = -120f;
            for (int i = 0; i < 5; i++)
            {
                GameObject slotObj = Instantiate(shopSlotPrefab, shopPanel);
                slotObj.name = $"UnitSlot_{i}";
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                // shopSlotPrefab의 권장 사이즈는 100 x 100 (에디터에서 미리 설정)
                float xPos = firstSlotX + i * (slotRect.sizeDelta.x + slotSpacing);
                slotRect.anchoredPosition = new Vector2(xPos, 0f);
                Button slotBtn = slotObj.GetComponent<Button>();
                if (slotBtn == null)
                    slotBtn = slotObj.AddComponent<Button>();
                
                int index = i;
                slotBtn.onClick.AddListener(() => OnUnitSlotClick(index));
                unitShopSlots[i] = slotBtn;
            }
        }
        else
        {
            // shopSlotPrefab이 없으면 기본 생성 로직 사용 (총 5개)
            unitShopSlots = new Button[5];
            float slotWidth = 100f;
            float slotSpacing = 10f;
            float firstSlotX = -120f;
            for (int i = 0; i < 5; i++)
            {
                GameObject slotObj = new GameObject($"UnitSlot_{i}");
                slotObj.transform.SetParent(shopPanel, false);
                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(slotWidth, 100f);
                float xPos = firstSlotX + i * (slotWidth + slotSpacing);
                slotRect.anchoredPosition = new Vector2(xPos, 0f);
                Image slotBg = slotObj.AddComponent<Image>();
                slotBg.color = Color.gray;
                Button slotBtn = slotObj.AddComponent<Button>();
                slotBtn.targetGraphic = slotBg;
                
                int index = i;
                slotBtn.onClick.AddListener(() => OnUnitSlotClick(index));
                unitShopSlots[i] = slotBtn;
            }
        }
        
        // 상단 정보 패널 생성
        if (topInfoPanel == null)
        {
            GameObject topInfoObj = new GameObject("TopInfoPanel");
            topInfoObj.transform.SetParent(mainCanvas.transform, false);
            topInfoPanel = topInfoObj.AddComponent<RectTransform>();

            // 상단 패널 BG Prefab 인스턴스 (별도 배경)
            if (topPanelBGPrefab != null)
            {
                GameObject bg = Instantiate(topPanelBGPrefab, topInfoObj.transform);
                bg.name = "TopPanelBG";
                RectTransform bgRect = bg.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
            }

            // 가로 배치를 위한 Layout Group 추가
            HorizontalLayoutGroup hLayout = topInfoObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 20f;
            hLayout.padding = new RectOffset(0, 0, 5, 5);
            hLayout.childAlignment = TextAnchor.UpperCenter;

            // 라이프 텍스트 생성
            GameObject lifeObj = new GameObject("LifeText");
            lifeObj.transform.SetParent(topInfoPanel, false);
            lifeText = lifeObj.AddComponent<TextMeshProUGUI>();
            lifeText.text = "Life: 100";
            lifeText.fontSize = 24;
            lifeText.color = Color.white;
            lifeText.alignment = TextAlignmentOptions.Center;
            RectTransform lifeRect = lifeText.GetComponent<RectTransform>();
            lifeRect.sizeDelta = new Vector2(100, 40);

            // 라운드 텍스트 생성
            GameObject roundObj = new GameObject("RoundText");
            roundObj.transform.SetParent(topInfoPanel, false);
            roundText = roundObj.AddComponent<TextMeshProUGUI>();
            roundText.text = "Round: 1";
            roundText.fontSize = 24;
            roundText.color = Color.white;
            roundText.alignment = TextAlignmentOptions.Center;
            RectTransform roundRect = roundText.GetComponent<RectTransform>();
            roundRect.sizeDelta = new Vector2(100, 40);

            // 골드 텍스트 생성
            GameObject goldObj = new GameObject("GoldText");
            goldObj.transform.SetParent(topInfoPanel, false);
            goldText = goldObj.AddComponent<TextMeshProUGUI>();
            goldText.text = "Gold: 50";
            goldText.fontSize = 24;
            goldText.color = Color.yellow;
            goldText.alignment = TextAlignmentOptions.Center;
            RectTransform goldRect = goldText.GetComponent<RectTransform>();
            goldRect.sizeDelta = new Vector2(100, 40);
        }
        
        // 시너지 패널 생성
        if (synergyPanel == null)
        {
            GameObject synergyObj = new GameObject("SynergyPanel");
            synergyObj.transform.SetParent(mainCanvas.transform, false);
            synergyPanel = synergyObj.AddComponent<RectTransform>();
            Image synergyBg = synergyObj.AddComponent<Image>();
            synergyBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            synergyPanel.sizeDelta = new Vector2(160f, 400f);
        }
        
        // 시너지 컨테이너 생성
        if (synergyContainer == null)
        {
            GameObject containerObj = new GameObject("SynergyContainer");
            containerObj.transform.SetParent(synergyPanel, false);
            synergyContainer = containerObj.AddComponent<RectTransform>();
            synergyContainer.anchorMin = new Vector2(0, 0);
            synergyContainer.anchorMax = new Vector2(1, 1);
            synergyContainer.sizeDelta = new Vector2(-20f, -20f);
            synergyContainer.anchoredPosition = Vector2.zero;
            
            VerticalLayoutGroup layout = synergyContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // 예시 시너지 아이템 추가 (Akasha, Sentinel)
            CreateSynergyItem("Akasha", 1, 1);
            CreateSynergyItem("Sentinel", 1, 1);
        }
        
        // 시너지 툴팁 생성
        if (synergyTooltip == null)
        {
            GameObject tooltipObj = new GameObject("SynergyTooltip");
            tooltipObj.transform.SetParent(mainCanvas.transform, false);
            synergyTooltip = tooltipObj.AddComponent<RectTransform>();
            Image tooltipBg = tooltipObj.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            synergyTooltip.sizeDelta = new Vector2(300f, 400f);
            synergyTooltip.anchorMin = new Vector2(0f, 0.5f);
            synergyTooltip.anchorMax = new Vector2(0f, 0.5f);
            synergyTooltip.pivot = new Vector2(0f, 0.5f);
            
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(synergyTooltip, false);
            synergyTitle = titleObj.AddComponent<TextMeshProUGUI>();
            synergyTitle.fontSize = 20;
            synergyTitle.fontStyle = FontStyles.Bold;
            synergyTitle.color = Color.white;
            synergyTitle.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = synergyTitle.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(0, 30);
            titleRect.anchoredPosition = new Vector2(0, -15);
            
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(synergyTooltip, false);
            synergyDesc = descObj.AddComponent<TextMeshProUGUI>();
            synergyDesc.fontSize = 16;
            synergyDesc.color = Color.white;
            synergyDesc.alignment = TextAlignmentOptions.Left;
            RectTransform descRect = synergyDesc.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 1);
            descRect.anchorMax = new Vector2(1, 1);
            descRect.sizeDelta = new Vector2(-20, 100);
            descRect.anchoredPosition = new Vector2(0, -65);
            
            GameObject unitContainerObj = new GameObject("UnitContainer");
            unitContainerObj.transform.SetParent(synergyTooltip, false);
            unitPortraitContainer = unitContainerObj.AddComponent<RectTransform>();
            unitPortraitContainer.anchorMin = new Vector2(0, 0);
            unitPortraitContainer.anchorMax = new Vector2(1, 0.7f);
            unitPortraitContainer.sizeDelta = new Vector2(-20, 0);
            unitPortraitContainer.anchoredPosition = new Vector2(0, 20);
            
            HorizontalLayoutGroup unitLayout = unitPortraitContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            unitLayout.spacing = 10f;
            unitLayout.padding = new RectOffset(10, 10, 10, 10);
            unitLayout.childAlignment = TextAnchor.UpperCenter;
            
            synergyTooltip.gameObject.SetActive(false);
        }
    }

private void InitializeUIManagers()
{
    // 상점 매니저 초기화
    if (shopManager == null)
    {
        GameObject shopManagerObj = new GameObject("UiShopManager");
        shopManagerObj.transform.SetParent(transform, false);
        shopManager = shopManagerObj.AddComponent<UiShopManager>();
        
        // 상점 패널 생성 및 설정
        shopManager.InitializeShop(bottomPanelContainer);
        shopManager.SetShopPanelActive(false);
        
        // 패널 딕셔너리에 추가
        bottomPanels[2] = shopManager.GetShopPanel().gameObject;
    }
    
    // 유닛 정보 매니저 초기화
    if (unitManager == null)
    {
        GameObject unitManagerObj = new GameObject("UiUnitManager");
        unitManagerObj.transform.SetParent(transform, false);
        unitManager = unitManagerObj.AddComponent<UiUnitManager>();
        
        // 유닛 정보 패널 생성 및 설정
        unitManager.InitializeUnitInfoPanel(bottomPanelContainer);
        unitManager.SetUnitInfoPanelActive(false);
        
        // 패널 딕셔너리에 추가
        bottomPanels[3] = unitManager.GetUnitInfoPanel().gameObject;
    }
}

    private void SetupUIPositions()
    {
        // 기준 해상도 Full HD (1920 x 1080)를 기준으로 스케일 계산
        float referenceWidth = 1920f;
        float referenceHeight = 1080f;
        float scaleX = Screen.width / referenceWidth;
        float scaleY = Screen.height / referenceHeight;
        
        // 상단 정보 패널 (화면 상단 중앙)
        if (topInfoPanel != null)
        {
            topInfoPanel.anchorMin = new Vector2(0.5f, 1f);
            topInfoPanel.anchorMax = new Vector2(0.5f, 1f);
            topInfoPanel.pivot = new Vector2(0.5f, 1f);
            topInfoPanel.anchoredPosition = new Vector2(0f, -20f * scaleY);
            topInfoPanel.sizeDelta = new Vector2(400f * scaleX, 40f * scaleY);
        }
        
        // 중앙 하단 패널 (화면 하단 중앙)
        if (bottomPanelContainer != null)
        {
            bottomPanelContainer.anchorMin = new Vector2(0.5f, 0f);
            bottomPanelContainer.anchorMax = new Vector2(0.5f, 0f);
            bottomPanelContainer.pivot = new Vector2(0.5f, 0f);
            bottomPanelContainer.anchoredPosition = new Vector2(0f, 15f * scaleY);
            bottomPanelContainer.sizeDelta = new Vector2(950f * scaleX, 120f * scaleY);
        }
        
        // 시너지 패널 (화면 좌측 중앙)
        if (synergyPanel != null)
        {
            synergyPanel.anchorMin = new Vector2(0f, 0.5f);
            synergyPanel.anchorMax = new Vector2(0f, 0.5f);
            synergyPanel.pivot = new Vector2(0f, 0.5f);
            synergyPanel.anchoredPosition = new Vector2(20f * scaleX, 0f);
            synergyPanel.sizeDelta = new Vector2(160f * scaleX, 400f * scaleY);
        }
    }

    private void UpdateUIVisibility()
    {
        bool isPreparationPhase = GameManager.Instance.gameState == BaseEnums.GameState.Preperation;
        if (bottomPanelContainer != null)
            bottomPanelContainer.gameObject.SetActive(isPreparationPhase);
        if (synergyPanel != null)
            synergyPanel.gameObject.SetActive(true);
    }

    public void OnNextRoundBtnClick()
    {
        GameManager.Instance.NextGameState(false);
        // 다음 라운드 버튼은 별도로 관리합니다.
    }

    public void OnRefreshButtonClick()
    {
        Debug.Log("Shop refreshed");
    }

    public void OnUpButtonClick()
    {
        Debug.Log("Cost range increased");
    }

    public void OnDownButtonClick()
    {
        Debug.Log("Cost range decreased");
    }

    public void OnUnitSlotClick(int slotIndex)
    {
        Debug.Log($"Unit slot {slotIndex} clicked");
    }

    // 현재 선택된 유닛 찾기
    private Unit FindSelectedUnit()
    {
        // 실제 구현에서는 게임 내에서 선택된 유닛을 찾는 로직 구현
        // 현재는 테스트를 위해 첫 번째 활성화된 유닛 반환
        
        // 예시: 첫 번째 활성화된 아군 유닛 반환
        if (GameManager.Instance != null)
        {
            foreach (Unit unit in FindObjectsOfType<Unit>())
            {
                if (unit.isActive && !unit.isEnemy)
                    return unit;
            }
        }
        
        return null;
    }

    private void InitializeSynergyData()
    {
        synergyInfos["Akasha"] = new SynergyInfo(
            "Akasha",
            "Increases attack damage by 20% for all allied units.",
            new List<UnitInfo> {
                new UnitInfo("Sei", "Sprit/Portraits/SEI_PORTRAIT")
            }
        );
        
        synergyInfos["Sentinel"] = new SynergyInfo(
            "Sentinel",
            "Increases defense by 100 for all allied units. Sentinel characters gain an additional 200% defense.",
            new List<UnitInfo> {
                new UnitInfo("Sei", "Sprit/Portraits/SEI_PORTRAIT")
            }
        );
    }

    private void CreateSynergyItem(string className, int current, int threshold)
    {
        GameObject synergyItem = new GameObject(className);
        synergyItem.transform.SetParent(synergyContainer, false);
        RectTransform itemRect = synergyItem.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(140f, 40f);
        Image itemBg = synergyItem.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        GameObject nameObj = new GameObject("ClassName");
        nameObj.transform.SetParent(synergyItem.transform, false);
        TextMeshProUGUI classText = nameObj.AddComponent<TextMeshProUGUI>();
        classText.text = className;
        classText.fontSize = 16;
        classText.alignment = TextAlignmentOptions.Left;
        RectTransform nameRect = classText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(0.7f, 0.5f);
        nameRect.sizeDelta = new Vector2(0, 30);
        nameRect.pivot = new Vector2(0, 0.5f);
        nameRect.anchoredPosition = new Vector2(10, 0);
        
        GameObject countObj = new GameObject("CountText");
        countObj.transform.SetParent(synergyItem.transform, false);
        TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
        countText.text = $"{current}/{threshold}";
        countText.fontSize = 16;
        countText.alignment = TextAlignmentOptions.Right;
        countText.color = (current >= threshold) ? Color.green : Color.white;
        RectTransform countRect = countText.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.7f, 0.5f);
        countRect.anchorMax = new Vector2(1f, 0.5f);
        countRect.sizeDelta = new Vector2(0, 30);
        countRect.pivot = new Vector2(1, 0.5f);
        countRect.anchoredPosition = new Vector2(-10, 0);
        
        EventTrigger trigger = synergyItem.AddComponent<EventTrigger>();
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnSynergyPointerEnter(className); });
        trigger.triggers.Add(enterEntry);
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnSynergyPointerExit(); });
        trigger.triggers.Add(exitEntry);
    }

    private void OnSynergyPointerEnter(string className)
    {
        if (synergyInfos.TryGetValue(className, out SynergyInfo info))
        {
            synergyTitle.text = info.Name;
            synergyDesc.text = info.Description;
            foreach (Transform child in unitPortraitContainer)
            {
                Destroy(child.gameObject);
            }
            RectTransform titleRect = synergyTitle.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(0, 30);
            titleRect.anchoredPosition = new Vector2(0, -20);
            
            RectTransform descRect = synergyDesc.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 1);
            descRect.anchorMax = new Vector2(1, 1);
            descRect.sizeDelta = new Vector2(-30, 60);
            descRect.anchoredPosition = new Vector2(0, -60);
            
            unitPortraitContainer.anchorMin = new Vector2(0, 0);
            unitPortraitContainer.anchorMax = new Vector2(1, 1);
            unitPortraitContainer.pivot = new Vector2(0.5f, 0.5f);
            unitPortraitContainer.sizeDelta = new Vector2(-30, -130);
            unitPortraitContainer.anchoredPosition = new Vector2(0, -20); 
            
            GameObject gridObj = new GameObject("PortraitGrid");
            gridObj.transform.SetParent(unitPortraitContainer, false);
            RectTransform gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.sizeDelta = Vector2.zero;
            gridRect.anchoredPosition = new Vector2(0, 10);
            
            GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
            gridLayout.spacing = new Vector2(20, 5);
            gridLayout.cellSize = new Vector2(45, 65);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.padding = new RectOffset(5, 5, 5, 5);
            
            foreach (UnitInfo unit in info.Units)
            {
                GameObject portraitObj = new GameObject(unit.Name);
                portraitObj.transform.SetParent(gridObj.transform, false);
                Image bgImage = portraitObj.AddComponent<Image>();
                bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                GameObject imageObj = new GameObject("Portrait");
                imageObj.transform.SetParent(portraitObj.transform, false);
                Image portrait = imageObj.AddComponent<Image>();
                portrait.sprite = Resources.Load<Sprite>(unit.PortraitPath);
                portrait.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                RectTransform imageRect = portrait.GetComponent<RectTransform>();
                imageRect.anchorMin = new Vector2(0, 0.2f);
                imageRect.anchorMax = new Vector2(1, 1);
                imageRect.sizeDelta = new Vector2(-6, -6);
                imageRect.anchoredPosition = Vector2.zero;
                
                GameObject nameObj = new GameObject("UnitName");
                nameObj.transform.SetParent(portraitObj.transform, false);
                TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.text = unit.Name;
                nameText.fontSize = 10;
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.color = Color.white;
                nameText.overflowMode = TextOverflowModes.Truncate;
                RectTransform nameRect = nameText.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0);
                nameRect.anchorMax = new Vector2(1, 0.2f);
                nameRect.sizeDelta = Vector2.zero;
                nameRect.anchoredPosition = Vector2.zero;
            }
            
            synergyTooltip.sizeDelta = new Vector2(300f, 280f);
            synergyTooltip.anchoredPosition = new Vector2(190f, 0f);
            synergyTooltip.gameObject.SetActive(true);
        }
    }

    private void OnSynergyPointerExit()
    {
        synergyTooltip.gameObject.SetActive(false);
    }

    [System.Serializable]
    private class SynergyInfo
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public List<UnitInfo> Units { get; private set; }
        
        public SynergyInfo(string name, string desc, List<UnitInfo> units)
        {
            Name = name;
            Description = desc;
            Units = units;
        }
    }

    [System.Serializable]
    private class UnitInfo
    {
        public string Name { get; private set; }
        public string PortraitPath { get; private set; }
        
        public UnitInfo(string name, string portraitPath)
        {
            Name = name;
            PortraitPath = portraitPath;
        }
    }
}