using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BaseClasses;

namespace Managers.UI
{
    public class BottomPanelManager : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private int activePanelIndex = 0; // 0: None, 2: UnitInfo
        
        [Header("Sub Panel Managers")]
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
            
            // Initialize unit panel only
            InitializeUnitPanel();
            initialized = true;
        }
        
        private void InitializeUnitPanel()
        {
            try
            {
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
                
                // Always show unit info panel
                unitInfoPanel.gameObject.SetActive(true);
                activePanelIndex = 2;
                
                Debug.Log("Unit panel initialized successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error initializing unit panel: " + e.Message);
                Debug.LogException(e);
            }
        }
        
        // Switch panel - now only supports unit info panel
        public void SwitchPanel(int panelIndex)
        {
            try
            {
                // Only unit info panel (2) is supported now
                if (panelIndex != 2)
                {
                    Debug.Log("Currently only Unit Info panel is supported");
                    return;
                }
                
                // Ensure unit panel is active
                if (unitInfoPanel != null)
                {
                    unitInfoPanel.gameObject.SetActive(true);
                    activePanelIndex = 2;
                }
                else
                {
                    Debug.LogWarning("Unit info panel not found");
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
            
            // Notify unit panel only
            if (unitInfoPanel != null) unitInfoPanel.OnGameStateChanged(state);
        }
        
        // Handle keyboard input
        public void HandleKeyboardInput()
        {
            // Only unit panel is supported now, so we just ensure it's active
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                Debug.Log("Key 2 pressed - showing unit info panel");
                SwitchPanel(2);
            }
        }
        
        // Get unit info panel
        public BottomUnitPanel GetUnitInfoPanel()
        {
            return unitInfoPanel;
        }
    }
}