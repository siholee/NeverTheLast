using System.Collections.Generic;
using UnityEngine;

namespace Managers.UI
{
    /// <summary>
    /// 런타임에 UI용 스프라이트를 생성한다. 아트 에셋 없이도 둥근 모서리 패널을 쓰기 위한 도구.
    /// 생성한 스프라이트는 9-슬라이스로 만들어 어떤 크기로 늘려도 모서리가 뭉개지지 않는다.
    /// (Image.type = Sliced 로 사용할 것)
    /// 같은 파라미터 조합은 캐시해서 재사용한다.
    /// </summary>
    public static class UISpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>공용 색상 팔레트. 사건(VN) 화면과 이후 UI 화면에서 함께 사용한다.</summary>
        public static class Palette
        {
            public static readonly Color Backdrop = new(0.035f, 0.042f, 0.055f, 0.94f);
            public static readonly Color PanelFill = new(0.074f, 0.090f, 0.118f, 0.97f);
            public static readonly Color PanelBorder = new(0.180f, 0.227f, 0.302f, 1f);
            public static readonly Color Accent = new(0.788f, 0.635f, 0.153f, 1f);       // 금색 (신화 테마)
            public static readonly Color AccentSoft = new(0.788f, 0.635f, 0.153f, 0.35f);
            public static readonly Color ChoiceFill = new(0.102f, 0.129f, 0.188f, 0.98f);
            public static readonly Color ChoiceBorder = new(0.200f, 0.251f, 0.353f, 1f);
            public static readonly Color TextPrimary = new(0.929f, 0.937f, 0.949f, 1f);
            public static readonly Color TextMuted = new(0.612f, 0.659f, 0.722f, 1f);
        }

        /// <summary>
        /// 둥근 모서리 사각형 스프라이트. borderWidth가 0보다 크면 테두리를 그린다.
        /// </summary>
        /// <param name="radius">모서리 반지름(px). 텍스처 크기의 절반 이하를 권장.</param>
        /// <param name="borderWidth">테두리 두께(px). 0이면 채움만.</param>
        public static Sprite RoundedRect(int radius, Color fill, Color border = default, int borderWidth = 0)
        {
            // 9-슬라이스가 늘어나는 중앙부를 최소 2px 확보하기 위해 여유를 둔다.
            int size = Mathf.Max(radius * 2 + 4, 8);
            string key = $"rr_{size}_{radius}_{ColorKey(fill)}_{ColorKey(border)}_{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = key,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 픽셀 중심 기준으로 "모서리가 둥근 사각형" 바깥면까지의 부호 있는 거리
                    float distance = RoundedRectSignedDistance(x + 0.5f, y + 0.5f, size, radius);

                    // 안티에일리어싱: 경계 1px 구간에서 알파를 부드럽게 감쇠
                    float outerAlpha = Mathf.Clamp01(0.5f - distance);
                    Color color;
                    if (borderWidth > 0)
                    {
                        // 바깥에서 borderWidth 안쪽까지는 테두리, 그보다 안쪽은 채움
                        float fillBlend = Mathf.Clamp01(0.5f - (distance + borderWidth));
                        color = Color.Lerp(border, fill, fillBlend);
                        color.a *= outerAlpha;
                    }
                    else
                    {
                        color = fill;
                        color.a *= outerAlpha;
                    }

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // border 파라미터가 9-슬라이스 경계를 정한다(좌/하/우/상).
            var slice = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                slice);
            sprite.name = key;

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>모서리가 둥근 사각형의 부호 있는 거리(양수 = 도형 바깥).</summary>
        private static float RoundedRectSignedDistance(float x, float y, int size, int radius)
        {
            float half = size * 0.5f;
            // 중심 기준 좌표의 절댓값
            float dx = Mathf.Abs(x - half);
            float dy = Mathf.Abs(y - half);

            // 모서리 원의 중심까지 남은 거리
            float cornerX = half - radius;
            float cornerY = half - radius;

            float outX = dx - cornerX;
            float outY = dy - cornerY;

            if (outX <= 0f && outY <= 0f)
            {
                // 십자(비-모서리) 영역: 가장 가까운 변까지의 거리
                return Mathf.Max(dx - half, dy - half);
            }

            outX = Mathf.Max(outX, 0f);
            outY = Mathf.Max(outY, 0f);
            return Mathf.Sqrt(outX * outX + outY * outY) - radius;
        }

        private static string ColorKey(Color color)
        {
            return $"{(int)(color.r * 255)}-{(int)(color.g * 255)}-{(int)(color.b * 255)}-{(int)(color.a * 255)}";
        }
    }
}
