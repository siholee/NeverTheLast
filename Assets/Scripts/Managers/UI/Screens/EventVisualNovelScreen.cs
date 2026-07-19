using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 사건(이벤트) 비주얼 노벨 화면.
    /// 우마무스메식 연출: 배경 암전 → 화자 초상화 등장 → 이름표 + 대사창 타자기 효과 → 선택지 카드.
    /// UI는 프리팹 없이 런타임에 생성한다(기존 UIManager 방식과 동일).
    ///
    /// 흐름 제어는 GameManager가 담당한다. 이 클래스는 표시만 책임진다:
    ///  - 대사 진행: GameManager.AdvanceEventDialogue()
    ///  - 선택지 선택: GameManager.SelectEventChoice(id)
    ///  - 결과 확인: GameManager.CompleteEventStage()
    /// </summary>
    public class EventVisualNovelScreen
    {
        private const float CharsPerSecond = 42f;   // 타자기 속도
        private const float FadeInDuration = 0.28f;
        private const float PortraitSlideDuration = 0.34f;

        private readonly MonoBehaviour _coroutineRunner;

        private GameObject _root;
        private CanvasGroup _rootGroup;
        private Image _portraitImage;
        private RectTransform _portraitRect;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _speakerText;
        private GameObject _namePlate;
        private TextMeshProUGUI _dialogueText;
        private GameObject _continueIndicator;
        private Button _advanceCatcher;
        private Button[] _choiceButtons;
        private TextMeshProUGUI[] _choiceLabels;
        private TextMeshProUGUI[] _choiceCostLabels;

        private Coroutine _typingRoutine;
        private Coroutine _introRoutine;
        private Coroutine _indicatorRoutine;
        private bool _isTyping;
        private string _pendingFullText = "";
        private string _currentEventId;
        private string _currentPortraitPath;

        public EventVisualNovelScreen(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        // ===== 공개 API (UIManager가 위임) =====

        /// <summary>사건 대사/선택지 표시. GameManager가 대사 인덱스를 올릴 때마다 호출된다.</summary>
        public void Show(StageEventData eventData, int dialogueIndex)
        {
            if (eventData == null) return;

            EnsureBuilt();
            bool isNewEvent = _currentEventId != eventData.id;
            _currentEventId = eventData.id;

            _root.SetActive(true);
            if (_titleText != null) _titleText.text = eventData.title ?? "사건";

            if (isNewEvent)
            {
                PlayIntro();
            }

            int dialogueCount = eventData.dialogue?.Count ?? 0;
            if (dialogueIndex < dialogueCount)
            {
                StageEventDialogueData line = eventData.dialogue[dialogueIndex];
                SetSpeaker(line.speaker);
                SetPortrait(ResolvePortraitPath(line), animate: isNewEvent);
                SetChoicesVisible(false, eventData);
                SetAdvanceCatcherEnabled(true);
                TypeDialogue(line.text ?? "");
                return;
            }

            // 대사가 끝나면 선택지 단계로 넘어간다.
            SetSpeaker(null);
            SetChoicesVisible(true, eventData);
            SetAdvanceCatcherEnabled(false);
            TypeDialogue("어떻게 하시겠습니까?");
        }

        /// <summary>대사창 문구만 교체(선택 실패 안내 등).</summary>
        public void ShowMessage(string message)
        {
            EnsureBuilt();
            _root.SetActive(true);
            TypeDialogue(message ?? "");
        }

        /// <summary>사건 결과 표시. 확인 시 CompleteEventStage로 흐름을 잇는다.</summary>
        public void ShowResolution(string message)
        {
            EnsureBuilt();
            _root.SetActive(true);

            SetSpeaker("결과");
            SetChoicesVisible(false, null);
            SetAdvanceCatcherEnabled(true, isResolution: true);
            TypeDialogue(message ?? "사건이 끝났다.");
        }

        public void Hide()
        {
            if (_root == null) return;

            StopRoutine(ref _typingRoutine);
            StopRoutine(ref _introRoutine);
            StopRoutine(ref _indicatorRoutine);
            _isTyping = false;
            _currentEventId = null;
            _currentPortraitPath = null;
            _root.SetActive(false);
        }

        // ===== 표시 로직 =====

        private void SetSpeaker(string speaker)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (_namePlate != null) _namePlate.SetActive(hasSpeaker);
            if (_speakerText != null) _speakerText.text = hasSpeaker ? speaker : "";
        }

        private void TypeDialogue(string text)
        {
            StopRoutine(ref _typingRoutine);
            StopRoutine(ref _indicatorRoutine);
            if (_continueIndicator != null) _continueIndicator.SetActive(false);

            _pendingFullText = text ?? "";
            if (_dialogueText == null) return;

            _typingRoutine = _coroutineRunner.StartCoroutine(TypewriterRoutine(_pendingFullText));
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            _isTyping = true;
            _dialogueText.text = fullText;
            _dialogueText.maxVisibleCharacters = 0;
            _dialogueText.ForceMeshUpdate();

            int total = _dialogueText.textInfo.characterCount;
            // 게임 속도 배율(Time.timeScale)의 영향을 받지 않도록 unscaled 시간을 쓴다.
            float delay = 1f / CharsPerSecond;

            for (int visible = 0; visible <= total; visible++)
            {
                _dialogueText.maxVisibleCharacters = visible;
                yield return new WaitForSecondsRealtime(delay);
            }

            _dialogueText.maxVisibleCharacters = total;
            _isTyping = false;
            _typingRoutine = null;
            StartContinueIndicator();
        }

        /// <summary>타자기 진행 중이면 즉시 완성, 이미 완성됐으면 다음 대사로 진행.</summary>
        private void OnAdvanceClicked()
        {
            if (_isTyping)
            {
                CompleteTypingImmediately();
                return;
            }

            GameManager.Instance?.AdvanceEventDialogue();
        }

        private void OnResolutionClicked()
        {
            if (_isTyping)
            {
                CompleteTypingImmediately();
                return;
            }

            GameManager.Instance?.CompleteEventStage();
        }

        private void CompleteTypingImmediately()
        {
            StopRoutine(ref _typingRoutine);
            _isTyping = false;
            if (_dialogueText != null)
            {
                _dialogueText.text = _pendingFullText;
                _dialogueText.ForceMeshUpdate();
                _dialogueText.maxVisibleCharacters = _dialogueText.textInfo.characterCount;
            }
            StartContinueIndicator();
        }

        private void StartContinueIndicator()
        {
            if (_continueIndicator == null) return;
            _continueIndicator.SetActive(true);
            StopRoutine(ref _indicatorRoutine);
            _indicatorRoutine = _coroutineRunner.StartCoroutine(BlinkIndicatorRoutine());
        }

        private IEnumerator BlinkIndicatorRoutine()
        {
            var indicatorText = _continueIndicator.GetComponent<TextMeshProUGUI>();
            while (_continueIndicator != null && _continueIndicator.activeSelf)
            {
                float t = Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
                if (indicatorText != null)
                {
                    Color color = indicatorText.color;
                    color.a = Mathf.Lerp(0.25f, 1f, t);
                    indicatorText.color = color;
                }
                yield return null;
            }
        }

        private void SetAdvanceCatcherEnabled(bool enabled, bool isResolution = false)
        {
            if (_advanceCatcher == null) return;

            _advanceCatcher.gameObject.SetActive(enabled);
            _advanceCatcher.onClick.RemoveAllListeners();
            if (!enabled) return;

            if (isResolution)
            {
                _advanceCatcher.onClick.AddListener(OnResolutionClicked);
            }
            else
            {
                _advanceCatcher.onClick.AddListener(OnAdvanceClicked);
            }
        }

        private void SetChoicesVisible(bool visible, StageEventData eventData)
        {
            if (_choiceButtons == null) return;

            int choiceCount = visible ? (eventData?.choices?.Count ?? 0) : 0;
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                bool active = i < choiceCount;
                _choiceButtons[i].gameObject.SetActive(active);
                if (!active) continue;

                StageEventChoiceData choice = eventData.choices[i];
                _choiceLabels[i].text = choice.text ?? "";

                // 비용/전투 여부 등 부가 정보를 별도 줄에 표시
                string subtitle = BuildChoiceSubtitle(choice);
                _choiceCostLabels[i].gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
                _choiceCostLabels[i].text = subtitle;

                string choiceId = choice.id;
                _choiceButtons[i].onClick.RemoveAllListeners();
                _choiceButtons[i].onClick.AddListener(() => GameManager.Instance?.SelectEventChoice(choiceId));
            }
        }

        /// <summary>선택지 아래에 붙는 부가 설명(골드 비용, 전투 발생 등).</summary>
        private static string BuildChoiceSubtitle(StageEventChoiceData choice)
        {
            var parts = new List<string>();

            if (choice.goldCostPerStage > 0)
            {
                int stage = GameManager.Instance?.RoundManager?.Stage ?? 1;
                int cost = choice.goldCostPerStage * Mathf.Max(1, stage);
                int gold = GameManager.Instance?.inventoryManager?.Gold ?? 0;
                bool affordable = gold >= cost;
                string color = affordable ? "#D9A23A" : "#C05B5B";
                parts.Add($"<color={color}>골드 {cost}</color> <size=80%>(보유 {gold})</size>");
            }

            if (choice.battleEnemyId > 0)
            {
                parts.Add("<color=#C05B5B>전투 발생</color>");
            }

            if (choice.grantUnitId > 0)
            {
                parts.Add("<color=#5BA8C0>동료 합류 가능</color>");
            }

            if (choice.grantItemId > 0)
            {
                parts.Add("<color=#5BA8C0>아이템 획득 가능</color>");
            }

            return string.Join("   ", parts);
        }

        // ===== 초상화 =====

        /// <summary>
        /// 대사 줄의 화자를 초상화 경로로 해석한다.
        /// 1) 대사에 portrait가 지정되어 있으면 그것을 사용
        /// 2) 없으면 화자 이름을 유닛 데이터의 name과 대조해 해당 유닛 초상화를 사용
        /// 3) 둘 다 실패하면 초상화 없음(내레이션 취급)
        /// </summary>
        private static string ResolvePortraitPath(StageEventDialogueData line)
        {
            if (line == null) return null;

            if (!string.IsNullOrWhiteSpace(line.portrait))
            {
                return line.portrait;
            }

            if (string.IsNullOrWhiteSpace(line.speaker)) return null;

            List<UnitData> units = GameManager.Instance?.unitDataList?.units;
            UnitData match = units?.FirstOrDefault(unit =>
                unit != null && string.Equals(unit.name?.Trim(), line.speaker.Trim(), System.StringComparison.OrdinalIgnoreCase));
            return match?.portrait;
        }

        private void SetPortrait(string portraitName, bool animate)
        {
            if (_portraitImage == null) return;

            if (string.IsNullOrWhiteSpace(portraitName))
            {
                _portraitImage.gameObject.SetActive(false);
                _currentPortraitPath = null;
                return;
            }

            if (_currentPortraitPath == portraitName && _portraitImage.gameObject.activeSelf)
            {
                return; // 같은 화자가 이어서 말하는 경우 다시 등장 연출을 하지 않는다.
            }

            Sprite sprite = Resources.Load<Sprite>($"Sprite/Portraits/{portraitName}");
            if (sprite == null)
            {
                Debug.LogWarning($"[사건] 초상화를 찾을 수 없습니다: Sprite/Portraits/{portraitName}");
                _portraitImage.gameObject.SetActive(false);
                _currentPortraitPath = null;
                return;
            }

            _currentPortraitPath = portraitName;
            _portraitImage.sprite = sprite;
            _portraitImage.gameObject.SetActive(true);

            if (animate)
            {
                StopRoutine(ref _introRoutine);
                _introRoutine = _coroutineRunner.StartCoroutine(PortraitSlideRoutine());
            }
            else
            {
                // 연출 없이 교체할 때는 이전 등장 연출의 알파/오프셋이 남지 않도록 확정한다.
                _portraitImage.color = Color.white;
                if (_portraitRect != null) _portraitRect.anchoredPosition = Vector2.zero;
            }
        }

        private void PlayIntro()
        {
            StopRoutine(ref _introRoutine);
            _introRoutine = _coroutineRunner.StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            if (_rootGroup == null) yield break;

            float elapsed = 0f;
            _rootGroup.alpha = 0f;
            while (elapsed < FadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _rootGroup.alpha = Mathf.Clamp01(elapsed / FadeInDuration);
                yield return null;
            }
            _rootGroup.alpha = 1f;
            _introRoutine = null;
        }

        private IEnumerator PortraitSlideRoutine()
        {
            if (_portraitRect == null) yield break;

            // 아래에서 살짝 떠오르며 등장
            Vector2 target = new(0f, 0f);
            Vector2 start = new(0f, -60f);
            float elapsed = 0f;
            while (elapsed < PortraitSlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / PortraitSlideDuration));
                _portraitRect.anchoredPosition = Vector2.Lerp(start, target, t);
                if (_portraitImage != null)
                {
                    Color color = _portraitImage.color;
                    color.a = t;
                    _portraitImage.color = color;
                }
                yield return null;
            }

            _portraitRect.anchoredPosition = target;
            if (_portraitImage != null)
            {
                _portraitImage.color = Color.white;
            }
            _introRoutine = null;
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(routine);
            }
            routine = null;
        }

        // ===== UI 구성 =====

        private void EnsureBuilt()
        {
            if (_root != null) return;

            Canvas canvas = EnsureCanvas("EventStageCanvas");

            _root = CreateRect("EventVisualNovel", canvas.transform, Vector2.zero, Vector2.one);
            _rootGroup = _root.AddComponent<CanvasGroup>();

            // 1) 배경 암전
            var backdrop = CreateRect("Backdrop", _root.transform, Vector2.zero, Vector2.one);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = UISpriteFactory.Palette.Backdrop;

            // 2) 클릭 캐처(대사 진행). 선택지보다 먼저 만들어 아래 레이어에 둔다.
            var catcher = CreateRect("AdvanceCatcher", _root.transform, Vector2.zero, Vector2.one);
            var catcherImage = catcher.AddComponent<Image>();
            catcherImage.color = new Color(0f, 0f, 0f, 0f); // 투명하지만 레이캐스트는 받음
            _advanceCatcher = catcher.AddComponent<Button>();
            _advanceCatcher.transition = Selectable.Transition.None;
            _advanceCatcher.targetGraphic = catcherImage;

            // 3) 화자 초상화 (대사창 위, 화면 중앙)
            var portrait = CreateRect("Portrait", _root.transform, new Vector2(0.30f, 0.28f), new Vector2(0.70f, 0.90f));
            _portraitRect = portrait.GetComponent<RectTransform>();
            _portraitImage = portrait.AddComponent<Image>();
            _portraitImage.preserveAspect = true;
            _portraitImage.raycastTarget = false;
            portrait.SetActive(false);

            // 4) 제목 플레이트
            var titlePlate = CreateRect("TitlePlate", _root.transform, new Vector2(0.28f, 0.905f), new Vector2(0.72f, 0.965f));
            var titleImage = titlePlate.AddComponent<Image>();
            titleImage.sprite = UISpriteFactory.RoundedRect(14, UISpriteFactory.Palette.PanelFill, UISpriteFactory.Palette.Accent, 2);
            titleImage.type = Image.Type.Sliced;
            titleImage.raycastTarget = false;
            _titleText = CreateText("Title", titlePlate.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                "사건", 30f, TextAlignmentOptions.Center, UISpriteFactory.Palette.Accent);

            // 5) 대사창
            var dialogueBox = CreateRect("DialogueBox", _root.transform, new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.29f));
            var dialogueImage = dialogueBox.AddComponent<Image>();
            dialogueImage.sprite = UISpriteFactory.RoundedRect(18, UISpriteFactory.Palette.PanelFill, UISpriteFactory.Palette.PanelBorder, 2);
            dialogueImage.type = Image.Type.Sliced;
            dialogueImage.raycastTarget = false;

            _dialogueText = CreateText("Dialogue", dialogueBox.transform, new Vector2(0.035f, 0.12f), new Vector2(0.965f, 0.80f),
                "", 26f, TextAlignmentOptions.TopLeft, UISpriteFactory.Palette.TextPrimary);
            _dialogueText.textWrappingMode = TextWrappingModes.Normal;
            _dialogueText.lineSpacing = 12f;

            // 6) 화자 이름표 (대사창 좌상단에 걸치도록)
            _namePlate = CreateRect("NamePlate", dialogueBox.transform, new Vector2(0.025f, 0.84f), new Vector2(0.30f, 1.16f));
            var nameImage = _namePlate.AddComponent<Image>();
            nameImage.sprite = UISpriteFactory.RoundedRect(12, UISpriteFactory.Palette.Accent);
            nameImage.type = Image.Type.Sliced;
            nameImage.raycastTarget = false;
            _speakerText = CreateText("Speaker", _namePlate.transform, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f),
                "", 21f, TextAlignmentOptions.Center, new Color(0.08f, 0.07f, 0.04f));
            _speakerText.fontStyle = FontStyles.Bold;
            _namePlate.SetActive(false);

            // 7) 계속 진행 표시(▼)
            _continueIndicator = CreateRect("ContinueIndicator", dialogueBox.transform, new Vector2(0.94f, 0.06f), new Vector2(0.985f, 0.28f));
            var indicatorText = _continueIndicator.AddComponent<TextMeshProUGUI>();
            indicatorText.text = "▼";
            indicatorText.fontSize = 22f;
            indicatorText.alignment = TextAlignmentOptions.Center;
            indicatorText.color = UISpriteFactory.Palette.Accent;
            indicatorText.raycastTarget = false;
            _continueIndicator.SetActive(false);

            // 8) 선택지 카드 (대사창 위쪽에 쌓기). 캐처보다 나중에 생성 → 위 레이어에서 클릭을 받는다.
            _choiceButtons = new Button[3];
            _choiceLabels = new TextMeshProUGUI[3];
            _choiceCostLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                // 아래에서 위로 쌓되, 첫 선택지가 가장 위에 오도록 배치
                float top = 0.62f - i * 0.105f;
                var card = CreateRect($"Choice_{i}", _root.transform,
                    new Vector2(0.20f, top - 0.09f), new Vector2(0.80f, top));

                var cardImage = card.AddComponent<Image>();
                cardImage.sprite = UISpriteFactory.RoundedRect(14, UISpriteFactory.Palette.ChoiceFill, UISpriteFactory.Palette.ChoiceBorder, 2);
                cardImage.type = Image.Type.Sliced;

                var button = card.AddComponent<Button>();
                button.targetGraphic = cardImage;
                // 틴트는 스프라이트 색에 곱해지므로 1을 넘기면 클램프된다.
                // 기본값을 약간 어둡게 두고 호버에서 원색으로 밝아지게 한다.
                var colors = button.colors;
                colors.normalColor = new Color(0.82f, 0.84f, 0.88f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.66f, 0.70f, 0.78f, 1f);
                colors.selectedColor = new Color(0.82f, 0.84f, 0.88f, 1f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;

                _choiceLabels[i] = CreateText("Label", card.transform, new Vector2(0.04f, 0.36f), new Vector2(0.96f, 0.94f),
                    "", 21f, TextAlignmentOptions.Left, UISpriteFactory.Palette.TextPrimary);
                _choiceLabels[i].textWrappingMode = TextWrappingModes.Normal;

                _choiceCostLabels[i] = CreateText("Subtitle", card.transform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.36f),
                    "", 16f, TextAlignmentOptions.Left, UISpriteFactory.Palette.TextMuted);
                _choiceCostLabels[i].richText = true;

                _choiceButtons[i] = button;
                card.SetActive(false);
            }

            _root.SetActive(false);
        }

        // ===== 생성 헬퍼 =====

        private static Canvas EnsureCanvas(string objectName)
        {
            var canvasObject = GameObject.Find(objectName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(objectName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var newCanvas = canvasObject.GetComponent<Canvas>();
                newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                newCanvas.sortingOrder = 60; // 전투 HUD보다 위
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            return canvasObject.GetComponent<Canvas>();
        }

        private static GameObject CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            string text, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = CreateRect(objectName, parent, anchorMin, anchorMax);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }
    }
}
