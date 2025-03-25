using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UiShopManager : MonoBehaviour
{
    [Header("상점 프리팹")]
    [SerializeField] private GameObject shopPanelPrefab;  // 상점 패널 프리팹
    [SerializeField] private GameObject shopSlotPrefab;   // 상점 슬롯 프리팹
    [SerializeField] private GameObject buttonPrefab;     // 버튼 프리팹

    [Header("상점 UI 요소")]
    [SerializeField] private RectTransform shopPanel;     // 상점 패널 자체
    [SerializeField] private Button[] unitShopSlots;      // 상점 슬롯 버튼들
    [SerializeField] private Button refreshButton;        // 새로고침 버튼
    [SerializeField] private Button upButton;             // 코스트 상향 버튼
    [SerializeField] private Button downButton;           // 코스트 하향 버튼
    [SerializeField] private TextMeshProUGUI costRangeText; // 코스트 범위 텍스트

    [Header("상점 설정")]
    [SerializeField] private int shopSlotCount = 5;       // 상점 슬롯 개수
    [SerializeField] private int minCost = 1;             // 최소 코스트
    [SerializeField] private int maxCost = 3;             // 최대 코스트
    [SerializeField] private int refreshCost = 2;         // 새로고침 비용

    // 상점에 표시할 유닛 데이터
    private List<ShopUnitData> shopUnits = new List<ShopUnitData>();
    
    // 현재 코스트 범위
    private int currentMinCost = 1;
    private int currentMaxCost = 3;

    private void Awake()
    {
        // 초기화
        InitializeShop();
    }

    // 매개변수 없는 버전의 초기화 메서드 추가
public void InitializeShop()
{
    // 기본 초기화: 부모를 자신의 트랜스폼으로 설정
    InitializeShop(transform);
}


    private void Start()
    {
        // 초기 상점 유닛 생성
        RefreshShop(false);
    }

public void InitializeShop(Transform parentContainer)
{
    // 상점 패널이 없으면 생성
    if (shopPanel == null)
    {
        GameObject shopObj;
        if (shopPanelPrefab != null)
        {
            shopObj = Instantiate(shopPanelPrefab, parentContainer);
            shopObj.name = "ShopPanel";
            shopPanel = shopObj.GetComponent<RectTransform>();
        }
        else
        {
            shopObj = new GameObject("ShopPanel");
            shopObj.transform.SetParent(parentContainer, false);
            shopPanel = shopObj.AddComponent<RectTransform>();
            Image shopBg = shopObj.AddComponent<Image>();
            shopBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            
            // 동일한 패널 레이아웃을 위한 앵커 설정
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
            refreshBtnText.text = $"Refresh ({refreshCost}G)";
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
            upBtnText.text = "▲";
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
            downBtnText.text = "▼";
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
            costRangeText.text = $"Cost <size=26>{currentMinCost}</size>~<size=26>{currentMaxCost}</size>";
            costRangeText.fontSize = 18;
            costRangeText.color = Color.white;
            costRangeText.alignment = TextAlignmentOptions.Left;
            costRangeText.richText = true;
            RectTransform costRangeRect = costRangeText.GetComponent<RectTransform>();
            costRangeRect.sizeDelta = new Vector2(120f, 40f);
            costRangeRect.anchoredPosition = new Vector2(-240f, -25f);
        }
        
        // 유닛 슬롯 생성
        InitializeShopSlots();
    }

    // 상점 슬롯 초기화
    private void InitializeShopSlots()
    {
        unitShopSlots = new Button[shopSlotCount];
        
        if (shopSlotPrefab != null)
        {
            float slotSpacing = 10f;
            float firstSlotX = -120f;
            
            // 기존 슬롯 제거
            for (int i = 0; i < shopPanel.childCount; i++)
            {
                Transform child = shopPanel.GetChild(i);
                if (child.name.StartsWith("UnitSlot_"))
                {
                    Destroy(child.gameObject);
                }
            }
            
            // 새 슬롯 생성
            for (int i = 0; i < shopSlotCount; i++)
            {
                GameObject slotObj = Instantiate(shopSlotPrefab, shopPanel);
                slotObj.name = $"UnitSlot_{i}";
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                
                float xPos = firstSlotX + i * (slotRect.sizeDelta.x + slotSpacing);
                slotRect.anchoredPosition = new Vector2(xPos, 0f);
                
                Button slotBtn = slotObj.GetComponent<Button>();
                if (slotBtn == null)
                    slotBtn = slotObj.AddComponent<Button>();
                
                int index = i;
                slotBtn.onClick.AddListener(() => OnUnitSlotClick(index));
                unitShopSlots[i] = slotBtn;
                
                // 가격 텍스트 추가
                GameObject priceObj = new GameObject("PriceText");
                priceObj.transform.SetParent(slotObj.transform, false);
                TextMeshProUGUI priceText = priceObj.AddComponent<TextMeshProUGUI>();
                priceText.fontSize = 16;
                priceText.color = Color.yellow;
                priceText.alignment = TextAlignmentOptions.Center;
                RectTransform priceRect = priceText.GetComponent<RectTransform>();
                priceRect.anchorMin = new Vector2(0, 0);
                priceRect.anchorMax = new Vector2(1, 0.2f);
                priceRect.offsetMin = Vector2.zero;
                priceRect.offsetMax = Vector2.zero;
                
                // 유닛 이름 텍스트 추가
                GameObject nameObj = new GameObject("NameText");
                nameObj.transform.SetParent(slotObj.transform, false);
                TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.fontSize = 14;
                nameText.color = Color.white;
                nameText.alignment = TextAlignmentOptions.Center;
                RectTransform nameRect = nameText.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.8f);
                nameRect.anchorMax = new Vector2(1, 1f);
                nameRect.offsetMin = Vector2.zero;
                nameRect.offsetMax = Vector2.zero;
                
                // 유닛 아이콘 이미지 추가
                GameObject iconObj = new GameObject("UnitIcon");
                iconObj.transform.SetParent(slotObj.transform, false);
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.color = Color.white;
                RectTransform iconRect = iconImage.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.2f);
                iconRect.anchorMax = new Vector2(0.9f, 0.8f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }
        }
        else
        {
            // 프리팹이 없는 경우 기본 슬롯 생성
            float slotWidth = 100f;
            float slotSpacing = 10f;
            float firstSlotX = -120f;
            
            for (int i = 0; i < shopSlotCount; i++)
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
                
                // 가격 텍스트 추가
                GameObject priceObj = new GameObject("PriceText");
                priceObj.transform.SetParent(slotObj.transform, false);
                TextMeshProUGUI priceText = priceObj.AddComponent<TextMeshProUGUI>();
                priceText.fontSize = 16;
                priceText.color = Color.yellow;
                priceText.alignment = TextAlignmentOptions.Center;
                RectTransform priceRect = priceText.GetComponent<RectTransform>();
                priceRect.anchorMin = new Vector2(0, 0);
                priceRect.anchorMax = new Vector2(1, 0.2f);
                priceRect.offsetMin = Vector2.zero;
                priceRect.offsetMax = Vector2.zero;
                
                // 유닛 이름 텍스트 추가
                GameObject nameObj = new GameObject("NameText");
                nameObj.transform.SetParent(slotObj.transform, false);
                TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.fontSize = 14;
                nameText.color = Color.white;
                nameText.alignment = TextAlignmentOptions.Center;
                RectTransform nameRect = nameText.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.8f);
                nameRect.anchorMax = new Vector2(1, 1f);
                nameRect.offsetMin = Vector2.zero;
                nameRect.offsetMax = Vector2.zero;
                
                // 유닛 아이콘 이미지 추가
                GameObject iconObj = new GameObject("UnitIcon");
                iconObj.transform.SetParent(slotObj.transform, false);
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.color = Color.white;
                RectTransform iconRect = iconImage.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.2f);
                iconRect.anchorMax = new Vector2(0.9f, 0.8f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }
        }
    }

    // 상점 새로고침
    public void RefreshShop(bool payGold = true)
    {
        if (payGold)
        {
            // 골드 차감 로직 (GameManager에서 처리)
            if (!TrySpendGold(refreshCost))
            {
                Debug.Log("골드가 부족합니다!");
                return;
            }
        }

        // 현재 코스트 범위 내의 유닛만 표시
        GenerateRandomUnits();
        UpdateShopUI();
        
        Debug.Log($"상점 새로고침 완료 (코스트 범위: {currentMinCost}~{currentMaxCost})");
    }

    // 랜덤 유닛 생성
    private void GenerateRandomUnits()
    {
        // 기존 유닛 목록 초기화
        shopUnits.Clear();
        
        // 현재 코스트 범위 내에서 랜덤 유닛 생성
        for (int i = 0; i < shopSlotCount; i++)
        {
            int cost = Random.Range(currentMinCost, currentMaxCost + 1);
            
            // 임시 테스트용 더미 유닛 (실제 게임에서는 UnitManager나 다른 매니저로부터 데이터를 받아와야 함)
            ShopUnitData unit = new ShopUnitData
            {
                UnitId = $"Unit_{i}_{cost}",
                UnitName = $"유닛 {i}",
                Cost = cost,
                IconPath = "UI/DefaultUnitIcon" // 기본 아이콘 경로 (Resources 폴더 내)
            };
            
            shopUnits.Add(unit);
        }
    }

    // 상점 UI 업데이트
    private void UpdateShopUI()
    {
        // 코스트 범위 텍스트 업데이트
        costRangeText.text = $"Cost <size=26>{currentMinCost}</size>~<size=26>{currentMaxCost}</size>";
        
        // 각 슬롯 업데이트
        for (int i = 0; i < unitShopSlots.Length; i++)
        {
            Transform slotTransform = unitShopSlots[i].transform;
            
            // 해당 인덱스의 유닛 데이터가 있을 경우 표시
            if (i < shopUnits.Count)
            {
                ShopUnitData unit = shopUnits[i];
                
                // 가격 텍스트 업데이트
                TextMeshProUGUI priceText = slotTransform.Find("PriceText").GetComponent<TextMeshProUGUI>();
                priceText.text = $"{unit.Cost}G";
                
                // 이름 텍스트 업데이트
                TextMeshProUGUI nameText = slotTransform.Find("NameText").GetComponent<TextMeshProUGUI>();
                nameText.text = unit.UnitName;
                
                // 아이콘 이미지 업데이트
                Image iconImage = slotTransform.Find("UnitIcon").GetComponent<Image>();
                iconImage.sprite = Resources.Load<Sprite>(unit.IconPath) ?? 
                                   Resources.Load<Sprite>("UI/DefaultUnitIcon"); // 기본 아이콘
                iconImage.color = Color.white;
                
                // 슬롯 활성화
                unitShopSlots[i].interactable = true;
            }
            else
            {
                // 빈 슬롯 처리
                TextMeshProUGUI priceText = slotTransform.Find("PriceText").GetComponent<TextMeshProUGUI>();
                priceText.text = "";
                
                TextMeshProUGUI nameText = slotTransform.Find("NameText").GetComponent<TextMeshProUGUI>();
                nameText.text = "";
                
                Image iconImage = slotTransform.Find("UnitIcon").GetComponent<Image>();
                iconImage.sprite = null;
                iconImage.color = new Color(0, 0, 0, 0);
                
                // 슬롯 비활성화
                unitShopSlots[i].interactable = false;
            }
        }
    }

    // 코스트 상승 버튼 클릭
    public void OnUpButtonClick()
    {
        if (currentMinCost < minCost + 2 && currentMaxCost < maxCost + 2)
        {
            currentMinCost++;
            currentMaxCost++;
            UpdateCostRangeText();
            Debug.Log($"코스트 범위 상승: {currentMinCost}~{currentMaxCost}");
        }
        else
        {
            Debug.Log("최대 코스트 범위입니다.");
        }
    }

    // 코스트 하락 버튼 클릭
    public void OnDownButtonClick()
    {
        if (currentMinCost > minCost && currentMaxCost > maxCost)
        {
            currentMinCost--;
            currentMaxCost--;
            UpdateCostRangeText();
            Debug.Log($"코스트 범위 하락: {currentMinCost}~{currentMaxCost}");
        }
        else
        {
            Debug.Log("최소 코스트 범위입니다.");
        }
    }

    // 새로고침 버튼 클릭
    public void OnRefreshButtonClick()
    {
        RefreshShop(true);
    }

    // 유닛 슬롯 클릭
    public void OnUnitSlotClick(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= shopUnits.Count)
            return;
            
        ShopUnitData selectedUnit = shopUnits[slotIndex];
        
        // 유닛 구매 처리
        if (TryBuyUnit(selectedUnit))
        {
            Debug.Log($"유닛 구매: {selectedUnit.UnitName} (코스트: {selectedUnit.Cost})");
            
            // 구매 후 해당 슬롯 비우기
            shopUnits.RemoveAt(slotIndex);
            UpdateShopUI();
        }
        else
        {
            Debug.Log($"유닛 구매 실패: 골드 부족 (필요: {selectedUnit.Cost})");
        }
    }

    // 코스트 범위 텍스트 업데이트
    private void UpdateCostRangeText()
    {
        costRangeText.text = $"Cost <size=26>{currentMinCost}</size>~<size=26>{currentMaxCost}</size>";
    }

    // 유닛 구매 시도
    private bool TryBuyUnit(ShopUnitData unit)
    {
        // 골드 차감 로직 (GameManager에서 처리)
        return TrySpendGold(unit.Cost);
    }

    // 골드 차감 처리 (GameManager와 연동)
    private bool TrySpendGold(int amount)
    {
        // 임시 로직 (실제로는 GameManager 등에서 처리)
        bool hasEnoughGold = true; // 일단 무조건 성공으로 처리
        
        // GameManager에 구현 예정
        /*
        if (GameManager.Instance != null)
        {
            hasEnoughGold = GameManager.Instance.TrySpendGold(amount);
        }
        */
        
        return hasEnoughGold;
    }
    
    // 상점 패널 활성화/비활성화
    public void SetShopPanelActive(bool active)
    {
        if (shopPanel != null)
            shopPanel.gameObject.SetActive(active);
    }
    
    // 상점 패널 참조 가져오기
    public RectTransform GetShopPanel()
    {
        return shopPanel;
    }

    // 상점 유닛 데이터 클래스
    [System.Serializable]
    public class ShopUnitData
    {
        public string UnitId;    // 유닛 고유 ID
        public string UnitName;  // 유닛 이름
        public int Cost;         // 유닛 가격
        public string IconPath;  // 아이콘 경로 (Resources 폴더 기준)
    }
}