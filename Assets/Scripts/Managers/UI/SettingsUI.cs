using System;
using System.Collections.Generic;
using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 설정 창. 씬에 배치된 슬라이더에 의존하지 않고 이 컴포넌트가 직접 조립한다.
    /// 메인 메뉴와 인게임 메뉴가 같은 창을 쓴다.
    ///
    /// 왼쪽 탭 셋 — <b>화면 · 인터페이스 · 소리</b>. 줄마다 왼쪽에 이름과 한 줄 설명, 오른쪽에 조작이 선다.
    /// 예전 설정은 음악 · 효과음 두 줄뿐이라 고해상도에서 글자를 키우거나 창 모드로 바꿀 길이 없었다(QA 두 번).
    ///
    /// 화면(표시 방식 · 해상도)은 고른 뒤 [적용]을 눌러야 바뀌고, 바뀐 뒤 12초 안에 [유지]를 누르지 않으면
    /// 되돌아간다 — 모니터가 받지 못하는 해상도를 골라 화면이 까맣게 되어도 기다리면 돌아온다.
    /// 나머지는 고르는 즉시 적용된다.
    ///
    /// 클래스 이름은 MainMenu 씬이 참조하므로 유지한다.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private enum Tab
        {
            Display,
            Interface,
            Sound,
        }

        private const float PanelWidth = 1000f;
        private const float PanelHeight = 660f;
        private const float RailWidth = 230f;
        private const float RowHeight = 78f;
        private const float ControlWidth = 400f;
        private const float RevertSeconds = 12f;

        private GameObject _panel;
        private Tab _tab = Tab.Display;
        private readonly Dictionary<Tab, Button> _tabButtons = new();
        private RectTransform _content;
        private TextMeshProUGUI _footerNote;
        private Button _apply;

        // 화면 — [적용] 전까지 들고 있는 값.
        private SettingsManager.DisplayMode _pendingMode;
        private int _pendingResolution;
        private List<Vector2Int> _resolutions = new();

        // 적용 뒤 되돌리기 대기.
        private bool _awaitingKeep;
        private float _keepDeadline;
        private SettingsManager.DisplayMode _revertMode;
        private Vector2Int _revertSize;
        private GameObject _keepBar;
        private TextMeshProUGUI _keepLabel;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Open()
        {
            EnsureBuilt();
            SyncPending();
            _panel.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            // [유지]를 누르지 않고 배경/뒤로 가기로 닫은 경우도 미확정 화면 설정을 되돌린다.
            // 시험 적용값은 저장하지 않으므로 강제 종료 뒤에도 마지막 확정값으로 시작한다.
            if (_awaitingKeep) RevertDisplay();
            SettingsManager.Instance?.SaveSettings();
            if (_panel != null) _panel.SetActive(false);
        }

        private static SettingsManager Settings => SettingsManager.Instance;

        // ── 조립 ─────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_panel != null) return;

            Canvas canvas = UIBuild.Canvas("SettingsCanvas", UITheme.LayerSettings);
            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_panel.GetComponent<RectTransform>());

            Image backdrop = UIBuild.Backdrop("Backdrop", _panel.transform);
            UIBuild.OnClick(backdrop.gameObject, Close);

            Image card = UIBuild.Panel("Card", _panel.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            UIBuild.Pin(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            UIBuild.Elevate(card, 40, 0.6f, 14f);
            _panel.AddComponent<UIPopIn>().Panel = card.rectTransform;

            BuildRail(card.transform);

            _content = UIBuild.Container("Content", card.transform);
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = new Vector2(RailWidth + 36f, 92f);
            _content.offsetMax = new Vector2(-36f, -36f);

            BuildFooter(card.transform);

            _panel.SetActive(false);
        }

        /// <summary>왼쪽 레일 — 제목과 탭 셋. 레일 면은 본문보다 한 단계 가라앉아 영역이 갈린다.</summary>
        private void BuildRail(Transform card)
        {
            Image rail = UIBuild.Panel("Rail", card, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 14);
            rail.raycastTarget = false;
            rail.rectTransform.anchorMin = new Vector2(0f, 0f);
            rail.rectTransform.anchorMax = new Vector2(0f, 1f);
            rail.rectTransform.pivot = new Vector2(0f, 0.5f);
            rail.rectTransform.sizeDelta = new Vector2(RailWidth, -12f);
            rail.rectTransform.anchoredPosition = new Vector2(6f, 0f);

            TextMeshProUGUI eyebrow = UIBuild.Label("Eyebrow", rail.transform, "SETTINGS", UITheme.FontMicro,
                UITheme.TextMuted);
            eyebrow.characterSpacing = 22f;
            UIBuild.Pin(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(RailWidth - 40f, 18f), new Vector2(24f, -30f));

            TextMeshProUGUI title = UIBuild.Text("Title", rail.transform, "설정", UITheme.FontTitle, UITheme.TextPrimary);
            title.fontStyle = FontStyles.Bold;
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(RailWidth - 40f, 36f), new Vector2(24f, -50f));

            AddTab(rail.transform, Tab.Display, "화면", "표시 방식 · 해상도 · 프레임", 0);
            AddTab(rail.transform, Tab.Interface, "인터페이스", "글자 크기 · HUD 크기", 1);
            AddTab(rail.transform, Tab.Sound, "소리", "음악 · 효과음", 2);
        }

        private void AddTab(Transform rail, Tab tab, string name, string sub, int index)
        {
            Button button = UIBuild.Button($"Tab{tab}", rail, name, () =>
            {
                _tab = tab;
                Rebuild();
            });
            UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 1f), new Vector2(RailWidth - 28f, 62f),
                new Vector2(14f, -112f - index * 70f));

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.richText = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = $"{name}\n<size=72%><color=#{Hex(UITheme.TextMuted)}>{sub}</color></size>";
            UIBuild.Stretch(label.rectTransform, 18f, 6f);
            _tabButtons[tab] = button;
        }

        private void BuildFooter(Transform card)
        {
            _footerNote = UIBuild.Text("FooterNote", card, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.MidlineLeft, wrap: true);
            _footerNote.richText = true;
            _footerNote.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footerNote.rectTransform.anchorMax = new Vector2(1f, 0f);
            _footerNote.rectTransform.pivot = new Vector2(0f, 0f);
            _footerNote.rectTransform.offsetMin = new Vector2(RailWidth + 36f, 28f);
            _footerNote.rectTransform.offsetMax = new Vector2(-380f, 74f);

            Button close = UIBuild.Button("Close", card, "닫기  <size=70%>ESC</size>", Close);
            close.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            UIBuild.Pin(close.image.rectTransform, new Vector2(1f, 0f), new Vector2(160f, 46f), new Vector2(-36f, 28f));

            _apply = UIBuild.Button("Apply", card, "적용", ApplyDisplay, primary: true);
            UIBuild.Pin(_apply.image.rectTransform, new Vector2(1f, 0f), new Vector2(160f, 46f), new Vector2(-208f, 28f));

            // 적용 뒤 확인 띠. 본문 아래쪽에 떠서 [유지] · [되돌리기]를 받는다.
            Image bar = UIBuild.Panel("KeepBar", card, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal, 10,
                UITheme.Accent, 1);
            UIBuild.Pin(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(PanelWidth - RailWidth - 72f, 64f),
                new Vector2(RailWidth * 0.5f + 3f, 88f));
            _keepBar = bar.gameObject;

            _keepLabel = UIBuild.Text("Label", bar.transform, "", UITheme.FontBody, UITheme.TextPrimary);
            UIBuild.Anchor(_keepLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), 18f, 0f);

            Button keep = UIBuild.Button("Keep", bar.transform, "유지", KeepDisplay, primary: true);
            UIBuild.Pin(keep.image.rectTransform, new Vector2(1f, 0.5f), new Vector2(120f, 42f), new Vector2(-12f, 0f));
            Button revert = UIBuild.Button("Revert", bar.transform, "되돌리기", RevertDisplay);
            UIBuild.Pin(revert.image.rectTransform, new Vector2(1f, 0.5f), new Vector2(120f, 42f), new Vector2(-140f, 0f));
            _keepBar.SetActive(false);
        }

        // ── 본문 ─────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_content == null) return;
            UIBuild.Clear(_content);

            foreach ((Tab tab, Button button) in _tabButtons)
            {
                bool active = tab == _tab;
                button.image.sprite = UIShapes.CutCorner(10,
                    active ? UITheme.SurfaceRaised : new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal,
                    active ? UITheme.Outline : default, active ? 1 : 0);

                Transform marker = button.transform.Find("ActiveMark");
                if (marker == null)
                {
                    Image mark = UIBuild.Solid("ActiveMark", button.transform, UITheme.Accent);
                    mark.raycastTarget = false;
                    UIBuild.Pin(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(3f, 28f), new Vector2(6f, 0f));
                    marker = mark.transform;
                }
                marker.gameObject.SetActive(active);
            }

            float y = 0f;
            switch (_tab)
            {
                case Tab.Display:
                    BuildDisplayRows(ref y);
                    break;
                case Tab.Interface:
                    BuildInterfaceRows(ref y);
                    break;
                default:
                    BuildSoundRows(ref y);
                    break;
            }

            RefreshFooter();
        }

        private void BuildDisplayRows(ref float y)
        {
            SectionHeading("DISPLAY", "화면", ref y);

            RectTransform mode = Row("표시 방식",
                "테두리 없는 전체 화면은 알트탭이 빠르고, 창 모드는 창 크기를 바꿀 수 있습니다.", ref y);
            Stepper(mode, SettingsManager.DisplayModeNames, (int)_pendingMode, index =>
            {
                _pendingMode = (SettingsManager.DisplayMode)index;
                Rebuild();
            });

            RectTransform resolution = Row("해상도",
                "전체 화면에서는 이 해상도로 그린 뒤 모니터에 맞춥니다.", ref y);
            var names = new string[_resolutions.Count];
            for (int i = 0; i < names.Length; i++) names[i] = $"{_resolutions[i].x} × {_resolutions[i].y}";
            Stepper(resolution, names, _pendingResolution, index =>
            {
                _pendingResolution = index;
                Rebuild();
            });

            bool vsync = Settings == null || Settings.VSync;
            RectTransform sync = Row("수직 동기화", "화면 찢김을 막습니다. 켜면 모니터 주사율에 맞춰 그립니다.", ref y);
            Segmented(sync, new[] { "끔", "켬" }, vsync ? 1 : 0, index =>
            {
                Settings?.SetVSync(index == 1);
                Rebuild();
            });

            int limit = Settings != null ? Settings.FrameLimit : 60;
            int limitIndex = Mathf.Max(0, Array.IndexOf(SettingsManager.FrameLimitSteps, limit));
            var limitNames = new string[SettingsManager.FrameLimitSteps.Length];
            for (int i = 0; i < limitNames.Length; i++)
                limitNames[i] = SettingsManager.FrameLimitName(SettingsManager.FrameLimitSteps[i]);
            RectTransform frame = Row("프레임 제한",
                vsync ? "수직 동기화가 켜져 있어 모니터 주사율이 대신 정합니다." : "전력과 발열을 아끼려면 낮춥니다.", ref y);
            Stepper(frame, limitNames, limitIndex, index =>
            {
                Settings?.SetFrameLimit(SettingsManager.FrameLimitSteps[index]);
                Rebuild();
            }, enabled: !vsync);
        }

        private void BuildInterfaceRows(ref float y)
        {
            SectionHeading("INTERFACE", "인터페이스", ref y);

            RectTransform text = Row("글자 크기",
                "모든 화면의 글자를 키웁니다. 칸이 좁은 곳은 넘치지 않을 만큼만 커집니다.", ref y);
            Segmented(text, SettingsManager.TextScaleNames, Settings != null ? Settings.TextScaleIndex : 0, index =>
            {
                if (Settings != null) Settings.TextScaleIndex = index;
                Rebuild();
            });

            RectTransform hud = Row("HUD 크기",
                "상단 바 · 행동 순서 · 준비 바 · 툴팁을 키웁니다. 전장은 남은 자리에 맞춰 다시 잡힙니다.", ref y);
            Segmented(hud, SettingsManager.HudScaleNames, Settings != null ? Settings.HudScaleIndex : 1, index =>
            {
                if (Settings != null) Settings.HudScaleIndex = index;
                Rebuild();
            });

            y += 16f;
            Image preview = UIBuild.Panel("Preview", _content, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                UITheme.Outline, 1);
            preview.raycastTarget = false;
            PlaceRow(preview.rectTransform, y, 118f);

            TextMeshProUGUI eyebrow = UIBuild.Label("Eyebrow", preview.transform, "PREVIEW", UITheme.FontMicro,
                UITheme.TextMuted);
            eyebrow.characterSpacing = 20f;
            UIBuild.Pin(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 18f), new Vector2(20f, -16f));

            TextMeshProUGUI sample = UIBuild.Text("Sample", preview.transform,
                "라이트  Lv.24\n<size=85%><color=#" + Hex(UITheme.TextSecondary) +
                ">화상 3중첩 · 2턴 — 행동할 때마다 최대 체력의 4%를 잃는다.</color></size>",
                UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap: true);
            sample.richText = true;
            UIBuild.Stretch(sample.rectTransform, 20f, 12f);
            sample.rectTransform.offsetMax = new Vector2(-20f, -38f);
        }

        private void BuildSoundRows(ref float y)
        {
            SectionHeading("SOUND", "소리", ref y);

            RectTransform music = Row("음악", "배경 음악의 크기.", ref y);
            BuildSlider(music, Settings != null ? Settings.MusicVolume : 1f, value =>
            {
                if (Settings != null) Settings.MusicVolume = value;
            });

            RectTransform sfx = Row("효과음", "전투 · 버튼 효과음의 크기.", ref y);
            BuildSlider(sfx, Settings != null ? Settings.SfxVolume : 1f, value =>
            {
                if (Settings != null) Settings.SfxVolume = value;
            });
        }

        // ── 줄과 조작 ────────────────────────────────────────────────

        private void SectionHeading(string eyebrow, string title, ref float y)
        {
            TextMeshProUGUI caption = UIBuild.Label("Eyebrow", _content, eyebrow, UITheme.FontMicro, UITheme.Accent);
            caption.characterSpacing = 22f;
            PlaceRow(caption.rectTransform, y, 18f);
            y += 20f;

            TextMeshProUGUI heading = UIBuild.Text("Heading", _content, title, UITheme.FontTitle, UITheme.TextPrimary);
            heading.fontStyle = FontStyles.Bold;
            PlaceRow(heading.rectTransform, y, 36f);
            y += 48f;
        }

        /// <summary>한 줄 — 왼쪽에 이름과 설명, 오른쪽에 조작 자리. 조작 자리(RectTransform)를 돌려준다.</summary>
        private RectTransform Row(string name, string description, ref float y)
        {
            RectTransform row = UIBuild.Container($"Row_{name}", _content);
            PlaceRow(row, y, RowHeight);
            y += RowHeight;

            Image rule = UIBuild.Divider("Rule", row);
            rule.raycastTarget = false;
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            rule.rectTransform.anchoredPosition = Vector2.zero;

            TextMeshProUGUI label = UIBuild.Text("Name", row, name, UITheme.FontBody, UITheme.TextPrimary);
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(0f, 0f);
            label.rectTransform.offsetMax = new Vector2(-ControlWidth - 24f, -8f);

            TextMeshProUGUI hint = UIBuild.Text("Hint", row, description, UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.TopLeft, wrap: true);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            hint.rectTransform.offsetMin = new Vector2(0f, 4f);
            hint.rectTransform.offsetMax = new Vector2(-ControlWidth - 24f, 0f);

            RectTransform control = UIBuild.Container("Control", row);
            UIBuild.Pin(control, new Vector2(1f, 0.5f), new Vector2(ControlWidth, 44f), Vector2.zero);
            return control;
        }

        private static void PlaceRow(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -y);
        }

        /// <summary>‹ 값 › — 선택지가 많을 때(해상도 · 표시 방식 · 프레임). 끝에서 돌지 않는다.</summary>
        private static void Stepper(RectTransform control, string[] options, int index, Action<int> onChange,
            bool enabled = true)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));

            Image field = UIBuild.Panel("Field", control, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                UITheme.Outline, 1);
            field.raycastTarget = false;
            UIBuild.Stretch(field.rectTransform);

            TextMeshProUGUI value = UIBuild.Text("Value", field.transform, options.Length > 0 ? options[index] : "—",
                UITheme.FontBody, enabled ? UITheme.TextPrimary : UITheme.TextDisabled, TextAlignmentOptions.Center);
            UIBuild.Stretch(value.rectTransform, 52f, 0f);

            Button previous = UIBuild.Button("Prev", field.transform, "‹", () => onChange(index - 1), false, UITheme.FontTitle);
            UIBuild.Pin(previous.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 36f), new Vector2(4f, 0f));
            previous.interactable = enabled && index > 0;

            Button next = UIBuild.Button("Next", field.transform, "›", () => onChange(index + 1), false, UITheme.FontTitle);
            UIBuild.Pin(next.image.rectTransform, new Vector2(1f, 0.5f), new Vector2(40f, 36f), new Vector2(-4f, 0f));
            next.interactable = enabled && index < options.Length - 1;
        }

        /// <summary>한 줄에 다 보이는 선택지(2~3개). 고른 것만 오로라 민트로 채운다.</summary>
        private static void Segmented(RectTransform control, string[] options, int selected, Action<int> onChange)
        {
            Image track = UIBuild.Panel("Track", control, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                UITheme.Outline, 1);
            track.raycastTarget = false;
            UIBuild.Stretch(track.rectTransform);

            float step = 1f / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                bool active = i == selected;
                Button button = UIBuild.Button($"Option{i}", track.transform, options[i], () => onChange(index),
                    active, UITheme.FontCaption);
                if (!active)
                {
                    button.image.sprite = UIShapes.CutCorner(8, new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal);
                    button.GetComponentInChildren<TextMeshProUGUI>().color = UITheme.TextSecondary;
                }
                UIBuild.Anchor(button.image.rectTransform, new Vector2(i * step, 0f), new Vector2((i + 1) * step, 1f), 3f, 3f);
            }
        }

        private static void BuildSlider(RectTransform control, float initial, Action<float> onChanged)
        {
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(control, false);
            var slider = sliderGo.GetComponent<Slider>();
            UIBuild.Anchor(sliderGo.GetComponent<RectTransform>(), new Vector2(0f, 0.36f), new Vector2(0.82f, 0.64f));

            Image track = UIBuild.Panel("Track", sliderGo.transform, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 6,
                UITheme.Outline, 1);
            UIBuild.Stretch(track.rectTransform);

            RectTransform fillArea = UIBuild.Container("FillArea", sliderGo.transform);
            UIBuild.Stretch(fillArea);
            Image fill = UIBuild.Panel("Fill", fillArea, UITheme.Accent, UIShapes.Corner.Diagonal, 6);
            UIBuild.Stretch(fill.rectTransform);

            RectTransform handleArea = UIBuild.Container("HandleArea", sliderGo.transform);
            UIBuild.Stretch(handleArea, 9f, 0f);
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea, false);
            var handle = handleGo.GetComponent<Image>();
            handle.sprite = UIShapes.Disc(64, Color.white);
            ((RectTransform)handleGo.transform).sizeDelta = new Vector2(20f, 20f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = (RectTransform)handleGo.transform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initial;

            TextMeshProUGUI valueLabel = UIBuild.Text("Value", control, Mathf.RoundToInt(initial * 100f).ToString(),
                UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(valueLabel.rectTransform, new Vector2(0.84f, 0f), new Vector2(1f, 1f));

            slider.onValueChanged.AddListener(v =>
            {
                onChanged(v);
                valueLabel.text = Mathf.RoundToInt(v * 100f).ToString();
            });
        }

        // ── 화면 적용 ────────────────────────────────────────────────

        private void SyncPending()
        {
            _resolutions = SettingsManager.AvailableResolutions();
            SettingsManager settings = Settings;
            _pendingMode = settings != null ? settings.Mode : SettingsManager.DisplayMode.Borderless;

            var current = new Vector2Int(settings != null ? settings.ResolutionWidth : Screen.width,
                settings != null ? settings.ResolutionHeight : Screen.height);
            _pendingResolution = _resolutions.IndexOf(current);
            if (_pendingResolution < 0)
            {
                _resolutions.Add(current);
                _resolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
                _pendingResolution = _resolutions.IndexOf(current);
            }
        }

        private bool DisplayPending
        {
            get
            {
                SettingsManager settings = Settings;
                if (settings == null || _resolutions.Count == 0) return false;
                Vector2Int size = _resolutions[Mathf.Clamp(_pendingResolution, 0, _resolutions.Count - 1)];
                return _pendingMode != settings.Mode ||
                       size.x != settings.ResolutionWidth || size.y != settings.ResolutionHeight;
            }
        }

        private void RefreshFooter()
        {
            bool pending = _tab == Tab.Display && DisplayPending;
            _apply.gameObject.SetActive(_tab == Tab.Display);
            _apply.interactable = pending && !_awaitingKeep;
            _footerNote.text = _tab switch
            {
                Tab.Display when pending => $"<color=#{Hex(UITheme.Accent)}>●</color>  표시 방식 · 해상도는 [적용]을 눌러야 바뀝니다.",
                Tab.Display => "표시 방식 · 해상도 말고는 고르는 즉시 적용되고 저장됩니다.",
                _ => "고르는 즉시 적용되고 저장됩니다.",
            };
        }

        private void ApplyDisplay()
        {
            SettingsManager settings = Settings;
            if (settings == null || !DisplayPending) return;

            _revertMode = settings.Mode;
            _revertSize = new Vector2Int(settings.ResolutionWidth, settings.ResolutionHeight);

            Vector2Int size = _resolutions[Mathf.Clamp(_pendingResolution, 0, _resolutions.Count - 1)];
            settings.SetDisplay(_pendingMode, size.x, size.y, save: false);

            _awaitingKeep = true;
            _keepDeadline = Time.unscaledTime + RevertSeconds;
            _keepBar.SetActive(true);
            _keepBar.transform.SetAsLastSibling();
            Rebuild();
        }

        private void KeepDisplay()
        {
            _awaitingKeep = false;
            _keepBar.SetActive(false);
            Settings?.SaveSettings();
            Rebuild();
        }

        private void RevertDisplay()
        {
            _awaitingKeep = false;
            _keepBar.SetActive(false);
            Settings?.SetDisplay(_revertMode, _revertSize.x, _revertSize.y, save: false);
            SyncPending();
            Rebuild();
        }

        private void Update()
        {
            if (!_awaitingKeep || _keepLabel == null) return;

            float left = _keepDeadline - Time.unscaledTime;
            if (left <= 0f)
            {
                RevertDisplay();
                return;
            }

            _keepLabel.text = $"이 화면을 유지할까요?  {Mathf.CeilToInt(left)}초 뒤 되돌립니다.";
        }

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
