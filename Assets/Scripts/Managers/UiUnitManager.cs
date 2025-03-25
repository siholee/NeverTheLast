using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class UiUnitManager : MonoBehaviour
{
    [Header("Unit Info Panel")]
    [SerializeField] private RectTransform unitInfoPanel;
    [SerializeField] private Image unitPortrait;
    [SerializeField] private TextMeshProUGUI unitNameText;
    
    [Header("Unit Stats Container")]
    [SerializeField] private RectTransform statsContainer;
    
    [Header("Unit Stat Items")]
    [SerializeField] private List<StatItem> statItems = new List<StatItem>();
    
    [Header("Tooltip Settings")]
    [SerializeField] private RectTransform statTooltip;
    [SerializeField] private TextMeshProUGUI tooltipTitleText;
    [SerializeField] private TextMeshProUGUI tooltipBaseText;
    [SerializeField] private TextMeshProUGUI tooltipAdditiveText;
    [SerializeField] private TextMeshProUGUI tooltipMultiplicativeText;
    [SerializeField] private TextMeshProUGUI tooltipTotalText;
    
    // Current selected unit
    private Unit selectedUnit;
    
    // Stat item caching dictionary
    private Dictionary<StatType, StatItem> statItemDict = new Dictionary<StatType, StatItem>();
    
    private void Awake()
    {
        InitializeUnitInfoPanel();
    }

    // 매개변수 없는 버전의 초기화 메서드 추가
public void InitializeUnitInfoPanel()
{
    // 기본 초기화: 부모를 자신의 트랜스폼으로 설정
    InitializeUnitInfoPanel(transform);
}
    
    private void Start()
    {
        // Hide tooltip on initialization
        if (statTooltip != null)
            statTooltip.gameObject.SetActive(false);
    }
    
public void InitializeUnitInfoPanel(Transform parentContainer)
{
    if (unitInfoPanel == null)
    {
        GameObject panelObj = new GameObject("UnitInfoPanel");
        panelObj.transform.SetParent(parentContainer, false);
        unitInfoPanel = panelObj.AddComponent<RectTransform>();
        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        
        // 패널 위치/앵커 설정 - 동일한 패널 레이아웃을 위한 설정
        unitInfoPanel.anchorMin = Vector2.zero;
        unitInfoPanel.anchorMax = Vector2.one;
        unitInfoPanel.offsetMin = Vector2.zero;
        unitInfoPanel.offsetMax = Vector2.zero;
            
            // Unit title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(unitInfoPanel, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Character Info (Key 3)";
            titleText.fontSize = 16;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.8f);
            titleRect.anchorMax = new Vector2(0.2f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            // Unit portrait area
            GameObject portraitContainerObj = new GameObject("PortraitContainer");
            portraitContainerObj.transform.SetParent(unitInfoPanel, false);
            RectTransform portraitContainer = portraitContainerObj.AddComponent<RectTransform>();
            portraitContainer.anchorMin = new Vector2(0, 0);
            portraitContainer.anchorMax = new Vector2(0, 1);
            portraitContainer.pivot = new Vector2(0, 0.5f);
            portraitContainer.sizeDelta = new Vector2(100, 0);
            portraitContainer.anchoredPosition = new Vector2(20, 0);
            
            // Unit portrait image
            GameObject portraitObj = new GameObject("Portrait");
            portraitObj.transform.SetParent(portraitContainer, false);
            unitPortrait = portraitObj.AddComponent<Image>();
            unitPortrait.preserveAspect = true;
            RectTransform portraitRect = unitPortrait.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0, 0);
            portraitRect.anchorMax = new Vector2(1, 1);
            portraitRect.offsetMin = new Vector2(5, 15);
            portraitRect.offsetMax = new Vector2(-5, -25);
            
            // Unit name text
            GameObject nameObj = new GameObject("UnitName");
            nameObj.transform.SetParent(portraitContainer, false);
            unitNameText = nameObj.AddComponent<TextMeshProUGUI>();
            unitNameText.fontSize = 14;
            unitNameText.fontStyle = FontStyles.Bold;
            unitNameText.color = Color.white;
            unitNameText.alignment = TextAlignmentOptions.Center;
            RectTransform nameRect = unitNameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0);
            nameRect.pivot = new Vector2(0.5f, 0);
            nameRect.sizeDelta = new Vector2(0, 20);
            nameRect.anchoredPosition = new Vector2(0, 5);
            
            // Stats container
            GameObject statsObj = new GameObject("StatsContainer");
            statsObj.transform.SetParent(unitInfoPanel, false);
            statsContainer = statsObj.AddComponent<RectTransform>();
            statsContainer.anchorMin = new Vector2(0, 0);
            statsContainer.anchorMax = new Vector2(1, 1);
            statsContainer.pivot = new Vector2(0.5f, 0.5f);
            statsContainer.offsetMin = new Vector2(130, 10);
            statsContainer.offsetMax = new Vector2(-20, -30);
            
            // Stats grid layout
            GridLayoutGroup gridLayout = statsObj.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(130, 35);
            gridLayout.spacing = new Vector2(15, 5);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;
            
            // Create stat items
            CreateStatItems();
        }
        
        // Create tooltip
        if (statTooltip == null)
        {
            GameObject tooltipObj = new GameObject("StatTooltip");
            tooltipObj.transform.SetParent(transform, false);
            statTooltip = tooltipObj.AddComponent<RectTransform>();
            Image tooltipBg = tooltipObj.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            statTooltip.sizeDelta = new Vector2(250, 150);
            statTooltip.pivot = new Vector2(0, 1);
            
            // Tooltip content
            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(statTooltip, false);
            tooltipTitleText = titleObj.AddComponent<TextMeshProUGUI>();
            tooltipTitleText.fontSize = 16;
            tooltipTitleText.fontStyle = FontStyles.Bold;
            tooltipTitleText.color = Color.white;
            tooltipTitleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = tooltipTitleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(0, 25);
            titleRect.anchoredPosition = new Vector2(0, -12.5f);
            
            // Base stat
            GameObject baseObj = new GameObject("BaseText");
            baseObj.transform.SetParent(statTooltip, false);
            tooltipBaseText = baseObj.AddComponent<TextMeshProUGUI>();
            tooltipBaseText.fontSize = 14;
            tooltipBaseText.color = Color.white;
            tooltipBaseText.alignment = TextAlignmentOptions.Left;
            RectTransform baseRect = tooltipBaseText.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(0, 1);
            baseRect.anchorMax = new Vector2(1, 1);
            baseRect.sizeDelta = new Vector2(-20, 20);
            baseRect.anchoredPosition = new Vector2(10, -35);
            
            // Additive stat
            GameObject additiveObj = new GameObject("AdditiveText");
            additiveObj.transform.SetParent(statTooltip, false);
            tooltipAdditiveText = additiveObj.AddComponent<TextMeshProUGUI>();
            tooltipAdditiveText.fontSize = 14;
            tooltipAdditiveText.color = new Color(0.5f, 1f, 0.5f);
            tooltipAdditiveText.alignment = TextAlignmentOptions.Left;
            RectTransform additiveRect = tooltipAdditiveText.GetComponent<RectTransform>();
            additiveRect.anchorMin = new Vector2(0, 1);
            additiveRect.anchorMax = new Vector2(1, 1);
            additiveRect.sizeDelta = new Vector2(-20, 20);
            additiveRect.anchoredPosition = new Vector2(10, -55);
            
            // Multiplicative stat
            GameObject multiObj = new GameObject("MultiplicativeText");
            multiObj.transform.SetParent(statTooltip, false);
            tooltipMultiplicativeText = multiObj.AddComponent<TextMeshProUGUI>();
            tooltipMultiplicativeText.fontSize = 14;
            tooltipMultiplicativeText.color = new Color(1f, 0.7f, 0.3f);
            tooltipMultiplicativeText.alignment = TextAlignmentOptions.Left;
            RectTransform multiRect = tooltipMultiplicativeText.GetComponent<RectTransform>();
            multiRect.anchorMin = new Vector2(0, 1);
            multiRect.anchorMax = new Vector2(1, 1);
            multiRect.sizeDelta = new Vector2(-20, 20);
            multiRect.anchoredPosition = new Vector2(10, -75);
            
            // Divider
            GameObject lineObj = new GameObject("Divider");
            lineObj.transform.SetParent(statTooltip, false);
            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            RectTransform lineRect = lineImage.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0, 1);
            lineRect.anchorMax = new Vector2(1, 1);
            lineRect.sizeDelta = new Vector2(-40, 1);
            lineRect.anchoredPosition = new Vector2(0, -95);
            
            // Total stat
            GameObject totalObj = new GameObject("TotalText");
            totalObj.transform.SetParent(statTooltip, false);
            tooltipTotalText = totalObj.AddComponent<TextMeshProUGUI>();
            tooltipTotalText.fontSize = 16;
            tooltipTotalText.fontStyle = FontStyles.Bold;
            tooltipTotalText.color = Color.yellow;
            tooltipTotalText.alignment = TextAlignmentOptions.Left;
            RectTransform totalRect = tooltipTotalText.GetComponent<RectTransform>();
            totalRect.anchorMin = new Vector2(0, 1);
            totalRect.anchorMax = new Vector2(1, 1);
            totalRect.sizeDelta = new Vector2(-20, 25);
            totalRect.anchoredPosition = new Vector2(10, -115);
            
            // Initially hide the tooltip
            statTooltip.gameObject.SetActive(false);
        }
    }
    
    private void CreateStatItems()
    {
        // Create stat items and add to Dictionary
        statItems.Clear();
        statItemDict.Clear();
        
        // HP stat
        StatItem hpItem = CreateStatItem("HP", StatType.HP, Color.green);
        statItems.Add(hpItem);
        statItemDict.Add(StatType.HP, hpItem);
        
        // Attack stat
        StatItem atkItem = CreateStatItem("Attack", StatType.ATK, new Color(1f, 0.3f, 0.3f));
        statItems.Add(atkItem);
        statItemDict.Add(StatType.ATK, atkItem);
        
        // Defense stat
        StatItem defItem = CreateStatItem("Defense", StatType.DEF, new Color(0.3f, 0.6f, 1f));
        statItems.Add(defItem);
        statItemDict.Add(StatType.DEF, defItem);
        
        // Crit chance stat
        StatItem critChanceItem = CreateStatItem("Crit Chance", StatType.CRIT_CHANCE, new Color(1f, 0.8f, 0.2f));
        statItems.Add(critChanceItem);
        statItemDict.Add(StatType.CRIT_CHANCE, critChanceItem);
        
        // Crit damage stat
        StatItem critDamageItem = CreateStatItem("Crit Damage", StatType.CRIT_DAMAGE, new Color(1f, 0.5f, 0f));
        statItems.Add(critDamageItem);
        statItemDict.Add(StatType.CRIT_DAMAGE, critDamageItem);
        
        // Cooldown reduction stat
        StatItem cooldownItem = CreateStatItem("Cooldown Reduction", StatType.COOLDOWN, new Color(0.6f, 0.4f, 1f));
        statItems.Add(cooldownItem);
        statItemDict.Add(StatType.COOLDOWN, cooldownItem);
    }
    
    private StatItem CreateStatItem(string name, StatType type, Color iconColor)
    {
        GameObject itemObj = new GameObject($"StatItem_{type}");
        itemObj.transform.SetParent(statsContainer, false);
        RectTransform itemRect = itemObj.AddComponent<RectTransform>();
        
        // Background image
        Image bgImage = itemObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        // Icon area
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(itemObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0);
        iconRect.anchorMax = new Vector2(0, 1);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.sizeDelta = new Vector2(30, 0);
        iconRect.anchoredPosition = new Vector2(5, 0);
        
        // Icon image
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = iconColor;
        iconImage.sprite = GetIconForStatType(type);
        if (iconImage.sprite == null)
        {
            // Default icon (circle shape)
            GameObject defaultIcon = new GameObject("DefaultIcon");
            defaultIcon.transform.SetParent(iconObj.transform, false);
            Image defaultIconImage = defaultIcon.AddComponent<Image>();
            defaultIconImage.color = iconColor;
            RectTransform defaultIconRect = defaultIconImage.GetComponent<RectTransform>();
            defaultIconRect.anchorMin = Vector2.zero;
            defaultIconRect.anchorMax = Vector2.one;
            defaultIconRect.offsetMin = new Vector2(5, 5);
            defaultIconRect.offsetMax = new Vector2(-5, -5);
        }
        
        // Stat name text
        GameObject nameObj = new GameObject("StatName");
        nameObj.transform.SetParent(itemObj.transform, false);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = name;
        nameText.fontSize = 12;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Left;
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(40, 0);
        nameRect.offsetMax = new Vector2(-5, 0);
        
        // Stat value text
        GameObject valueObj = new GameObject("StatValue");
        valueObj.transform.SetParent(itemObj.transform, false);
        TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "0";
        valueText.fontSize = 14;
        valueText.color = iconColor;
        valueText.alignment = TextAlignmentOptions.Right;
        RectTransform valueRect = valueText.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0, 0);
        valueRect.anchorMax = new Vector2(1, 0.5f);
        valueRect.offsetMin = new Vector2(40, 0);
        valueRect.offsetMax = new Vector2(-5, 0);
        
        // Add event trigger
        EventTrigger trigger = itemObj.AddComponent<EventTrigger>();
        
        // Mouse enter event
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnStatPointerEnter(type, data as PointerEventData); });
        trigger.triggers.Add(enterEntry);
        
        // Mouse exit event
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnStatPointerExit(); });
        trigger.triggers.Add(exitEntry);
        
        return new StatItem {
            Type = type,
            ItemObject = itemObj,
            NameText = nameText,
            ValueText = valueText,
            IconImage = iconImage
        };
    }
    
    // Set selected unit and update UI
    public void SetSelectedUnit(Unit unit)
    {
        selectedUnit = unit;
        UpdateUnitInfo();
    }
    
    // Update unit information
    public void UpdateUnitInfo()
    {
        if (selectedUnit == null)
        {
            // If no unit is selected
            if (unitNameText != null)
                unitNameText.text = "No Unit Selected";
                
            if (unitPortrait != null)
                unitPortrait.sprite = null;
                
            foreach (StatItem item in statItems)
            {
                item.ValueText.text = "-";
            }
            
            return;
        }
        
        // Set unit name
        if (unitNameText != null)
            unitNameText.text = $"{selectedUnit.unitName} Lv.{selectedUnit.level}";
            
        // Set unit portrait (get sprite from Cell)
        if (unitPortrait != null && selectedUnit.currentCell != null)
            unitPortrait.sprite = selectedUnit.currentCell.portraitRenderer.sprite;
            
        // Set stat values
        if (statItemDict.ContainsKey(StatType.HP))
            statItemDict[StatType.HP].ValueText.text = $"{selectedUnit.hpCurr}/{selectedUnit.hpMax}";
            
        if (statItemDict.ContainsKey(StatType.ATK))
            statItemDict[StatType.ATK].ValueText.text = selectedUnit.atkCurr.ToString();
            
        if (statItemDict.ContainsKey(StatType.DEF))
            statItemDict[StatType.DEF].ValueText.text = selectedUnit.defCurr.ToString();
            
        if (statItemDict.ContainsKey(StatType.CRIT_CHANCE))
            statItemDict[StatType.CRIT_CHANCE].ValueText.text = $"{selectedUnit.critChanceCurr * 100:F1}%";
            
        if (statItemDict.ContainsKey(StatType.CRIT_DAMAGE))
            statItemDict[StatType.CRIT_DAMAGE].ValueText.text = $"{selectedUnit.critMultiplierCurr * 100:F1}%";
            
        if (statItemDict.ContainsKey(StatType.COOLDOWN))
            statItemDict[StatType.COOLDOWN].ValueText.text = $"{(selectedUnit.codeAcceleration - 1) * 100:F1}%";
    }
    
    // Show stat tooltip
    private void OnStatPointerEnter(StatType statType, PointerEventData eventData)
    {
        if (selectedUnit == null || statTooltip == null)
            return;
            
        // Set tooltip position
        Vector2 position = eventData.position;
        position.y -= 10;
        statTooltip.position = position;
        
        // Set values and description based on stat type
        switch (statType)
        {
            case StatType.HP:
                SetTooltipContent(
                    "HP",
                    $"Base: {selectedUnit.GetBaseHp()}",
                    GetAdditiveModifierText("HP"),
                    GetMultiplicativeModifierText("HP"),
                    $"Total: {selectedUnit.hpMax} (Current: {selectedUnit.hpCurr})"
                );
                break;
                
            case StatType.ATK:
                SetTooltipContent(
                    "Attack",
                    $"Base: {selectedUnit.GetBaseAtk()}",
                    GetAdditiveModifierText("Attack"),
                    GetMultiplicativeModifierText("Attack"),
                    $"Total: {selectedUnit.atkCurr}"
                );
                break;
                
            case StatType.DEF:
                SetTooltipContent(
                    "Defense",
                    $"Base: {selectedUnit.GetBaseDef()}",
                    GetAdditiveModifierText("Defense"),
                    GetMultiplicativeModifierText("Defense"),
                    $"Total: {selectedUnit.defCurr}"
                );
                break;
                
            case StatType.CRIT_CHANCE:
                SetTooltipContent(
                    "Crit Chance",
                    $"Base: {selectedUnit.GetBaseCritChance() * 100:F1}%",
                    GetAdditiveModifierText("Crit Chance"),
                    GetMultiplicativeModifierText("Crit Chance"),
                    $"Total: {selectedUnit.critChanceCurr * 100:F1}%"
                );
                break;
                
            case StatType.CRIT_DAMAGE:
                SetTooltipContent(
                    "Crit Damage",
                    $"Base: {selectedUnit.GetBaseCritDamage() * 100:F1}%",
                    GetAdditiveModifierText("Crit Damage"),
                    GetMultiplicativeModifierText("Crit Damage"),
                    $"Total: {selectedUnit.critMultiplierCurr * 100:F1}%"
                );
                break;
                
            case StatType.COOLDOWN:
                SetTooltipContent(
                    "Cooldown Reduction",
                    "Base: 0%",
                    GetAdditiveModifierText("Cooldown Reduction"),
                    GetMultiplicativeModifierText("Cooldown Reduction"),
                    $"Total: {(selectedUnit.codeAcceleration - 1) * 100:F1}%"
                );
                break;
        }
        
        // Show tooltip
        statTooltip.gameObject.SetActive(true);
    }
    
    // Set tooltip content
    private void SetTooltipContent(string title, string baseText, string additiveText, string multiplicativeText, string totalText)
    {
        tooltipTitleText.text = title;
        tooltipBaseText.text = baseText;
        tooltipAdditiveText.text = additiveText;
        tooltipMultiplicativeText.text = multiplicativeText;
        tooltipTotalText.text = totalText;
        
        // Adjust tooltip size (hide empty sections)
        float height = 130;
        
        if (string.IsNullOrEmpty(additiveText))
        {
            tooltipAdditiveText.gameObject.SetActive(false);
            height -= 20;
        }
        else
        {
            tooltipAdditiveText.gameObject.SetActive(true);
        }
        
        if (string.IsNullOrEmpty(multiplicativeText))
        {
            tooltipMultiplicativeText.gameObject.SetActive(false);
            height -= 20;
        }
        else
        {
            tooltipMultiplicativeText.gameObject.SetActive(true);
        }
        
        statTooltip.sizeDelta = new Vector2(250, height);
    }
    
    // Hide tooltip
    private void OnStatPointerExit()
    {
        if (statTooltip != null)
            statTooltip.gameObject.SetActive(false);
    }
    
    // Generate additive modifier text
    private string GetAdditiveModifierText(string statName)
    {
        // In an actual game, get additive modifiers from Unit.statusEffects
        // Currently using dummy data
        return $"Added: +0";
    }
    
    // Generate multiplicative modifier text
    private string GetMultiplicativeModifierText(string statName)
    {
        // In an actual game, get multiplicative modifiers from Unit.statusEffects
        // Currently using dummy data
        return $"Multiplier: +0%";
    }
    
    // Get icon for stat type
    private Sprite GetIconForStatType(StatType type)
    {
        // Actually load icon resource
        // string iconPath = $"UI/Icons/{type.ToString().ToLower()}";
        // return Resources.Load<Sprite>(iconPath);
        return null; // Use default shape if no icon
    }
    
    // Show/hide unit info panel
    public void SetUnitInfoPanelActive(bool active)
    {
        if (unitInfoPanel != null)
            unitInfoPanel.gameObject.SetActive(active);
    }
    
    // Get panel reference
    public RectTransform GetUnitInfoPanel()
    {
        return unitInfoPanel;
    }
    
    // Stat type enum
    public enum StatType
    {
        HP,
        ATK,
        DEF,
        CRIT_CHANCE,
        CRIT_DAMAGE,
        COOLDOWN
    }
    
    // Stat item class
    [System.Serializable]
    public class StatItem
    {
        public StatType Type;
        public GameObject ItemObject;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI ValueText;
        public Image IconImage;
    }
}