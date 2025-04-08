using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BaseClasses;

namespace Managers.UI
{
    public class TopPanelManager : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TextMeshProUGUI lifeText;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI goldText;

        // 패널 초기화
        public void Initialize(RectTransform parentRect)
        {
            if (panelRect == null)
            {
                GameObject panelObj = new GameObject("TopPanel");
                panelObj.transform.SetParent(parentRect, false);
                panelRect = panelObj.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                // 기본 UI 요소 생성
                CreateUIElements();
            }
        }

        private void CreateUIElements()
        {
            // 가로 배치를 위한 레이아웃 추가
            HorizontalLayoutGroup layout = panelRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 50f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(20, 20, 5, 5);

            // 라이프 텍스트 생성
            lifeText = CreateTextElement("LifeText", "생명력: 100");
            
            // 라운드 텍스트 생성
            roundText = CreateTextElement("RoundText", "라운드: 1");
            
            // 골드 텍스트 생성
            goldText = CreateTextElement("GoldText", "골드: 50");
            goldText.color = Color.yellow;
        }

        private TextMeshProUGUI CreateTextElement(string name, string initialText)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(panelRect, false);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = initialText;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            
            RectTransform rect = tmp.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 40);
            
            return tmp;
        }

        // 플레이어 생명력 업데이트
        public void UpdateLife(int currentLife)
        {
            if (lifeText != null)
                lifeText.text = $"생명력: {currentLife}";
        }

        // 현재 라운드 업데이트
        public void UpdateRound(int currentRound)
        {
            if (roundText != null)
                roundText.text = $"라운드: {currentRound}";
        }

        // 플레이어 골드 업데이트
        public void UpdateGold(int currentGold)
        {
            if (goldText != null)
                goldText.text = $"골드: {currentGold}";
        }
        
        // 게임 상태 변경 시 호출
        public void OnGameStateChanged(BaseEnums.GameState state)
        {
            // 게임 상태에 따른 UI 업데이트 (필요시 구현)
        }
    }
}