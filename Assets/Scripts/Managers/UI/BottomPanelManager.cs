using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BaseClasses;

namespace Managers
{
    public class BottomPanelManager : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private int activePanelIndex = 0; // 0: None, 1: Shop, 2: UnitInfo
        
        [Header("Sub Panel Managers")]
        [SerializeField] private BottomShopPanel shopPanel;
        [SerializeField] private BottomUnitPanel unitInfoPanel;
        
        private Dictionary<int, MonoBehaviour> panels = new Dictionary<int, MonoBehaviour>();
        private bool initialized = false;
        
        // Update is called once per frame
        private void Update()
        {
            // Make sure keyboard input is processed every frame
            if (initialized)
            {
                HandleKeyboardInput();
            }
        }
        
        // Initialize panel
        public void Initialize(RectTransform parentRect)
        {
            if (panelRect == null)
            {
                GameObject panelObj = new GameObject("BottomPanel");
                panelObj.transform.SetParent(parentRect, false);
                panelRect = panelObj.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                
                // Add panel background
                Image bg = panelObj.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            }
            
            // Initialize sub panels
            InitializeSubPanels();
            initialized = true;
        }
        
        private void InitializeSubPanels()
        {
            try
            {
                // Initialize shop panel (panel 1)
                if (shopPanel == null)
                {
                    GameObject shopPanelObj = new GameObject("ShopPanel");
                    shopPanelObj.transform.SetParent(panelRect, false);
                    
                    // Explicitly add RectTransform first
                    RectTransform shopRect = shopPanelObj.AddComponent<RectTransform>();
                    shopRect.anchorMin = Vector2.zero;
                    shopRect.anchorMax = Vector2.one;
                    shopRect.offsetMin = Vector2.zero;
                    shopRect.offsetMax = Vector2.zero;
                    
                    shopPanel = shopPanelObj.AddComponent<BottomShopPanel>();
                    shopPanel.Initialize(panelRect);
                }
                panels[1] = shopPanel;
                shopPanel.gameObject.SetActive(false);
                
                // Initialize unit info panel (panel 2)
                if (unitInfoPanel == null)
                {
                    GameObject unitInfoPanelObj = new GameObject("UnitInfoPanel");
                    unitInfoPanelObj.transform.SetParent(panelRect, false);
                    
                    // Explicitly add RectTransform first
                    RectTransform unitInfoRect = unitInfoPanelObj.AddComponent<RectTransform>();
                    unitInfoRect.anchorMin = Vector2.zero;
                    unitInfoRect.anchorMax = Vector2.one;
                    unitInfoRect.offsetMin = Vector2.zero;
                    unitInfoRect.offsetMax = Vector2.zero;
                    
                    unitInfoPanel = unitInfoPanelObj.AddComponent<BottomUnitPanel>();
                    unitInfoPanel.Initialize(panelRect);
                }
                panels[2] = unitInfoPanel;
                unitInfoPanel.gameObject.SetActive(false);
                
                // Activate default panel (unit info panel)
                SwitchPanel(2);
                Debug.Log("Sub panels initialized successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error initializing sub panels: " + e.Message);
                Debug.LogException(e);
            }
        }
        
        // Switch panel
        public void SwitchPanel(int panelIndex)
        {
            try
            {
                // Ignore if already active
                if (activePanelIndex == panelIndex)
                    return;
                    
                // Deactivate all panels
                foreach (var panel in panels)
                {
                    if (panel.Value != null)
                        panel.Value.gameObject.SetActive(false);
                }
                
                // Activate selected panel
                if (panels.TryGetValue(panelIndex, out MonoBehaviour selectedPanel) && selectedPanel != null)
                {
                    selectedPanel.gameObject.SetActive(true);
                    activePanelIndex = panelIndex;
                    
                    Debug.Log($"Panel switched: Panel {panelIndex}");
                }
                else
                {
                    activePanelIndex = 0;
                    Debug.LogWarning($"Panel {panelIndex} not found");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error switching panels: " + e.Message);
            }
        }
        
        // Get active panel index
        public int GetActivePanelIndex()
        {
            return activePanelIndex;
        }
        
        // Called when game state changes
        public void OnGameStateChanged(BaseEnums.GameState state)
        {
            switch (state)
            {
                case BaseEnums.GameState.Preparation:
                    panelRect.gameObject.SetActive(true);
                    break;
                case BaseEnums.GameState.RoundInProgress:
                    panelRect.gameObject.SetActive(false);
                    break;
                case BaseEnums.GameState.RoundEnd:
                    panelRect.gameObject.SetActive(true);
                    break;
            }
            
            // Notify sub panels
            if (shopPanel != null) shopPanel.OnGameStateChanged(state);
            if (unitInfoPanel != null) unitInfoPanel.OnGameStateChanged(state);
        }
        
        // Handle keyboard input
        public void HandleKeyboardInput()
        {
            // Key 1: Show shop panel
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                Debug.Log("Key 1 pressed - switching to shop panel");
                SwitchPanel(1);
            }
            // Key 2: Show unit info panel
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                Debug.Log("Key 2 pressed - switching to unit info panel");
                SwitchPanel(2);
            }
        }
        
        // Get shop panel
        public BottomShopPanel GetShopPanel()
        {
            return shopPanel;
        }
        
        // Get unit info panel
        public BottomUnitPanel GetUnitInfoPanel()
        {
            return unitInfoPanel;
        }
    }
}