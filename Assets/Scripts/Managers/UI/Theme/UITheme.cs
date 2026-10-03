using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>
    /// UI 전역의 밝은 하늘 성채 팔레트다. 진주색 면과 라이트 글라스를 기본으로 두고,
    /// 은청색 선과 하늘색 상호작용만 남겨 전투 원소색과 경쟁하지 않는다.
    /// </summary>
    public static class UITheme
    {
        // ── 배경 계열 ────────────────────────────────────────────────
        /// <summary>밝은 화면 위 모달의 뒤 클릭을 막는 청회색 베일.</summary>
        public static readonly Color Backdrop = new(0.145f, 0.204f, 0.263f, 0.50f); // #253443
        public static readonly Color Surface = new(0.973f, 0.988f, 1f, 0.97f);      // #F8FCFF
        public static readonly Color SurfaceRaised = new(1f, 1f, 1f, 0.98f);
        public static readonly Color SurfaceSunken = new(0.918f, 0.949f, 0.969f, 0.98f); // #EAF2F7
        public static readonly Color HudBar = new(0.973f, 0.988f, 1f, 0.88f);

        /// <summary>원화와 VFX 위에 직접 뜨는 패널만 쓰는 중명도 스모크.</summary>
        public static readonly Color PanelCharacter = new(0.149f, 0.220f, 0.290f, 0.62f); // #26384A

        /// <summary>백색 카드 안의 아주 옅은 구획 면.</summary>
        public static readonly Color FaintFill = new(0.475f, 0.796f, 0.910f, 0.055f);
        /// <summary>선택 전 슬롯·빗금처럼 한 단계 더 보이는 보조 면.</summary>
        public static readonly Color SubtleFill = new(0.475f, 0.796f, 0.910f, 0.10f);
        /// <summary>스크롤바·게이지의 비어 있는 트랙.</summary>
        public static readonly Color Track = new(0.396f, 0.498f, 0.569f, 0.18f);

        // ── 포인트 컬러 ──────────────────────────────────────────────
        /// <summary>맑은 하늘색. 선택·호버·진행 상태의 얇은 선과 작은 면에만 쓴다.</summary>
        public static readonly Color Accent = new(0.247f, 0.663f, 0.902f, 1f);       // #3FA9E6
        /// <summary>흰 글자와 4.5:1에 가까운 대비를 확보하는 주 행동 면.</summary>
        public static readonly Color AccentDim = new(0.055f, 0.443f, 0.616f, 1f);    // #0E719D
        public static readonly Color AccentFaint = new(0.247f, 0.663f, 0.902f, 0.15f);
        public static readonly Color AccentCyan = new(0.192f, 0.816f, 0.776f, 1f);    // #31D0C6
        public static readonly Color Highlight = new(0.086f, 0.561f, 0.769f, 1f);     // #168FC4

        // 호환 이름은 유지하되 의미는 안 2의 백색 판/진주 필드/하늘 인레이로 바뀐다.
        public static readonly Color BoardInk = new(0.969f, 0.980f, 0.988f, 1f);     // #F7FAFC
        public static readonly Color BoardFelt = new(0.918f, 0.949f, 0.969f, 1f);    // #EAF2F7
        public static readonly Color BoardSlate = new(0.847f, 0.929f, 0.969f, 1f);   // #D8EDF7
        public static readonly Color MetalBrass = new(0.780f, 0.698f, 0.490f, 1f);   // #C7B27D
        public static readonly Color MetalSilver = new(0.612f, 0.698f, 0.765f, 1f);  // #9CB2C3

        // ── 상태 컬러 ────────────────────────────────────────────────
        public static readonly Color Hp = new(0.157f, 0.780f, 0.682f, 1f);        // #28C7AE
        public static readonly Color HpLow = new(0.800f, 0.220f, 0.270f, 1f);     // 위험 체력
        /// <summary>
        /// 마나 · 궁극기 자원. <b>파랑은 자원 전용이다.</b>
        /// 충전 중 · 충전 완료 · 예약을 색으로 나누지 않고, 링이 찬 각도로만 읽힌다.
        /// (예전의 ManaFull 액센트 전환은 민트를 "지금 눌러야 할 것"에만 쓰기 위해 없앴다.)
        /// </summary>
        public static readonly Color Mana = new(0.129f, 0.431f, 0.741f, 1f);

        /// <summary>
        /// 방어막. 체력 바 윗면에 겹치는 좁은 샴페인 금색 선이다.
        /// 밝은 판에서도 청록 HP와 하늘색 상호작용선에 섞이지 않도록 색 역할을 분리한다.
        /// </summary>
        public static readonly Color Shield = new(0.894f, 0.745f, 0.365f, 1f);    // #E4BE5D

        public static readonly Color ActionYellow = new(0.612f, 0.698f, 0.765f, 1f);
        public static readonly Color Enemy = new(0.878f, 0.353f, 0.408f, 1f);     // #E05A68
        public static readonly Color Danger = new(0.800f, 0.220f, 0.270f, 1f);
        public static readonly Color Positive = new(0.063f, 0.525f, 0.302f, 1f);

        /// <summary>
        /// 행동 불가 계열(기절·빙결·에어본·침묵·속박·도발).
        /// 이로움/해로움과는 다른 축이라 적색에 섞어 두면 "그냥 나쁜 것"으로만 읽힌다.
        /// 8배속에서 카드를 훑을 때 <b>지금 못 움직이는 유닛</b>이 먼저 눈에 들어와야 한다.
        /// </summary>
        public static readonly Color Control = new(0.471f, 0.286f, 0.718f, 1f);

        // ── 텍스트 ───────────────────────────────────────────────────
        public static readonly Color TextOnDark = new(0.973f, 0.988f, 1f, 1f);       // #F8FCFF
        public static readonly Color TextOnDarkSecondary = new(0.847f, 0.910f, 0.949f, 1f);
        public static readonly Color TextOnLight = new(0.145f, 0.204f, 0.263f, 1f);  // #253443
        public static readonly Color TextPrimary = TextOnLight;
        public static readonly Color TextSecondary = new(0.325f, 0.431f, 0.502f, 1f); // #536E80
        /// <summary>
        /// 보조 텍스트. 카드의 부연 설명 · 캡션 · 잠김 사유가 전부 이 색을 쓴다.
        ///
        /// 밝은 카드에서 본문과 경쟁하지 않되 작은 캡션과 잠김 사유가 흐려지지 않는 중간 청회색이다.
        /// </summary>
        public static readonly Color TextMuted = new(0.322f, 0.424f, 0.490f, 1f);

        /// <summary>
        /// 꺼진 버튼의 글자. 보조 텍스트(TextMuted)보다 옅게 잡아 "회색이지만 눌리는 것"과
        /// "눌리지 않는 것"이 갈리게 한다.
        /// </summary>
        public static readonly Color TextDisabled = new(0.145f, 0.204f, 0.263f, 0.62f);
        public static readonly Color TextOnAccent = TextOnDark;

        // ── 코드 등급 ────────────────────────────────────────────────
        /// <summary>일반 등급 코드 — 은색.</summary>
        public static readonly Color CodeNormal = new(0.390f, 0.435f, 0.480f, 1f);

        /// <summary>강화 등급 코드 — 금색. 같은 계열의 일반 등급을 대체한다.</summary>
        public static readonly Color CodeEnhanced = new(0.780f, 0.535f, 0.035f, 1f);

        /// <summary>고유 등급 코드 — 보라색. 은·금 사다리 밖의 P 슬롯 전용이다.</summary>
        public static readonly Color CodeUnique = new(0.475f, 0.310f, 0.745f, 1f);

        // ── 선/구분 ──────────────────────────────────────────────────
        public static readonly Color Divider = new(0.612f, 0.698f, 0.765f, 0.44f);
        public static readonly Color Outline = new(0.612f, 0.698f, 0.765f, 0.82f);

        // ── 등급 컬러(아이템/유닛 레어도) ────────────────────────────
        public static readonly Color[] RarityColors =
        {
            new(0.390f, 0.435f, 0.480f, 1f), // 1성 회색
            new(0.180f, 0.545f, 0.720f, 1f), // 2성 하늘
            new(0.150f, 0.620f, 0.330f, 1f), // 3성 초록
            new(0.520f, 0.350f, 0.760f, 1f), // 4성 보라
            new(0.780f, 0.535f, 0.035f, 1f), // 5성 앰버
            new(0.900f, 0.390f, 0.090f, 1f), // 6성 주황
        };

        // ── 스탯 색축(레거시 이름) ───────────────────────────────────
        // 기존 UI 상수를 보존한 팔레트다. 자원 파랑(Mana) · 액센트 민트 · 적색과 겹치지 않게 쓴다.
        public static readonly Color StatStr = new(0.878f, 0.478f, 0.290f, 1f);   // #E07A4A 주황
        public static readonly Color StatDex = new(0.310f, 0.749f, 0.659f, 1f);   // #4FBFA8 청록
        public static readonly Color StatCon = new(0.427f, 0.831f, 0.404f, 1f);   // #6DD467 연두
        public static readonly Color StatInt = new(0.663f, 0.533f, 0.878f, 1f);   // #A988E0 보라
        public static readonly Color StatLuk = new(0.898f, 0.627f, 0.784f, 1f);   // #E5A0C8 분홍

        /// <summary>스탯 줄의 색. 공격 계열 주황 · 방어 계열 청록 · 체력 연두 · 속도 노랑 · 치명 분홍.</summary>
        public static Color Stat(BaseClasses.BaseEnums.StatType stat) => stat switch
        {
            BaseClasses.BaseEnums.StatType.MaxHp => StatCon,
            BaseClasses.BaseEnums.StatType.Attack => StatStr,
            BaseClasses.BaseEnums.StatType.SpecialAttack => StatInt,
            BaseClasses.BaseEnums.StatType.Defense => StatDex,
            BaseClasses.BaseEnums.StatType.SpecialDefense => new Color(0.36f, 0.62f, 0.86f, 1f),
            BaseClasses.BaseEnums.StatType.Speed => ActionYellow,
            _ => StatLuk,
        };
        public static Color Rarity(int star)
        {
            if (star <= 0) return RarityColors[0];
            return RarityColors[Mathf.Clamp(star - 1, 0, RarityColors.Length - 1)];
        }

        /// <summary>체력 비율에 따라 색을 바꾼다. 30% 이하부터 붉게 물든다.</summary>
        public static Color HpColor(float ratio)
        {
            if (ratio > 0.3f) return Hp;
            return Color.Lerp(HpLow, Hp, Mathf.InverseLerp(0f, 0.3f, ratio));
        }

        // ── 타이포 스케일 ────────────────────────────────────────────
        // 1920x1080 기준. CanvasScaler가 해상도에 맞춰 스케일한다.
        //
        // 예전(34/24/19/16/13/11)은 캡션·마이크로가 1080p에서 11~13px라 PC 모니터 거리에서
        // 읽히지 않았다. 한 단계씩 올리고, 자리가 모자라는 칸은 UIBuild.Text의 자동 맞춤이
        // 예전 크기 근처까지만 줄여 넘치지 않게 한다. 사용자는 설정의 글자 크기로 한 번 더 키운다.
        public const float FontDisplay = 38f;
        public const float FontTitle = 27f;
        public const float FontHeading = 21f;
        public const float FontBody = 18f;
        public const float FontCaption = 15f;
        public const float FontMicro = 13f;

        /// <summary>자동 맞춤이 줄일 수 있는 하한. 이보다 작으면 1080p에서 읽을 수 없다.</summary>
        public const float FontFloor = 11f;

        /// <summary>
        /// 설정의 글자 크기 배율(<see cref="Core.SettingsManager.TextScale"/>).
        /// 설정 매니저가 없는 씬(에디터에서 Game 씬 직행)에서도 1로 동작한다.
        /// </summary>
        /// <summary>설정의 HUD 크기 배율. 상단 바 · 행동 순서 · 준비 바 · 툴팁에만 먹는다.</summary>
        public static float HudScale => global::Core.SettingsManager.Instance != null
            ? global::Core.SettingsManager.Instance.HudScale
            : 1f;

        public static float TextScale => global::Core.SettingsManager.Instance != null
            ? global::Core.SettingsManager.Instance.TextScale
            : 1f;

        // ── 레이아웃 상수 ────────────────────────────────────────────
        public const float CornerCut = 10f;   // 호환 이름은 유지하지만 실제 도형에서는 둥근 모서리 반지름으로 쓴다.
        public const float Gutter = 12f;      // 요소 간 기본 간격
        public const float PanelPad = 20f;    // 패널 내부 여백

        /// <summary>
        /// 모든 화면 고정 크롬이 공유하는 안전 여백. 정규화 앵커가 아니라 기준 해상도 픽셀을 쓰므로
        /// 4:3부터 21:9까지 상단 바 · 좌우 레일 · 하단 독의 끝선이 같은 수직선에 남는다.
        /// </summary>
        public const float ScreenEdgeInset = 24f;
        public const float ScreenTopInset = 10f;
        public const float TopBarHeight = 116f;
        public const float ChromeGap = 20f;
        public const float BelowTopBar = ScreenTopInset + TopBarHeight + ChromeGap;

        private static float _backdropLuminance = 0.62f;

        /// <summary>현재 전장 배경의 선형 상대 휘도 평균. 배경 교체 때 한 번 측정한다.</summary>
        public static float BackdropLuminance => _backdropLuminance;

        /// <summary>밝은 배경에서 흰 글라스가 배경 무늬를 더 확실히 가리도록 불투명도를 올린다.</summary>
        public static float AdaptiveHudAlpha => Mathf.Lerp(0.88f, 0.97f, BackdropBrightnessWeight);

        /// <summary>밝은 배경에서 카드·보스 띠의 스모크를 보강하는 추가 알파.</summary>
        public static float AdaptiveSmokeAlphaBonus => Mathf.Lerp(0.01f, 0.13f, BackdropBrightnessWeight);

        /// <summary>밝은 바닥일수록 흰 원화 아래 접지 각인을 진하게 잡는다.</summary>
        public static float AdaptiveGroundingMultiplier => Mathf.Lerp(0.82f, 1.45f, BackdropBrightnessWeight);

        /// <summary>밝은 원화·피해 숫자를 받치는 월드 글자 그림자의 강도.</summary>
        public static float AdaptiveWorldTextShadow => Mathf.Lerp(0.16f, 0.52f, BackdropBrightnessWeight);

        private static float BackdropBrightnessWeight => Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0.28f, 0.78f, _backdropLuminance));

        /// <summary>배경 샘플러만 갱신한다. UI 제작 코드가 임의의 휘도를 주입하지 않게 내부로 제한한다.</summary>
        internal static void SetBackdropLuminance(float value)
        {
            _backdropLuminance = Mathf.Clamp01(value);
        }

        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        // ── 캔버스 층 ────────────────────────────────────────────────
        // TAB 캐릭터 창 위에 ESC 메뉴 묶음이 온다. ESC는 어느 화면 위에서든 메뉴를 띄워야 하므로
        // 메뉴와 그 위에서 여는 자료실 · 설정 · 확인 창은 전부 캐릭터 창보다 높다.
        // 진행 모달(보상 70 · 선택 80 등)은 둘 다보다 낮다.
        public const int LayerCharacterSheet = 120;
        public const int LayerMenu = 130;
        public const int LayerWiki = 133;
        public const int LayerSettings = 136;
        public const int LayerConfirm = 140;
    }
}
