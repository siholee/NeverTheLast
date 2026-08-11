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
        public static readonly Color Mana = new(0.322f, 0.671f, 0.937f, 1f);      // 마나/궁극기 자원
        public static readonly Color ManaFull = new(1f, 0.804f, 0.239f, 1f);      // 궁극기 준비 완료
        public static readonly Color Shield = new(0.847f, 0.851f, 0.878f, 1f);

        // 파티 카드 전용 색. 스타레일과 달리 이 게임은 체력을 적색으로,
        // 방어막을 그 위에 덮이는 청색으로, 행동 게이지를 노란색으로 구분한다.
        public static readonly Color HpRed = new(0.851f, 0.243f, 0.243f, 1f);
        public static readonly Color ShieldBlue = new(0.298f, 0.616f, 0.937f, 1f);
        public static readonly Color ActionYellow = new(0.980f, 0.816f, 0.196f, 1f);
        public static readonly Color Enemy = new(0.855f, 0.267f, 0.286f, 1f);
        public static readonly Color Danger = new(0.902f, 0.286f, 0.243f, 1f);
        public static readonly Color Positive = new(0.400f, 0.788f, 0.545f, 1f);

        // ── 텍스트 ───────────────────────────────────────────────────
        public static readonly Color TextPrimary = new(0.957f, 0.961f, 0.965f, 1f);
        public static readonly Color TextSecondary = new(0.667f, 0.686f, 0.706f, 1f);
        public static readonly Color TextMuted = new(0.427f, 0.443f, 0.459f, 1f);
        public static readonly Color TextOnAccent = new(0.071f, 0.071f, 0.071f, 1f);

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
