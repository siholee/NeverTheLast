using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BaseClasses;

namespace Managers
{
    public class BottomShopPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Button[] unitShopSlots;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private TextMeshProUGUI costRangeText;
        
        public void Initialize(RectTransform parentRect)
        {
            // RectTransform 확인 및 설정
            if (panelRect == null)
            {
                // RectTransform이 없으면 현재 게임오브젝트에서 가져오거나 추가
                panelRect = GetComponent<RectTransform>();
                if (panelRect == null)
                {
                    Debug.LogWarning("BottomShopPanel에 RectTransform이 없어 추가합니다.");
                    panelRect = gameObject.AddComponent<RectTransform>();
                }
                
                // 부모 RectTransform 내에 위치시키기
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                
                // 패널 타이틀 추가
                CreateTitle("상점 (1번 키)");
                
                // 상점 UI 요소들 생성
                CreateShopUI();
            }
        }
        
        private void CreateTitle(string titleText)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(transform, false);
            
            // RectTransform 명시적 추가
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
        
        private void CreateShopUI()
        {
            // 슬롯 컨테이너 생성
            GameObject slotsContainer = new GameObject("SlotsContainer");
            slotsContainer.transform.SetParent(transform, false);
            
            // RectTransform 명시적 추가
            RectTransform slotsRect = slotsContainer.AddComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0.2f, 0);
            slotsRect.anchorMax = new Vector2(0.9f, 1);
            slotsRect.offsetMin = Vector2.zero;
            slotsRect.offsetMax = Vector2.zero;
            
            // 수평 레이아웃 추가
            HorizontalLayoutGroup layout = slotsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 10, 10);
            
            // 유닛 슬롯 5개 생성
            unitShopSlots = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject slotObj = new GameObject($"UnitSlot_{i}");
                slotObj.transform.SetParent(slotsContainer.transform, false);
                
                // RectTransform 명시적 추가
                RectTransform slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(80, 80);
                
                Image slotImage = slotObj.AddComponent<Image>();
                slotImage.color = new Color(0.3f, 0.3f, 0.3f);
                
                Button slotButton = slotObj.AddComponent<Button>();
                slotButton.targetGraphic = slotImage;
                
                int slotIndex = i;
                slotButton.onClick.AddListener(() => OnUnitSlotClick(slotIndex));
                
                unitShopSlots[i] = slotButton;
            }
            
            // 컨트롤 패널 생성 (오른쪽)
            GameObject controlPanel = new GameObject("ControlPanel");
            controlPanel.transform.SetParent(transform, false);
            
            // RectTransform 명시적 추가
            RectTransform controlRect = controlPanel.AddComponent<RectTransform>();
            controlRect.anchorMin = new Vector2(0.9f, 0);
            controlRect.anchorMax = new Vector2(1, 1);
            controlRect.offsetMin = Vector2.zero;
            controlRect.offsetMax = Vector2.zero;
            
            // 수직 레이아웃 추가
            VerticalLayoutGroup controlLayout = controlPanel.AddComponent<VerticalLayoutGroup>();
            controlLayout.spacing = 10f;
            controlLayout.childAlignment = TextAnchor.MiddleCenter;
            controlLayout.childForceExpandWidth = true;
            controlLayout.childForceExpandHeight = false;
            controlLayout.padding = new RectOffset(5, 5, 5, 5);
            
            // 리프레시 버튼 생성
            refreshButton = CreateControlButton(controlPanel.transform, "Refresh", "새로고침", () => OnRefreshButtonClick());
            
            // UP 버튼 생성
            upButton = CreateControlButton(controlPanel.transform, "Up", "▲", () => OnUpButtonClick());
            
            // 비용 표시 텍스트
            GameObject costObj = new GameObject("CostRange");
            costObj.transform.SetParent(controlPanel.transform, false);
            
            // RectTransform 명시적 추가
            RectTransform costRect = costObj.AddComponent<RectTransform>();
            costRect.sizeDelta = new Vector2(0, 30);
            
            costRangeText = costObj.AddComponent<TextMeshProUGUI>();
            costRangeText.text = "비용: 1-3";
            costRangeText.fontSize = 14;
            costRangeText.alignment = TextAlignmentOptions.Center;
            
            // DOWN 버튼 생성
            downButton = CreateControlButton(controlPanel.transform, "Down", "▼", () => OnDownButtonClick());
        }
        
        private Button CreateControlButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);
            
            // RectTransform 명시적 추가
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0, 40);
            
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.9f);
            
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(action);
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            // RectTransform 명시적 추가
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
        
        // 이벤트 핸들러들
        private void OnUnitSlotClick(int slotIndex)
        {
            Debug.Log($"유닛 슬롯 {slotIndex} 클릭됨");
            // 구매 로직 추가
        }
        
        private void OnRefreshButtonClick()
        {
            Debug.Log("상점 새로고침 클릭됨");
            // 새로고침 로직 추가
        }
        
        private void OnUpButtonClick()
        {
            Debug.Log("비용 범위 증가 클릭됨");
            // 비용 범위 증가 로직 추가
        }
        
        private void OnDownButtonClick()
        {
            Debug.Log("비용 범위 감소 클릭됨");
            // 비용 범위 감소 로직 추가
        }
        
        // 게임 상태 변경 시 호출
        public void OnGameStateChanged(BaseEnums.GameState state)
        {
            // 필요한 처리 추가
        }
        
        // 상점 데이터 업데이트
        public void UpdateShopData(int minCost, int maxCost, Sprite[] unitSprites)
        {
            costRangeText.text = $"비용: {minCost}-{maxCost}";
            
            // 유닛 슬롯 업데이트
            for (int i = 0; i < unitShopSlots.Length && i < unitSprites.Length; i++)
            {
                Image slotImage = unitShopSlots[i].GetComponent<Image>();
                slotImage.sprite = unitSprites[i];
                slotImage.color = Color.white;
            }
        }
    }
}