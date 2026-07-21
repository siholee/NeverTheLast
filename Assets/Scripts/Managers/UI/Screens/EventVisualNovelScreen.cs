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
        private const float AutoAdvanceDelay = 1.4f; // AUTO 모드에서 다음 대사까지 대기 시간

        /// <summary>
        /// 초상화 하단 위치(뷰포트 비율). 대사/이름표 배치의 기준선이며,
        /// 이 값보다 아래는 텍스트 영역이므로 초상화가 침범하지 않는다.
        /// </summary>
        private const float PortraitBottom = 0.30f;

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

        private Button _autoButton;
        private Button _skipButton;
        private TextMeshProUGUI _autoLabel;

        private Image _flashImage;

        private Coroutine _typingRoutine;
        private Coroutine _introRoutine;
        private Coroutine _indicatorRoutine;
        private Coroutine _autoRoutine;
        private Coroutine _effectRoutine;
        private bool _isTyping;
        private bool _autoAdvance;
        private bool _hasMoreDialogue;
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
            _hasMoreDialogue = dialogueIndex < dialogueCount;

            if (_hasMoreDialogue)
            {
                StageEventDialogueData line = eventData.dialogue[dialogueIndex];
                SetSpeaker(line.speaker);
                SetPortrait(ResolvePortraitPath(line), animate: isNewEvent);
                SetChoicesVisible(false, eventData);
                SetAdvanceCatcherEnabled(true);
                SetAuxiliaryButtonsVisible(true);
                PlayLineAudio(line);
                PlayLineEffect(line.effect);
                TypeDialogue(line.text ?? "", line.textSpeed);
                return;
            }

            // 대사가 끝나면 선택지 단계로 넘어간다.
            SetSpeaker(null);
            SetChoicesVisible(true, eventData);
            SetAdvanceCatcherEnabled(false);
            SetAuxiliaryButtonsVisible(false);
            SetAutoAdvance(false);
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

            _hasMoreDialogue = false;
            SetSpeaker("결과");
            SetChoicesVisible(false, null);
            SetAdvanceCatcherEnabled(true, isResolution: true);
            SetAuxiliaryButtonsVisible(false);
            SetAutoAdvance(false);
            TypeDialogue(message ?? "사건이 끝났다.");
        }

        public void Hide()
        {
            if (_root == null) return;

            StopRoutine(ref _typingRoutine);
            StopRoutine(ref _introRoutine);
            StopRoutine(ref _indicatorRoutine);
            StopRoutine(ref _autoRoutine);
            StopRoutine(ref _effectRoutine);
            if (_flashImage != null) _flashImage.gameObject.SetActive(false);
            _isTyping = false;
            _hasMoreDialogue = false;
            SetAutoAdvance(false);
            _currentEventId = null;
            _currentPortraitPath = null;
            _root.SetActive(false);
        }

        // ===== AUTO / SKIP =====

        private void ToggleAuto()
        {
            SetAutoAdvance(!_autoAdvance);
            // 이미 대사 출력이 끝난 상태에서 켜면 즉시 자동 진행을 예약한다.
            if (_autoAdvance && !_isTyping) ScheduleAutoAdvance();
        }

        private void SetAutoAdvance(bool enabled)
        {
            _autoAdvance = enabled;
            if (!enabled) StopRoutine(ref _autoRoutine);
            if (_autoLabel != null)
            {
                _autoLabel.text = enabled ? "AUTO ●" : "AUTO";
                _autoLabel.color = enabled ? UISpriteFactory.Palette.Accent : UISpriteFactory.Palette.TextPrimary;
            }
        }

        private void ScheduleAutoAdvance()
        {
            if (!_autoAdvance || !_hasMoreDialogue) return;
            StopRoutine(ref _autoRoutine);
            _autoRoutine = _coroutineRunner.StartCoroutine(AutoAdvanceRoutine());
        }

        private IEnumerator AutoAdvanceRoutine()
        {
            yield return new WaitForSecondsRealtime(AutoAdvanceDelay);
            _autoRoutine = null;
            if (_autoAdvance && !_isTyping && _hasMoreDialogue)
            {
                GameManager.Instance?.AdvanceEventDialogue();
            }
        }

        /// <summary>남은 대사를 건너뛰고 선택지(또는 결과)까지 진행한다.</summary>
        private void SkipToChoices()
        {
            SetAutoAdvance(false);

            // AdvanceEventDialogue가 다시 Show를 호출해 _hasMoreDialogue를 갱신하므로
            // 무한 루프 방지용 상한만 두고 반복한다.
            const int guard = 64;
            for (int i = 0; i < guard && _hasMoreDialogue; i++)
            {
                CompleteTypingImmediately();
                GameManager.Instance?.AdvanceEventDialogue();
            }
        }

        /// <summary>AUTO/SKIP은 대사 진행 중에만 노출한다(선택지·결과 화면에서는 숨김).</summary>
        private void SetAuxiliaryButtonsVisible(bool visible)
        {
            if (_autoButton != null) _autoButton.gameObject.SetActive(visible);
            if (_skipButton != null) _skipButton.gameObject.SetActive(visible);
        }

        // ===== 표시 로직 =====

        private void SetSpeaker(string speaker)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (_namePlate != null) _namePlate.SetActive(hasSpeaker);
            if (_speakerText != null) _speakerText.text = hasSpeaker ? speaker : "";
        }

        private void TypeDialogue(string text, float speedScale = 1f)
        {
            StopRoutine(ref _typingRoutine);
            StopRoutine(ref _indicatorRoutine);
            if (_continueIndicator != null) _continueIndicator.SetActive(false);

            _pendingFullText = text ?? "";
            if (_dialogueText == null) return;

            _typingRoutine = _coroutineRunner.StartCoroutine(TypewriterRoutine(_pendingFullText, speedScale));
        }

        private IEnumerator TypewriterRoutine(string fullText, float speedScale)
        {
            _isTyping = true;
            _dialogueText.text = fullText;
            _dialogueText.maxVisibleCharacters = 0;
            _dialogueText.ForceMeshUpdate();

            int total = _dialogueText.textInfo.characterCount;
            // 게임 속도 배율(Time.timeScale)의 영향을 받지 않도록 unscaled 시간을 쓴다.
            // textSpeed가 지정되지 않은(0) 대사는 기본 속도를 쓴다.
            float scale = speedScale > 0.01f ? speedScale : 1f;
            float delay = 1f / (CharsPerSecond * scale);

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

            // 대사 출력이 끝난 시점이 AUTO 진행의 기준점이다.
            ScheduleAutoAdvance();
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
        /// emotion이 지정되면 "{초상화}_{표정}"을 우선 시도하고, 파일이 없으면 기본 초상화로 폴백한다.
        /// </summary>
        private static string ResolvePortraitPath(StageEventDialogueData line)
        {
            if (line == null) return null;

            string basePortrait = line.portrait;
            if (string.IsNullOrWhiteSpace(basePortrait))
            {
                if (string.IsNullOrWhiteSpace(line.speaker)) return null;

                List<UnitData> units = GameManager.Instance?.unitDataList?.units;
                UnitData match = units?.FirstOrDefault(unit =>
                    unit != null && string.Equals(unit.name?.Trim(), line.speaker.Trim(), System.StringComparison.OrdinalIgnoreCase));
                basePortrait = match?.portrait;
            }

            if (string.IsNullOrWhiteSpace(basePortrait)) return null;
            if (string.IsNullOrWhiteSpace(line.emotion)) return basePortrait;

            // 표정 이미지가 준비되어 있으면 사용하고, 없으면 기본 초상화를 쓴다.
            string emotionPath = $"{basePortrait}_{line.emotion.Trim()}";
            return Resources.Load<Sprite>($"Sprite/Portraits/{emotionPath}") != null ? emotionPath : basePortrait;
        }

        // ===== 사운드 / 연출 =====

        /// <summary>대사에 지정된 BGM/효과음을 재생한다. 오디오 에셋이 없으면 조용히 넘어간다.</summary>
        private static void PlayLineAudio(StageEventDialogueData line)
        {
            if (line == null) return;

            AudioManager audio = AudioManager.EnsureExists();
            if (!string.IsNullOrWhiteSpace(line.bgm))
            {
                if (string.Equals(line.bgm.Trim(), "stop", System.StringComparison.OrdinalIgnoreCase))
                {
                    audio.StopBgm();
                }
                else
                {
                    audio.PlayBgm(line.bgm.Trim());
                }
            }

            audio.PlaySfx(line.sfx);
        }

        /// <summary>대사에 지정된 연출 효과를 실행한다(shake / bounce / flash).</summary>
        private void PlayLineEffect(string effect)
        {
            if (string.IsNullOrWhiteSpace(effect)) return;

            switch (effect.Trim().ToLowerInvariant())
            {
                case "shake":
                    StopRoutine(ref _effectRoutine);
                    _effectRoutine = _coroutineRunner.StartCoroutine(ShakeRoutine());
                    break;
                case "bounce":
                    StopRoutine(ref _effectRoutine);
                    _effectRoutine = _coroutineRunner.StartCoroutine(BounceRoutine());
                    break;
                case "flash":
                    StopRoutine(ref _effectRoutine);
                    _effectRoutine = _coroutineRunner.StartCoroutine(FlashRoutine());
                    break;
                case "none":
                    break;
                default:
                    Debug.LogWarning($"[사건] 알 수 없는 연출 효과: {effect}");
                    break;
            }
        }

        /// <summary>초상화를 좌우로 흔든다(충격/동요).</summary>
        private IEnumerator ShakeRoutine()
        {
            if (_portraitRect == null) yield break;

            const float duration = 0.35f;
            const float magnitude = 18f;
            Vector2 origin = _portraitRect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / duration);
                float offset = Mathf.Sin(elapsed * 52f) * magnitude * damper;
                _portraitRect.anchoredPosition = origin + new Vector2(offset, 0f);
                yield return null;
            }

            _portraitRect.anchoredPosition = origin;
            _effectRoutine = null;
        }

        /// <summary>초상화를 살짝 위로 튀긴다(놀람/강조).</summary>
        private IEnumerator BounceRoutine()
        {
            if (_portraitRect == null) yield break;

            const float duration = 0.30f;
            const float height = 26f;
            Vector2 origin = _portraitRect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 위로 솟았다가 내려오는 포물선
                float offset = Mathf.Sin(t * Mathf.PI) * height;
                _portraitRect.anchoredPosition = origin + new Vector2(0f, offset);
                yield return null;
            }

            _portraitRect.anchoredPosition = origin;
            _effectRoutine = null;
        }

        /// <summary>화면 전체를 흰색으로 번쩍인다(번개/충격).</summary>
        private IEnumerator FlashRoutine()
        {
            if (_flashImage == null) yield break;

            const float duration = 0.32f;
            _flashImage.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Color color = _flashImage.color;
                color.a = Mathf.Lerp(0.85f, 0f, t);
                _flashImage.color = color;
                yield return null;
            }

            _flashImage.gameObject.SetActive(false);
            _effectRoutine = null;
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
                return; // 같은 화자가 같은 표정으로 이어서 말하는 경우 다시 등장 연출을 하지 않는다.
            }

            // 이미 초상화가 떠 있는 상태에서 다른 이미지로 바뀌면 표정 변화로 보고 살짝 튀겨준다.
            bool isExpressionChange = !animate
                && _portraitImage.gameObject.activeSelf
                && !string.IsNullOrEmpty(_currentPortraitPath);

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

                if (isExpressionChange)
                {
                    StopRoutine(ref _effectRoutine);
                    _effectRoutine = _coroutineRunner.StartCoroutine(BounceRoutine());
                }
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

            // 1) 배경 살짝 눌러주기 (블루 아카이브는 장면을 완전히 덮지 않는다)
            var backdrop = CreateRect("Backdrop", _root.transform, Vector2.zero, Vector2.one);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = UISpriteFactory.Palette.Backdrop;

            // 2) 클릭 캐처(대사 진행). 선택지/보조 버튼보다 먼저 만들어 아래 레이어에 둔다.
            var catcher = CreateRect("AdvanceCatcher", _root.transform, Vector2.zero, Vector2.one);
            var catcherImage = catcher.AddComponent<Image>();
            catcherImage.color = new Color(0f, 0f, 0f, 0f); // 투명하지만 레이캐스트는 받음
            _advanceCatcher = catcher.AddComponent<Button>();
            _advanceCatcher.transition = Selectable.Transition.None;
            _advanceCatcher.targetGraphic = catcherImage;

            // 3) 화자 초상화 — 화면 오른쪽에 크게 세운다(블루 아카이브 스토리 구도).
            //    하단(y)은 대사 영역보다 위에서 끝나야 한다. 초상화가 대사 뒤에 깔리면
            //    텍스트가 일러스트에 묻혀 읽기 어려워진다.
            var portrait = CreateRect("Portrait", _root.transform, new Vector2(0.42f, PortraitBottom), new Vector2(0.90f, 1.02f));
            _portraitRect = portrait.GetComponent<RectTransform>();
            _portraitImage = portrait.AddComponent<Image>();
            _portraitImage.preserveAspect = true;
            _portraitImage.raycastTarget = false;
            portrait.SetActive(false);

            // 4) 하단 스크림: 딱딱한 상자 대신 그라데이션으로 텍스트 가독성만 확보.
            //    초상화 하단과 겹치도록 조금 높게 잡아 인물의 발치가 자연스럽게 어두워지게 한다.
            var scrim = CreateRect("BottomScrim", _root.transform, new Vector2(0f, 0f), new Vector2(1f, 0.46f));
            var scrimImage = scrim.AddComponent<Image>();
            scrimImage.sprite = UISpriteFactory.VerticalGradient(96,
                UISpriteFactory.Palette.ScrimTop, UISpriteFactory.Palette.ScrimBottom);
            scrimImage.type = Image.Type.Simple;
            scrimImage.raycastTarget = false;

            // 5) 제목 — 좌상단에 얇은 하늘색 강조 바와 함께
            var titleBar = CreateRect("TitleAccent", _root.transform, new Vector2(0.06f, 0.928f), new Vector2(0.075f, 0.965f));
            var titleBarImage = titleBar.AddComponent<Image>();
            titleBarImage.sprite = UISpriteFactory.Parallelogram(36, 8, UISpriteFactory.Palette.Accent);
            titleBarImage.type = Image.Type.Sliced;
            titleBarImage.raycastTarget = false;

            _titleText = CreateText("Title", _root.transform, new Vector2(0.085f, 0.925f), new Vector2(0.60f, 0.968f),
                "사건", 30f, TextAlignmentOptions.Left, UISpriteFactory.Palette.TextPrimary);
            ApplyTextShadow(_titleText);

            // 6) 화자 이름표 — 기울어진 평행사변형(블루 아카이브 시그니처).
            //    초상화 하단 경계에 걸치도록 두어 인물과 대사를 시각적으로 잇는다.
            _namePlate = CreateRect("NamePlate", _root.transform, new Vector2(0.075f, PortraitBottom - 0.005f), new Vector2(0.30f, PortraitBottom + 0.055f));
            var nameImage = _namePlate.AddComponent<Image>();
            nameImage.sprite = UISpriteFactory.Parallelogram(48, 12, UISpriteFactory.Palette.Accent);
            nameImage.type = Image.Type.Sliced;
            nameImage.raycastTarget = false;
            _speakerText = CreateText("Speaker", _namePlate.transform, new Vector2(0.08f, 0f), new Vector2(0.92f, 1f),
                "", 22f, TextAlignmentOptions.Center, Color.white);
            _speakerText.fontStyle = FontStyles.Bold;
            // 화자 이름이 길어도 이름표를 넘치지 않게 축소한다.
            _speakerText.enableAutoSizing = true;
            _speakerText.fontSizeMax = 22f;
            _speakerText.fontSizeMin = 13f;
            _namePlate.SetActive(false);

            // 7) 대사 — 장면 위에 바로 얹고 그림자로 가독성 확보.
            //    상단은 초상화 하단(=이름표) 아래에서 시작해 일러스트와 겹치지 않는다.
            _dialogueText = CreateText("Dialogue", _root.transform, new Vector2(0.075f, 0.075f), new Vector2(0.90f, PortraitBottom - 0.015f),
                "", 28f, TextAlignmentOptions.TopLeft, UISpriteFactory.Palette.TextPrimary);
            _dialogueText.textWrappingMode = TextWrappingModes.Normal;
            _dialogueText.lineSpacing = 14f;
            _dialogueText.enableAutoSizing = true;
            _dialogueText.fontSizeMax = 28f;
            _dialogueText.fontSizeMin = 18f;
            ApplyTextShadow(_dialogueText);

            // 8) 계속 진행 표시(▼)
            _continueIndicator = CreateRect("ContinueIndicator", _root.transform, new Vector2(0.905f, 0.09f), new Vector2(0.945f, 0.135f));
            var indicatorText = _continueIndicator.AddComponent<TextMeshProUGUI>();
            indicatorText.text = "▼";
            indicatorText.fontSize = 24f;
            indicatorText.alignment = TextAlignmentOptions.Center;
            indicatorText.color = UISpriteFactory.Palette.Accent;
            indicatorText.raycastTarget = false;
            _continueIndicator.SetActive(false);

            // 9) 보조 버튼(AUTO / SKIP) — 우상단 필. 캐처보다 위 레이어라 클릭이 먼저 잡힌다.
            _autoButton = CreatePillButton("AutoButton", _root.transform,
                new Vector2(0.795f, 0.925f), new Vector2(0.875f, 0.972f), "AUTO", out _autoLabel);
            _autoButton.onClick.AddListener(ToggleAuto);

            _skipButton = CreatePillButton("SkipButton", _root.transform,
                new Vector2(0.885f, 0.925f), new Vector2(0.955f, 0.972f), "SKIP", out _);
            _skipButton.onClick.AddListener(SkipToChoices);

            // 10) 선택지 카드 — 흰 카드 + 남색 글씨 + 하늘색 강조 스트라이프
            _choiceButtons = new Button[3];
            _choiceLabels = new TextMeshProUGUI[3];
            _choiceCostLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                // 첫 선택지가 가장 위에 오도록 위에서 아래로 쌓는다.
                float top = 0.70f - i * 0.115f;
                var card = CreateRect($"Choice_{i}", _root.transform,
                    new Vector2(0.24f, top - 0.098f), new Vector2(0.76f, top));

                var cardImage = card.AddComponent<Image>();
                cardImage.sprite = UISpriteFactory.RoundedRect(12, UISpriteFactory.Palette.CardFill, UISpriteFactory.Palette.CardBorder, 2);
                cardImage.type = Image.Type.Sliced;

                var button = card.AddComponent<Button>();
                button.targetGraphic = cardImage;
                // 틴트는 스프라이트 색에 곱해지므로 1을 넘기면 클램프된다.
                // 평소를 살짝 눌러두고 호버에서 원색(흰 카드)으로 밝아지게 한다.
                var colors = button.colors;
                colors.normalColor = new Color(0.90f, 0.93f, 0.97f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.72f, 0.82f, 0.93f, 1f);
                colors.selectedColor = new Color(0.90f, 0.93f, 0.97f, 1f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;

                // 카드 왼쪽 하늘색 스트라이프
                var stripe = CreateRect("Stripe", card.transform, new Vector2(0f, 0.12f), new Vector2(0.012f, 0.88f));
                var stripeImage = stripe.AddComponent<Image>();
                stripeImage.sprite = UISpriteFactory.Parallelogram(32, 6, UISpriteFactory.Palette.Accent);
                stripeImage.type = Image.Type.Sliced;
                stripeImage.raycastTarget = false;

                _choiceLabels[i] = CreateText("Label", card.transform, new Vector2(0.045f, 0.38f), new Vector2(0.965f, 0.93f),
                    "", 21f, TextAlignmentOptions.Left, UISpriteFactory.Palette.CardText);
                _choiceLabels[i].textWrappingMode = TextWrappingModes.Normal;
                _choiceLabels[i].fontStyle = FontStyles.Bold;

                _choiceCostLabels[i] = CreateText("Subtitle", card.transform, new Vector2(0.045f, 0.08f), new Vector2(0.965f, 0.38f),
                    "", 16f, TextAlignmentOptions.Left, UISpriteFactory.Palette.CardTextMuted);
                _choiceCostLabels[i].richText = true;

                _choiceButtons[i] = button;
                card.SetActive(false);
            }

            // 11) 플래시 오버레이 — 최상단. 클릭은 통과시킨다.
            var flash = CreateRect("FlashOverlay", _root.transform, Vector2.zero, Vector2.one);
            _flashImage = flash.AddComponent<Image>();
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.raycastTarget = false;
            flash.SetActive(false);

            // 렌더 순서를 명시적으로 고정한다(뒤 → 앞).
            // Unity UI는 계층 순서대로 그리므로, 생성 순서에 의존하면 나중에 요소를 추가하다가
            // 텍스트가 일러스트 뒤로 들어가는 식의 사고가 나기 쉽다.
            var renderOrder = new List<Transform>
            {
                backdrop.transform,
                catcher.transform,
                portrait.transform,      // 인물 일러스트
                scrim.transform,         // 인물 발치를 눌러 텍스트 가독성 확보
                titleBar.transform,
                _titleText.transform,
                _namePlate.transform,
                _dialogueText.transform, // 반드시 초상화/스크림보다 위
                _continueIndicator.transform,
                _autoButton.transform,
                _skipButton.transform,
            };
            foreach (Button choice in _choiceButtons) renderOrder.Add(choice.transform);
            renderOrder.Add(flash.transform);

            foreach (Transform layer in renderOrder) layer.SetAsLastSibling();

            _root.SetActive(false);
        }

        /// <summary>장면 위에 얹히는 텍스트의 가독성을 위해 부드러운 그림자를 준다.</summary>
        private static void ApplyTextShadow(TextMeshProUGUI label)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>AUTO/SKIP용 작은 필 버튼.</summary>
        private static Button CreatePillButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            string label, out TextMeshProUGUI labelText)
        {
            var go = CreateRect(objectName, parent, anchorMin, anchorMax);
            var image = go.AddComponent<Image>();
            image.sprite = UISpriteFactory.RoundedRect(14, UISpriteFactory.Palette.PillFill, UISpriteFactory.Palette.PillBorder, 2);
            image.type = Image.Type.Sliced;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.86f, 0.90f, 0.95f, 1f);
            colors.highlightedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            labelText = CreateText("Label", go.transform, Vector2.zero, Vector2.one,
                label, 17f, TextAlignmentOptions.Center, UISpriteFactory.Palette.TextPrimary);
            labelText.fontStyle = FontStyles.Bold;
            return button;
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
