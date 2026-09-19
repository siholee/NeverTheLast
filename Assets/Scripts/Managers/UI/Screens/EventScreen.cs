using System.Collections.Generic;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 사건 스테이지 화면. 비주얼 노벨 형태로 대사를 읽고 마지막에 선택지를 고른다.
    ///
    /// 레이아웃은 블루 아카이브식이다 — 스탠딩이 좌우에 서고, 화면 아래를 가로지르는
    /// 넓은 대사 상자가 깔리며, 이름표가 그 상자 위 왼쪽에 걸친다.
    /// 색과 톤은 나머지 화면과 같은 명일방주 계열이다(무채색 면 + 헤어라인 + 앰버 하나).
    ///
    /// 말하지 않는 쪽은 어둡게 눌러 뒤로 물린다. 이름표를 읽지 않아도
    /// <b>누가 말하는지가 밝기로 먼저 읽히는</b> 것이 이 배치의 핵심이다.
    ///
    /// 예전에는 가운데 작은 모달에 초상화 한 장을 붙여 놓는 형태였다.
    /// 그래서 <see cref="ModalScreen"/>을 상속하지 않고 화면 전체를 직접 조립한다.
    /// </summary>
    public class EventScreen
    {
        /// <summary>자동 진행에서 한 대사를 보여 주는 기본 시간(초).</summary>
        private const float AutoBaseDelay = 1.2f;

        /// <summary>글자 하나당 더해 주는 시간(초). 긴 대사는 그만큼 오래 머문다.</summary>
        private const float AutoDelayPerChar = 0.045f;

        /// <summary>자동 진행 대기 시간의 상한(초).</summary>
        private const float AutoMaxDelay = 6f;

        // ── 레이아웃(1920x1080 기준) ─────────────────────────────────
        private const float BoxMarginX = 250f;   // 대사 상자 좌우 여백
        private const float BoxBottom = 120f;    // 상자 아랫변 높이
        private const float BoxHeight = 214f;
        private const float PlateHeight = 54f;
        private const float PlateOverlap = 8f;   // 이름표가 상자 위로 걸치는 양
        private const float StandHeight = 1000f;
        private const float StandCenterX = 520f; // 화면 바깥쪽 끝에서 잰 스탠딩 중심

        /// <summary>
        /// 빗금 색. <b>흰색 + 낮은 알파를 쓰면 안 된다.</b>
        /// 선형 색공간에서 렌더링하므로 거의 검정인 배경 위에 알파 2%짜리 흰색을 얹으면
        /// sRGB로 되돌아올 때 밝기가 서너 배로 튀어 의도보다 훨씬 진하게 보인다.
        /// 면 색보다 한 단계만 밝은 <b>불투명</b> 회색을 쓰면 계산이 어긋날 일이 없다.
        /// </summary>
        private static readonly Color StripeInk = new(0.086f, 0.090f, 0.098f, 1f);

        /// <summary>말하지 않는 쪽에 곱하는 색. 밝기를 3분의 1로 눌러 뒤로 보낸다.</summary>
        private static readonly Color StandDim = new(0.34f, 0.36f, 0.38f, 0.85f);
        private static readonly Color StandLit = Color.white;

        private GameObject _root;
        private Image _leftStand;
        private Image _rightStand;
        private RectTransform _plate;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _progress;
        private TextMeshProUGUI _nextMark;
        private TextMeshProUGUI _eventTitle;
        private RectTransform _choiceArea;
        private GameObject _clickCatcher;
        private Button _auto;
        private Button _skip;

        private bool _autoPlay;
        private float _autoTimer;
        private bool _autoWaiting;
        private bool _canAdvance;

        // 연출(shake/bounce/flash)용. 대사가 바뀔 때마다 다시 감긴다.
        private RectTransform _shakeTarget;
        private Vector2 _shakeBase;
        private Image _flash;
        private string _effect;
        private float _effectTime;

        public bool IsVisible => _root != null && _root.activeSelf;

        // ── 조립 ─────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_root != null) return;

            Canvas canvas = UIBuild.Canvas("EventCanvas", 75);
            _root = new GameObject(nameof(EventScreen), typeof(RectTransform));
            _root.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_root.GetComponent<RectTransform>());
            Transform root = _root.transform;

            BuildBackdrop(root);
            BuildStandings(root);

            // 아무 데나 눌러도 넘어간다. 선택지·조작 칩보다 먼저 만들어 두면
            // 그것들이 형제 순서상 위에 놓여 클릭을 먼저 가져간다.
            _clickCatcher = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image));
            _clickCatcher.transform.SetParent(root, false);
            var catcher = _clickCatcher.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            UIBuild.Stretch(catcher.rectTransform);
            UIBuild.OnClick(_clickCatcher, AdvanceByClick);

            BuildBox(root);
            BuildPlate(root);
            BuildHeader(root);
            BuildControls(root);

            _choiceArea = UIBuild.Container("Choices", root);
            UIBuild.Stretch(_choiceArea);

            // 화면 번쩍임. 항상 맨 위에 있어야 하므로 마지막에 만든다.
            _flash = UIBuild.Solid("Flash", root, new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;
            UIBuild.Stretch(_flash.rectTransform);

            _root.SetActive(false);
        }

        private static void BuildBackdrop(Transform root)
        {
            Image background = UIBuild.Solid("Background", root, new Color(0.039f, 0.043f, 0.047f, 1f));
            UIBuild.Stretch(background.rectTransform);

            Image stripes = UIBuild.Solid("TopStripes", root, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(14, StripeInk, Color.clear);
            stripes.type = Image.Type.Tiled;
            stripes.color = Color.white;
            stripes.raycastTarget = false;
            stripes.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripes.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripes.rectTransform.pivot = new Vector2(0.5f, 1f);
            stripes.rectTransform.sizeDelta = new Vector2(0f, 180f);
            stripes.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void BuildStandings(Transform root)
        {
            _leftStand = MakeStanding(root, "LeftStanding", -1);
            _rightStand = MakeStanding(root, "RightStanding", 1);

            // 발치를 화면에 녹이는 암전. 대사 상자와 인물 사이의 경계를 지운다.
            var fadeGo = new GameObject("BottomFade", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(root, false);
            var fade = fadeGo.GetComponent<Image>();
            fade.sprite = UIShapes.VerticalGradient(64,
                new Color(0.031f, 0.035f, 0.039f, 0f), new Color(0.031f, 0.035f, 0.039f, 1f));
            fade.type = Image.Type.Simple;   // 1xN 스프라이트라 9-슬라이스가 없다
            fade.raycastTarget = false;
            fade.rectTransform.anchorMin = new Vector2(0f, 0f);
            fade.rectTransform.anchorMax = new Vector2(1f, 0f);
            fade.rectTransform.pivot = new Vector2(0.5f, 0f);
            fade.rectTransform.sizeDelta = new Vector2(0f, 520f);
            fade.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>좌 또는 우 스탠딩 자리. side가 -1이면 왼쪽, 1이면 오른쪽.</summary>
        private static Image MakeStanding(Transform root, string name, int side)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;

            RectTransform rect = image.rectTransform;
            float anchorX = side < 0 ? 0f : 1f;
            rect.anchorMin = new Vector2(anchorX, 0f);
            rect.anchorMax = new Vector2(anchorX, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(StandHeight, StandHeight);
            rect.anchoredPosition = new Vector2(side < 0 ? StandCenterX : -StandCenterX, -40f);
            return image;
        }

        private void BuildBox(Transform root)
        {
            Image box = UIBuild.Panel("Box", root, new Color(0.043f, 0.051f, 0.063f, 0.92f),
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            box.raycastTarget = false;
            RectTransform rect = box.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(BoxMarginX, BoxBottom);
            rect.offsetMax = new Vector2(-BoxMarginX, BoxBottom + BoxHeight);
            // 흔들림은 이 자리를 기준으로 더한다. 늘어난 앵커라 0,0이 제자리가 아니다.
            _shakeTarget = rect;
            _shakeBase = rect.anchoredPosition;

            Image stripes = UIBuild.Solid("BoxStripes", box.transform, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(12, StripeInk, Color.clear);
            stripes.type = Image.Type.Tiled;
            stripes.color = Color.white;
            stripes.raycastTarget = false;
            // 아래로 다 내려오면 상자의 우하단 컷코너를 덮어 실루엣이 뭉갠다. 위쪽만 덮는다.
            UIBuild.Pin(stripes.rectTransform, new Vector2(1f, 1f), new Vector2(280f, 150f),
                new Vector2(-2f, -2f));

            _body = UIBuild.Text("Body", box.transform, "", 26f, new Color(0.902f, 0.914f, 0.925f),
                TextAlignmentOptions.TopLeft, wrap: true);
            _body.lineSpacing = 22f;
            UIBuild.Stretch(_body.rectTransform);
            _body.rectTransform.offsetMin = new Vector2(40f, 44f);
            _body.rectTransform.offsetMax = new Vector2(-70f, -40f);

            _progress = UIBuild.Label("Progress", box.transform, "", UITheme.FontMicro,
                new Color(0.263f, 0.275f, 0.290f));
            _progress.characterSpacing = 18f;
            UIBuild.Pin(_progress.rectTransform, new Vector2(0f, 0f), new Vector2(160f, 14f),
                new Vector2(40f, 22f));

            _nextMark = UIBuild.Text("NextMark", box.transform, "▼", 20f, UITheme.Accent,
                TextAlignmentOptions.Center);
            UIBuild.Pin(_nextMark.rectTransform, new Vector2(1f, 0f), new Vector2(30f, 26f),
                new Vector2(-34f, 22f));
        }

        /// <summary>이름표. 상자 윗변에 걸쳐 놓아 화자가 바뀐 것이 제일 먼저 보인다.</summary>
        private void BuildPlate(Transform root)
        {
            Image plate = UIBuild.Panel("NamePlate", root, new Color(0.063f, 0.075f, 0.090f, 0.98f),
                UIShapes.Corner.Diagonal, 12, UITheme.Outline, 1);
            plate.raycastTarget = false;
            _plate = plate.rectTransform;
            UIBuild.Pin(_plate, new Vector2(0f, 0f), new Vector2(280f, PlateHeight),
                new Vector2(BoxMarginX, BoxBottom + BoxHeight - PlateOverlap));

            Image tick = UIBuild.Solid("Tick", plate.transform, UITheme.Accent);
            tick.raycastTarget = false;
            UIBuild.Pin(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(3f, 24f),
                new Vector2(22f, 0f));

            _speaker = UIBuild.Text("Speaker", plate.transform, "", 24f, UITheme.TextPrimary);
            _speaker.characterSpacing = 6f;
            UIBuild.Stretch(_speaker.rectTransform);
            _speaker.rectTransform.offsetMin = new Vector2(38f, 0f);
        }

        private void BuildHeader(Transform root)
        {
            Image tick = UIBuild.Solid("HeaderTick", root, UITheme.Accent);
            tick.raycastTarget = false;
            UIBuild.Pin(tick.rectTransform, new Vector2(0f, 1f), new Vector2(3f, 30f),
                new Vector2(56f, -48f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", root, "EVENT",
                UITheme.FontMicro, UITheme.TextMuted);
            caption.characterSpacing = 24f;
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f),
                new Vector2(68f, -48f));

            _eventTitle = UIBuild.Text("EventTitle", root, "사건", 19f, UITheme.TextPrimary);
            _eventTitle.characterSpacing = 4f;
            UIBuild.Pin(_eventTitle.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 24f),
                new Vector2(68f, -66f));
        }

        private void BuildControls(Transform root)
        {
            _auto = MakeChip(root, "Auto", "자동", ToggleAuto, -56f);
            _skip = MakeChip(root, "Skip", "건너뛰기", SkipAll, -166f);
        }

        /// <summary>우상단 조작 칩. 대사를 가리지 않게 화면 끝에 붙인다.</summary>
        private static Button MakeChip(Transform root, string name, string text, System.Action onClick,
            float x)
        {
            Button button = UIBuild.Button(name, root, text, onClick, false, UITheme.FontCaption);
            button.image.sprite = UIShapes.CutCorner(8,
                new Color(0.039f, 0.043f, 0.051f, 0.86f), UIShapes.Corner.Diagonal,
                UITheme.Outline, 1);
            UIBuild.Pin(button.image.rectTransform, new Vector2(1f, 1f), new Vector2(100f, 40f),
                new Vector2(x, -48f));

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = UITheme.TextSecondary;
            return button;
        }

        // ── 자동 진행 / 건너뛰기 ──────────────────────────────────────

        private void AdvanceByClick()
        {
            if (!_canAdvance) return;
            GameManager.Instance?.AdvanceEventDialogue();
        }

        private void ToggleAuto()
        {
            _autoPlay = !_autoPlay;
            _autoTimer = 0f;
            RefreshAutoLabel();
        }

        private void SkipAll()
        {
            _autoPlay = false;
            _autoWaiting = false;
            RefreshAutoLabel();
            GameManager.Instance?.SkipEventDialogue();
        }

        private void RefreshAutoLabel()
        {
            if (_auto == null) return;
            var label = _auto.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.text = _autoPlay ? "자동 ●" : "자동";
            label.color = _autoPlay ? UITheme.Accent : UITheme.TextSecondary;
        }

        /// <summary>
        /// 자동 진행 타이머와 연출 갱신. <see cref="UIManager"/>가 매 프레임 넘겨 준다.
        /// 대사 길이에 비례해 기다리므로 긴 대사를 읽을 시간이 남는다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsVisible) return;

            TickEffect(deltaTime);

            if (!_autoPlay || !_autoWaiting) return;

            _autoTimer -= deltaTime;
            if (_autoTimer > 0f) return;

            _autoWaiting = false;
            GameManager.Instance?.AdvanceEventDialogue();
        }

        private void ArmAutoTimer(string line)
        {
            _autoWaiting = true;
            _autoTimer = Mathf.Min(AutoMaxDelay,
                AutoBaseDelay + (line?.Length ?? 0) * AutoDelayPerChar);
        }

        /// <summary>선택지·결과 화면에서는 자동 진행을 끈다. 선택은 플레이어가 해야 한다.</summary>
        private void StopAuto()
        {
            _autoPlay = false;
            _autoWaiting = false;
            if (_auto != null) _auto.gameObject.SetActive(false);
            if (_skip != null) _skip.gameObject.SetActive(false);
            RefreshAutoLabel();
        }

        // ── 연출 ─────────────────────────────────────────────────────

        /// <summary>대사에 지정된 effect를 감는다. none/미지정이면 아무 일도 없다.</summary>
        private void ArmEffect(string effect)
        {
            _effect = string.IsNullOrWhiteSpace(effect) ? null : effect.Trim().ToLowerInvariant();
            _effectTime = _effect == null || _effect == "none" ? 0f : 0.45f;
            if (_effect == "flash" && _flash != null) _flash.color = new Color(1f, 1f, 1f, 0.55f);
        }

        private void TickEffect(float deltaTime)
        {
            if (_effectTime <= 0f) return;
            _effectTime = Mathf.Max(0f, _effectTime - deltaTime);

            float t = _effectTime / 0.45f;   // 1 → 0

            switch (_effect)
            {
                case "shake":
                    // 좌우로 빠르게 떨다가 잦아든다.
                    if (_shakeTarget != null)
                    {
                        float dx = Mathf.Sin(_effectTime * 60f) * 10f * t;
                        _shakeTarget.anchoredPosition = _shakeBase + new Vector2(dx, 0f);
                    }
                    break;

                case "bounce":
                    // 위로 톡 튀었다 내려앉는다.
                    if (_shakeTarget != null)
                    {
                        float dy = Mathf.Sin(t * Mathf.PI) * 12f;
                        _shakeTarget.anchoredPosition = _shakeBase + new Vector2(0f, dy);
                    }
                    break;

                case "flash":
                    if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0.55f * t);
                    break;
            }

            if (_effectTime > 0f) return;

            // 끝났으면 원래 자리로 되돌린다.
            if (_shakeTarget != null) _shakeTarget.anchoredPosition = _shakeBase;
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
            _effect = null;
        }

        // ── 표시 ─────────────────────────────────────────────────────

        public void Show()
        {
            EnsureBuilt();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>대사 인덱스에 맞춰 화면을 갱신한다. 대사가 끝나면 선택지를 띄운다.</summary>
        public void Show(StageEventData stageEvent, int dialogueIndex)
        {
            Show();

            string title = string.IsNullOrWhiteSpace(stageEvent?.title) ? "사건" : stageEvent.title;
            _eventTitle.text = (stageEvent?.tier ?? 0) > 0 ? $"T{stageEvent.tier} · {title}" : title;
            UIBuild.Clear(_choiceArea);

            List<StageEventDialogueData> dialogue = stageEvent?.dialogue;
            bool hasDialogueLeft = dialogue != null && dialogueIndex < dialogue.Count;

            if (hasDialogueLeft)
            {
                StageEventDialogueData line = dialogue[dialogueIndex];
                SetSpeaker(line.speaker);
                _body.text = line.text ?? "";
                ApplyStandings(dialogue, dialogueIndex);
                ArmAutoTimer(line.text);
                ArmEffect(line.effect);
                _progress.text = $"{dialogueIndex + 1} / {dialogue.Count}";
            }
            else
            {
                // 대사를 다 읽었으면 마지막 대사를 남겨둔 채 선택지를 연다.
                SetSpeaker(null);
                if (dialogue == null || dialogue.Count == 0) _body.text = "";
                _autoWaiting = false;
                _progress.text = "";
            }

            bool showChoices = !hasDialogueLeft && stageEvent?.choices != null && stageEvent.choices.Count > 0;

            SetAdvanceEnabled(hasDialogueLeft);
            _auto.gameObject.SetActive(hasDialogueLeft);
            _skip.gameObject.SetActive(hasDialogueLeft);
            RefreshAutoLabel();

            if (showChoices) BuildChoices(stageEvent.choices);
        }

        private void SetSpeaker(string speaker)
        {
            bool has = !string.IsNullOrWhiteSpace(speaker);
            _plate.gameObject.SetActive(has);
            if (!has) return;

            _speaker.text = speaker;
            // 이름 길이에 맞춰 이름표 폭을 줄인다. 두 글자 이름에 빈 판이 딸려 오지 않도록.
            _speaker.ForceMeshUpdate();
            float width = Mathf.Clamp(_speaker.preferredWidth + 68f, 150f, 520f);
            _plate.sizeDelta = new Vector2(width, PlateHeight);
        }

        private void SetAdvanceEnabled(bool enabled)
        {
            _canAdvance = enabled;
            _clickCatcher.SetActive(enabled);
            _nextMark.gameObject.SetActive(enabled);
        }

        // ── 스탠딩 배치 ──────────────────────────────────────────────

        /// <summary>
        /// 스탠딩을 좌우에 세운다.
        ///
        /// 자리는 사건 전체를 훑어 <b>처음 등장한 순서대로</b> 왼쪽 · 오른쪽으로 나눠 준다.
        /// 그래야 같은 인물이 대사 내내 같은 자리에 서 있어 눈이 편하다.
        /// 스탠딩이 하나뿐인 사건이면 왼쪽에만 세운다.
        /// </summary>
        private void ApplyStandings(List<StageEventDialogueData> dialogue, int index)
        {
            string leftKey = null;
            string rightKey = null;

            for (int i = 0; i < dialogue.Count; i++)
            {
                string key = StandingKey(dialogue[i]);
                if (key == null || key == leftKey || key == rightKey) continue;
                if (leftKey == null) leftKey = key;
                else if (rightKey == null) rightKey = key;
            }

            string speaking = StandingKey(dialogue[index]);

            // 등장인물이 한 명뿐인 사건은 좌우로 나눌 이유가 없다. 가운데 세운다.
            bool solo = rightKey == null;
            RectTransform left = _leftStand.rectTransform;
            left.anchorMin = new Vector2(solo ? 0.5f : 0f, 0f);
            left.anchorMax = left.anchorMin;
            left.anchoredPosition = new Vector2(solo ? 0f : StandCenterX, left.anchoredPosition.y);

            SetStanding(_leftStand, leftKey, speaking);
            SetStanding(_rightStand, rightKey, speaking);
        }

        private static void SetStanding(Image slot, string key, string speaking)
        {
            Sprite sprite = SpriteResource.LoadStanding(key);
            slot.sprite = sprite;
            slot.enabled = sprite != null;
            if (sprite == null) return;

            bool lit = key == speaking;
            slot.color = lit ? StandLit : StandDim;

            // 말하지 않는 쪽은 조금 내려앉는다. 밝기만으로 부족할 때의 두 번째 신호.
            Vector2 pos = slot.rectTransform.anchoredPosition;
            slot.rectTransform.anchoredPosition = new Vector2(pos.x, lit ? -40f : -54f);
        }

        /// <summary>이 대사가 세울 스탠딩 파일명. 없으면 null(=화면을 그대로 둔다).</summary>
        private static string StandingKey(StageEventDialogueData line)
        {
            if (line == null) return null;
            if (!string.IsNullOrWhiteSpace(line.standing)) return line.standing;

            // 스탠딩 지정이 없으면 초상화 이름에서 유추한다(XXX_PORTRAIT → XXX_STANDING).
            if (!string.IsNullOrWhiteSpace(line.portrait))
            {
                return line.portrait.EndsWith("_PORTRAIT")
                    ? line.portrait[..^"_PORTRAIT".Length] + "_STANDING"
                    : line.portrait;
            }

            return null;
        }

        // ── 선택지 ───────────────────────────────────────────────────

        private void BuildChoices(List<StageEventChoiceData> choices)
        {
            const float width = 760f;
            const float height = 74f;
            const float gap = 14f;

            // 대사 상자 바로 위에서 위로 쌓는다. 선택지가 몇 개든 상자와의 간격이 일정하다.
            const float lift = 46f;
            float bottom = BoxBottom + BoxHeight + lift;

            for (int i = 0; i < choices.Count; i++)
            {
                StageEventChoiceData choice = choices[i];
                string choiceId = choice.id;

                Image frame = UIBuild.Panel($"Choice{i}", _choiceArea,
                    new Color(0.043f, 0.051f, 0.063f, 0.94f), UIShapes.Corner.Diagonal, 12,
                    UITheme.Accent, 1);
                UIBuild.Pin(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(width, height),
                    new Vector2(0f, bottom + (choices.Count - 1 - i) * (height + gap)));

                Image tick = UIBuild.Solid("Tick", frame.transform, UITheme.Accent);
                tick.raycastTarget = false;
                UIBuild.Pin(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(3f, 26f),
                    new Vector2(30f, 0f));

                TextMeshProUGUI label = UIBuild.Text("Label", frame.transform, choice.text ?? "",
                    20f, UITheme.TextPrimary);
                UIBuild.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(50f, 0f);
                label.rectTransform.offsetMax = new Vector2(-300f, 0f);

                string note = ChoiceNote(choice);
                if (!string.IsNullOrEmpty(note))
                {
                    TextMeshProUGUI hint = UIBuild.Label("Note", frame.transform, note,
                        UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                    hint.characterSpacing = 2f;
                    // 적 이름·보상까지 싣게 되어 길어졌다. 오른쪽 칸에 가두고 줄바꿈·축소로 맞춘다.
                    RectTransform hintRect = hint.rectTransform;
                    hintRect.anchorMin = new Vector2(1f, 0f);
                    hintRect.anchorMax = new Vector2(1f, 1f);
                    hintRect.pivot = new Vector2(1f, 0.5f);
                    hintRect.sizeDelta = new Vector2(260f, -8f);
                    hintRect.anchoredPosition = new Vector2(-24f, 0f);
                    hint.textWrappingMode = TextWrappingModes.Normal;
                    hint.enableAutoSizing = true;
                    hint.fontSizeMin = 10f;
                    hint.fontSizeMax = UITheme.FontCaption;
                }

                UIBuild.OnClick(frame.gameObject,
                    () => GameManager.Instance?.SelectEventChoice(choiceId));
            }
        }

        /// <summary>
        /// 선택지 오른쪽 꼬리표. 데이터에 적힌 대가/보상을 요약한다.
        ///
        /// 예전에는 "전투"·"골드 N × 스테이지"만 적혀, 누구와 싸우는지·얼마를 내는지·이기면 무엇을 받는지
        /// 고르기 전에 알 수 없었다(QA). 지금 스테이지로 값을 확정하고 적의 이름·등급과 보상을 함께 싣는다.
        /// </summary>
        private static string ChoiceNote(StageEventChoiceData choice)
        {
            if (choice == null) return null;
            int stage = Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);
            string reward = RewardNote(choice);

            if (choice.goldCostPerStage > 0)
            {
                string cost = $"골드 {choice.goldCostPerStage * stage:N0}";
                return string.IsNullOrEmpty(reward) ? cost : $"{cost} → {reward}";
            }
            if (choice.battleEnemyId > 0)
            {
                EnemyData enemy = FindEnemy(choice.battleEnemyId);
                string foe = enemy == null ? "전투" : $"전투 · {enemy.name}({TierName(enemy.tier)})";
                return string.IsNullOrEmpty(reward) ? foe : $"{foe} → 승리 시 {reward}";
            }
            return reward;
        }

        private static string RewardNote(StageEventChoiceData choice)
        {
            if (choice.grantUnitId > 0) return "동료 합류";
            if (choice.grantItemId > 0)
            {
                ItemData item = GameManager.Instance?.itemDataList?.items?.Find(entry => entry != null && entry.id == choice.grantItemId);
                return item != null ? $"장비 {item.name}" : "장비 획득";
            }
            if (choice.grantPassiveCodeId > 0) return choice.grantPassiveToAll ? "전원 패시브" : "패시브";
            return null;
        }

        private static List<EnemyData> _enemies;

        private static EnemyData FindEnemy(int id)
        {
            _enemies ??= GameManager.Instance?.dataManager?.FetchEnemyDataList()?.enemies;
            return _enemies?.Find(enemy => enemy != null && enemy.id == id);
        }

        private static string TierName(string tier) => tier switch
        {
            "boss" => "보스",
            "elite" => "엘리트",
            _ => "일반",
        };

        // ── 자리 비우기 ──────────────────────────────────────────────

        /// <summary>
        /// 자리가 꽉 차 영입이 막혔을 때 여는 화면. 떠나보낼 사람을 고르거나 합류를 포기한다.
        ///
        /// 선택지처럼 세로로 쌓으면 최대 12명이 화면 밖으로 밀려나므로 격자로 깐다.
        /// 메인 캐릭터는 애초에 후보에 없다 — 런의 축을 실수로 내보낼 수 있으면 안 된다.
        /// </summary>
        public void ShowRosterPrompt(string message, List<Unit> candidates)
        {
            const int columns = 3;
            const float cardWidth = 250f;
            const float cardHeight = 62f;
            const float gapX = 14f;
            const float gapY = 12f;

            Show();

            SetSpeaker(null);
            _body.text = message ?? "";
            _progress.text = "";
            SetAdvanceEnabled(false);
            StopAuto();
            UIBuild.Clear(_choiceArea);

            if (candidates == null || candidates.Count == 0) return;

            int rows = (candidates.Count + columns - 1) / columns;

            // 아래에서부터 쌓되, 포기 버튼 한 줄을 대사 상자 위에 먼저 비워 둔다.
            const float declineHeight = 52f;
            float gridBottom = BoxBottom + BoxHeight + 46f + declineHeight + gapY;

            for (int i = 0; i < candidates.Count; i++)
            {
                Unit candidate = candidates[i];
                if (candidate == null) continue;
                int unitId = candidate.ID;

                int row = i / columns;
                int column = i % columns;

                // 마지막 줄이 덜 찼으면 그 줄만 가운데로 모은다.
                int inRow = Mathf.Min(columns, candidates.Count - row * columns);
                float rowWidth = inRow * cardWidth + (inRow - 1) * gapX;
                float x = -rowWidth * 0.5f + cardWidth * 0.5f + column * (cardWidth + gapX);
                float y = gridBottom + (rows - 1 - row) * (cardHeight + gapY);

                Image frame = UIBuild.Panel($"Leave{i}", _choiceArea,
                    new Color(0.043f, 0.051f, 0.063f, 0.94f), UIShapes.Corner.Diagonal, 10,
                    UITheme.Outline, 1);
                UIBuild.Pin(frame.rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(cardWidth, cardHeight), new Vector2(x, y));

                TextMeshProUGUI name = UIBuild.Text("Name", frame.transform, candidate.UnitName ?? "",
                    19f, UITheme.TextPrimary);
                UIBuild.Stretch(name.rectTransform);
                name.rectTransform.offsetMin = new Vector2(18f, 0f);
                name.rectTransform.offsetMax = new Vector2(-72f, 0f);

                TextMeshProUGUI level = UIBuild.Label("Level", frame.transform, $"Lv {candidate.Level}",
                    UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                UIBuild.Stretch(level.rectTransform);
                level.rectTransform.offsetMax = new Vector2(-18f, 0f);

                UIBuild.OnClick(frame.gameObject,
                    () => GameManager.Instance?.DismissUnitForRecruit(unitId));
            }

            Button decline = UIBuild.Button("DeclineRecruit", _choiceArea, "아무도 보내지 않는다",
                () => GameManager.Instance?.CancelRecruitForRoster());
            UIBuild.Pin(decline.image.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(360f, declineHeight), new Vector2(0f, BoxBottom + BoxHeight + 46f));
        }

        // ── 안내 / 결말 ──────────────────────────────────────────────

        /// <summary>선택 실패 등 중간 안내. 화면은 그대로 두고 본문만 바꾼다.</summary>
        public void ShowMessage(string message)
        {
            Show();
            _body.text = message ?? "";
            SetAdvanceEnabled(false);
            StopAuto();
        }

        /// <summary>사건 종료 안내. 확인을 누르면 다음 진행으로 넘어간다.</summary>
        public void ShowResolution(string message)
        {
            Show();

            SetSpeaker(null);
            _body.text = message ?? "";
            _progress.text = "";
            SetAdvanceEnabled(false);
            StopAuto();

            // 결말에서는 스탠딩을 모두 같은 밝기로 되돌린다. 말하는 사람이 없다.
            if (_leftStand.enabled) _leftStand.color = StandDim;
            if (_rightStand.enabled) _rightStand.color = StandDim;

            UIBuild.Clear(_choiceArea);
            Button confirm = UIBuild.Button("Confirm", _choiceArea, "확인",
                () => GameManager.Instance?.CompleteEventStage(), primary: true);
            UIBuild.Pin(confirm.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(260f, 52f),
                new Vector2(0f, BoxBottom - 78f));
        }
    }
}
