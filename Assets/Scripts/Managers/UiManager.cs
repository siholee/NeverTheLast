using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class UiManager : MonoBehaviour
{
  public GameManager gameManager;
  [SerializeField]
  private Button _nextRoundBtn;
  
  [Header("유닛 상점")]
  [SerializeField] private RectTransform shopPanel;
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

  private Canvas mainCanvas;
  
  // 시너지 정보 저장용 딕셔너리
  private Dictionary<string, SynergyInfo> synergyInfos = new Dictionary<string, SynergyInfo>();

  private void Start()
  {
      // 시너지 정보 초기화
      InitializeSynergyData();
      
      // 필요한 UI 요소들 검사 및 생성
      EnsureUIComponents();
      
      // 기본 UI 위치 설정
      SetupUIPositions();
      
      // 게임 상태에 따른 UI 표시 설정
      UpdateUIVisibility();
  }

  private void EnsureUIComponents()
  {
      // 메인 캔버스 찾기 또는 생성
      mainCanvas = FindObjectOfType<Canvas>();
      if (mainCanvas == null)
      {
          GameObject canvasObj = new GameObject("MainCanvas");
          mainCanvas = canvasObj.AddComponent<Canvas>();
          mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
          canvasObj.AddComponent<CanvasScaler>();
          canvasObj.AddComponent<GraphicRaycaster>();
      }
      
      // 상단 정보 패널 생성
      if (topInfoPanel == null)
      {
          GameObject topInfoObj = new GameObject("TopInfoPanel");
          topInfoObj.transform.SetParent(mainCanvas.transform, false);
          
          topInfoPanel = topInfoObj.AddComponent<RectTransform>();
          
          // 가로로 나란히 배치를 위한 Horizontal Layout Group
          HorizontalLayoutGroup hLayout = topInfoObj.AddComponent<HorizontalLayoutGroup>();
          hLayout.spacing = 20f;
          hLayout.padding = new RectOffset(0, 0, 5, 5);
          hLayout.childAlignment = TextAnchor.UpperCenter;
          
          // 남은 라이프 텍스트
          GameObject lifeObj = new GameObject("LifeText");
          lifeObj.transform.SetParent(topInfoPanel, false);
          
          lifeText = lifeObj.AddComponent<TextMeshProUGUI>();
          lifeText.text = "Life: 100";
          lifeText.fontSize = 24;
          lifeText.color = Color.white;
          lifeText.alignment = TextAlignmentOptions.Center;
          
          RectTransform lifeRect = lifeText.GetComponent<RectTransform>();
          lifeRect.sizeDelta = new Vector2(100, 40);
          
          // 현재 라운드 텍스트
          GameObject roundObj = new GameObject("RoundText");
          roundObj.transform.SetParent(topInfoPanel, false);
          
          roundText = roundObj.AddComponent<TextMeshProUGUI>();
          roundText.text = "Round: 1";
          roundText.fontSize = 24;
          roundText.color = Color.white;
          roundText.alignment = TextAlignmentOptions.Center;
          
          RectTransform roundRect = roundText.GetComponent<RectTransform>();
          roundRect.sizeDelta = new Vector2(100, 40);
          
          // 골드 텍스트
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

      // 상점 패널 생성
      if (shopPanel == null)
      {
          GameObject shopObj = new GameObject("ShopPanel");
          shopObj.transform.SetParent(mainCanvas.transform, false);
          
          shopPanel = shopObj.AddComponent<RectTransform>();
          Image shopBg = shopObj.AddComponent<Image>();
          shopBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
          
          // 상점 패널 설정 - 너비를 충분히 늘림
          shopPanel.sizeDelta = new Vector2(950f, 120f);  // 900f에서 950f로 늘림
      }
      
      // 새로고침 버튼 생성
      if (refreshButton == null)
      {
          GameObject refreshObj = new GameObject("RefreshButton");
          refreshObj.transform.SetParent(shopPanel, false);
          
          refreshButton = refreshObj.AddComponent<Button>();
          Image refreshBg = refreshObj.AddComponent<Image>();
          refreshBg.color = new Color(0.2f, 0.6f, 0.9f);      
          refreshButton.targetGraphic = refreshBg;
          
          RectTransform refreshRect = refreshObj.GetComponent<RectTransform>();
          refreshRect.sizeDelta = new Vector2(120f, 40f);
          refreshRect.anchoredPosition = new Vector2(-390f, -25f);  // 위치 조정
          
          // 버튼 텍스트
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
          
          // 버튼 이벤트 연결
          refreshButton.onClick.AddListener(OnRefreshButtonClick);
      }
      
      // Up 버튼 생성 (왼쪽에 배치)
      if (upButton == null)
      {
          GameObject upObj = new GameObject("UpButton");
          upObj.transform.SetParent(shopPanel, false);
          
          upButton = upObj.AddComponent<Button>();
          Image upBg = upObj.AddComponent<Image>();
          upBg.color = new Color(0.3f, 0.5f, 0.8f);
          upButton.targetGraphic = upBg;
          
          RectTransform upRect = upObj.GetComponent<RectTransform>();
          upRect.sizeDelta = new Vector2(60f, 40f);
          upRect.anchoredPosition = new Vector2(-420f, 25f);  // x 위치 조정
          
          // 버튼 텍스트
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
          
          // 버튼 이벤트 연결
          upButton.onClick.AddListener(OnUpButtonClick);
      }
      
      // Down 버튼 생성 (오른쪽에 배치)
      if (downButton == null)
      {
          GameObject downObj = new GameObject("DownButton");
          downObj.transform.SetParent(shopPanel, false);
          
          downButton = downObj.AddComponent<Button>();
          Image downBg = downObj.AddComponent<Image>();
          downBg.color = new Color(0.3f, 0.5f, 0.8f);     
          downButton.targetGraphic = downBg;
          
          RectTransform downRect = downObj.GetComponent<RectTransform>();
          downRect.sizeDelta = new Vector2(60f, 40f);
          downRect.anchoredPosition = new Vector2(-360f, 25f);  // x 위치 조정
          
          // 버튼 텍스트
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
          
          // 버튼 이벤트 연결
          downButton.onClick.AddListener(OnDownButtonClick);
      }
      
      // 코스트 범위 텍스트 생성
      if (costRangeText == null)
      {
          GameObject costRangeObj = new GameObject("CostRangeText");
          costRangeObj.transform.SetParent(shopPanel, false);
          
          costRangeText = costRangeObj.AddComponent<TextMeshProUGUI>();
          
          // Cost X~Y 텍스트에서 X와 Y 부분의 폰트 크기를 키우기 위한 Rich Text 사용
          costRangeText.text = "Cost <size=26>1</size>~<size=26>3</size>";
          costRangeText.fontSize = 18;
          costRangeText.color = Color.white;
          costRangeText.alignment = TextAlignmentOptions.Left;
          costRangeText.richText = true; // Rich Text를 활성화하여 크기 태그 사용
          
          RectTransform costRangeRect = costRangeText.GetComponent<RectTransform>();
          costRangeRect.sizeDelta = new Vector2(120f, 40f);  // 높이를 버튼과 동일하게
          costRangeRect.anchoredPosition = new Vector2(-240f, -25f);  // 리프레시 버튼과 같은 높이에 배치
      }
      
      // 유닛 슬롯 생성
      if (unitShopSlots == null || unitShopSlots.Length == 0)
      {
          unitShopSlots = new Button[5]; // TFT처럼 5개의 유닛 슬롯
          float slotWidth = 100f;
          float slotSpacing = 10f;
          
          // 모든 요소의 간격을 동일하게 설정
          float spacing = 30f;
          
          // 첫 번째 슬롯의 위치 조정 (코스트 텍스트와의 간격을 유지하며)
          float firstSlotX = -120f; // 코스트 텍스트와의 간격을 고려한 위치
          
          for (int i = 0; i < 5; i++)
          {
              GameObject slotObj = new GameObject($"UnitSlot_{i}");
              slotObj.transform.SetParent(shopPanel, false);
              
              RectTransform slotRect = slotObj.AddComponent<RectTransform>();
              slotRect.sizeDelta = new Vector2(slotWidth, 100f);
              
              // 이전 슬롯으로부터 적절한 간격 유지
              float xPos = firstSlotX + i * (slotWidth + slotSpacing);
              slotRect.anchoredPosition = new Vector2(xPos, 0f);
              
              Image slotBg = slotObj.AddComponent<Image>();
              slotBg.color = Color.gray;
              
              Button slotBtn = slotObj.AddComponent<Button>();
              slotBtn.targetGraphic = slotBg;
              
              // 텍스트 추가 (유닛 가격 표시용)
              GameObject textObj = new GameObject("CostText");
              textObj.transform.SetParent(slotObj.transform, false);
              
              TextMeshProUGUI costText = textObj.AddComponent<TextMeshProUGUI>();
              costText.text = $"{i+1}G";
              costText.color = Color.yellow;
              costText.fontSize = 16;
              costText.alignment = TextAlignmentOptions.Bottom;
              
              RectTransform textRect = costText.GetComponent<RectTransform>();
              textRect.anchorMin = new Vector2(0, 0);
              textRect.anchorMax = new Vector2(1, 1);
              textRect.sizeDelta = Vector2.zero;
              
              // 버튼 이벤트 연결
              int index = i; // 클로저를 위해 인덱스 복사
              slotBtn.onClick.AddListener(() => OnUnitSlotClick(index));
              
              unitShopSlots[i] = slotBtn;
          }
      }
      
      // 시너지 패널 생성
      if (synergyPanel == null)
      {
          GameObject synergyObj = new GameObject("SynergyPanel");
          synergyObj.transform.SetParent(mainCanvas.transform, false);
          
          synergyPanel = synergyObj.AddComponent<RectTransform>();
          Image synergyBg = synergyObj.AddComponent<Image>();
          synergyBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
          
          // 시너지 패널 설정 (너비 축소)
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
          
          // Vertical Layout Group 추가
          VerticalLayoutGroup layout = synergyContainer.gameObject.AddComponent<VerticalLayoutGroup>();
          layout.spacing = 10f;
          layout.padding = new RectOffset(5, 5, 5, 5);
          layout.childAlignment = TextAnchor.UpperCenter;
          layout.childControlWidth = true;
          layout.childControlHeight = false;
          layout.childForceExpandWidth = true;
          layout.childForceExpandHeight = false;
          
          // 예시 시너지 아이템 추가 (Akasha, Sentinel로 변경)
          CreateSynergyItem("Akasha", 1);
          CreateSynergyItem("Sentinel", 1);
      }
      
      // 시너지 툴팁 생성
      if (synergyTooltip == null)
      {
          GameObject tooltipObj = new GameObject("SynergyTooltip");
          tooltipObj.transform.SetParent(mainCanvas.transform, false);
          
          synergyTooltip = tooltipObj.AddComponent<RectTransform>();
          Image tooltipBg = tooltipObj.AddComponent<Image>();
          tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
          
          // 툴팁 패널 설정
          synergyTooltip.sizeDelta = new Vector2(300f, 400f);
          synergyTooltip.anchorMin = new Vector2(0f, 0.5f);
          synergyTooltip.anchorMax = new Vector2(0f, 0.5f);
          synergyTooltip.pivot = new Vector2(0f, 0.5f);
          
          // 툴팁 제목
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
          
          // 툴팁 설명
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
          
          // 유닛 초상화 컨테이너
          GameObject unitContainerObj = new GameObject("UnitContainer");
          unitContainerObj.transform.SetParent(synergyTooltip, false);
          
          unitPortraitContainer = unitContainerObj.AddComponent<RectTransform>();
          unitPortraitContainer.anchorMin = new Vector2(0, 0);
          unitPortraitContainer.anchorMax = new Vector2(1, 0.7f);
          unitPortraitContainer.sizeDelta = new Vector2(-20, 0);
          unitPortraitContainer.anchoredPosition = new Vector2(0, 20);
          
          // 가로 정렬 그룹 추가
          HorizontalLayoutGroup unitLayout = unitPortraitContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
          unitLayout.spacing = 10f;
          unitLayout.padding = new RectOffset(10, 10, 10, 10);
          unitLayout.childAlignment = TextAnchor.UpperCenter;
          
          // 기본적으로 툴팁 숨기기
          synergyTooltip.gameObject.SetActive(false);
      }
  }
  
  private void CreateSynergyItem(string className, int current, int threshold)
  {
      GameObject synergyItem = new GameObject(className);
      synergyItem.transform.SetParent(synergyContainer, false);
      
      RectTransform itemRect = synergyItem.AddComponent<RectTransform>();
      itemRect.sizeDelta = new Vector2(140f, 40f);  // 너비 축소
      
      // 배경
      Image itemBg = synergyItem.AddComponent<Image>();
      itemBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
      
      // 클래스 이름 텍스트
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
      
      // 현재/목표 텍스트
      GameObject countObj = new GameObject("CountText");
      countObj.transform.SetParent(synergyItem.transform, false);
      
      TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
      countText.text = $"{current}/{threshold}";
      countText.fontSize = 16;
      countText.alignment = TextAlignmentOptions.Right;
      
      // 시너지 활성화 여부에 따라 색상 설정
      countText.color = (current >= threshold) ? Color.green : Color.white;
      
      RectTransform countRect = countText.GetComponent<RectTransform>();
      countRect.anchorMin = new Vector2(0.7f, 0.5f);
      countRect.anchorMax = new Vector2(1f, 0.5f);
      countRect.sizeDelta = new Vector2(0, 30);
      countRect.pivot = new Vector2(1, 0.5f);
      countRect.anchoredPosition = new Vector2(-10, 0);
  }
  
  private void SetupUIPositions()
  {
      // 상단 정보 패널 위치 설정
      if (topInfoPanel != null)
      {
          topInfoPanel.anchorMin = new Vector2(0.5f, 1f);
          topInfoPanel.anchorMax = new Vector2(0.5f, 1f);
          topInfoPanel.pivot = new Vector2(0.5f, 1f);
          topInfoPanel.anchoredPosition = new Vector2(0f, -20f);
          topInfoPanel.sizeDelta = new Vector2(400f, 40f);
      }
      
      // 상점 패널 위치 설정 (화면 하단 중앙, 조금 더 낮게 배치)
      if (shopPanel != null)
      {
          shopPanel.anchorMin = new Vector2(0.5f, 0f);
          shopPanel.anchorMax = new Vector2(0.5f, 0f);
          shopPanel.pivot = new Vector2(0.5f, 0f);
          shopPanel.anchoredPosition = new Vector2(0f, 15f);  // 조금 더 낮게 조정 (20에서 15로)
      }
      
      // 시너지 패널 위치 설정 (화면 좌측)
      if (synergyPanel != null)
      {
          synergyPanel.anchorMin = new Vector2(0f, 0.5f);
          synergyPanel.anchorMax = new Vector2(0f, 0.5f);
          synergyPanel.pivot = new Vector2(0f, 0.5f);
          synergyPanel.anchoredPosition = new Vector2(20f, 0f);
      }
  }
  
  private void UpdateUIVisibility()
  {
      // 게임 상태에 따라 상점 패널의 표시 여부 결정
      bool isPreparationPhase = GameManager.Instance.gameState == BaseEnums.GameState.Preperation;
      
      if (shopPanel != null)
          shopPanel.gameObject.SetActive(isPreparationPhase);
          
      // 시너지 패널은 항상 표시
      if (synergyPanel != null)
          synergyPanel.gameObject.SetActive(true);
  }
  
  public void OnNextRoundBtnClick()
  {
    GameManager.Instance.NextGameState(false);
    _nextRoundBtn.gameObject.SetActive(false);
  }
  
  // 상점 UI용 더미 메소드들
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

   private void InitializeSynergyData()
  {
      // 시너지 정보 설정 (실제 게임에서는 DataManager에서 불러올 수 있음)
      synergyInfos["Akasha"] = new SynergyInfo(
          "Akasha",
          "Increases attack damage by 20% for all allied units.",
          new List<UnitInfo> {
              new UnitInfo("Sei", "Sprit/Portraits/SEI_PORTRAIT"),
          }
      );
      
      synergyInfos["Sentinel"] = new SynergyInfo(
          "Sentinel",
          "Increases defense by 100 for all allied units. Sentinel characters gain an additional 200% defense.",
          new List<UnitInfo> {
              new UnitInfo("Sei", "Sprit/Portraits/SEI_PORTRAIT"),
          }
      );
  }

  private void CreateSynergyItem(string className, int current)
  {
      GameObject synergyItem = new GameObject(className);
      synergyItem.transform.SetParent(synergyContainer, false);
      
      RectTransform itemRect = synergyItem.AddComponent<RectTransform>();
      itemRect.sizeDelta = new Vector2(140f, 40f);  // 너비 축소
      
      // 배경
      Image itemBg = synergyItem.AddComponent<Image>();
      itemBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
      
      // 클래스 이름 텍스트
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
      
      // 현재 배치 수 텍스트 (최대치 제거)
      GameObject countObj = new GameObject("CountText");
      countObj.transform.SetParent(synergyItem.transform, false);
      
      TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
      countText.text = current.ToString();  // 최대치 제거하고 현재 수만 표시
      countText.fontSize = 16;
      countText.alignment = TextAlignmentOptions.Right;
      countText.color = Color.white;
      
      RectTransform countRect = countText.GetComponent<RectTransform>();
      countRect.anchorMin = new Vector2(0.7f, 0.5f);
      countRect.anchorMax = new Vector2(1f, 0.5f);
      countRect.sizeDelta = new Vector2(0, 30);
      countRect.pivot = new Vector2(1, 0.5f);
      countRect.anchoredPosition = new Vector2(-10, 0);
      
      // 마우스 이벤트 추가
      EventTrigger trigger = synergyItem.AddComponent<EventTrigger>();
      
      // 마우스 Enter 이벤트
      EventTrigger.Entry enterEntry = new EventTrigger.Entry();
      enterEntry.eventID = EventTriggerType.PointerEnter;
      enterEntry.callback.AddListener((data) => { OnSynergyPointerEnter(className); });
      trigger.triggers.Add(enterEntry);
      
      // 마우스 Exit 이벤트
      EventTrigger.Entry exitEntry = new EventTrigger.Entry();
      exitEntry.eventID = EventTriggerType.PointerExit;
      exitEntry.callback.AddListener((data) => { OnSynergyPointerExit(); });
      trigger.triggers.Add(exitEntry);
  }

  // 마우스가 시너지 아이템 위로 올라갔을 때 
private void OnSynergyPointerEnter(string className)
{
    if (synergyInfos.TryGetValue(className, out SynergyInfo info))
    {
        // 툴팁 내용 업데이트
        synergyTitle.text = info.Name;
        synergyDesc.text = info.Description;
        foreach (Transform child in unitPortraitContainer)
        {
            Destroy(child.gameObject);
        }
        // 1. 제목 
        RectTransform titleRect = synergyTitle.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -20);
        
        // 2. 설명 
        RectTransform descRect = synergyDesc.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.sizeDelta = new Vector2(-30, 60);
        descRect.anchoredPosition = new Vector2(0, -60);
        
        // 3. Portrait 컨테이너 
        unitPortraitContainer.anchorMin = new Vector2(0, 0);
        unitPortraitContainer.anchorMax = new Vector2(1, 1);
        unitPortraitContainer.pivot = new Vector2(0.5f, 0.5f);
        unitPortraitContainer.sizeDelta = new Vector2(-30, -130);
        unitPortraitContainer.anchoredPosition = new Vector2(0, -20); 
        
        // Portrait 그리드 레이아웃 
        GameObject gridObj = new GameObject("PortraitGrid");
        gridObj.transform.SetParent(unitPortraitContainer, false);
        
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.sizeDelta = Vector2.zero;
        gridRect.anchoredPosition = new Vector2(0, 10); // 그리드를 약간 위로 이동
        
        // 그리드 레이아웃 설정 (현재 4x3 그리드)
        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.spacing = new Vector2(20, 5);
        gridLayout.cellSize = new Vector2(45, 65); // 셀 크기 축소 (4열 맞춤)
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;  // 4열 그리드로 변경
        gridLayout.padding = new RectOffset(5, 5, 5, 5); // 약간의 패딩 추가
        
        // 각 유닛 초상화 추가
        foreach (UnitInfo unit in info.Units)
        {
            GameObject portraitObj = new GameObject(unit.Name);
            portraitObj.transform.SetParent(gridObj.transform, false);
            
            // 초상화 배경 설정
            Image bgImage = portraitObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // 유닛 초상화 추가
            GameObject imageObj = new GameObject("Portrait");
            imageObj.transform.SetParent(portraitObj.transform, false);
            
            Image portrait = imageObj.AddComponent<Image>();
            // 실제 리소스가 있다면 로드, 없으면 더미 이미지 사용
            portrait.sprite = Resources.Load<Sprite>(unit.PortraitPath);
            portrait.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            
            RectTransform imageRect = portrait.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0, 0.2f);
            imageRect.anchorMax = new Vector2(1, 1);
            imageRect.sizeDelta = new Vector2(-6, -6); // 패딩 축소
            imageRect.anchoredPosition = Vector2.zero;
            
            // 유닛 이름 추가 (작은 글씨로 변경)
            GameObject nameObj = new GameObject("UnitName");
            nameObj.transform.SetParent(portraitObj.transform, false);
            
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = unit.Name;
            nameText.fontSize = 10; // 글씨 크기 축소
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.overflowMode = TextOverflowModes.Truncate;
            
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0.2f);
            nameRect.sizeDelta = Vector2.zero;
            nameRect.anchoredPosition = Vector2.zero;
        }
        
        // 툴팁 크기 설정 (좀 더 높게 조정하여 3행의 초상화를 표시)
        synergyTooltip.sizeDelta = new Vector2(300f, 280f);
        synergyTooltip.anchoredPosition = new Vector2(190f, 0f);
        
        // 툴팁 활성화
        synergyTooltip.gameObject.SetActive(true);
    }
}
  // 마우스가 시너지 아이템에서 벗어났을 때
  private void OnSynergyPointerExit()
  {
      // 툴팁 숨기기
      synergyTooltip.gameObject.SetActive(false);
  }

  // 시너지 정보를 관리하기 위한 클래스
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

  // 유닛 정보를 관리하기 위한 클래스
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