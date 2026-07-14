using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
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
        public TMPro.TextMeshProUGUI gameGoldText;
        public TMPro.TextMeshProUGUI gameStageText;
        public TMPro.TextMeshProUGUI gameSpeedText;

        // 유닛 정보 탭
        public InfoTab infoTab;

        [Header("보상 패널")]
        public GameObject rewardPanel;
        public TMPro.TextMeshProUGUI[] rewardTexts;
        public Button[] rewardButtons;

        private List<RewardDef> _currentRewards;
        private GameObject _characterSelectionPanel;
        private readonly Dictionary<int, Image> _heroSelectionButtonImages = new();
        private readonly Dictionary<int, TMPro.TextMeshProUGUI> _heroSelectionButtonLabels = new();
        private readonly Dictionary<int, string> _heroSelectionButtonBaseLabels = new();
        private TMPro.TextMeshProUGUI _selectionSummaryLabel;
        private static readonly Color HeroButtonDefaultColor = new(0.12f, 0.18f, 0.20f, 1f);
        private static readonly Color HeroButtonSelectedColor = new(0.85f, 0.65f, 0.15f, 1f);
        private GameObject _eventStagePanel;
        private TMPro.TextMeshProUGUI _eventStageText;
        private TMPro.TextMeshProUGUI _eventTitleText;
        private TMPro.TextMeshProUGUI _eventSpeakerText;
        private Button _eventNextButton;
        private Button[] _eventChoiceButtons;
        private TMPro.TextMeshProUGUI[] _eventChoiceTexts;
        private GameObject _preparationPhasePanel;
        private TMPro.TextMeshProUGUI _preparationInfoLabel;
        private Button _prepTrainingButton;
        private Button _prepExtraBattleButton;
        private Button _prepRestButton;
        private Button _prepDeckButton;
        private Button _prepEquipmentButton;
        private Button _prepStartButton;
        private GameObject _equipmentPanel;
        private int _equipmentPage;
        private GameObject _trainingPhasePanel;
        private TMPro.TextMeshProUGUI _trainingInfoLabel;
        
        private Camera mainCamera;
        
        private void Start()
        {
            EnsureBattleHud();

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
            UpdateGoldText();
        }

        private void EnsureBattleHud()
        {
            if (gameStatusText != null && gameLifeText != null && gameStageText != null && gameSpeedText != null && gameGoldText == null)
            {
                gameGoldText = CreateHudText("GameGoldText", gameLifeText.transform.parent,
                    new Vector2(0.42f, 0.12f), new Vector2(0.52f, 0.88f), "골드: 0", 18f, TMPro.TextAlignmentOptions.Center);
                return;
            }
            if (gameStatusText != null && gameLifeText != null && gameGoldText != null && gameStageText != null && gameSpeedText != null)
            {
                return;
            }

            var canvasObject = GameObject.Find("BattleHUD");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("BattleHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(canvasObject.transform, false);
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0f, 0.925f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;
            topBar.GetComponent<Image>().color = new Color(0.045f, 0.05f, 0.06f, 0.92f);

            gameStatusText ??= CreateHudText("GameStatusText", topBar.transform, new Vector2(0.02f, 0.12f), new Vector2(0.30f, 0.88f), "준비 단계", 22f, TMPro.TextAlignmentOptions.MidlineLeft);
            gameLifeText ??= CreateHudText("GameLifeText", topBar.transform, new Vector2(0.30f, 0.12f), new Vector2(0.41f, 0.88f), "생명력: 20", 20f, TMPro.TextAlignmentOptions.Center);
            gameGoldText ??= CreateHudText("GameGoldText", topBar.transform, new Vector2(0.42f, 0.12f), new Vector2(0.52f, 0.88f), "골드: 0", 18f, TMPro.TextAlignmentOptions.Center);
            gameStageText ??= CreateHudText("GameStageText", topBar.transform, new Vector2(0.53f, 0.12f), new Vector2(0.73f, 0.88f), "라운드", 18f, TMPro.TextAlignmentOptions.Center);

            var speedButton = CreateHudButton("SpeedButton", topBar.transform, new Vector2(0.75f, 0.18f), new Vector2(0.82f, 0.82f), OnGameSpeedButtonClick);
            gameSpeedText ??= CreateHudText("Label", speedButton.transform, Vector2.zero, Vector2.one, "1X", 20f, TMPro.TextAlignmentOptions.Center);

            var startButton = CreateHudButton("StartRoundButton", topBar.transform, new Vector2(0.84f, 0.18f), new Vector2(0.96f, 0.82f), OnGameStartButtonClick);
            CreateHudText("Label", startButton.transform, Vector2.zero, Vector2.one, "시작", 20f, TMPro.TextAlignmentOptions.Center);
        }

        private static TMPro.TextMeshProUGUI CreateHudText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string text, float fontSize, TMPro.TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            var label = textObject.GetComponent<TMPro.TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return label;
        }

        private static Button CreateHudButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.19f, 1f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        public void UpdateGameStatus(GameState currentState, int remainingTime)
        {
            if (gameStatusText == null) return;
            UpdateStageText();

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
                case GameState.EventStage:
                    gameStatusText.text = "이벤트 스테이지";
                    break;
                case GameState.TrainingPhase:
                    gameStatusText.text = "육성 페이즈";
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
            UpdateStageText();

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

        public void UpdateGoldText()
        {
            if (gameGoldText != null && GameManager.Instance?.inventoryManager != null)
            {
                gameGoldText.text = $"골드: {GameManager.Instance.inventoryManager.Gold}";
            }
        }

        private void UpdateStageText()
        {
            if (gameStageText == null || GameManager.Instance?.RoundManager == null) return;

            var roundManager = GameManager.Instance.RoundManager;
            string modeText = GameManager.Instance.CurrentMode == GameMode.Infinite ? "무한" : "육성";
            string themeText = string.IsNullOrEmpty(roundManager.CurrentThemeName) ? "" : $" | {roundManager.CurrentThemeName}";
            gameStageText.text = $"{modeText} | 라운드 {roundManager.Round}-{roundManager.StageInRound} | 스테이지 {roundManager.Stage}{themeText}";
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

        public void ShowEventStagePanel(StageEventData eventData, int dialogueIndex)
        {
            EnsureEventStagePanel();
            if (eventData == null) return;
            _eventStagePanel.SetActive(true);
            if (_eventTitleText != null) _eventTitleText.text = eventData.title ?? "사건";

            int dialogueCount = eventData.dialogue?.Count ?? 0;
            if (dialogueIndex < dialogueCount)
            {
                StageEventDialogueData line = eventData.dialogue[dialogueIndex];
                if (_eventSpeakerText != null) _eventSpeakerText.text = line.speaker ?? "";
                if (_eventStageText != null) _eventStageText.text = line.text ?? "";
                SetEventChoicesVisible(false);
                ConfigureEventNextButton("다음", () => GameManager.Instance?.AdvanceEventDialogue());
                return;
            }

            if (_eventSpeakerText != null) _eventSpeakerText.text = "선택";
            if (_eventStageText != null) _eventStageText.text = "어떻게 하시겠습니까?";
            if (_eventNextButton != null) _eventNextButton.gameObject.SetActive(false);
            for (int i = 0; i < _eventChoiceButtons.Length; i++)
            {
                int capturedIndex = i;
                bool visible = eventData.choices != null && i < eventData.choices.Count;
                _eventChoiceButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                StageEventChoiceData choice = eventData.choices[i];
                int cost = Mathf.Max(0, choice.goldCostPerStage * (GameManager.Instance?.RoundManager?.Stage ?? 1));
                _eventChoiceTexts[i].text = choice.goldCostPerStage > 0 ? $"{choice.text}\n골드 -{cost}" : choice.text;
                _eventChoiceButtons[i].onClick.RemoveAllListeners();
                _eventChoiceButtons[i].onClick.AddListener(() => GameManager.Instance?.SelectEventChoice(eventData.choices[capturedIndex].id));
            }
        }

        public void ShowEventMessage(string message)
        {
            if (_eventStageText != null) _eventStageText.text = message ?? "";
        }

        public void ShowEventResolution(string message)
        {
            EnsureEventStagePanel();
            _eventStagePanel.SetActive(true);
            if (_eventSpeakerText != null) _eventSpeakerText.text = "결과";
            if (_eventStageText != null) _eventStageText.text = message ?? "사건이 끝났다.";
            SetEventChoicesVisible(false);
            ConfigureEventNextButton("계속", () => GameManager.Instance?.CompleteEventStage());
        }

        private void ConfigureEventNextButton(string label, UnityEngine.Events.UnityAction action)
        {
            if (_eventNextButton == null) return;
            _eventNextButton.gameObject.SetActive(true);
            _eventNextButton.onClick.RemoveAllListeners();
            _eventNextButton.onClick.AddListener(action);
            TMPro.TextMeshProUGUI text = _eventNextButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        private void SetEventChoicesVisible(bool visible)
        {
            if (_eventChoiceButtons == null) return;
            foreach (Button button in _eventChoiceButtons)
            {
                button?.gameObject.SetActive(visible);
            }
        }

        public void HideEventStagePanel()
        {
            if (_eventStagePanel != null)
            {
                _eventStagePanel.SetActive(false);
            }
        }

        public void ShowEquipmentPanel()
        {
            EnsureEquipmentPanel();
            _equipmentPage = 0;
            RefreshEquipmentPanel();
            _equipmentPanel?.SetActive(true);
        }

        public void HideEquipmentPanel()
        {
            _equipmentPanel?.SetActive(false);
        }

        public void ShowPreparationPhasePanel(bool deckOnly, bool actionUsed, string message = null)
        {
            EnsurePreparationPhasePanel();
            RefreshPreparationPhasePanel(deckOnly, actionUsed, message);
            if (_preparationPhasePanel != null)
            {
                _preparationPhasePanel.SetActive(true);
            }
        }

        public void HidePreparationPhasePanel()
        {
            if (_preparationPhasePanel != null)
            {
                _preparationPhasePanel.SetActive(false);
            }
        }

        public void ShowTrainingPhasePanel()
        {
            EnsureTrainingPhasePanel();
            RefreshTrainingInfo();
            if (_trainingPhasePanel != null)
            {
                _trainingPhasePanel.SetActive(true);
            }
        }

        public void HideTrainingPhasePanel()
        {
            if (_trainingPhasePanel != null)
            {
                _trainingPhasePanel.SetActive(false);
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
            RefreshCharacterSelectionVisuals();
        }

        public void HideCharacterSelection()
        {
            if (_characterSelectionPanel != null)
            {
                _characterSelectionPanel.SetActive(false);
            }
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

        private void EnsurePreparationPhasePanel()
        {
            if (_preparationPhasePanel != null) return;

            var canvas = EnsureOverlayCanvas("PreparationPhaseCanvas");
            _preparationPhasePanel = new GameObject("PreparationPhasePanel", typeof(RectTransform), typeof(Image));
            _preparationPhasePanel.transform.SetParent(canvas.transform, false);
            var panelRect = _preparationPhasePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.04f);
            panelRect.anchorMax = new Vector2(0.92f, 0.24f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _preparationPhasePanel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.94f);

            CreateHudText("Title", _preparationPhasePanel.transform,
                new Vector2(0.03f, 0.64f), new Vector2(0.24f, 0.92f),
                "준비 페이즈", 24f, TMPro.TextAlignmentOptions.MidlineLeft);

            _preparationInfoLabel = CreateHudText("Info", _preparationPhasePanel.transform,
                new Vector2(0.25f, 0.64f), new Vector2(0.97f, 0.92f),
                "", 18f, TMPro.TextAlignmentOptions.MidlineLeft);
            _preparationInfoLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;

            _prepTrainingButton = CreatePreparationButton("PrepTrainingButton", 0, "훈련", () => GameManager.Instance?.OpenTrainingFromPreparation());
            _prepExtraBattleButton = CreatePreparationButton("PrepExtraBattleButton", 1, "추가 전투", () => GameManager.Instance?.BeginAdditionalBattleFromPreparation());
            _prepRestButton = CreatePreparationButton("PrepRestButton", 2, "휴식", () => GameManager.Instance?.RestFromPreparation());
            _prepDeckButton = CreatePreparationButton("PrepDeckButton", 3, "덱 구성", () => GameManager.Instance?.OpenDeckSetupFromPreparation());
            _prepEquipmentButton = CreatePreparationButton("PrepEquipmentButton", 4, "장비", () => GameManager.Instance?.OpenEquipmentFromPreparation());
            _prepStartButton = CreatePreparationButton("PrepStartButton", 5, "전투 시작", () => GameManager.Instance?.StartRound());

            _preparationPhasePanel.SetActive(false);
        }

        private Button CreatePreparationButton(string objectName, int index, string labelText, UnityEngine.Events.UnityAction onClick)
        {
            float width = 0.145f;
            float gap = 0.014f;
            float xMin = 0.03f + index * (width + gap);
            float xMax = xMin + width;

            var button = CreateHudButton(objectName, _preparationPhasePanel.transform,
                new Vector2(xMin, 0.16f), new Vector2(xMax, 0.52f), onClick);
            CreateHudText("Label", button.transform,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                labelText, 19f, TMPro.TextAlignmentOptions.Center);
            return button;
        }

        private void RefreshPreparationPhasePanel(bool deckOnly, bool actionUsed, string message = null)
        {
            if (_preparationInfoLabel != null)
            {
                _preparationInfoLabel.text = !string.IsNullOrWhiteSpace(message)
                    ? message
                    : deckOnly
                    ? "보스전 준비: 덱 구성과 전투 시작만 가능합니다."
                    : actionUsed
                        ? "준비 행동 완료: 덱을 정리한 뒤 전투를 시작하세요."
                        : "훈련, 추가 전투, 휴식 중 하나를 선택하거나 바로 전투를 시작하세요.";
            }

            bool actionAvailable = !deckOnly && !actionUsed;
            SetPreparationButtonState(_prepTrainingButton, actionAvailable);
            SetPreparationButtonState(_prepExtraBattleButton, actionAvailable);
            SetPreparationButtonState(_prepRestButton, actionAvailable);
            SetPreparationButtonState(_prepDeckButton, true);
            SetPreparationButtonState(_prepEquipmentButton, true);
            SetPreparationButtonState(_prepStartButton, true, true);
        }

        private void EnsureEquipmentPanel()
        {
            if (_equipmentPanel != null) return;
            Canvas canvas = EnsureOverlayCanvas("EquipmentCanvas");
            _equipmentPanel = new GameObject("EquipmentPanel", typeof(RectTransform), typeof(Image));
            _equipmentPanel.transform.SetParent(canvas.transform, false);
            RectTransform rect = _equipmentPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.18f, 0.16f);
            rect.anchorMax = new Vector2(0.82f, 0.84f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _equipmentPanel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.98f);
            _equipmentPanel.SetActive(false);
        }

        private void RefreshEquipmentPanel()
        {
            if (_equipmentPanel == null) return;
            foreach (Transform child in _equipmentPanel.transform)
            {
                Destroy(child.gameObject);
            }

            Unit target = TrainingManager.GetMainUnit();
            CreateHudText("Title", _equipmentPanel.transform, new Vector2(0.04f, 0.88f), new Vector2(0.76f, 0.98f),
                target == null ? "장비 보관함" : $"{target.UnitName} 장비 | 중량 {target.CarryWeightCurrent}/{target.CarryWeightMax}",
                24f, TMPro.TextAlignmentOptions.MidlineLeft);
            Button close = CreateHudButton("Close", _equipmentPanel.transform, new Vector2(0.82f, 0.90f), new Vector2(0.96f, 0.97f), HideEquipmentPanel);
            CreateHudText("Label", close.transform, Vector2.zero, Vector2.one, "닫기", 17f, TMPro.TextAlignmentOptions.Center);

            List<int> itemIds = GameManager.Instance?.inventoryManager?.ItemIdsInHand?.ToList() ?? new List<int>();
            ItemDataList itemData = GameManager.Instance?.itemDataList;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(itemIds.Count / 12f));
            _equipmentPage = Mathf.Clamp(_equipmentPage, 0, pageCount - 1);
            int startIndex = _equipmentPage * 12;
            int visibleCount = Mathf.Min(12, itemIds.Count - startIndex);
            for (int i = 0; i < visibleCount; i++)
            {
                int itemId = itemIds[startIndex + i];
                ItemData item = itemData?.items?.FirstOrDefault(entry => entry.id == itemId);
                if (item == null) continue;
                int row = i / 3;
                int column = i % 3;
                float xMin = 0.04f + column * 0.31f;
                float yMax = 0.84f - row * 0.18f;
                Button button = CreateHudButton($"Item_{i}", _equipmentPanel.transform,
                    new Vector2(xMin, yMax - 0.14f), new Vector2(xMin + 0.27f, yMax), () =>
                    {
                        if (target == null) return;
                        if (GameManager.Instance.inventoryManager.TryEquipStoredItem(target, itemId, out string reason))
                        {
                            GameManager.Instance.runManager?.SaveCurrentRun();
                            RefreshEquipmentPanel();
                        }
                        else
                        {
                            CreateTransientEquipmentMessage(reason);
                        }
                    });
                CreateHudText("Label", button.transform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f),
                    $"{item.name}\n중량 {item.weight}", 16f, TMPro.TextAlignmentOptions.Center);
            }

            if (pageCount > 1)
            {
                Button previous = CreateHudButton("PreviousPage", _equipmentPanel.transform,
                    new Vector2(0.35f, 0.02f), new Vector2(0.44f, 0.09f), () => { _equipmentPage--; RefreshEquipmentPanel(); });
                CreateHudText("Label", previous.transform, Vector2.zero, Vector2.one, "<", 18f, TMPro.TextAlignmentOptions.Center);
                CreateHudText("Page", _equipmentPanel.transform, new Vector2(0.45f, 0.02f), new Vector2(0.55f, 0.09f),
                    $"{_equipmentPage + 1}/{pageCount}", 16f, TMPro.TextAlignmentOptions.Center);
                Button next = CreateHudButton("NextPage", _equipmentPanel.transform,
                    new Vector2(0.56f, 0.02f), new Vector2(0.65f, 0.09f), () => { _equipmentPage++; RefreshEquipmentPanel(); });
                CreateHudText("Label", next.transform, Vector2.zero, Vector2.one, ">", 18f, TMPro.TextAlignmentOptions.Center);
                previous.interactable = _equipmentPage > 0;
                next.interactable = _equipmentPage < pageCount - 1;
            }
        }

        private void CreateTransientEquipmentMessage(string message)
        {
            Transform existing = _equipmentPanel?.transform.Find("Message");
            if (existing != null) Destroy(existing.gameObject);
            CreateHudText("Message", _equipmentPanel.transform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.10f),
                string.IsNullOrWhiteSpace(message) ? "장착할 수 없습니다." : message, 16f, TMPro.TextAlignmentOptions.Center);
        }

        private static void SetPreparationButtonState(Button button, bool interactable, bool primary = false)
        {
            if (button == null) return;

            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            if (image == null) return;

            if (!interactable)
            {
                image.color = new Color(0.11f, 0.12f, 0.13f, 0.75f);
            }
            else if (primary)
            {
                image.color = new Color(0.08f, 0.30f, 0.20f, 1f);
            }
            else
            {
                image.color = new Color(0.12f, 0.16f, 0.19f, 1f);
            }
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

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            titleObject.transform.SetParent(_characterSelectionPanel.transform, false);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.08f, 0.86f);
            titleRect.anchorMax = new Vector2(0.92f, 0.94f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var title = titleObject.GetComponent<TMPro.TextMeshProUGUI>();
            title.text = GameManager.Instance != null && GameManager.Instance.CurrentMode == GameMode.Infinite
                ? "무한 모드 - 육성 완료 캐릭터 5명 선택"
                : "육성 모드 - 아탈란테 메인, 초기 서포터 최대 3명";
            title.alignment = TMPro.TextAlignmentOptions.Center;
            title.fontSize = 24f;
            title.color = Color.white;

            var summaryObject = new GameObject("SelectionSummary", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            summaryObject.transform.SetParent(_characterSelectionPanel.transform, false);
            var summaryRect = summaryObject.GetComponent<RectTransform>();
            summaryRect.anchorMin = new Vector2(0.08f, 0.80f);
            summaryRect.anchorMax = new Vector2(0.92f, 0.855f);
            summaryRect.offsetMin = Vector2.zero;
            summaryRect.offsetMax = Vector2.zero;
            _selectionSummaryLabel = summaryObject.GetComponent<TMPro.TextMeshProUGUI>();
            _selectionSummaryLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _selectionSummaryLabel.fontSize = 18f;
            _selectionSummaryLabel.color = new Color(1f, 0.82f, 0.35f);

            _heroSelectionButtonImages.Clear();
            _heroSelectionButtonLabels.Clear();
            _heroSelectionButtonBaseLabels.Clear();

            var units = GameManager.Instance.unitDataList?.units?.FindAll(unit => unit.id < 100) ?? new List<UnitData>();
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameMode.Infinite)
            {
                var trainedIds = SaveSystem.LoadTrainedCharacters().unitIds;
                units = units.Where(unit => unit.canUseInInfinite && trainedIds.Contains(unit.id)).ToList();
            }
            else
            {
                units = units
                    .Where(unit => unit.canStartAsMain || unit.canStartAsSupport || SaveSystem.IsStarterUnlocked(unit.id))
                    .OrderByDescending(unit => unit.canStartAsMain)
                    .ThenByDescending(unit => unit.canStartAsSupport)
                    .ThenBy(unit => unit.id)
                    .ToList();
            }
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
                var buttonImage = buttonObject.GetComponent<Image>();
                buttonImage.color = HeroButtonDefaultColor;
                _heroSelectionButtonImages[unitId] = buttonImage;

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 8f);
                labelRect.offsetMax = new Vector2(-8f, -8f);
                var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
                string roleText = units[i].canStartAsMain
                    ? "메인"
                    : units[i].canStartAsSupport
                        ? "서포터"
                        : SaveSystem.IsStarterUnlocked(unitId)
                            ? "동료"
                            : "";
                string baseLabelText = string.IsNullOrWhiteSpace(roleText) ? units[i].name : $"{units[i].name}\n{roleText}";
                label.text = baseLabelText;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.fontSize = 16f;
                label.color = Color.white;
                _heroSelectionButtonLabels[unitId] = label;
                _heroSelectionButtonBaseLabels[unitId] = baseLabelText;

                buttonObject.GetComponent<Button>().onClick.AddListener(() =>
                {
                    CharacterSelectionManager.Instance.AddHero(unitId);
                    RefreshCharacterSelectionVisuals();
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

        private void RefreshCharacterSelectionVisuals()
        {
            var manager = CharacterSelectionManager.Instance;
            if (manager == null) return;

            foreach (var kvp in _heroSelectionButtonImages)
            {
                int unitId = kvp.Key;
                var entry = manager.Lineup.FirstOrDefault(e => e.UnitId == unitId);
                bool isSelected = entry != null;

                kvp.Value.color = isSelected ? HeroButtonSelectedColor : HeroButtonDefaultColor;
                var button = kvp.Value.GetComponent<Button>();
                if (button != null) button.interactable = !isSelected;

                if (_heroSelectionButtonLabels.TryGetValue(unitId, out var label) &&
                    _heroSelectionButtonBaseLabels.TryGetValue(unitId, out var baseText))
                {
                    label.text = isSelected
                        ? $"✓ {baseText}"
                        : baseText;
                }
            }

            if (_selectionSummaryLabel != null)
            {
                string mainText = manager.MainUnitId > 0 ? GetUnitDisplayName(manager.MainUnitId) : "미선택";
                var supportIds = manager.SupportUnitIds;
                string supportText = supportIds.Count > 0
                    ? string.Join(", ", supportIds.Select(GetUnitDisplayName))
                    : "미선택";
                _selectionSummaryLabel.text = $"메인: {mainText}   |   서포터 {supportIds.Count}명: {supportText}";
            }
        }

        private static string GetUnitDisplayName(int unitId)
        {
            return GameManager.Instance?.unitDataList?.units?.FirstOrDefault(unit => unit.id == unitId)?.name
                ?? unitId.ToString();
        }

        private void EnsureTrainingPhasePanel()
        {
            if (_trainingPhasePanel != null) return;

            var canvas = EnsureOverlayCanvas("TrainingPhaseCanvas");
            _trainingPhasePanel = new GameObject("TrainingPhasePanel", typeof(RectTransform), typeof(Image));
            _trainingPhasePanel.transform.SetParent(canvas.transform, false);
            var panelRect = _trainingPhasePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _trainingPhasePanel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.92f);

            CreateHudText("Title", _trainingPhasePanel.transform,
                new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.86f),
                "육성 페이즈\n집중 훈련할 스탯을 선택하세요", 30f, TMPro.TextAlignmentOptions.Center);

            _trainingInfoLabel = CreateHudText("Info", _trainingPhasePanel.transform,
                new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.66f),
                "", 22f, TMPro.TextAlignmentOptions.Center);

            // 5스탯 집중 훈련 버튼 (근력/민첩/체력/지능/행운).
            CreateTrainingStatButton(0, BaseEnums.PrimaryStat.STR, "근력 STR", "아이템 중량 한도");
            CreateTrainingStatButton(1, BaseEnums.PrimaryStat.DEX, "민첩 DEX", "공격속도");
            CreateTrainingStatButton(2, BaseEnums.PrimaryStat.CON, "체력 CON", "최대 체력·회복·보호막");
            CreateTrainingStatButton(3, BaseEnums.PrimaryStat.INT, "지능 INT", "마나 회복·최대 코드 수");
            CreateTrainingStatButton(4, BaseEnums.PrimaryStat.LUK, "행운 LUK", "치명타 확률");

            _trainingPhasePanel.SetActive(false);
        }

        // 육성 집중 훈련 버튼 하나를 5칸 가로 배열로 생성한다.
        private void CreateTrainingStatButton(int index, BaseEnums.PrimaryStat stat, string title, string desc)
        {
            float xMin = 0.06f + index * 0.184f;
            float xMax = xMin + 0.164f;

            var button = CreateHudButton($"TrainBtn_{stat}", _trainingPhasePanel.transform,
                new Vector2(xMin, 0.14f), new Vector2(xMax, 0.34f),
                () => OnTrainingFocusSelected(stat));

            CreateHudText("Title", button.transform,
                new Vector2(0.02f, 0.55f), new Vector2(0.98f, 0.95f),
                title, 20f, TMPro.TextAlignmentOptions.Center);
            CreateHudText("Desc", button.transform,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.5f),
                desc, 15f, TMPro.TextAlignmentOptions.Center);
        }

        // 집중 스탯 선택 시: 훈련 적용 -> 패널 숨김 -> 다음 진행.
        private void OnTrainingFocusSelected(BaseEnums.PrimaryStat stat)
        {
            GameManager.Instance?.CompleteTrainingPhaseWithFocus(stat);
        }

        // 육성 패널을 열 때 서포트/성장 정보를 갱신한다.
        // 각 스탯의 예상 강화량은 서포트 특기(클래스 주 스탯) 일치 보너스를 반영한다.
        private void RefreshTrainingInfo()
        {
            if (_trainingInfoLabel == null) return;

            int supports = TrainingManager.GetSupportCount();
            var main = TrainingManager.GetMainUnit();
            string mainName = main != null ? main.UnitName : "메인";
            int trainingLv = main != null ? main.TrainingLevel : 0;

            string gains =
                $"STR +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.STR)}   " +
                $"DEX +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.DEX)}   " +
                $"CON +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.CON)}   " +
                $"INT +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.INT)}   " +
                $"LUK +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.LUK)}";

            _trainingInfoLabel.text =
                $"{mainName} · 트레이닝 Lv.{trainingLv} · 서포트 {supports}명\n" +
                $"예상 강화량:  {gains}\n훈련 실패 없음 · 특기 훈련 참여 시 우정 상승 및 스킬 전수 판정";
        }

        private void EnsureEventStagePanel()
        {
            if (_eventStagePanel != null) return;

            var canvas = EnsureOverlayCanvas("EventStageCanvas");
            _eventStagePanel = new GameObject("EventStagePanel", typeof(RectTransform), typeof(Image));
            _eventStagePanel.transform.SetParent(canvas.transform, false);
            var panelRect = _eventStagePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _eventStagePanel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.035f, 0.97f);

            _eventTitleText = CreateHudText("Title", _eventStagePanel.transform,
                new Vector2(0.14f, 0.82f), new Vector2(0.86f, 0.92f), "사건", 34f, TMPro.TextAlignmentOptions.Center);
            _eventSpeakerText = CreateHudText("Speaker", _eventStagePanel.transform,
                new Vector2(0.18f, 0.66f), new Vector2(0.40f, 0.74f), "", 20f, TMPro.TextAlignmentOptions.MidlineLeft);
            _eventStageText = CreateHudText("Dialogue", _eventStagePanel.transform,
                new Vector2(0.18f, 0.45f), new Vector2(0.82f, 0.66f), "", 26f, TMPro.TextAlignmentOptions.TopLeft);
            _eventStageText.textWrappingMode = TMPro.TextWrappingModes.Normal;

            _eventNextButton = CreateHudButton("Next", _eventStagePanel.transform,
                new Vector2(0.68f, 0.30f), new Vector2(0.82f, 0.37f), () => GameManager.Instance?.AdvanceEventDialogue());
            CreateHudText("Label", _eventNextButton.transform, Vector2.zero, Vector2.one, "다음", 18f, TMPro.TextAlignmentOptions.Center);

            _eventChoiceButtons = new Button[3];
            _eventChoiceTexts = new TMPro.TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                float yMax = 0.40f - i * 0.10f;
                Button button = CreateHudButton($"Choice_{i}", _eventStagePanel.transform,
                    new Vector2(0.22f, yMax - 0.075f), new Vector2(0.78f, yMax), () => { });
                TMPro.TextMeshProUGUI label = CreateHudText("Label", button.transform,
                    new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f), "", 18f, TMPro.TextAlignmentOptions.Center);
                label.textWrappingMode = TMPro.TextWrappingModes.Normal;
                _eventChoiceButtons[i] = button;
                _eventChoiceTexts[i] = label;
                button.gameObject.SetActive(false);
            }

            _eventStagePanel.SetActive(false);
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
