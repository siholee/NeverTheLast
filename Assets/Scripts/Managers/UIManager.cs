using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using Managers.UI;
using UnityEngine;
using UnityEngine.UI;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class UIManager : MonoBehaviour
    {
        // 상단
        public TMPro.TextMeshProUGUI gameStatusText;
        public TMPro.TextMeshProUGUI gameLifeText;
        public TMPro.TextMeshProUGUI gameStageText;
        public TMPro.TextMeshProUGUI gameSpeedText;

        // 좌측 사이드바
        public Transform synergyTagContainer;
        
        // 시너지 팝업
        public SynergyPopup synergyPopupObj;
        
        // 유닛 정보 탭
        public InfoTab infoTab;

        [Header("보상 패널")]
        public GameObject rewardPanel;
        public TMPro.TextMeshProUGUI[] rewardTexts;
        public Button[] rewardButtons;

        private List<RewardDef> _currentRewards;
        private GameObject _characterSelectionPanel;
        
        private Camera mainCamera;
        
        private void Start()
        {
            // 시작할 때 팝업 비활성화
            if (synergyPopupObj != null)
            {
                synergyPopupObj.synergyPopupPanel.SetActive(false);
            }
            
            // 시작할 때 InfoTab 비활성화
            if (infoTab != null)
            {
                infoTab.gameObject.SetActive(false);
            }

            // 메인 카메라 참조 가져오기
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            // 생명력 UI 초기화
            UpdateLifeText();
        }

        public void UpdateGameStatus(GameState currentState, int remainingTime)
        {
            if (gameStatusText == null) return;

            switch (currentState)
            {
                case GameState.Preparation:
                    if (remainingTime > 0)
                    {
                        gameStatusText.text = $"준비 단계 - 남은시간: {remainingTime}초";
                    }
                    else
                    {
                        gameStatusText.text = "준비 단계";
                    }
                    break;
                case GameState.CharacterSelection:
                    gameStatusText.text = "캐릭터 선택";
                    break;
                case GameState.RoundInProgress:
                    gameStatusText.text = "라운드 진행 중";
                    break;
                case GameState.RoundEnd:
                    gameStatusText.text = "라운드 종료";
                    break;
                case GameState.RewardSelection:
                    gameStatusText.text = "보상 선택";
                    break;
                case GameState.RunComplete:
                    gameStatusText.text = "런 완료";
                    break;
                case GameState.GameOver:
                    gameStatusText.text = "게임 오버";
                    break;
                default:
                    gameStatusText.text = "알 수 없는 상태";
                    break;
            }
        }

        public void UpdateGameStatusWithEnemyCount(GameState currentState, int remainingTime, int enemyCount)
        {
            if (gameStatusText == null) return;

            switch (currentState)
            {
                case GameState.RoundInProgress:
                    if (remainingTime > 0)
                    {
                        gameStatusText.text = $"라운드 진행 중 - 시간: {remainingTime}초, 적: {enemyCount}마리";
                    }
                    else
                    {
                        gameStatusText.text = $"라운드 진행 중 - 적: {enemyCount}마리";
                    }
                    break;
                default:
                    // 다른 상태는 기본 메서드 사용
                    UpdateGameStatus(currentState, remainingTime);
                    break;
            }
        }

        public void UpdateLifeText()
        {
            if (gameLifeText != null && GameManager.Instance != null)
            {
                gameLifeText.text = $"생명력: {GameManager.Instance.life}";
            }
        }
        
        public void ShowSynergyPopup(Vector3 position, SynergyInfo synergyInfo)
        {
            if (synergyPopupObj == null) return;
            synergyPopupObj.synergyPopupPanel.SetActive(true);

            // 팝업 위치 설정 (우상단 -> 좌상단)
            RectTransform popupRect = synergyPopupObj.synergyPopupPanel.GetComponent<RectTransform>();
            // Vector2 screenPosition = Camera.main.WorldToScreenPoint(position);
            popupRect.position = position;

            // 시너지 정보 설정
            synergyPopupObj.synergyNameText.text = synergyInfo.Name;
            synergyPopupObj.synergyDescText.text = synergyInfo.Description;
            synergyPopupObj.synergyCountText.text = $"{synergyInfo.Count}/{synergyInfo.MaxCount}";

            // 유닛 포트레이트 설정
            for (int i = 0; i < synergyPopupObj.unitPortraits.Count; i++)
            {
                if (i < synergyInfo.Units.Count)
                {
                    synergyPopupObj.unitPortraits[i].sprite = Resources.Load<Sprite>(synergyInfo.Units[i].PortraitPath);
                    synergyPopupObj.unitPortraits[i].gameObject.SetActive(true);
                }
                else
                {
                    synergyPopupObj.unitPortraits[i].gameObject.SetActive(false);
                }
            }
        }

        public void HideSynergyPopup()
        {
            if (synergyPopupObj != null)
            {
                synergyPopupObj.synergyPopupPanel.SetActive(false);
            }
        }

        public void SetSynergyText(Dictionary<int, SynergyInfo> synergyCounts)
        {
            List<SynergyInfo> synergyList = new List<SynergyInfo>();
            foreach (var synergy in synergyCounts.Values)
            {
                if (synergy.Count > 0)
                {
                    synergyList.Add(synergy);
                }
            }
            synergyList.Sort((a, b) => b.Count.CompareTo(a.Count));
            
            for (int i = 0; i < synergyTagContainer.childCount; i++)
            {
                var synergyTag = synergyTagContainer.GetChild(i).GetComponent<SynergyTag>();
                if (i < synergyList.Count && synergyList[i].Count > 0)
                {
                    SynergyInfo synergyInfo = synergyList[i];
                    synergyTag.Initialize(synergyInfo, Resources.Load<Sprite>(synergyInfo.Units[0].PortraitPath));
                    synergyTag.synergyCountText.text = $"{synergyInfo.Count} | {synergyInfo.MaxCount}";
                    synergyTag.SetActive(true);
                }
                else
                {
                    synergyTag.SetActive(false);
                }
            }
        }

        // InfoTab 관련 메서드들
        public void ShowInfoTab(Unit unit)
        {
            if (infoTab == null || unit == null) return;
            
            // InfoTab 자체의 ShowInfoTab 메서드 호출
            infoTab.ShowInfoTab(unit);
        }
        
        public void HideInfoTab()
        {
            if (infoTab != null)
            {
                infoTab.gameObject.SetActive(false);
            }
        }

        public void ShowRewardPanel(List<RewardDef> rewards)
        {
            EnsureRewardPanel();
            if (rewardPanel == null) return;

            _currentRewards = rewards ?? new List<RewardDef>();
            rewardPanel.SetActive(true);

            for (int i = 0; i < rewardButtons.Length; i++)
            {
                int capturedIndex = i;
                bool hasReward = i < _currentRewards.Count;

                if (rewardTexts != null && i < rewardTexts.Length && rewardTexts[i] != null)
                {
                    rewardTexts[i].text = hasReward
                        ? $"{_currentRewards[i].displayName}\n{_currentRewards[i].description}"
                        : "";
                }

                if (rewardButtons[i] == null) continue;
                rewardButtons[i].gameObject.SetActive(hasReward);
                rewardButtons[i].onClick.RemoveAllListeners();
                rewardButtons[i].onClick.AddListener(() =>
                {
                    if (capturedIndex >= _currentRewards.Count) return;
                    var target = GridManager.Instance.heroList.Find(hero => hero != null && hero.isActive && !hero.IsEnemy);
                    GameManager.Instance.rewardManager?.ApplyReward(_currentRewards[capturedIndex], target);
                });
            }
        }

        public void HideRewardPanel()
        {
            if (rewardPanel != null)
            {
                rewardPanel.SetActive(false);
            }
        }

        public void ShowCharacterSelection()
        {
            EnsureCharacterSelectionManager();
            EnsureCharacterSelectionPanel();
            if (_characterSelectionPanel != null)
            {
                _characterSelectionPanel.SetActive(true);
            }
        }

        public void HideCharacterSelection()
        {
            if (_characterSelectionPanel != null)
            {
                _characterSelectionPanel.SetActive(false);
            }
        }

        public void TestButtonClick()
        {
            Debug.Log("Test Button Clicked");
        }

        public void OnGameSpeedButtonClick()
        {
            string currentText = gameSpeedText.text;
            string speedString = currentText.Replace("X", "");
            if (int.TryParse(speedString, out int currentSpeed))
            {
                int newSpeed;
                switch (currentSpeed)
                {
                    case 1:
                        newSpeed = 2;
                        break;
                    case 2:
                        newSpeed = 3;
                        break;
                    case 3:
                        newSpeed = 1;
                        break;
                    default:
                        newSpeed = 1;
                        break;
                }

                Time.timeScale = newSpeed;
                gameSpeedText.text = newSpeed + "X";
            }
            else
            {
                Debug.LogError("게임 속도 텍스트 파싱 실패: " + currentText);
            }
        }

        public void OnGameStartButtonClick()
        {
            if (GameManager.Instance.gameState == GameState.Preparation)
            {
                GameManager.Instance.StartRound();
            }
        }

        private void EnsureRewardPanel()
        {
            if (rewardPanel != null && rewardButtons != null && rewardButtons.Length > 0) return;

            var canvas = EnsureOverlayCanvas("RewardCanvas");
            rewardPanel = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
            rewardPanel.transform.SetParent(canvas.transform, false);
            var panelRect = rewardPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            rewardPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.92f);

            rewardTexts = new TMPro.TextMeshProUGUI[3];
            rewardButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var buttonObject = new GameObject($"RewardButton{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(rewardPanel.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.12f + i * 0.28f, 0.35f);
                rect.anchorMax = new Vector2(0.34f + i * 0.28f, 0.65f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                buttonObject.GetComponent<Image>().color = new Color(0.13f, 0.16f, 0.22f, 1f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 12f);
                labelRect.offsetMax = new Vector2(-12f, -12f);
                var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.fontSize = 18f;
                label.color = Color.white;

                rewardTexts[i] = label;
                rewardButtons[i] = buttonObject.GetComponent<Button>();
            }

            rewardPanel.SetActive(false);
        }

        private void EnsureCharacterSelectionPanel()
        {
            if (_characterSelectionPanel != null) return;

            var canvas = EnsureOverlayCanvas("CharacterSelectionCanvas");
            _characterSelectionPanel = new GameObject("CharacterSelectionPanel", typeof(RectTransform), typeof(Image));
            _characterSelectionPanel.transform.SetParent(canvas.transform, false);
            var panelRect = _characterSelectionPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _characterSelectionPanel.GetComponent<Image>().color = new Color(0.01f, 0.03f, 0.04f, 0.94f);

            var units = GameManager.Instance.unitDataList?.units?.FindAll(unit => unit.id < 100) ?? new List<UnitData>();
            for (int i = 0; i < units.Count; i++)
            {
                int unitId = units[i].id;
                var buttonObject = new GameObject($"HeroButton_{unitId}", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(_characterSelectionPanel.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                int col = i % 4;
                int row = i / 4;
                rect.anchorMin = new Vector2(0.08f + col * 0.22f, 0.68f - row * 0.18f);
                rect.anchorMax = new Vector2(0.25f + col * 0.22f, 0.80f - row * 0.18f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.20f, 1f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 8f);
                labelRect.offsetMax = new Vector2(-8f, -8f);
                var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
                label.text = units[i].name;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.fontSize = 16f;
                label.color = Color.white;

                buttonObject.GetComponent<Button>().onClick.AddListener(() =>
                {
                    CharacterSelectionManager.Instance.AddHero(unitId);
                });
            }

            var startButton = new GameObject("StartRunButton", typeof(RectTransform), typeof(Image), typeof(Button));
            startButton.transform.SetParent(_characterSelectionPanel.transform, false);
            var startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.38f, 0.12f);
            startRect.anchorMax = new Vector2(0.62f, 0.22f);
            startRect.offsetMin = Vector2.zero;
            startRect.offsetMax = Vector2.zero;
            startButton.GetComponent<Image>().color = new Color(0.08f, 0.28f, 0.18f, 1f);
            startButton.GetComponent<Button>().onClick.AddListener(() => CharacterSelectionManager.Instance.ConfirmSelection());

            var startLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            startLabelObject.transform.SetParent(startButton.transform, false);
            var startLabelRect = startLabelObject.GetComponent<RectTransform>();
            startLabelRect.anchorMin = Vector2.zero;
            startLabelRect.anchorMax = Vector2.one;
            startLabelRect.offsetMin = Vector2.zero;
            startLabelRect.offsetMax = Vector2.zero;
            var startLabel = startLabelObject.GetComponent<TMPro.TextMeshProUGUI>();
            startLabel.text = "전투 시작";
            startLabel.alignment = TMPro.TextAlignmentOptions.Center;
            startLabel.fontSize = 20f;
            startLabel.color = Color.white;
        }

        private static Canvas EnsureOverlayCanvas(string objectName)
        {
            var canvasObject = GameObject.Find(objectName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(objectName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            return canvasObject.GetComponent<Canvas>();
        }

        private static void EnsureCharacterSelectionManager()
        {
            if (CharacterSelectionManager.Instance != null) return;
            new GameObject("CharacterSelectionManager").AddComponent<CharacterSelectionManager>();
        }
    }
}
