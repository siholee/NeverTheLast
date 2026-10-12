using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Effects.Vfx
{
    /// <summary>구매 손그림·기존 캐릭터 원화·절차 보조 도형을 연결한다. 기본 보조 도형은 Hovl 텍스처를 읽지 않는다.</summary>
    public static class VfxArt
    {
        private static readonly Dictionary<string, Sprite> Loaded = new();
        private static readonly Dictionary<string, Sprite> Painted = new();
        private static Material _trailAdditive;
        private static Material _trailAlpha;
        private static Texture2D _trailTexture;

        // ── 그린 텍스처 ──────────────────────────────────────────────

        /// <summary>혜성 머리와 꼬리. 머리가 <b>왼쪽</b>이다 — 진행 방향 + 180°로 돌려 쓴다.</summary>
        public static Sprite Streak => VfxSpace.Streak;
        /// <summary>여러 갈래로 뻗는 별 섬광. 타격의 심지.</summary>
        public static Sprite Starburst => VfxSpace.Sparkle;
        /// <summary>네 갈래 반짝임(원신식 별빛).</summary>
        public static Sprite Glint => VfxSpace.Sparkle;
        /// <summary>부드러운 빛무리.</summary>
        public static Sprite SoftGlow => VfxSpace.Glow;
        /// <summary>초승달 모양의 휘두름 자국.</summary>
        public static Sprite Crescent => VfxSpace.Crescent;
        /// <summary>안쪽이 비고 가장자리가 밝은 충격파 고리.</summary>
        public static Sprite Shockwave => VfxSpace.Ring;
        /// <summary>빛살이 뻗는 문양 마법진.</summary>
        public static Sprite RuneRadiant => VfxSpace.RuneRing;
        /// <summary>장식 고리 마법진. 오래 유지되는 표식(집중)에 쓴다.</summary>
        public static Sprite RuneOrnate => VfxSpace.RuneRing;
        /// <summary>뭉게 연기. 알파로 흙먼지 색을 입혀 쓴다.</summary>
        public static Sprite Smoke => VfxSpace.SoftDot;
        /// <summary>가운데서 뻗는 빛살.</summary>
        public static Sprite Rays => VfxSpace.Glow;
        /// <summary>뾰족한 타격 별.</summary>
        public static Sprite ImpactStar => VfxSpace.Sparkle;
        /// <summary>마름모 — 바위 원소의 표식.</summary>
        public static Sprite Diamond => VfxSpace.Diamond;

        // ── 생성 원화 ───────────────────────────────────────────────
        // 단순 도형으로는 속도 껍질과 지층의 면 분할이 지나치게 매끈하게 보였다. 생성 원화는
        // 알파 가장자리를 정리한 뒤 Resources에 두고, 임포트 전에는 기존 절차 원화로 물러난다.
        /// <summary>밝은 전장에서도 중심이 남는 황토색 방향성 먼지 원뿔.</summary>
        public static Sprite DustCone => Tex("Gen/DustCone_Ochre", Smoke);

        /// <summary>쿠베라 시전의 입체 금고. 원화 재질은 투명 손그림 텍스처로 유지한다.</summary>
        public static Sprite TreasuryCoffer => Tex("Gen/TreasuryCoffer_Kubera_v1", VfxSpace.RoundedRect);

        /// <summary>
        /// 시의 「귀참」 전용 청록 검기. 흰 코어 바깥을 세 갈래 발톱과 송곳니처럼 찢고
        /// 먹청 야수령 잔상을 한 장에 담아, 밝은 전장에서도 가산광만 날아가지 않게 한다.
        /// </summary>
        public static Sprite ShiTealSlash => Tex("Gen/ShiTealSlash", Crescent);

        /// <summary>시의 귀참이 대상 위에 남기는 흑색 몸체·붉은 칼심의 세 줄 발톱 자국.</summary>
        public static Sprite ShiCrimsonClawRake => Tex("Maple/Shi/CrimsonClawRake", Crescent);

        /// <summary>진행 와이프와 줄별 시간차를 위해 분리한 흑색 몸체·붉은 칼심의 단일 발톱 자국.</summary>
        public static Sprite ShiCrimsonClawStroke => Tex("Maple/Shi/CrimsonClawStroke", Streak);

        /// <summary>팩의 3단 셀 셰이딩 문법으로 새로 그린 직선 발톱 한 가닥. 세 줄·색 변형은 런타임이 조합한다.</summary>
        public static Sprite HandpaintClawStroke => Tex("Handpaint/ClawStroke_Blue", ShiCrimsonClawStroke);

        /// <summary>96_vfx의 전용 렌더러가 지정한 손그림 단일 원화를 읽는다.</summary>
        public static Sprite HandpaintTexture(string name, Sprite fallback = null)
            => string.IsNullOrWhiteSpace(name) ? fallback : Tex("Handpaint/" + name, fallback);

        /// <summary>교체 전 히미코 일반행동의 주황 몸체·백열 코어 세 줄 불꽃 발톱.</summary>
        public static Sprite PyroFlameClawRake => Tex("Maple/Pyro/FlameClawRake", Crescent);

        /// <summary>히미코 불씨 연발의 진행 방향 꼬리를 가진 화염탄. 원화의 선단은 오른쪽이다.</summary>
        public static Sprite HimikoFlameVolleyBolt => Tex("Maple/Pyro/FlameVolleyBolt", Streak);

        /// <summary>찬드라 「샤친」의 응축된 월식 그림자. 진행 방향은 오른쪽이다.</summary>
        public static Sprite ChandraSachiShadow => Tex("Gen/ChandraSachiShadow", Streak);

        /// <summary>찬드라 「창야」의 지면 월륜과 상향 분출을 한 장에 묶은 원화.</summary>
        public static Sprite ChandraChangyaEruption => Tex("Gen/ChandraChangyaEruption", RuneRadiant);

        /// <summary>찬드라의 공용 여섯 발 연발에 쓰는 은백·연청 월광 탄.</summary>
        public static Sprite ChandraMoonlightVolleyBolt => Tex("Maple/Chandra/MoonlightVolleyBolt", Streak);

        /// <summary>얼굴 중앙을 비우고 원화 뒤로 내리는 월광 축복 강림.</summary>
        public static Sprite ChandraMoonBlessing => Tex("Maple/Chandra/MoonBlessing", Rays);

        /// <summary>소마와 창야가 크기 차이를 두고 공유하는 타원형 월광 파장.</summary>
        public static Sprite ChandraMoonWaveRing => Tex("Maple/Chandra/MoonWaveRing", Shockwave);

        /// <summary>수렴한 월광이 전 아군을 하나로 감싸며 닫히는 찬드라의 성역 돔.</summary>
        public static Sprite ChandraMoonSanctuaryDome => Tex("Maple/Chandra/MoonSanctuaryDome", RuneRadiant);

        /// <summary>히미코·피그말리온 공용 — 대상 발밑에서 솟는 주홍 불씨.</summary>
        public static Sprite SharedEmberRise => Tex("Gen/SharedEmberRise", Starburst);

        /// <summary>세이의 전방 연발용 결정탄. 원화의 진행 방향은 오른쪽이다.</summary>
        public static Sprite SeiRadiantVolleyBolt => Tex("Maple/Sei/RadiantVolleyBolt", Streak);

        /// <summary>세이 빅뱅의 추적 파편. 원화의 바늘 끝은 오른쪽이다.</summary>
        public static Sprite SeiBigBangShard => Tex("Maple/Sei/BigBangCrystalShard", Streak);

        /// <summary>사바흐가 녹아 이동할 때 바닥을 타는 검보라 그림자 웅덩이.</summary>
        public static Sprite SabahShadowPuddle => Tex("Gen/Sabah/ShadowPuddle", SoftGlow);

        /// <summary>아즈라엘의 화면 횡단 영혼베기. 몸체 알파와 휘도 코어가 같은 원화를 따로 합성한다.</summary>
        public static Sprite SabahSoulCleaveArc => Tex("Gen/Sabah/SoulCleaveArc", Crescent);

        /// <summary>아즈라엘 예비 단계에서 전장 뒤를 점유하는 사바흐·대검 그림자 실루엣.</summary>
        public static Sprite SabahAzraelShadowDominion => Tex("Gen/Sabah/AzraelShadowDominion", Smoke);

        /// <summary>아즈라엘 절단 단계의 화면 폭 전체 검보라 몸체와 좁은 백색 칼심.</summary>
        public static Sprite SabahAzraelCleaveBand => Tex("Gen/Sabah/AzraelCleaveBand", Streak);

        /// <summary>진행 방향(오른쪽)이 고정된 작은 사암 파편 여섯 종.</summary>
        public static Sprite SandShard(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            return Tex("Gen/SandShard_" + (variant + 1), TectonicShard(variant));
        }

        /// <summary>사각 경계 없이 방사형으로 사그라드는 흙먼지 네 종. 색은 소비처에서 황토 중심으로 곱한다.</summary>
        public static Sprite DustPuff(int variant)
        {
            variant = Mathf.Abs(variant) % 4;
            return Tex("Gen/DustPuff_" + (variant + 1), Smoke);
        }

        /// <summary><c>Resources/VfxTex/{name}</c>를 한 변이 월드 1인 스프라이트로 읽는다.</summary>
        private static Sprite Tex(string name, Sprite fallback)
        {
            if (Loaded.TryGetValue(name, out Sprite cached)) return cached != null ? cached : fallback;

            Texture2D texture = Resources.Load<Texture2D>("VfxTex/" + name);
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
                sprite.name = "VfxTex_" + name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            Loaded[name] = sprite;
            return sprite != null ? sprite : fallback;
        }

        // ── 로우폴리 바위 ────────────────────────────────────────────

        /// <summary>
        /// 모난 바위 덩어리. 들쭉날쭉한 외곽을 윗왼쪽에 치우친 꼭짓점으로 쪼개 면마다 빛을 따로 받게 한다
        /// (로우폴리 · 셀 음영). 굵은 외곽선과 면 사이의 주름이 무게를 만든다.
        /// <paramref name="variant"/>가 모양을 가른다 — 여러 개를 섞어 뿌려야 같은 돌이 반복돼 보이지 않는다.
        /// 짝수 변형에는 금빛 광맥이 한 줄 지나간다(바위 원소가 깃든 돌).
        /// </summary>
        public static Sprite Rock(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            string key = "rock_" + variant;
            if (Painted.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var rng = new System.Random(1301 + variant * 7919);
            int count = 7 + variant % 3;
            var outline = new Vector2[count];
            var heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)(rng.NextDouble() - 0.5) * 0.55f) / count * Mathf.PI * 2f;
                float radius = 0.64f + (float)rng.NextDouble() * 0.30f;
                outline[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.84f);
                heights[i] = 0.10f + (float)rng.NextDouble() * 0.34f;
            }
            var apex = new Vector2(-0.14f + (float)(rng.NextDouble() - 0.5) * 0.12f, 0.16f + (float)(rng.NextDouble() - 0.5) * 0.12f);

            // 면마다 빛을 받는 정도. 꼭짓점 높이로 기운 평면의 법선과 빛 방향(윗왼쪽 앞)의 내적이다.
            var light = new Vector3(-0.48f, 0.56f, 0.68f).normalized;
            var facetShade = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = new(apex.x, apex.y, 1f);
                Vector3 p1 = new(outline[i].x, outline[i].y, heights[i]);
                Vector2 o2 = outline[(i + 1) % count];
                Vector3 p2 = new(o2.x, o2.y, heights[(i + 1) % count]);
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                if (normal.z < 0f) normal = -normal;
                facetShade[i] = Mathf.Clamp01(Vector3.Dot(normal, light) * 1.15f + (float)(rng.NextDouble() - 0.5) * 0.08f);
            }

            bool vein = variant % 2 == 0;
            Vector2 veinFrom = apex + new Vector2(0.08f, -0.12f);
            Vector2 veinTo = outline[(variant + 3) % count] * 0.86f;

            // 안 2의 백색 판에서는 밝은 사암만 쓰면 면이 사라진다. 황토 중간면보다 엄버 외곽과 골을 먼저 남긴다.
            Color dark = new(0.439f, 0.275f, 0.180f); // Earth.Deep #70462E
            Color mid = new(0.608f, 0.376f, 0.220f);  // Earth.Ochre #9B6038
            Color bright = new(0.886f, 0.718f, 0.478f); // Earth.Lit #E2B77A
            Color ink = new(0.224f, 0.125f, 0.078f);
            Color gold = new(0.949f, 0.725f, 0.314f); // 좁은 광맥만 밝힌다.

            Sprite sprite = PaintSupersampled(key, 112, (x, y) =>
            {
                var p = new Vector2(x, y);
                int facet = FacetAt(p, apex, outline);
                if (facet < 0) return Color.clear;

                float s = facetShade[facet];
                Color c = s < 0.5f ? Color.Lerp(dark, mid, s / 0.5f) : Color.Lerp(mid, bright, (s - 0.5f) / 0.5f);

                // 면 사이 주름 — 안쪽 모서리를 살짝 어둡게.
                float crease = Mathf.Min(SegmentDistance(p, apex, outline[facet]),
                    SegmentDistance(p, apex, outline[(facet + 1) % count]));
                if (crease < 0.022f) c = Color.Lerp(c, dark, 0.35f * (1f - crease / 0.022f));

                if (vein)
                {
                    float v = SegmentDistance(p, veinFrom, veinTo);
                    if (v < 0.03f) c = Color.Lerp(c, gold, 0.9f * (1f - v / 0.03f));
                }

                // 굵은 외곽선.
                float edge = SegmentDistance(p, outline[facet], outline[(facet + 1) % count]);
                if (edge < 0.060f) c = Color.Lerp(c, ink, 0.92f * (1f - edge / 0.060f));
                c.a = 1f;
                return c;
            });
            Painted[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 세이의 「땅고르기」 전용 암편. 둥근 돌덩이가 아니라 진행 방향으로 길게 찢어진 지층 조각이다.
        /// 넓은 면, 칼날 같은 앞끝, 뒤쪽의 깨진 단면과 비대칭 금빛 광맥으로 한눈에 방향이 읽힌다.
        /// </summary>
        public static Sprite TectonicShard(int variant)
        {
            variant = Mathf.Abs(variant) % 4;
            string key = "tectonic_shard_" + variant;
            if (Painted.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var rng = new System.Random(7927 + variant * 6151);
            var outline = new[]
            {
                new Vector2(-0.98f,  0.01f), // 깊게 깨진 꼬리 홈
                new Vector2(-0.73f,  0.18f),
                new Vector2(-0.20f,  0.25f),
                new Vector2( 0.46f,  0.13f),
                new Vector2( 0.99f,  0.00f), // 바늘처럼 모이는 선단
                new Vector2( 0.43f, -0.12f),
                new Vector2(-0.12f, -0.23f),
                new Vector2(-0.76f, -0.15f),
            };
            for (int i = 0; i < outline.Length; i++)
            {
                float jitterX = (float)(rng.NextDouble() - 0.5) * 0.06f;
                float jitterY = (float)(rng.NextDouble() - 0.5) * 0.035f;
                outline[i] += new Vector2(jitterX, jitterY);
            }

            Vector2 apex = new(-0.16f + variant * 0.028f, 0.025f - variant * 0.012f);
            var shade = new[] { 0.32f, 0.82f, 0.96f, 0.62f, 0.38f, 0.18f, 0.50f, 0.70f };
            Vector2 veinA = new(-0.58f, 0.08f);
            Vector2 veinB = new(-0.12f, 0.01f + variant * 0.025f);
            Vector2 veinC = new(0.32f, 0.10f - variant * 0.03f);
            Vector2 veinD = new(0.70f, 0.035f);

            Color shadow = new(0.439f, 0.275f, 0.180f); // Earth.Deep
            Color earth = new(0.608f, 0.376f, 0.220f);  // Earth.Ochre
            Color sand = new(0.773f, 0.541f, 0.325f);   // Earth.Sand
            Color lit = new(0.886f, 0.718f, 0.478f);    // Earth.Lit
            Color ink = new(0.224f, 0.125f, 0.078f);
            Color gold = new(0.949f, 0.725f, 0.314f);   // Earth.Fissure

            Sprite sprite = PaintSupersampled(key, 144, (x, y) =>
            {
                var p = new Vector2(x, y);
                int facet = FacetAt(p, apex, outline);
                if (facet < 0) return Color.clear;

                float s = shade[facet % shade.Length];
                Color c = s < 0.48f
                    ? Color.Lerp(shadow, earth, s / 0.48f)
                    : s < 0.82f
                        ? Color.Lerp(earth, sand, (s - 0.48f) / 0.34f)
                        : Color.Lerp(sand, lit, (s - 0.82f) / 0.18f);

                // 방사형 면 경계는 얇고 짙게, 앞끝의 윗면은 밝게 잡아 속도를 읽힌다.
                float crease = Mathf.Min(SegmentDistance(p, apex, outline[facet]),
                    SegmentDistance(p, apex, outline[(facet + 1) % outline.Length]));
                if (crease < 0.018f) c = Color.Lerp(c, shadow, 0.42f * (1f - crease / 0.018f));
                if (p.x > 0.32f && p.y > -0.02f) c = Color.Lerp(c, lit, 0.24f);
                if (p.x > 0.76f) c = Color.Lerp(c, lit, Mathf.InverseLerp(0.76f, 1f, p.x) * 0.38f);

                float vein = Mathf.Min(
                    Mathf.Min(SegmentDistance(p, veinA, veinB), SegmentDistance(p, veinB, veinC)),
                    SegmentDistance(p, veinC, veinD));
                if (vein < 0.022f) c = Color.Lerp(c, gold, 0.90f * (1f - vein / 0.022f));
                if (vein < 0.007f) c = Color.Lerp(c, new Color(1f, 0.88f, 0.56f), 0.34f);

                float edge = SegmentDistance(p, outline[facet], outline[(facet + 1) % outline.Length]);
                if (edge < 0.052f) c = Color.Lerp(c, ink, 0.94f * (1f - edge / 0.052f));
                c.a = 1f;
                return c;
            });
            Painted[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 금빛 결정 조각(원신 바위 원소의 결정화 보호막). 세로로 긴 마름모를 네 면으로 나눠
        /// 한쪽을 밝게, 반대쪽을 짙은 호박색으로 칠하고 가장자리에 흰 반사광을 두른다.
        /// </summary>
        public static Sprite Crystal
        {
            get
            {
                const string key = "geo_crystal";
                if (Painted.TryGetValue(key, out Sprite cached) && cached != null) return cached;

                Color lit = new(0.98f, 0.85f, 0.52f), face = new(0.86f, 0.58f, 0.20f), deep = new(0.42f, 0.25f, 0.10f);
                Sprite sprite = PaintSupersampled(key, 96, (x, y) =>
                {
                    // 가로 0.46, 세로 0.95의 마름모.
                    float d = Mathf.Abs(x) / 0.46f + Mathf.Abs(y) / 0.95f;
                    if (d > 1f) return Color.clear;
                    Color c;
                    if (x < 0f) c = y > 0f ? lit : face;
                    else c = y > 0f ? face : deep;
                    // 가운데 능선과 가장자리 반사광.
                    if (Mathf.Abs(x) < 0.035f) c = Color.Lerp(c, Color.white, 0.55f);
                    if (d > 0.86f) c = Color.Lerp(c, new Color(0.34f, 0.20f, 0.09f), (d - 0.86f) / 0.14f * 0.82f);
                    c.a = 1f;
                    return c;
                });
                Painted[key] = sprite;
                return sprite;
            }
        }

        private static int FacetAt(Vector2 p, Vector2 apex, Vector2[] outline)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (InTriangle(p, apex, outline[i], outline[(i + 1) % outline.Length])) return i;
            }
            return -1;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.00001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>한 픽셀을 3 × 3으로 나눠 칠하고 평균한다 — 외곽선과 면 경계가 계단 없이 매끈하다.</summary>
        private static Sprite PaintSupersampled(string key, int size, System.Func<float, float, Color> paint)
        {
            const int ss = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VfxArt_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float x = (px + (sx + 0.5f) / ss) / size * 2f - 1f;
                            float y = (py + (sy + 0.5f) / ss) / size * 2f - 1f;
                            Color c = paint(x, y);
                            // 색은 알파로 가중해 더한다 — 가장자리에서 검은 테가 번지지 않게.
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    }
                    float a = sum.a / (ss * ss);
                    pixels[py * size + px] = a > 0f
                        ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, a)
                        : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ── 궤적 ─────────────────────────────────────────────────────

        /// <summary>
        /// 날아가는 물체 뒤로 이어지는 띠. 점 파티클을 흘리는 것과 달리 <b>끊기지 않는 선</b>이라
        /// 빠르게 날아도 궤적이 한 획으로 읽힌다(탈리야의 실 · 원신의 원소 궤적).
        /// </summary>
        /// <param name="additive">빛이면 참(금빛 실), 흙먼지면 거짓.</param>
        public static TrailRenderer NewTrail(Transform parent, Color head, Color tail, float width, float time,
            int sortingLayer, int order, bool additive, float endWidth = 0.15f)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = additive ? TrailAdditive : TrailAlpha;
            trail.time = time;
            trail.minVertexDistance = VfxSpace.CardSize * 0.02f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, endWidth));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 1f) },
                new[] { new GradientAlphaKey(head.a, 0f), new GradientAlphaKey(head.a * 0.6f, 0.35f), new GradientAlphaKey(tail.a, 1f) });
            trail.colorGradient = gradient;
            trail.numCapVertices = 3;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sortingLayerID = VfxSpace.ResolveSortingLayer(sortingLayer);
            trail.sortingOrder = order;
            return trail;
        }

        /// <summary>물체가 사라져도 궤적은 제 수명만큼 남아 사그라들게 떼어 놓는다.</summary>
        public static void ReleaseTrail(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            Object.Destroy(trail.gameObject, trail.time + 0.1f);
        }

        private static Material TrailAdditive
        {
            get
            {
                if (_trailAdditive != null) return _trailAdditive;
                _trailAdditive = new Material(VfxSpace.Additive) { name = "Vfx_TrailAdditive", hideFlags = HideFlags.HideAndDontSave };
                if (_trailAdditive.HasProperty("_MainTex")) _trailAdditive.SetTexture("_MainTex", TrailTexture);
                return _trailAdditive;
            }
        }

        private static Material TrailAlpha
        {
            get
            {
                if (_trailAlpha != null) return _trailAlpha;
                _trailAlpha = new Material(VfxSpace.Alpha) { name = "Vfx_TrailAlpha", hideFlags = HideFlags.HideAndDontSave };
                _trailAlpha.mainTexture = TrailTexture;
                return _trailAlpha;
            }
        }

        /// <summary>띠의 단면 — 가운데가 짙고 양쪽 가장자리로 부드럽게 사라진다.</summary>
        private static Texture2D TrailTexture
        {
            get
            {
                if (_trailTexture != null) return _trailTexture;
                const int w = 8, h = 32;
                _trailTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "Vfx_TrailSection",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - v), 1.6f);
                    for (int x = 0; x < w; x++) pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
                _trailTexture.SetPixels(pixels);
                _trailTexture.Apply(false, true);
                return _trailTexture;
            }
        }
    }
}
