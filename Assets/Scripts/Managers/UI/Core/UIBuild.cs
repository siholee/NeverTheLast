using System;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// UI 요소를 코드로 조립하는 헬퍼.
    /// 모든 화면이 이 함수들만 거쳐 위젯을 만들기 때문에,
    /// 나중에 시판 UI 에셋(Modern UI Pack / Heat 등)을 도입하면
    /// 각 함수의 "내부"만 그 에셋의 프리팹 생성으로 바꾸면 화면 코드는 그대로 둘 수 있다.
    /// </summary>
    public static class UIBuild
    {
        private const string PrimaryUIFontResource = "Font/Maplestory Light SDF";
        private const string NotoRegularUIFontResource = "Font/NotoSansKR-Regular SDF";
        private const string NotoLegacyUIFontResource = "Font/NotoSansKR-VariableFont_wght SDF";
        private static TMP_FontAsset _uiFont;
        private static Material _uiFontMaterial;
        private static Material _worldFontMaterial;
        private static bool _usingRegularFont;

        // ── 캔버스 / 컨테이너 ────────────────────────────────────────

        /// <summary>이름으로 오버레이 캔버스를 찾거나 만든다. sortingOrder가 클수록 위에 그려진다.</summary>
        public static Canvas Canvas(string name, int sortingOrder)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Canvas found = existing.GetComponent<Canvas>();
                if (found != null) return found;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            // 0.5 = 가로/세로 변화를 같은 비중으로 반영. 모바일 세로↔가로 대응에 무난하다.
            scaler.matchWidthOrHeight = 0.5f;

            if (IsHudCanvas(name))
            {
                HudScalers.Add(scaler);
                ApplyHudScale(scaler);
            }

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// 노치·카메라 홀·홈 인디케이터를 피하는 콘텐츠 루트.
        /// 배경은 캔버스에 그대로 두고 버튼과 본문만 이 아래에 만든다.
        /// </summary>
        public static RectTransform SafeArea(Canvas canvas)
        {
            if (canvas == null) return null;
            UISafeArea safeArea = canvas.GetComponent<UISafeArea>();
            if (safeArea == null) safeArea = canvas.gameObject.AddComponent<UISafeArea>();
            return safeArea.Content;
        }

        // ── HUD 크기 ─────────────────────────────────────────────────
        // 설정의 HUD 크기는 늘 떠 있는 캔버스에만 먹인다. 기준 해상도를 배율만큼 줄이면
        // 같은 픽셀 크기의 요소가 그만큼 크게 그려진다. 모달은 기준 해상도로 칸을 나누므로 빠진다.

        private static readonly string[] HudCanvasNames =
        {
            "BattleHudCanvas", "PreparationCanvas", "TooltipCanvas",
        };

        private static readonly System.Collections.Generic.List<CanvasScaler> HudScalers = new();

        static UIBuild()
        {
            global::Core.SettingsManager.HudScaleChanged += () =>
            {
                HudScalers.RemoveAll(scaler => scaler == null);
                foreach (CanvasScaler scaler in HudScalers) ApplyHudScale(scaler);
            };
        }

        private static bool IsHudCanvas(string name) => Array.IndexOf(HudCanvasNames, name) >= 0;

        private static void ApplyHudScale(CanvasScaler scaler)
        {
            scaler.referenceResolution = UITheme.ReferenceResolution / Mathf.Max(0.5f, UITheme.HudScale);
        }

        /// <summary>클릭이 동작하려면 씬에 EventSystem이 하나는 있어야 한다.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                UpgradeLegacyInputModule(EventSystem.current);
                return;
            }
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        /// <summary>
        /// 씬에 놓인 EventSystem이 구형 StandaloneInputModule을 쓰면 새 입력 시스템용 모듈로 바꾼다.
        ///
        /// Game 씬은 InputSystemUIInputModule인데 MainMenu 씬에만 구형 모듈이 남아 있었다. 구형 모듈은
        /// 옛 InputManager가 켜져 있을 때만 동작해서, 에디터가 입력 설정을 "새 입력 시스템만"으로 읽는 순간
        /// 메인 메뉴의 모든 버튼이 클릭을 받지 못했다(QA — 눌림은 잡히지만 클릭으로 확정되지 않음).
        /// 새 모듈은 두 설정 모두에서 동작하므로 쪽을 하나로 맞춘다.
        /// </summary>
        private static void UpgradeLegacyInputModule(EventSystem system)
        {
#if ENABLE_INPUT_SYSTEM
            if (system == null) return;
            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy == null) return;

            UnityEngine.Object.DestroyImmediate(legacy);
            var module = system.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module == null) module = system.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
#endif
        }

        /// <summary>그래픽 없는 빈 컨테이너. 레이아웃 묶음용.</summary>
        public static RectTransform Container(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 세로 스크롤 영역. 돌려준 content 아래를 위에서부터 채우고, 다 채운 뒤
        /// content의 높이(<c>sizeDelta.y</c>)를 호출한 쪽이 직접 정한다.
        /// 배치는 돌려받은 <paramref name="scroll"/>의 RectTransform으로 잡는다.
        ///
        /// 안에 넣는 항목의 클릭은 Button이나 <see cref="OnClick"/>으로 받는다. EventTrigger는 휠까지
        /// 먹어 버려 항목 위에서 굴리면 목록이 움직이지 않으니 붙이지 않는다.
        /// </summary>
        public static RectTransform ScrollArea(string name, Transform parent, out ScrollRect scroll)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            go.transform.SetParent(parent, false);

            // 투명해도 레이캐스트는 받아야 빈 곳에서 굴려도 휠이 들어온다.
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);

            RectTransform content = Container("Content", go.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = go.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;

            // 세로 스크롤 막대. 긴 본문(자료실 · 코드 목록)에서 지금 어디쯤 읽고 있는지가
            // 보이지 않는다는 QA가 있었다. 내용이 칸보다 짧으면 스스로 숨는다.
            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barGo.transform.SetParent(go.transform, false);
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(5f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            var barTrack = barGo.GetComponent<Image>();
            barTrack.sprite = UIShapes.Solid(Color.white);
            barTrack.color = UITheme.Track;

            RectTransform handleArea = Container("HandleArea", barGo.transform);
            Stretch(handleArea);
            Image handle = Solid("Handle", handleArea,
                new Color(UITheme.TextMuted.r, UITheme.TextMuted.g, UITheme.TextMuted.b, 0.58f));
            Stretch(handle.rectTransform);

            var bar = barGo.GetComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        // ── 패널 ─────────────────────────────────────────────────────

        /// <summary>컷코너 패널. 이 UI의 기본 면(surface).</summary>
        public static Image Panel(string name, Transform parent, Color fill,
            UIShapes.Corner corners = UIShapes.Corner.Diagonal, int cut = 10,
            Color outline = default, int outlineWidth = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.CutCorner(cut, fill, corners, outline, outlineWidth);
            image.type = Image.Type.Sliced;
            // 스프라이트에 색이 이미 구워져 있으므로 tint는 흰색으로 둔다.
            image.color = Color.white;
            return image;
        }

        /// <summary>
        /// 애플 리퀴드 글라스풍 면. 반투명 백색 판 + 또렷한 흰 테두리(빛이 모서리에 맺힌 선) +
        /// 윗부분의 옅은 반사광 한 겹으로 이루어진다. 뒤의 전장이 비쳐 보여 HUD가 무대를 덜 가린다.
        ///
        /// UGUI에는 배경 흐림이 없어 진짜 굴절은 흉내 내지 않는다. 그 대신 판의 불투명도를 낮추고
        /// 테두리 · 반사광을 밝게 잡아 "유리 한 장이 떠 있다"로 읽히게 한다.
        /// 반사광은 첫 자식이므로 호출한 쪽이 나중에 붙이는 내용물은 모두 그 위에 그려진다.
        /// </summary>
        public static Image Glass(string name, Transform parent, int radius = 12, float opacity = 0.55f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.RoundedRect(radius, new Color(0.975f, 0.985f, 0.990f, opacity),
                new Color(1f, 1f, 1f, 0.92f), 1);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var sheenGo = new GameObject("GlassSheen", typeof(RectTransform), typeof(Image));
            sheenGo.transform.SetParent(go.transform, false);
            var sheen = sheenGo.GetComponent<Image>();
            sheen.sprite = UIShapes.RoundedRect(Mathf.Max(2, radius - 1), new Color(1f, 1f, 1f, 0.30f));
            sheen.type = Image.Type.Sliced;
            sheen.raycastTarget = false;
            RectTransform sheenRect = sheen.rectTransform;
            sheenRect.anchorMin = new Vector2(0f, 0.52f);
            sheenRect.anchorMax = new Vector2(1f, 1f);
            sheenRect.offsetMin = new Vector2(2f, 0f);
            sheenRect.offsetMax = new Vector2(-2f, -2f);
            return image;
        }

        /// <summary>단색 사각형. 구분선·배경처럼 모양이 필요 없을 때.</summary>
        public static Image Solid(string name, Transform parent, Color fill)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.Solid(Color.white);
            image.type = Image.Type.Sliced;
            image.color = fill;
            return image;
        }

        /// <summary>전체 화면 암전. 뒤 클릭을 막는 역할도 한다.</summary>
        public static Image Backdrop(string name, Transform parent)
        {
            Image image = Solid(name, parent, UITheme.Backdrop);
            Stretch(image.rectTransform);
            return image;
        }

        // ── 텍스트 ───────────────────────────────────────────────────

        public static TextMeshProUGUI Text(string name, Transform parent, string content,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            // 메이플스토리 Light를 기본으로 쓰고, 해당 글꼴에 없는 문자는 TMP Font Asset에
            // 연결된 NotoSansKR 폴백으로 보낸다.
            ApplyFont(label);
            label.text = content;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.raycastTarget = false; // 텍스트가 아래 버튼 클릭을 막지 않도록
            // 크기는 설정의 글자 크기를 따라 자동 맞춤으로 준다(UIScaledText).
            UIScaledText.Attach(label, fontSize);
            return label;
        }

        /// <summary>
        /// 캔버스와 월드 TMP에 공용 한글 폰트·가독성 재질을 적용한다.
        /// 재질은 한 번만 만들어 공유하므로 호출한 쪽에서 복제하거나 해제하지 않는다.
        /// </summary>
        public static void ApplyFont(TMP_Text label)
        {
            if (label == null) return;
            EnsureUIFont();
            if (_uiFont == null) return;

            label.font = _uiFont;
            Material material = label is TextMeshProUGUI ? UIFontMaterial : WorldFontMaterial;
            if (material == null) return;

            label.fontSharedMaterial = material;
            if (label is TextMeshPro world)
            {
                // World TMP는 CanvasGraphic의 materialForRendering 경로를 쓰지 않는다.
                // MeshRenderer에도 직접 연결하지 않으면 UI/Default가 남아 글자가 완전히 투명해질 수 있다.
                MeshRenderer renderer = world.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = material;
                world.UpdateMeshPadding();
                world.havePropertiesChanged = true;
            }
        }

        private static void EnsureUIFont()
        {
            if (_uiFont != null) return;
            _uiFont = Resources.Load<TMP_FontAsset>(PrimaryUIFontResource);
            _usingRegularFont = _uiFont != null;
            if (_uiFont == null) _uiFont = Resources.Load<TMP_FontAsset>(NotoRegularUIFontResource);
            if (_uiFont == null) _uiFont = Resources.Load<TMP_FontAsset>(NotoLegacyUIFontResource);
        }

        private static Material UIFontMaterial
        {
            get
            {
                if (_uiFontMaterial != null) return _uiFontMaterial;
                EnsureUIFont();
                if (_uiFont == null || _uiFont.material == null) return null;

                Shader mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
                _uiFontMaterial = new Material(_uiFont.material)
                {
                    name = "NeverTheLast UI Font",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (mobile != null) _uiFontMaterial.shader = mobile;

                // Mobile Distance Field는 bevel·glow 경로가 없고 vertex tint를 바로 곱한다.
                // 작은 한글 획만 살짝 보강하되 외곽선이나 그림자는 만들지 않는다.
                _uiFontMaterial.shaderKeywords = Array.Empty<string>();
                // Regular 에셋은 본래 획을 유지하고, 아직 생성되지 않았을 때 쓰는 Thin 폴백만 보강한다.
                _uiFontMaterial.SetFloat("_FaceDilate", _usingRegularFont ? 0.02f : 0.10f);
                _uiFontMaterial.SetFloat("_WeightNormal", _usingRegularFont ? 0f : 0.40f);
                _uiFontMaterial.SetFloat("_OutlineWidth", 0f);
                _uiFontMaterial.SetFloat("_UnderlayOffsetX", 0f);
                _uiFontMaterial.SetFloat("_UnderlayOffsetY", 0f);
                return _uiFontMaterial;
            }
        }

        private static Material WorldFontMaterial
        {
            get
            {
                if (_worldFontMaterial != null) return _worldFontMaterial;
                EnsureUIFont();
                if (_uiFont == null || _uiFont.material == null) return null;

                // MeshRenderer 기반 TextMeshPro에는 UI clip/stencil 경로를 공유하지 않는다.
                _worldFontMaterial = new Material(_uiFont.material)
                {
                    name = "NeverTheLast World Font",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                Shader distanceField = Shader.Find("TextMeshPro/Distance Field");
                if (distanceField != null) _worldFontMaterial.shader = distanceField;
                _worldFontMaterial.shaderKeywords = Array.Empty<string>();
                _worldFontMaterial.SetFloat("_FaceDilate", _usingRegularFont ? 0.02f : 0.10f);
                _worldFontMaterial.SetFloat("_WeightNormal", _usingRegularFont ? 0f : 0.40f);
                _worldFontMaterial.SetFloat("_OutlineWidth", 0f);
                return _worldFontMaterial;
            }
        }

        /// <summary>대문자 + 자간 넓힘. 명일방주 UI의 라벨 표기 습관.</summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string content,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI label = Text(name, parent, content, fontSize, color, alignment);
            label.characterSpacing = 8f;
            return label;
        }

        // ── 버튼 ─────────────────────────────────────────────────────

        /// <summary>
        /// 컷코너 버튼. 색 전이는 Unity 기본 ColorTint를 쓰되,
        /// 스프라이트에 색이 구워져 있으므로 tint를 곱해 명도만 흔든다.
        /// </summary>
        public static Button Button(string name, Transform parent, string text, Action onClick,
            bool primary = false, float fontSize = UITheme.FontBody)
        {
            Color fill = primary ? UITheme.Accent : UITheme.SurfaceRaised;
            Color textColor = primary ? UITheme.TextOnAccent : UITheme.TextPrimary;
            // 보조 버튼에는 옅은 테두리를 준다. 면만 있으면 패널 위의 회색 상자와 구별되지 않아
            // 누를 수 있는 것인지 읽히지 않았다. 꺼진 버튼은 UIButtonStyle이 테두리를 걷는다.
            // Spotlight처럼 보조 행동은 면보다 글자가 먼저 읽히게 한다. 테두리가 강하면
            // 카드·그룹·버튼이 모두 상자로 보여 화면이 격자처럼 굳으므로 1px 헤어라인만 남긴다.
            Color outline = primary ? default : UITheme.Outline;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = UIShapes.CutCorner(8, fill, UIShapes.Corner.Diagonal, outline, primary ? 0 : 1);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            // 정점 색은 1을 넘지 못한다. 예전 highlightedColor(1.18)는 잘려 1이 되어 호버 반응이 없었다.
            // 평소를 조금 낮춰 두고 호버에서 원래 밝기로 올린다.
            colors.normalColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            colors.selectedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            // 꺼진 모습은 UIButtonStyle이 면을 바꿔 그린다. 여기서 한 번 더 곱하면 너무 어두워 사라진다.
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(text))
            {
                TextMeshProUGUI label = Text("Label", go.transform, text, fontSize, textColor,
                    TextAlignmentOptions.Center);
                // 강조색 면 위의 얇은 글자는 축소된 Game View와 고해상도에서 번져 보인다.
                // 주 행동만 굵게 잡아 버튼의 우선순위와 글자 윤곽을 함께 선명하게 만든다.
                if (primary) label.fontStyle = FontStyles.Bold;
                Stretch(label.rectTransform, 8f, 4f);
            }

            UIButtonStyle.Attach(button);
            return button;
        }

        /// <summary>아이콘(문자) 하나만 들어가는 정사각 버튼. HUD 우상단 배속/인벤/설정용.</summary>
        public static Button IconButton(string name, Transform parent, string glyph, Action onClick,
            string tooltip = null)
        {
            Button button = Button(name, parent, glyph, onClick, false, UITheme.FontHeading);
            if (!string.IsNullOrEmpty(tooltip)) button.gameObject.name = $"{name}_{tooltip}";
            return button;
        }

        // ── 게이지 ───────────────────────────────────────────────────

        /// <summary>
        /// 가로 게이지. 반환된 Image의 fillAmount를 0~1로 조절해 쓴다.
        /// track(배경)은 자동 생성되며 fill만 반환한다.
        /// </summary>
        public static Image Bar(string name, Transform parent, Color fillColor, Color trackColor,
            int cut = 0)
        {
            Image track = cut > 0
                ? Panel($"{name}Track", parent, trackColor, UIShapes.Corner.None, cut)
                : Solid($"{name}Track", parent, trackColor);

            var go = new GameObject($"{name}Fill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(track.transform, false);
            var fill = go.GetComponent<Image>();
            fill.sprite = UIShapes.Solid(Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            return fill;
        }

        /// <summary>
        /// 원형 프로그레스. fillAmount 0~1이 12시 방향에서 시계방향으로 찬다.
        /// 라운드 진행 타이머처럼 "남은 양"을 보여줄 때 쓴다.
        /// </summary>
        public static Image RadialBar(string name, Transform parent, Color fillColor, Color trackColor,
            int diameter = 64, float innerRatio = 0.72f)
        {
            var trackGo = new GameObject($"{name}Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            var track = trackGo.GetComponent<Image>();
            track.sprite = UIShapes.Disc(diameter, trackColor, innerRatio);
            track.raycastTarget = false;

            var fillGo = new GameObject($"{name}Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = UIShapes.Disc(diameter, Color.white, innerRatio);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            return fill;
        }

        /// <summary>가로 구분선.</summary>
        public static Image Divider(string name, Transform parent)
        {
            return Solid(name, parent, UITheme.Divider);
        }

        // ── RectTransform 배치 ───────────────────────────────────────

        /// <summary>부모를 꽉 채운다.</summary>
        public static RectTransform Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
            return rect;
        }

        /// <summary>앵커 비율로 배치(0~1). 부모 크기에 비례해 늘어난다.</summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 min, Vector2 max,
            float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
            return rect;
        }

        /// <summary>
        /// 한 점에 고정하고 픽셀 크기를 준다. 모서리에 붙는 HUD 요소에 적합하다.
        /// pivot/anchor가 같으므로 offset은 그 모서리 기준 이동량이 된다.
        /// </summary>
        public static RectTransform Pin(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        // ── 상호작용 ─────────────────────────────────────────────────

        /// <summary>
        /// 임의의 오브젝트에 클릭 반응을 붙인다(카드 전체를 누르게 할 때).
        /// 누를 수 있는 면은 마우스를 올리면 테두리가 떠오른다(<see cref="UIHoverRing"/>).
        /// 예전에는 보상 · 상점 카드가 호버에 아무 반응이 없어 눌리는 것인지 알 수 없었다.
        /// 전체 화면 암전(Backdrop)은 테두리를 붙이지 않는다.
        /// </summary>
        public static void OnClick(GameObject target, Action callback)
        {
            if (target == null || callback == null) return;
            // EventTrigger는 휠 · 드래그까지 먹어 스크롤 목록 안에서 쓸 수 없었다. 클릭만 받는 수신기를 쓴다.
            UIPointerEvents.On(target).Clicked += callback;

            if (target.name != "Backdrop") Ensure<UIHoverRing>(target);
        }

        /// <summary>
        /// 면 뒤에 부드러운 그림자를 깐다. <b>면의 자리를 다 잡은 뒤에</b> 부른다 — 그림자는 형제로 들어가
        /// 면보다 먼저 그려지며, 만들 때의 앵커 · 크기를 복사한다.
        /// 모달 · 떠 있는 바 · 툴팁처럼 "한 층 위"에 있는 면에만 쓴다. 전부에 깔면 층이 사라진다.
        /// </summary>
        public static Image Elevate(Image panel, int blur = 28, float alpha = 0.55f, float drop = 10f)
        {
            if (panel == null || panel.transform.parent == null) return null;

            var go = new GameObject(panel.name + "_Shadow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel.transform.parent, false);
            go.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());

            var shadow = go.GetComponent<Image>();
            shadow.sprite = UIShapes.SoftShadow(12, blur, alpha);
            shadow.type = Image.Type.Sliced;
            shadow.raycastTarget = false;

            RectTransform source = panel.rectTransform;
            RectTransform rect = shadow.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.offsetMin = source.offsetMin + new Vector2(-blur, -blur - drop);
            rect.offsetMax = source.offsetMax + new Vector2(blur, blur - drop);
            return shadow;
        }

        /// <summary>CanvasGroup을 보장한다. 페이드/입력차단 제어용.</summary>
        public static CanvasGroup Group(GameObject target)
        {
            return Ensure<CanvasGroup>(target);
        }

        /// <summary>
        /// 컴포넌트를 보장한다. 없으면 붙인다.
        ///
        /// <b><c>??</c>를 쓰면 안 된다.</b> 널 병합 연산자는 참조 동등성만 보므로
        /// UnityEngine.Object의 수명 검사(파괴된 객체를 null로 취급하는 <c>==</c> 오버로드)를
        /// 건너뛴다. 그 결과 "있는 것처럼 보이지만 실제로는 없는" 컴포넌트를 그대로 돌려주고,
        /// 나중에 접근하는 쪽에서 MissingComponentException이 난다.
        /// (행동서열 슬롯의 CanvasGroup이 실제로 이 경로로 깨졌다.)
        /// </summary>
        private static T Ensure<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        /// <summary>자식을 전부 지운다. 목록을 다시 그릴 때.</summary>
        public static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
