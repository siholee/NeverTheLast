using System.Collections.Generic;
using System.IO;
using Managers;
using Managers.UI.Core;
using Managers.UI.Theme;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// 전장을 밝은 하늘 성채의 유리 경기장으로 묶는다.
    /// 화면 고정 환경 원화 위에 테마 공기색, 경계 없는 카드 접지 각인과 중앙선을 얹는다.
    /// 카드 수와 카메라 배율이 달라져도 각인은 월드 좌표를 따르므로 1:1부터 5:N까지 같은 판으로 읽힌다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlefieldBackdrop : MonoBehaviour
    {
        private const int BackdropSortingOrder = -900;
        private const int FocusSortingOrder = -700;
        private const int MaxSlotMarks = 20;
        private const float EnvironmentCrossfadeSeconds = 0.45f;
        private const float FocusScreenOverscan = 1.12f;

        private static readonly Color BoardBase = new(0.969f, 0.980f, 0.988f, 1f);
        private static readonly Color BoardField = new(0.918f, 0.949f, 0.969f, 1f);
        private static readonly Color LinePrimary = new(0.612f, 0.698f, 0.765f, 1f);
        private static readonly Color CoolShadow = new(0.498f, 0.612f, 0.686f, 1f);

        private static BattlefieldBackdrop _instance;
        private static Rect _boardArea;
        private static bool _hasBoard;
        private static readonly List<Vector2> SlotPositions = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string _verificationOverridePath;
        private static Sprite _verificationOverrideSprite;
        private static Texture2D _verificationOverrideTexture;
        private static int _verificationOverrideVersion;
#endif

        private sealed class FocusRequest
        {
            public float Strength;
            public float FadeIn;
            public float Hold;
            public float FadeOut;
            public float Age;
            public Color Tint;
            public Vector2? FocusWorld;
            public Vector2? SecondaryWorld;

            public float Duration => FadeIn + Hold + FadeOut;

            public float Weight()
            {
                if (FadeIn > 0f && Age < FadeIn) return Mathf.SmoothStep(0f, 1f, Age / FadeIn);
                if (Age <= FadeIn + Hold) return 1f;
                if (FadeOut <= 0f) return 0f;
                return 1f - Mathf.SmoothStep(0f, 1f, (Age - FadeIn - Hold) / FadeOut);
            }
        }

        private sealed class FlashRequest
        {
            public Color Color;
            public float Strength;
            public float Duration;
            public float Age;
        }

        private SpriteRenderer _environment;
        private SpriteRenderer _environmentPrevious;
        private SpriteRenderer _themeVeil;
        private SpriteRenderer _divider;
        private SpriteRenderer _focusVignette;
        private SpriteRenderer _focusSpotlight;
        private SpriteRenderer _focusSecondarySpotlight;
        private SpriteRenderer _focusFlash;
        private readonly List<SpriteRenderer> _slotMarks = new();
        private readonly List<FocusRequest> _focusRequests = new();
        private readonly List<FlashRequest> _flashRequests = new();
        private Camera _camera;
        private int _appliedThemeId = int.MinValue;
        private string _appliedBackgroundToken;
        private float _environmentBlend = 1f;
        private float _environmentAlpha = 1f;
        private float _previousEnvironmentAlpha;
        private float _backgroundLuminance = 0.62f;
        private float _currentFocusStrength;
        private Color _themeVeilBase = Color.clear;
        private Color _dividerBase = Color.clear;
        private Color _markBase = Color.clear;

        /// <summary>카메라 프레이밍과 같은 월드 영역 및 현재 표시할 카드 중심을 받는다.</summary>
        public static void SetBoard(Rect worldArea, bool visible, IReadOnlyList<Vector2> slots = null)
        {
            _boardArea = worldArea;
            _hasBoard = visible && worldArea.width > 0f && worldArea.height > 0f;
            if (!_hasBoard) ClearFocus();
            SlotPositions.Clear();
            if (slots == null) return;

            int count = Mathf.Min(slots.Count, MaxSlotMarks);
            for (int i = 0; i < count; i++) SlotPositions.Add(slots[i]);
        }

        public static void Ensure()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            if (_instance != null && _instance.gameObject == camera.gameObject) return;
            if (_instance != null) Destroy(_instance);
            _instance = camera.gameObject.GetComponent<BattlefieldBackdrop>()
                        ?? camera.gameObject.AddComponent<BattlefieldBackdrop>();
        }

        /// <summary>
        /// 전투 연출 동안 배경만 눌러 카드·HUD·가산광의 실루엣을 띄운다.
        /// 요청이 겹치면 합산하지 않고 현재 가장 강한 곡선을 쓰므로 연사 착탄에서도 명도가 펌핑하지 않는다.
        /// 시간은 <see cref="Time.deltaTime"/>을 따라 배속과 히트스톱에 함께 반응한다.
        /// </summary>
        public static void PushFocus(float strength, float fadeIn, float hold, float fadeOut, Color tint,
            Vector2? focusWorld = null, Vector2? secondaryWorld = null)
        {
            Ensure();
            if (_instance == null || strength <= 0f) return;
            _instance._focusRequests.Add(new FocusRequest
            {
                Strength = Mathf.Min(.28f, Mathf.Clamp01(strength)) * Mathf.Lerp(1f,.75f,Mathf.InverseLerp(2f,4f,Time.timeScale)),
                FadeIn = Mathf.Max(.16f,Mathf.Max(fadeIn,.16f*Time.timeScale)),
                Hold = Mathf.Max(0f, hold),
                FadeOut = Mathf.Max(.22f,Mathf.Max(fadeOut,.10f*Time.timeScale)),
                Tint = tint,
                FocusWorld = focusWorld,
                SecondaryWorld = secondaryWorld,
            });
        }

        /// <summary>충돌 프레임의 짧은 화면 섬광. 카드와 HUD 아래에서 배경에만 번쩍인다.</summary>
        public static void PulseFlash(Color color, float strength = 0.08f, float duration = 0.09f)
        {
            Ensure();
            if (_instance == null || strength <= 0f || duration <= 0f) return;
            _instance._flashRequests.Add(new FlashRequest
            {
                Color = color,
                Strength = Mathf.Min(.018f, Mathf.Clamp01(strength)),
                Duration = Mathf.Max(.22f, duration),
            });
        }

        /// <summary>라운드 종료·재시작처럼 연출 수명이 강제로 끊기는 경계에서 잔류 딤을 즉시 지운다.</summary>
        public static void ClearFocus()
        {
            if (_instance != null) _instance.ResetFocusImmediate();
        }

        public static float CurrentFocusStrength => _instance != null ? _instance._currentFocusStrength : 0f;

        /// <summary>현재 배경 원화의 평균 선형 휘도. UI 자동 보정과 하네스 기록이 같은 값을 쓴다.</summary>
        public static float CurrentBackgroundLuminance => _instance != null
            ? _instance._backgroundLuminance
            : UITheme.BackdropLuminance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 하네스 전용 외부 PNG 배경. 제품 데이터나 Resources를 오염시키지 않고 성격이 다른 배경을 검증한다.
        /// 빈 경로를 주면 현재 테마의 <c>backgroundKey</c>로 돌아간다.
        /// </summary>
        public static void SetVerificationBackgroundOverride(string path)
        {
            string normalized = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path.Trim());
            if (string.Equals(normalized, _verificationOverridePath, System.StringComparison.OrdinalIgnoreCase)) return;

            if (_verificationOverrideSprite != null) Destroy(_verificationOverrideSprite);
            if (_verificationOverrideTexture != null) Destroy(_verificationOverrideTexture);
            _verificationOverrideSprite = null;
            _verificationOverrideTexture = null;
            _verificationOverridePath = normalized;

            if (!string.IsNullOrEmpty(normalized))
            {
                try
                {
                    if (!File.Exists(normalized)) throw new FileNotFoundException("배경 오버라이드 PNG를 찾지 못했다.", normalized);
                    byte[] bytes = File.ReadAllBytes(normalized);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = "verify_backdrop",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    if (!texture.LoadImage(bytes, false)) throw new IOException("PNG 디코딩에 실패했다.");
                    _verificationOverrideTexture = texture;
                    _verificationOverrideSprite = Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                        100f, 0u, SpriteMeshType.FullRect);
                    _verificationOverrideSprite.name = "verify_backdrop";
                    _verificationOverrideSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[시각 검증] 배경 오버라이드를 읽지 못해 테마 배경을 쓴다: {exception.Message}");
                    if (_verificationOverrideTexture != null) Destroy(_verificationOverrideTexture);
                    _verificationOverrideTexture = null;
                    _verificationOverrideSprite = null;
                    _verificationOverridePath = "";
                }
            }

            _verificationOverrideVersion++;
            if (_instance != null) _instance._appliedBackgroundToken = null;
        }
#endif

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = BoardBase;
            }
            BuildLayers();
        }

        private void BuildLayers()
        {
            Sprite board = UIChromeArt.BoardEnvironment;
            _environment = NewLayer("BoardEnvironment", board != null ? board : Solid("board_fallback"),
                BackdropSortingOrder, transform);
            _environmentPrevious = NewLayer("BoardEnvironmentPrevious", _environment.sprite,
                BackdropSortingOrder - 1, transform);
            _environmentPrevious.enabled = false;
            _themeVeil = NewLayer("ThemeVeil", Solid("theme_veil"), BackdropSortingOrder + 1, transform);

            // 전역 명도 곱은 매우 작게 제한한다. 이 정렬대에는 가장자리와 국소 스포트만 남겨
            // 밝은 원화 위에 회색 반투명 막을 얹은 듯한 탁한 인상을 피한다.
            _focusVignette = NewLayer("CombatFocusVignette", VignetteMask(), FocusSortingOrder, transform);
            Sprite spotlight = SpotlightMask();
            _focusSpotlight = NewLayer("CombatFocusSpotlight", spotlight, FocusSortingOrder + 1, transform);
            _focusSecondarySpotlight = NewLayer("CombatFocusSecondarySpotlight", spotlight,
                FocusSortingOrder + 1, transform);
            _focusFlash = NewLayer("CombatFocusFlash", Solid("combat_focus_flash"), FocusSortingOrder + 2, transform);
            ResetFocusImmediate();
            _backgroundLuminance = MeasureAverageLuminance(_environment.sprite != null
                ? _environment.sprite.texture
                : null);
            UIBuild.ApplyBackdropReadability(_backgroundLuminance);

            // 카드와 같은 월드에는 경계 없는 원형 각인과 중앙선만 둔다.
            // 전장 크기로 늘린 바닥/틴트는 배경마다 직사각형 판으로 읽히므로 사용하지 않는다.
            Sprite divider = UIChromeArt.CenterDivider;
            _divider = NewLayer("CenterDivider", divider != null ? divider : Solid("divider_fallback"),
                BackdropSortingOrder + 4, null);

            Sprite mark = FormationMark();
            for (int i = 0; i < MaxSlotMarks; i++)
                _slotMarks.Add(NewLayer($"FormationMark{i}", mark, BackdropSortingOrder + 4, null));
        }

        private void OnDestroy()
        {
            ResetFocusImmediate();
            DestroyWorldLayer(_divider);
            foreach (SpriteRenderer mark in _slotMarks) DestroyWorldLayer(mark);
        }

        private static void DestroyWorldLayer(SpriteRenderer renderer)
        {
            if (renderer != null) Destroy(renderer.gameObject);
        }

        private SpriteRenderer NewLayer(string label, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = SortingLayer.layers[0].id;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.orthographic) return;
            RefreshTheme();
            UpdateEnvironmentTransition();

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            Cover(_environment, halfWidth * 2f, halfHeight * 2f, 20f);
            Cover(_environmentPrevious, halfWidth * 2f, halfHeight * 2f, 20f);
            Stretch(_themeVeil, halfWidth * 2f, halfHeight * 2f, Vector2.zero, 19.9f);
            // 화면 합성 마스크는 카메라 프레이밍과 무관하게 뷰포트보다 크게 다시 맞춘다.
            // 런타임 스프라이트의 투명 페더까지 화면 밖에 두어 비율이 달라도 사각 경계가 들어오지 않는다.
            Cover(_focusVignette, halfWidth * 2f * FocusScreenOverscan,
                halfHeight * 2f * FocusScreenOverscan, 19.8f);
            Cover(_focusSpotlight, halfWidth * 1.08f, halfHeight * 1.56f, 19.7f);
            Cover(_focusSecondarySpotlight, halfWidth * 0.94f, halfHeight * 1.38f, 19.7f);
            Cover(_focusFlash, halfWidth * 2f, halfHeight * 2f, 19.6f);
            UpdateFocus(halfWidth, halfHeight);
            PlaceBoardLayers();
        }

        private void UpdateFocus(float halfWidth, float halfHeight)
        {
            float strongest = 0f;
            Color tint = new(0.16f, 0.30f, 0.46f, 1f);
            Vector2? focusWorld = null;
            Vector2? secondaryWorld = null;
            for (int i = _focusRequests.Count - 1; i >= 0; i--)
            {
                FocusRequest request = _focusRequests[i];
                request.Age += Time.deltaTime;
                float amount = request.Strength * request.Weight();
                if (amount >= strongest)
                {
                    strongest = amount;
                    tint = request.Tint;
                    focusWorld = request.FocusWorld;
                    secondaryWorld = request.SecondaryWorld;
                }
                if (request.Age >= request.Duration) _focusRequests.RemoveAt(i);
            }

            _currentFocusStrength = strongest;
            // 초상과 정보판 위를 균일하게 누르지 않고 화면 외곽에서만 행동의 무게를 만든다.
            ApplyBackdropTone(strongest * 0.08f, tint);
            _focusVignette.color = new Color(0.10f, 0.16f, 0.25f, strongest * 0.90f);
            _focusVignette.transform.localPosition = new Vector3(0f, 0f, 19.8f);
            PlaceSpotlight(_focusSpotlight, focusWorld, halfWidth, halfHeight, strongest * 0.15f,
                new Color(0.70f, 0.84f, 1f));
            PlaceSpotlight(_focusSecondarySpotlight, secondaryWorld, halfWidth, halfHeight,
                strongest * 0.105f, new Color(0.78f, 0.88f, 1f));

            float flash = 0f;
            Color flashColor = Color.white;
            for (int i = _flashRequests.Count - 1; i >= 0; i--)
            {
                FlashRequest request = _flashRequests[i];
                request.Age += Time.deltaTime;
                float amount = request.Strength * (1f - Mathf.SmoothStep(0f, 1f, request.Age / request.Duration));
                if (amount >= flash)
                {
                    flash = amount;
                    flashColor = request.Color;
                }
                if (request.Age >= request.Duration) _flashRequests.RemoveAt(i);
            }
            _focusFlash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flash);
        }

        private void ResetFocusImmediate()
        {
            _focusRequests.Clear();
            _flashRequests.Clear();
            _currentFocusStrength = 0f;
            if (_focusVignette != null) _focusVignette.color = Color.clear;
            if (_focusSpotlight != null) _focusSpotlight.color = Color.clear;
            if (_focusSecondarySpotlight != null) _focusSecondarySpotlight.color = Color.clear;
            if (_focusFlash != null) _focusFlash.color = Color.clear;
            ApplyBackdropTone(0f, Color.white);
        }

        private void PlaceSpotlight(SpriteRenderer renderer, Vector2? world, float halfWidth, float halfHeight,
            float alpha, Color color)
        {
            if (renderer == null) return;
            if (!world.HasValue || alpha <= 0.001f || _camera == null)
            {
                renderer.color = Color.clear;
                return;
            }

            Vector3 viewport = _camera.WorldToViewportPoint(world.Value);
            float x = Mathf.Clamp(viewport.x - 0.5f, -0.52f, 0.52f) * halfWidth * 1.30f;
            float y = Mathf.Clamp(viewport.y - 0.5f, -0.48f, 0.48f) * halfHeight * 1.18f;
            renderer.transform.localPosition = new Vector3(x, y, 19.7f);
            renderer.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void ApplyBackdropTone(float strength, Color target)
        {
            Color multiplier = Color.Lerp(Color.white,
                new Color(Mathf.Clamp01(target.r), Mathf.Clamp01(target.g), Mathf.Clamp01(target.b), 1f),
                Mathf.Clamp01(strength));
            if (_environment != null)
                _environment.color = new Color(multiplier.r, multiplier.g, multiplier.b, _environmentAlpha);
            if (_environmentPrevious != null)
                _environmentPrevious.color = new Color(multiplier.r, multiplier.g, multiplier.b,
                    _previousEnvironmentAlpha);
            if (_themeVeil != null) _themeVeil.color = MultiplyRgb(_themeVeilBase, multiplier);
            if (_divider != null) _divider.color = MultiplyRgb(_dividerBase, multiplier);
            foreach (SpriteRenderer mark in _slotMarks)
                if (mark != null) mark.color = MultiplyRgb(_markBase, multiplier);
        }

        private static Color MultiplyRgb(Color source, Color multiplier)
            => new(source.r * multiplier.r, source.g * multiplier.g, source.b * multiplier.b, source.a);

        private void OnDisable() => ResetFocusImmediate();

        private void PlaceBoardLayers()
        {
            bool visible = _hasBoard;
            _divider.enabled = visible;
            if (!visible)
            {
                foreach (SpriteRenderer mark in _slotMarks) mark.enabled = false;
                return;
            }

            // 원본은 가로 분리선이다. 90도 돌려 양 진영 사이의 세로 축으로 쓴다.
            _divider.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Vector2 dividerSize = _divider.sprite.bounds.size;
            _divider.transform.localScale = new Vector3(
                _boardArea.height / Mathf.Max(0.001f, dividerSize.x),
                Mathf.Min(1.35f, _boardArea.width * 0.042f) / Mathf.Max(0.001f, dividerSize.y), 1f);
            _divider.transform.position = new Vector3(0f, _boardArea.center.y, 5f);

            float markSize = Mathf.Clamp(Mathf.Min(_boardArea.width, _boardArea.height) * 0.20f, 5.2f, 8.4f);
            for (int i = 0; i < _slotMarks.Count; i++)
            {
                SpriteRenderer mark = _slotMarks[i];
                bool show = i < SlotPositions.Count;
                mark.enabled = show;
                if (!show) continue;
                Stretch(mark, markSize, markSize, SlotPositions[i], 5f);
            }
        }

        /// <summary>원화 비율을 보존하며 화면을 완전히 덮는다. 남는 쪽은 화면 밖으로 잘린다.</summary>
        private static void Cover(SpriteRenderer renderer, float width, float height, float z)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 size = renderer.sprite.bounds.size;
            float scale = Mathf.Max(width / size.x, height / size.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.localPosition = new Vector3(0f, 0f, z);
        }

        private static void Stretch(SpriteRenderer renderer, float width, float height, Vector2 center, float z)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.001f, size.x), height / Mathf.Max(0.001f, size.y), 1f);
            renderer.transform.position = new Vector3(center.x, center.y, z);
        }

        private void RefreshTheme()
        {
            RoundManager rounds = GameManager.Instance?.RoundManager;
            int themeId = rounds?.CurrentThemeId ?? 0;
            string backgroundKey = rounds?.CurrentThemeBackgroundKey ?? "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string backgroundToken = _verificationOverrideSprite != null
                ? $"verify:{_verificationOverrideVersion}:{_verificationOverridePath}"
                : $"theme:{backgroundKey}";
#else
            string backgroundToken = $"theme:{backgroundKey}";
#endif
            bool themeChanged = themeId != _appliedThemeId;
            bool backgroundChanged = backgroundToken != _appliedBackgroundToken;
            if (!themeChanged && !backgroundChanged) return;

            _appliedThemeId = themeId;
            _appliedBackgroundToken = backgroundToken;
            if (backgroundChanged)
            {
                Sprite next;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                next = _verificationOverrideSprite != null
                    ? _verificationOverrideSprite
                    : UIChromeArt.LoadBackdrop(backgroundKey);
#else
                next = UIChromeArt.LoadBackdrop(backgroundKey);
#endif
                BeginEnvironmentTransition(next);
                _backgroundLuminance = MeasureAverageLuminance(next != null ? next.texture : null);
                UIBuild.ApplyBackdropReadability(_backgroundLuminance);
            }

            Color ambient = rounds?.CurrentThemeAmbient ?? BoardField;
            // 밝은 원화의 명도를 누르지 않고 테마색은 공기와 바닥에 4~7%만 남긴다.
            _themeVeilBase = new Color(ambient.r, ambient.g, ambient.b, 0.045f);
            _dividerBase = new Color(LinePrimary.r, LinePrimary.g, LinePrimary.b, 0.42f);
            _markBase = new Color(CoolShadow.r, CoolShadow.g, CoolShadow.b,
                Mathf.Clamp01(0.20f * UITheme.AdaptiveGroundingMultiplier));
            ApplyBackdropTone(_currentFocusStrength * 0.08f, new Color(0.16f, 0.30f, 0.46f, 1f));
        }

        private void BeginEnvironmentTransition(Sprite next)
        {
            if (next == null || _environment == null) return;
            if (_environment.sprite == next)
            {
                _environmentBlend = 1f;
                _environmentAlpha = 1f;
                _previousEnvironmentAlpha = 0f;
                if (_environmentPrevious != null) _environmentPrevious.enabled = false;
                return;
            }

            if (_environmentPrevious != null)
            {
                _environmentPrevious.sprite = _environment.sprite;
                _environmentPrevious.enabled = _environmentPrevious.sprite != null;
            }
            _environment.sprite = next;
            _environmentBlend = 0f;
            _environmentAlpha = 0f;
            _previousEnvironmentAlpha = 1f;
        }

        private void UpdateEnvironmentTransition()
        {
            if (_environmentBlend >= 1f) return;
            _environmentBlend = Mathf.Clamp01(_environmentBlend +
                                               Time.unscaledDeltaTime / EnvironmentCrossfadeSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, _environmentBlend);
            _environmentAlpha = eased;
            // 이전 원화는 완전 불투명한 받침으로 두고 새 원화를 위에서 올린다.
            // 둘 다 반대로 페이드하면 중간 프레임 합성 알파가 0.75까지 내려가 카메라 바탕이 비친다.
            _previousEnvironmentAlpha = 1f;
            if (_environmentBlend < 1f || _environmentPrevious == null) return;
            _environmentPrevious.enabled = false;
            _previousEnvironmentAlpha = 0f;
        }

        private static float MeasureAverageLuminance(Texture2D source)
        {
            if (source == null) return 0.62f;
            const int width = 32;
            const int height = 18;
            RenderTexture previous = RenderTexture.active;
            RenderTexture render = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Texture2D probe = null;
            try
            {
                Graphics.Blit(source, render);
                RenderTexture.active = render;
                probe = new Texture2D(width, height, TextureFormat.RGB24, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                probe.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                probe.Apply(false, false);
                Color32[] pixels = probe.GetPixels32();
                double sum = 0d;
                foreach (Color32 pixel in pixels)
                {
                    float r = Mathf.GammaToLinearSpace(pixel.r / 255f);
                    float g = Mathf.GammaToLinearSpace(pixel.g / 255f);
                    float b = Mathf.GammaToLinearSpace(pixel.b / 255f);
                    sum += 0.2126f * r + 0.7152f * g + 0.0722f * b;
                }
                return pixels.Length > 0 ? Mathf.Clamp01((float)(sum / pixels.Length)) : 0.62f;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[전장 배경] 평균 휘도를 측정하지 못해 기본 보정을 쓴다: {exception.Message}");
                return 0.62f;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
                if (probe != null) Destroy(probe);
            }
        }

        private static Sprite FormationMark()
        {
            const int size = 192;
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Band(radius, 0.72f, 0.008f) + Band(radius, 0.51f, 0.005f);
                float axis = (1f - SmoothRange(0.012f, 0.025f, Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy))))
                             * Mathf.Clamp01((0.82f - radius) * 4f);
                float diamond = Band(Mathf.Abs(dx) + Mathf.Abs(dy), 0.27f, 0.010f);
                // 흰 원화가 밝은 바닥에 녹지 않도록 중심에 넓고 약한 접지 그림자를 둔다.
                // SmoothStep의 셋째 인자는 경계값이 아니라 0..1 보간율이므로 거리부터 정규화한다.
                // 세 항 모두 표식 바깥에서 정확히 0이어야 절차 텍스처의 사각 외곽이 드러나지 않는다.
                float grounding = (1f - SmoothRange(0.18f, 0.78f, radius)) * 0.34f;
                float alpha = Mathf.Clamp01(grounding + ring * 0.62f + axis * 0.18f + diamond * 0.46f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            return Build("formation_slot", pixels, size, size);
        }

        private static Sprite VignetteMask()
        {
            const int width = 256;
            const int height = 144;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = ((x + 0.5f) / width - 0.5f) * 2f;
                float dy = ((y + 0.5f) / height - 0.5f) * 2f;
                float radius = Mathf.Max(Mathf.Abs(dx) / 0.88f, Mathf.Abs(dy) / 0.82f);
                float alpha = SmoothRange(0.62f, 1.15f, radius);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
            return Build("combat_focus_vignette", pixels, width, height);
        }

        private static Sprite SpotlightMask()
        {
            const int width = 192;
            const int height = 192;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = ((x + 0.5f) / width - 0.5f) * 2f;
                float dy = ((y + 0.5f) / height - 0.5f) * 2f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                // Mathf.SmoothStep(from, to, t)는 거리 경계 함수가 아니다. 거리를 먼저 0..1로
                // 정규화해야 바깥 투명 여백이 정확히 0이 되어 스포트 사각형이 드러나지 않는다.
                float alpha = 1f - SmoothRange(0.05f, 0.92f, radius);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
            return Build("combat_focus_spotlight", pixels, width, height);
        }

        private static float Band(float value, float center, float halfWidth)
        {
            return 1f - SmoothRange(halfWidth, halfWidth * 2f, Mathf.Abs(value - center));
        }

        private static float SmoothRange(float from, float to, float value)
        {
            float t = Mathf.InverseLerp(from, to, value);
            return t * t * (3f - 2f * t);
        }

        private static Sprite Solid(string name)
        {
            return Build(name, new[] { Color.white }, 1, 1);
        }

        private static Sprite Build(string name, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            // 마스크가 0까지 닫힌 뒤에도 Tight 메시가 저알파 페더를 다시 자르지 않게 투명 여백을 보존한다.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                100f, 0u, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
