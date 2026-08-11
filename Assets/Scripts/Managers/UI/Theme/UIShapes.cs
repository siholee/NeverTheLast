using System.Collections.Generic;
using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>
    /// 아트 에셋 없이 UI 도형을 런타임에 생성한다.
    /// 만들어진 스프라이트는 9-슬라이스라 어떤 크기로 늘려도 모서리가 뭉개지지 않는다
    /// (반드시 Image.type = Sliced 로 쓸 것. <see cref="UIBuild"/>가 알아서 설정한다).
    ///
    /// 명일방주풍의 핵심은 "둥근 모서리가 아니라 잘라낸 모서리"다.
    /// 그래서 기본 도형은 <see cref="CutCorner"/>이고, 둥근 사각형은 보조로만 쓴다.
    /// 같은 파라미터 조합은 캐시해 재사용한다.
    /// </summary>
    public static class UIShapes
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>모서리를 대각선으로 잘라낸 사각형. 어느 모서리를 자를지 선택할 수 있다.</summary>
        public enum Corner
        {
            None = 0,
            TopLeft = 1,
            TopRight = 2,
            BottomRight = 4,
            BottomLeft = 8,
            All = TopLeft | TopRight | BottomRight | BottomLeft,
            /// <summary>명일방주에서 가장 흔한 조합: 좌상/우하만 잘라 흐름을 만든다.</summary>
            Diagonal = TopLeft | BottomRight,
        }

        /// <summary>
        /// 컷코너 사각형. 이 UI의 기본 패널 형태.
        /// </summary>
        /// <param name="cut">잘라낼 크기(px).</param>
        /// <param name="corners">자를 모서리 조합.</param>
        /// <param name="borderWidth">0보다 크면 테두리를 그린다.</param>
        public static Sprite CutCorner(int cut, Color fill, Corner corners = Corner.Diagonal,
            Color border = default, int borderWidth = 0)
        {
            cut = Mathf.Max(cut, 1);
            int size = cut * 2 + 4; // 9-슬라이스 중앙부 최소 2px 확보
            string key = $"cc_{size}_{cut}_{(int)corners}_{Key(fill)}_{Key(border)}_{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = CutCornerDistance(x + 0.5f, y + 0.5f, size, cut, corners);
                    pixels[y * size + x] = Shade(distance, fill, border, borderWidth);
                }
            }

            return Build(key, pixels, size, size, new Vector4(cut + 1, cut + 1, cut + 1, cut + 1));
        }

        /// <summary>둥근 모서리 사각형. 초상화 프레임처럼 부드러움이 필요한 곳에만 쓴다.</summary>
        public static Sprite RoundedRect(int radius, Color fill, Color border = default, int borderWidth = 0)
        {
            radius = Mathf.Max(radius, 1);
            int size = Mathf.Max(radius * 2 + 4, 8);
            string key = $"rr_{size}_{radius}_{Key(fill)}_{Key(border)}_{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedRectDistance(x + 0.5f, y + 0.5f, size, radius);
                    pixels[y * size + x] = Shade(distance, fill, border, borderWidth);
                }
            }

            return Build(key, pixels, size, size, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        /// <summary>테두리만 있는 컷코너 사각형(속이 빈 프레임).</summary>
        public static Sprite CutCornerOutline(int cut, Color border, int borderWidth = 2,
            Corner corners = Corner.Diagonal)
        {
            return CutCorner(cut, new Color(border.r, border.g, border.b, 0f), corners, border, borderWidth);
        }

        /// <summary>
        /// 기울어진 평행사변형. 라운드 표시나 태그 배지처럼 속도감을 주고 싶은 곳에 쓴다.
        /// 좌우 경사면을 9-슬라이스 가장자리에 두어 가로로 늘려도 기울기가 유지된다.
        /// </summary>
        public static Sprite Parallelogram(int height, int slant, Color fill,
            Color border = default, int borderWidth = 0)
        {
            height = Mathf.Max(height, 4);
            slant = Mathf.Clamp(slant, 0, height);
            int width = slant * 2 + 6;
            string key = $"pg_{width}_{height}_{slant}_{Key(fill)}_{Key(border)}_{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = height <= 1 ? 0f : y / (float)(height - 1);
                float leftEdge = t * slant;
                float rightEdge = width - slant + t * slant;

                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float distance = Mathf.Max(
                        Mathf.Max(leftEdge - px, px - rightEdge),
                        Mathf.Max(0.5f - (y + 0.5f), (y + 0.5f) - (height - 0.5f)));
                    pixels[y * width + x] = Shade(distance, fill, border, borderWidth);
                }
            }

            return Build(key, pixels, width, height, new Vector4(slant + 2, 0f, slant + 2, 0f));
        }

        /// <summary>
        /// 원판. Image.type = Filled + fillMethod = Radial360 과 조합해 원형 프로그레스로 쓴다.
        /// </summary>
        public static Sprite Disc(int diameter, Color fill, float innerRatio = 0f)
        {
            diameter = Mathf.Max(diameter, 8);
            string key = $"dc_{diameter}_{Key(fill)}_{innerRatio:F2}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            float outer = diameter * 0.5f;
            float inner = outer * Mathf.Clamp01(innerRatio);
            var pixels = new Color[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - outer;
                    float dy = y + 0.5f - outer;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // 바깥 경계와 안쪽 구멍 경계 모두에서 1px 안티에일리어싱
                    float alpha = Mathf.Clamp01(outer - r) * (inner > 0f ? Mathf.Clamp01(r - inner) : 1f);
                    Color color = fill;
                    color.a *= alpha;
                    pixels[y * diameter + x] = color;
                }
            }

            // 원은 늘리면 찌그러지므로 9-슬라이스를 쓰지 않는다.
            return Build(key, pixels, diameter, diameter, Vector4.zero);
        }

        /// <summary>세로 그라데이션(위 → 아래). 스크림이나 패널 페이드에 쓴다.</summary>
        public static Sprite VerticalGradient(int height, Color top, Color bottom)
        {
            height = Mathf.Max(height, 2);
            string key = $"vg_{height}_{Key(top)}_{Key(bottom)}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                pixels[y] = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
            }

            return Build(key, pixels, 1, height, Vector4.zero);
        }

        /// <summary>
        /// 사선 빗금 타일. 명일방주가 "비활성/공사중/위험" 영역에 즐겨 쓰는 패턴.
        /// Image.type = Tiled 로 쓴다.
        /// </summary>
        public static Sprite DiagonalStripes(int period, Color stripe, Color gap, float thickness = 0.5f)
        {
            period = Mathf.Max(period, 4);
            string key = $"ds_{period}_{Key(stripe)}_{Key(gap)}_{thickness:F2}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[period * period];
            for (int y = 0; y < period; y++)
            {
                for (int x = 0; x < period; x++)
                {
                    // (x + y) % period 로 45도 줄무늬를 만든다. 타일 경계가 이어지도록 period로 나눈다.
                    float phase = ((x + y) % period) / (float)period;
                    pixels[y * period + x] = phase < thickness ? stripe : gap;
                }
            }

            return Build(key, pixels, period, period, Vector4.zero);
        }

        /// <summary>1x1 단색. 배경·구분선처럼 도형이 필요 없을 때.</summary>
        public static Sprite Solid(Color fill)
        {
            string key = $"sl_{Key(fill)}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            return Build(key, new[] { fill }, 1, 1, Vector4.zero);
        }

        // ── 내부 구현 ────────────────────────────────────────────────

        /// <summary>부호 있는 거리 → 픽셀 색. 경계 1px에서 알파를 감쇠해 안티에일리어싱한다.</summary>
        private static Color Shade(float distance, Color fill, Color border, int borderWidth)
        {
            float outerAlpha = Mathf.Clamp01(0.5f - distance);
            Color color;
            if (borderWidth > 0)
            {
                // 바깥에서 borderWidth 안쪽까지는 테두리, 그보다 안쪽은 채움
                float fillBlend = Mathf.Clamp01(0.5f - (distance + borderWidth));
                color = Color.Lerp(border, fill, fillBlend);
                color.a = Mathf.Lerp(border.a, fill.a, fillBlend) * outerAlpha;
            }
            else
            {
                color = fill;
                color.a *= outerAlpha;
            }

            return color;
        }

        /// <summary>컷코너 사각형의 부호 있는 거리(양수 = 도형 바깥).</summary>
        private static float CutCornerDistance(float x, float y, int size, int cut, Corner corners)
        {
            // 사각형 네 변까지의 거리
            float distance = Mathf.Max(
                Mathf.Max(0.5f - x, x - (size - 0.5f)),
                Mathf.Max(0.5f - y, y - (size - 0.5f)));

            // 잘라낸 모서리는 45도 반평면으로 깎아낸다.
            // 텍스처 좌표는 y가 위로 증가한다(y=0이 아래).
            if (Has(corners, Corner.BottomLeft)) distance = Mathf.Max(distance, cut - x - y);
            if (Has(corners, Corner.BottomRight)) distance = Mathf.Max(distance, cut - (size - x) - y);
            if (Has(corners, Corner.TopLeft)) distance = Mathf.Max(distance, cut - x - (size - y));
            if (Has(corners, Corner.TopRight)) distance = Mathf.Max(distance, cut - (size - x) - (size - y));

            return distance;
        }

        private static bool Has(Corner value, Corner flag) => (value & flag) == flag;

        /// <summary>둥근 모서리 사각형의 부호 있는 거리(양수 = 도형 바깥).</summary>
        private static float RoundedRectDistance(float x, float y, int size, int radius)
        {
            float half = size * 0.5f;
            float dx = Mathf.Abs(x - half);
            float dy = Mathf.Abs(y - half);
            float outX = dx - (half - radius);
            float outY = dy - (half - radius);

            if (outX <= 0f && outY <= 0f)
            {
                return Mathf.Max(dx - half, dy - half);
            }

            outX = Mathf.Max(outX, 0f);
            outY = Mathf.Max(outY, 0f);
            return Mathf.Sqrt(outX * outX + outY * outY) - radius;
        }

        private static Sprite Build(string key, Color[] pixels, int width, int height, Vector4 slice)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = key,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, slice);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[key] = sprite;
            return sprite;
        }

        private static string Key(Color c) =>
            $"{(int)(c.r * 255)}-{(int)(c.g * 255)}-{(int)(c.b * 255)}-{(int)(c.a * 255)}";
    }
}
