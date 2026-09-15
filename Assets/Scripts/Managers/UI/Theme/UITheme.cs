using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>
    /// UI 전역 테마. 명일방주(Arknights) 계열을 기준으로 한다.
    /// 특징: 거의 무채색인 어두운 배경 + 흰색 텍스트 + 단일 포인트 컬러(앰버).
    /// 색을 남발하지 않고 "강조하고 싶은 것 하나"에만 포인트 컬러를 쓰는 것이 이 톤의 핵심이다.
    ///
    /// 시판 UI 에셋(Modern UI Pack / Heat 등)을 도입하게 되면
    /// 이 파일과 <see cref="UIShapes"/>만 교체하면 전체 톤이 따라온다.
    /// </summary>
    public static class UITheme
    {
        // ── 배경 계열 ────────────────────────────────────────────────
        /// <summary>전체 화면을 덮는 암전. 모달 뒤에 깔린다.</summary>
        public static readonly Color Backdrop = new(0.031f, 0.033f, 0.036f, 0.88f);
        /// <summary>패널 본체. 거의 검정에 가까운 무채색.</summary>
        public static readonly Color Surface = new(0.086f, 0.090f, 0.098f, 0.96f);
        /// <summary>패널 위에 한 단계 올라간 요소(카드, 슬롯).</summary>
        public static readonly Color SurfaceRaised = new(0.130f, 0.136f, 0.145f, 0.98f);
        /// <summary>눌린/비활성 요소.</summary>
        public static readonly Color SurfaceSunken = new(0.055f, 0.058f, 0.063f, 0.98f);
        /// <summary>HUD 바 배경. 전투 화면이 비쳐야 하므로 더 투명하다.</summary>
        public static readonly Color HudBar = new(0.043f, 0.047f, 0.051f, 0.82f);

        // ── 포인트 컬러 ──────────────────────────────────────────────
        /// <summary>주 포인트 컬러(앰버). 명일방주의 시그니처 톤.</summary>
        public static readonly Color Accent = new(1f, 0.804f, 0.239f, 1f);        // #FFCD3D
        public static readonly Color AccentDim = new(0.62f, 0.49f, 0.14f, 1f);
        public static readonly Color AccentFaint = new(1f, 0.804f, 0.239f, 0.18f);

        // ── 상태 컬러 ────────────────────────────────────────────────
        public static readonly Color Hp = new(0.427f, 0.831f, 0.404f, 1f);        // 아군 체력(연두)
        public static readonly Color HpLow = new(0.902f, 0.286f, 0.243f, 1f);     // 위험 체력
        /// <summary>
        /// 마나 · 궁극기 자원. <b>파랑은 자원 전용이다.</b>
        /// 충전 중 · 충전 완료 · 예약을 색으로 나누지 않고, 링이 찬 각도로만 읽힌다.
        /// (예전의 ManaFull 앰버 전환은 앰버를 "지금 눌러야 할 것"에만 쓰기 위해 없앴다.)
        /// </summary>
        public static readonly Color Mana = new(0.322f, 0.671f, 0.937f, 1f);

        /// <summary>방어막. 체력 바 <b>오른쪽에 이어 붙는</b> 회백색.</summary>
        public static readonly Color Shield = new(0.847f, 0.851f, 0.878f, 1f);

        public static readonly Color ActionYellow = new(0.980f, 0.816f, 0.196f, 1f);
        public static readonly Color Enemy = new(0.855f, 0.267f, 0.286f, 1f);
        public static readonly Color Danger = new(0.902f, 0.286f, 0.243f, 1f);
        public static readonly Color Positive = new(0.400f, 0.788f, 0.545f, 1f);

        /// <summary>
        /// 행동 불가 계열(기절·빙결·에어본·침묵·속박·도발).
        /// 이로움/해로움과는 다른 축이라 적색에 섞어 두면 "그냥 나쁜 것"으로만 읽힌다.
        /// 8배속에서 카드를 훑을 때 <b>지금 못 움직이는 유닛</b>이 먼저 눈에 들어와야 한다.
        /// </summary>
        public static readonly Color Control = new(0.780f, 0.510f, 0.988f, 1f);

        // ── 텍스트 ───────────────────────────────────────────────────
        public static readonly Color TextPrimary = new(0.957f, 0.961f, 0.965f, 1f);
        public static readonly Color TextSecondary = new(0.667f, 0.686f, 0.706f, 1f);
        /// <summary>
        /// 보조 텍스트. 카드의 부연 설명 · 캡션 · 잠김 사유가 전부 이 색을 쓴다.
        ///
        /// 예전 값(0.427)은 <see cref="SurfaceSunken"/> 위에서 대비가 <b>3.9:1</b>이라
        /// 작은 글자 기준선(4.5:1)에 못 미쳤다. 축소된 에디터 화면에서 특히 읽기 어려웠다.
        /// 한 단계 올려 <b>5.9:1</b>로 맞췄다. 더 올리면 본문(TextSecondary, 8.7:1)과 구분이 사라진다.
        /// </summary>
        public static readonly Color TextMuted = new(0.545f, 0.561f, 0.576f, 1f);
        public static readonly Color TextOnAccent = new(0.071f, 0.071f, 0.071f, 1f);

        // ── 코드 등급 ────────────────────────────────────────────────
        /// <summary>일반 등급 코드 — 은색.</summary>
        public static readonly Color CodeNormal = new(0.847f, 0.851f, 0.878f, 1f);

        /// <summary>강화 등급 코드 — 금색. 같은 계열의 일반 등급을 대체한다.</summary>
        public static readonly Color CodeEnhanced = new(1f, 0.804f, 0.239f, 1f);

        /// <summary>고유 등급 코드 — 보라색. 은·금 사다리 밖의 P 슬롯 전용이다.</summary>
        public static readonly Color CodeUnique = new(0.702f, 0.549f, 1f, 1f);

        // ── 선/구분 ──────────────────────────────────────────────────
        public static readonly Color Divider = new(1f, 1f, 1f, 0.10f);
        public static readonly Color Outline = new(1f, 1f, 1f, 0.16f);

        // ── 등급 컬러(아이템/유닛 레어도) ────────────────────────────
        public static readonly Color[] RarityColors =
        {
            new(0.596f, 0.612f, 0.627f, 1f), // 1성 회색
            new(0.400f, 0.729f, 0.867f, 1f), // 2성 하늘
            new(0.443f, 0.784f, 0.494f, 1f), // 3성 초록
            new(0.706f, 0.529f, 0.898f, 1f), // 4성 보라
            new(1f, 0.804f, 0.239f, 1f),     // 5성 앰버
            new(1f, 0.545f, 0.259f, 1f),     // 6성 주황
        };

        // ── 5스탯 색축 ───────────────────────────────────────────────
        // 스탯을 가리키는 별도 축이다. 자원 파랑(Mana) · 액센트 앰버 · 적색과 겹치지 않도록
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
        public const float FontDisplay = 34f;
        public const float FontTitle = 24f;
        public const float FontHeading = 19f;
        public const float FontBody = 16f;
        public const float FontCaption = 13f;
        public const float FontMicro = 11f;

        // ── 레이아웃 상수 ────────────────────────────────────────────
        public const float CornerCut = 10f;   // 컷코너 크기(px). 명일방주풍 각진 모서리.
        public const float Gutter = 12f;      // 요소 간 기본 간격
        public const float PanelPad = 20f;    // 패널 내부 여백

        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
    }
}
