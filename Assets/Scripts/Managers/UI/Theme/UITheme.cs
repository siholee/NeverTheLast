using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>
    /// UI 전역 테마. Radiant의 넓은 여백과 Spotlight의 절제된 카드 언어를 게임 화면에 맞춘
    /// <b>Porcelain Aurora</b> 계열이다.
    /// 특징: 따뜻한 백색 페이지 + 순백 카드 + 짙은 잉크색 텍스트 + 단일 인터랙션 컬러(딥 틸).
    /// 금색은 장비 등급과 보상처럼 게임 세계의 가치 표현에만 남기고, 눌러야 하는 것은 틸로 통일한다.
    ///
    /// 시판 UI 에셋(Modern UI Pack / Heat 등)을 도입하게 되면
    /// 이 파일과 <see cref="UIShapes"/>만 교체하면 전체 톤이 따라온다.
    /// </summary>
    public static class UITheme
    {
        // ── 배경 계열 ────────────────────────────────────────────────
        /// <summary>전체 화면을 덮는 암전. 밝은 모달과 뒤 화면을 확실히 분리한다.</summary>
        public static readonly Color Backdrop = new(0.035f, 0.051f, 0.063f, 0.48f);
        /// <summary>페이지 위의 기본 패널. 순백보다 아주 조금 차가운 백색이다.</summary>
        public static readonly Color Surface = new(0.973f, 0.980f, 0.984f, 0.99f);
        /// <summary>패널 위에 한 단계 올라간 요소(카드, 슬롯).</summary>
        public static readonly Color SurfaceRaised = new(1f, 1f, 1f, 1f);
        /// <summary>눌린/비활성 요소와 입력 영역. 옅은 회청색으로 깊이를 만든다.</summary>
        public static readonly Color SurfaceSunken = new(0.925f, 0.941f, 0.949f, 1f);
        /// <summary>HUD 바 배경. 전투 화면 위에서도 정보가 읽히도록 거의 불투명하게 둔다.</summary>
        public static readonly Color HudBar = new(0.985f, 0.990f, 0.992f, 0.95f);

        /// <summary>백색 카드 안의 아주 옅은 구획 면.</summary>
        public static readonly Color FaintFill = new(0.055f, 0.078f, 0.098f, 0.035f);
        /// <summary>선택 전 슬롯·빗금처럼 한 단계 더 보이는 보조 면.</summary>
        public static readonly Color SubtleFill = new(0.055f, 0.078f, 0.098f, 0.070f);
        /// <summary>스크롤바·게이지의 비어 있는 트랙.</summary>
        public static readonly Color Track = new(0.055f, 0.078f, 0.098f, 0.110f);

        // ── 포인트 컬러 ──────────────────────────────────────────────
        /// <summary>주 포인트 컬러(딥 틸). 클릭·선택·진행 상태에만 쓴다.</summary>
        public static readonly Color Accent = new(0.047f, 0.451f, 0.404f, 1f);    // #0C7367
        public static readonly Color AccentDim = new(0.035f, 0.282f, 0.259f, 1f);
        public static readonly Color AccentFaint = new(0.047f, 0.451f, 0.404f, 0.10f);

        // ── 상태 컬러 ────────────────────────────────────────────────
        public static readonly Color Hp = new(0.086f, 0.604f, 0.353f, 1f);        // 아군 체력(초록)
        public static readonly Color HpLow = new(0.800f, 0.220f, 0.270f, 1f);     // 위험 체력
        /// <summary>
        /// 마나 · 궁극기 자원. <b>파랑은 자원 전용이다.</b>
        /// 충전 중 · 충전 완료 · 예약을 색으로 나누지 않고, 링이 찬 각도로만 읽힌다.
        /// (예전의 ManaFull 액센트 전환은 민트를 "지금 눌러야 할 것"에만 쓰기 위해 없앴다.)
        /// </summary>
        public static readonly Color Mana = new(0.129f, 0.431f, 0.741f, 1f);

        /// <summary>
        /// 방어막. 체력 바 <b>오른쪽에 이어 붙는</b> 밝은 하늘색(블루 아카이브식).
        /// 예전의 회백색은 어두운 체력 트랙 위에서 빈칸과 구분되지 않았다. 자원 파랑(Mana)보다
        /// 훨씬 밝고 채도가 높아 궁극기 링과 섞여 읽히지 않는다.
        /// </summary>
        public static readonly Color Shield = new(0.200f, 0.690f, 1.000f, 1f);

        public static readonly Color ActionYellow = new(0.835f, 0.580f, 0.000f, 1f);
        public static readonly Color Enemy = new(0.760f, 0.180f, 0.245f, 1f);
        public static readonly Color Danger = new(0.800f, 0.220f, 0.270f, 1f);
        public static readonly Color Positive = new(0.063f, 0.525f, 0.302f, 1f);

        /// <summary>
        /// 행동 불가 계열(기절·빙결·에어본·침묵·속박·도발).
        /// 이로움/해로움과는 다른 축이라 적색에 섞어 두면 "그냥 나쁜 것"으로만 읽힌다.
        /// 8배속에서 카드를 훑을 때 <b>지금 못 움직이는 유닛</b>이 먼저 눈에 들어와야 한다.
        /// </summary>
        public static readonly Color Control = new(0.471f, 0.286f, 0.718f, 1f);

        // ── 텍스트 ───────────────────────────────────────────────────
        // 프로젝트가 Linear 색공간을 쓰므로 웹의 sRGB 색상보다 수치를 낮게 둬야 같은 명도로 보인다.
        public static readonly Color TextPrimary = new(0.012f, 0.018f, 0.026f, 1f);
        public static readonly Color TextSecondary = new(0.018f, 0.028f, 0.040f, 1f);
        /// <summary>
        /// 보조 텍스트. 카드의 부연 설명 · 캡션 · 잠김 사유가 전부 이 색을 쓴다.
        ///
        /// 밝은 카드에서 본문과 경쟁하지 않되 작은 캡션과 잠김 사유가 흐려지지 않는 중간 청회색이다.
        /// </summary>
        public static readonly Color TextMuted = new(0.040f, 0.055f, 0.075f, 1f);

        /// <summary>
        /// 꺼진 버튼의 글자. 보조 텍스트(TextMuted)보다 옅게 잡아 "회색이지만 눌리는 것"과
        /// "눌리지 않는 것"이 갈리게 한다.
        /// </summary>
        public static readonly Color TextDisabled = new(0.220f, 0.250f, 0.280f, 1f);
        public static readonly Color TextOnAccent = new(1f, 1f, 1f, 1f);

        // ── 코드 등급 ────────────────────────────────────────────────
        /// <summary>일반 등급 코드 — 은색.</summary>
        public static readonly Color CodeNormal = new(0.390f, 0.435f, 0.480f, 1f);

        /// <summary>강화 등급 코드 — 금색. 같은 계열의 일반 등급을 대체한다.</summary>
        public static readonly Color CodeEnhanced = new(0.780f, 0.535f, 0.035f, 1f);

        /// <summary>고유 등급 코드 — 보라색. 은·금 사다리 밖의 P 슬롯 전용이다.</summary>
        public static readonly Color CodeUnique = new(0.475f, 0.310f, 0.745f, 1f);

        // ── 선/구분 ──────────────────────────────────────────────────
        public static readonly Color Divider = new(0.055f, 0.078f, 0.098f, 0.09f);
        public static readonly Color Outline = new(0.055f, 0.078f, 0.098f, 0.15f);

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

        // ── 5스탯 색축 ───────────────────────────────────────────────
        // 스탯을 가리키는 별도 축이다. 자원 파랑(Mana) · 액센트 민트 · 적색과 겹치지 않도록
        // 골랐다. 훈련 화면과 코덱스에서만 쓴다 — 전투 HUD는 이 색을 쓰지 않는다.
        public static readonly Color StatStr = new(0.878f, 0.478f, 0.290f, 1f);   // #E07A4A 주황
        public static readonly Color StatDex = new(0.310f, 0.749f, 0.659f, 1f);   // #4FBFA8 청록
        public static readonly Color StatCon = new(0.427f, 0.831f, 0.404f, 1f);   // #6DD467 연두
        public static readonly Color StatInt = new(0.663f, 0.533f, 0.878f, 1f);   // #A988E0 보라
        public static readonly Color StatLuk = new(0.898f, 0.627f, 0.784f, 1f);   // #E5A0C8 분홍

        public static Color Stat(BaseClasses.BaseEnums.PrimaryStat stat) => stat switch
        {
            BaseClasses.BaseEnums.PrimaryStat.STR => StatStr,
            BaseClasses.BaseEnums.PrimaryStat.DEX => StatDex,
            BaseClasses.BaseEnums.PrimaryStat.CON => StatCon,
            BaseClasses.BaseEnums.PrimaryStat.INT => StatInt,
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
