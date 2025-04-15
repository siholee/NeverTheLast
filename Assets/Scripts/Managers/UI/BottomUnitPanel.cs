using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BaseClasses;
using Entities;

namespace Managers.UI
{
    public class BottomUnitPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        
        [Header("Unit Info")]
        [SerializeField] private Image unitPortrait;
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI unitDescriptionText;
        [SerializeField] private TextMeshProUGUI unitStatsText;
        
        [Header("Unit Actions")]
        [SerializeField] private Button sellButton;
        [SerializeField] private Button upgradeButton;
        
        [Header("Stat Details")]
        [SerializeField] private GameObject statTooltip;
        [SerializeField] private TextMeshProUGUI tooltipTitleText;
        [SerializeField] private TextMeshProUGUI tooltipContentText;
        
        private Unit selectedUnit;
        
        public void Initialize(RectTransform parentRect)
        {
            // RectTransform check and setup
            if (panelRect == null)
            {
                panelRect = GetComponent<RectTransform>();
                if (panelRect == null)
                {
                    Debug.LogWarning("BottomUnitPanel missing RectTransform, adding one.");
                    panelRect = gameObject.AddComponent<RectTransform>();
                }
                
                // Position within parent
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                
                // Create title
                CreateTitle("Unit Info (Key 2)");
                
                // Create unit info UI
                CreateUnitInfoUI();
                
                // Create stat tooltip
                CreateStatTooltip();
            }
            
            // Hide tooltip initially
            if (statTooltip != null)
                statTooltip.SetActive(false);
        }
        
        private void CreateTitle(string titleText)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(transform, false);
            
            // Explicitly add RectTransform
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f);
            titleRect.anchorMax = new Vector2(0.2f, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = titleText;
            title.fontSize = 16;
            title.alignment = TextAlignmentOptions.Left;
        }
        
        private void CreateUnitInfoUI()
        {
            // Portrait area (left)
            GameObject portraitObj = new GameObject("UnitPortrait");
            portraitObj.transform.SetParent(transform, false);
            
            // Explicitly add RectTransform
            RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.05f, 0.2f);
            portraitRect.anchorMax = new Vector2(0.2f, 0.8f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            
            unitPortrait = portraitObj.AddComponent<Image>();
            unitPortrait.color = new Color(0.3f, 0.3f, 0.3f);
            
            // Info area (center)
            GameObject infoObj = new GameObject("UnitInfo");
            infoObj.transform.SetParent(transform, false);
            
            // Explicitly add RectTransform
            RectTransform infoRect = infoObj.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.25f, 0.1f);
            infoRect.anchorMax = new Vector2(0.7f, 0.9f);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;
            
            // Add vertical layout
            VerticalLayoutGroup infoLayout = infoObj.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 10f;
            infoLayout.childAlignment = TextAnchor.UpperLeft;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.padding = new RectOffset(10, 10, 10, 10);
            
            // Unit name
            GameObject nameObj = new GameObject("UnitName");
            nameObj.transform.SetParent(infoObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(0, 30);
            
            unitNameText = nameObj.AddComponent<TextMeshProUGUI>();
            unitNameText.text = "Unit Name";
            unitNameText.fontSize = 18;
            unitNameText.fontStyle = FontStyles.Bold;
            unitNameText.alignment = TextAlignmentOptions.Left;
            
            // Unit description
            GameObject descObj = new GameObject("UnitDescription");
            descObj.transform.SetParent(infoObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.sizeDelta = new Vector2(0, 60);
            
            unitDescriptionText = descObj.AddComponent<TextMeshProUGUI>();
            unitDescriptionText.text = "Unit description will appear here.";
            unitDescriptionText.fontSize = 14;
            unitDescriptionText.alignment = TextAlignmentOptions.Left;
            
            // Unit stats - add hover events
            GameObject statsObj = new GameObject("UnitStats");
            statsObj.transform.SetParent(infoObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform statsRect = statsObj.AddComponent<RectTransform>();
            statsRect.sizeDelta = new Vector2(0, 90);
            
            unitStatsText = statsObj.AddComponent<TextMeshProUGUI>();
            unitStatsText.text = "HP: 0/0\nATK: 0\nDEF: 0\nATK Speed: 0\nCrit Rate: 0%\nCrit DMG: 0%";
            unitStatsText.fontSize = 14;
            unitStatsText.alignment = TextAlignmentOptions.Left;
            
            // Set up hover events
            EventTrigger statsTrigger = statsObj.AddComponent<EventTrigger>();
            
            // Mouse enter event
            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { OnStatsPointerEnter((PointerEventData)data); });
            statsTrigger.triggers.Add(enterEntry);
            
            // Mouse exit event
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { OnStatsPointerExit((PointerEventData)data); });
            statsTrigger.triggers.Add(exitEntry);
            
            // Action area (right)
            GameObject actionObj = new GameObject("UnitActions");
            actionObj.transform.SetParent(transform, false);
            
            // Explicitly add RectTransform
            RectTransform actionRect = actionObj.AddComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0.75f, 0.2f);
            actionRect.anchorMax = new Vector2(0.95f, 0.8f);
            actionRect.offsetMin = Vector2.zero;
            actionRect.offsetMax = Vector2.zero;
            
            // Add vertical layout
            VerticalLayoutGroup actionLayout = actionObj.AddComponent<VerticalLayoutGroup>();
            actionLayout.spacing = 20f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = false;
            actionLayout.padding = new RectOffset(10, 10, 10, 10);
            
            // Sell button
            sellButton = CreateActionButton(actionObj.transform, "Sell", "Sell", () => OnSellButtonClick());
            
            // Upgrade button
            upgradeButton = CreateActionButton(actionObj.transform, "Upgrade", "Upgrade", () => OnUpgradeButtonClick());
        }
        
        private void CreateStatTooltip()
        {
            // Tooltip background
            GameObject tooltipObj = new GameObject("StatTooltip");
            tooltipObj.transform.SetParent(transform, false);
            statTooltip = tooltipObj;
            
            // Explicitly add RectTransform
            RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.25f, 0.25f);
            tooltipRect.anchorMax = new Vector2(0.75f, 0.75f);
            tooltipRect.offsetMin = Vector2.zero;
            tooltipRect.offsetMax = Vector2.zero;
            
            Image tooltipBg = tooltipObj.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            // Add vertical layout
            VerticalLayoutGroup tooltipLayout = tooltipObj.AddComponent<VerticalLayoutGroup>();
            tooltipLayout.spacing = 10f;
            tooltipLayout.childAlignment = TextAnchor.UpperCenter;
            tooltipLayout.childForceExpandWidth = true;
            tooltipLayout.childForceExpandHeight = false;
            tooltipLayout.padding = new RectOffset(15, 15, 15, 15);
            
            // Tooltip title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(tooltipObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 30);
            
            tooltipTitleText = titleObj.AddComponent<TextMeshProUGUI>();
            tooltipTitleText.text = "Stat Details";
            tooltipTitleText.fontSize = 18;
            tooltipTitleText.fontStyle = FontStyles.Bold;
            tooltipTitleText.alignment = TextAlignmentOptions.Center;
            
            // Tooltip content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(tooltipObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, 200);
            
            tooltipContentText = contentObj.AddComponent<TextMeshProUGUI>();
            tooltipContentText.text = "Stat breakdown will appear here.";
            tooltipContentText.fontSize = 14;
            tooltipContentText.alignment = TextAlignmentOptions.Left;
        }
        
        private Button CreateActionButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);
            
            // Explicitly add RectTransform
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0, 40);
            
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.9f);
            
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(action);
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            // Explicitly add RectTransform
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = text;
            buttonText.fontSize = 16;
            buttonText.alignment = TextAlignmentOptions.Center;
            
            return button;
        }
        
        // Event handlers
        private void OnSellButtonClick()
        {
            if (selectedUnit != null)
            {
                Debug.Log($"Sell {selectedUnit.UnitName} clicked");
                // Add sell logic
            }
        }
        
        private void OnUpgradeButtonClick()
        {
            if (selectedUnit != null)
            {
                Debug.Log($"Upgrade {selectedUnit.UnitName} clicked");
                // Add upgrade logic
            }
        }
        
        // Show stat tooltip
        private void OnStatsPointerEnter(PointerEventData eventData)
        {
            if (selectedUnit != null && statTooltip != null)
            {
                UpdateTooltipContent();
                statTooltip.SetActive(true);
            }
        }
        
        // Hide stat tooltip
        private void OnStatsPointerExit(PointerEventData eventData)
        {
            if (statTooltip != null)
            {
                statTooltip.SetActive(false);
            }
        }
        
        // Update tooltip content
        private void UpdateTooltipContent()
        {
            if (selectedUnit == null) return;
            
            // Calculate base stats and bonuses
            int baseHp = selectedUnit.HpBase + (selectedUnit.Level - 1) * selectedUnit.HpIncrementLvl;
            int totalHp = selectedUnit.HpMax;
            int bonusHp = totalHp - baseHp;
            
            int baseAtk = selectedUnit.AtkBase + (selectedUnit.Level - 1) * selectedUnit.AtkIncrementLvl;
            int totalAtk = selectedUnit.AtkCurr;
            int bonusAtk = totalAtk - baseAtk;
            
            int baseDef = selectedUnit.DefBase + (selectedUnit.Level - 1) * selectedUnit.DefIncrementLvl;
            int totalDef = selectedUnit.DefCurr;
            int bonusDef = totalDef - baseDef;
            
            float baseCrit = selectedUnit.CritChanceBase + (selectedUnit.Level - 1) * selectedUnit.CritChanceIncrementLvl;
            float totalCrit = selectedUnit.CritChanceCurr;
            float bonusCrit = totalCrit - baseCrit;
            
            float baseCritDmg = selectedUnit.CritMultiplierBase + (selectedUnit.Level - 1) * selectedUnit.CritMultiplierIncrementLvl;
            float totalCritDmg = selectedUnit.CritMultiplierCurr;
            float bonusCritDmg = totalCritDmg - baseCritDmg;
            
            float codeAccel = selectedUnit.CodeAcceleration;
            
            string content = "";
            content += $"<b>HP:</b> {selectedUnit.HpCurr}/{selectedUnit.HpMax}\n";
            content += $"  Base: {baseHp}";
            if (bonusHp != 0) content += $" + Buff: {(bonusHp > 0 ? "+" : "")}{bonusHp}";
            content += "\n\n";
            
            content += $"<b>ATK:</b> {totalAtk}\n";
            content += $"  Base: {baseAtk}";
            if (bonusAtk != 0) content += $" + Buff: {(bonusAtk > 0 ? "+" : "")}{bonusAtk}";
            content += "\n\n";
            
            content += $"<b>DEF:</b> {totalDef}\n";
            content += $"  Base: {baseDef}";
            if (bonusDef != 0) content += $" + Buff: {(bonusDef > 0 ? "+" : "")}{bonusDef}";
            content += "\n\n";
            
            content += $"<b>ATK Speed:</b> {codeAccel:F2}x\n\n";
            
            content += $"<b>Crit Rate:</b> {totalCrit * 100:F1}%\n";
            content += $"  Base: {baseCrit * 100:F1}%";
            if (bonusCrit != 0) content += $" + Buff: {(bonusCrit > 0 ? "+" : "")}{bonusCrit * 100:F1}%";
            content += "\n\n";
            
            content += $"<b>Crit DMG:</b> {totalCritDmg * 100:F1}%\n";
            content += $"  Base: {baseCritDmg * 100:F1}%";
            if (bonusCritDmg != 0) content += $" + Buff: {(bonusCritDmg > 0 ? "+" : "")}{bonusCritDmg * 100:F1}%";
            
            tooltipTitleText.text = $"{selectedUnit.UnitName} (Level {selectedUnit.Level}) Stats";
            tooltipContentText.text = content;
        }
        
        // Called when game state changes
        public void OnGameStateChanged(BaseEnums.GameState state)
        {
            // Add necessary handling
        }
        
        // Set the selected unit
        public void SetSelectedUnit(Unit unit)
        {
            selectedUnit = unit;
            
            if (unit != null && unit.isActive)
            {
                Debug.Log($"Setting selected unit to {unit.UnitName}");
                
                // Update UI elements
                unitNameText.text = $"{unit.UnitName} (Level {unit.Level})";
                unitDescriptionText.text = "Unit description"; // Unit class has no Description property
                
                // Display stats
                string statsText = $"HP: {unit.HpCurr}/{unit.HpMax}\n";
                statsText += $"ATK: {unit.AtkCurr}\n";
                statsText += $"DEF: {unit.DefCurr}\n";
                statsText += $"ATK Speed: {unit.CodeAcceleration:F2}x\n";
                statsText += $"Crit Rate: {unit.CritChanceCurr * 100:F1}%\n";
                statsText += $"Crit DMG: {unit.CritMultiplierCurr * 100:F1}%";
                
                unitStatsText.text = statsText;
                
                // Update portrait image
                if (!string.IsNullOrEmpty(unit.PortraitPath))
                {
                    Sprite portrait = Resources.Load<Sprite>(unit.PortraitPath);
                    if (portrait != null)
                        unitPortrait.sprite = portrait;
                }
                else
                {
                    unitPortrait.sprite = null;
                    unitPortrait.color = new Color(0.3f, 0.3f, 0.3f);
                }
                
                // Enable buttons
                sellButton.interactable = true;
                
                // Enable upgrade button if level < 3
                bool canUpgrade = unit.Level < 3;
                upgradeButton.interactable = canUpgrade;
            }
            else
            {
                Debug.Log("No unit selected or unit is inactive");
                
                // Reset to default values when no unit selected
                unitNameText.text = "No Unit Selected";
                unitDescriptionText.text = "Select a unit to view its information.";
                unitStatsText.text = "HP: -/-\nATK: -\nDEF: -\nATK Speed: -\nCrit Rate: -%\nCrit DMG: -%";
                unitPortrait.sprite = null;
                unitPortrait.color = new Color(0.3f, 0.3f, 0.3f);
                
                // Disable buttons
                sellButton.interactable = false;
                upgradeButton.interactable = false;
                
                // Close tooltip
                if (statTooltip != null)
                    statTooltip.SetActive(false);
            }
        }
    }
}