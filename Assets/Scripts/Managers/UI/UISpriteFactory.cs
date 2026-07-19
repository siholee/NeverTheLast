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

        /// <summary>
        /// 공용 색상 팔레트. 사건(VN) 화면 기준으로 블루 아카이브풍(밝은 흰색 + 하늘색 포인트)을 따른다.
        /// </summary>
        public static class Palette
        {
            // 하늘색 포인트 컬러
            public static readonly Color Accent = new(0.247f, 0.663f, 0.961f, 1f);        // #3FA9F5
            public static readonly Color AccentDeep = new(0.106f, 0.498f, 0.831f, 1f);    // #1B7FD4
            public static readonly Color AccentSoft = new(0.247f, 0.663f, 0.961f, 0.28f);

            // 하단 스크림(그라데이션) — 위는 투명, 아래는 짙은 남색
            public static readonly Color ScrimTop = new(0.039f, 0.055f, 0.086f, 0f);
            public static readonly Color ScrimBottom = new(0.039f, 0.055f, 0.086f, 0.90f);

            // 배경 암전(사건 진입 시 전투 화면을 눌러주는 용도)
            public static readonly Color Backdrop = new(0.043f, 0.059f, 0.090f, 0.72f);

            // 밝은 카드(선택지) — 흰 배경 + 남색 텍스트가 블루 아카이브 특징
            public static readonly Color CardFill = new(1f, 1f, 1f, 0.95f);
            public static readonly Color CardBorder = new(0.247f, 0.663f, 0.961f, 0.9f);
            public static readonly Color CardText = new(0.133f, 0.188f, 0.247f, 1f);      // #22303F
            public static readonly Color CardTextMuted = new(0.376f, 0.447f, 0.522f, 1f);

            // 다크 필(Auto/Skip 등 보조 버튼)
            public static readonly Color PillFill = new(0.078f, 0.106f, 0.157f, 0.66f);
            public static readonly Color PillBorder = new(1f, 1f, 1f, 0.35f);

            // 본문 텍스트는 장면 위에 바로 얹히므로 흰색 + 그림자로 가독성을 확보한다.
            public static readonly Color TextPrimary = new(1f, 1f, 1f, 1f);
            public static readonly Color TextMuted = new(0.800f, 0.851f, 0.902f, 1f);

            // 구형 이름 호환(다른 화면에서 참조할 수 있으므로 유지)
            public static readonly Color PanelFill = new(0.074f, 0.090f, 0.118f, 0.95f);
            public static readonly Color PanelBorder = new(0.247f, 0.663f, 0.961f, 0.55f);
            public static readonly Color ChoiceFill = CardFill;
            public static readonly Color ChoiceBorder = CardBorder;
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

        /// <summary>
        /// 기울어진 평행사변형 스프라이트. 블루 아카이브 UI의 대표적인 형태로,
        /// 이름표·강조 바 등에 사용한다.
        /// 좌우 경사면을 9-슬라이스 가장자리에 두어 가로로 늘려도 기울기가 유지된다.
        /// </summary>
        /// <param name="height">텍스처 높이(px). 실제 표시 크기는 RectTransform이 정한다.</param>
        /// <param name="slant">위로 갈수록 오른쪽으로 밀리는 픽셀 수(기울기).</param>
        public static Sprite Parallelogram(int height, int slant, Color fill, Color border = default, int borderWidth = 0)
        {
            height = Mathf.Max(height, 4);
            slant = Mathf.Clamp(slant, 0, height);
            int width = slant * 2 + 6; // 좌우 경사면 + 늘어날 중앙부 최소 폭
            string key = $"pg_{width}_{height}_{slant}_{ColorKey(fill)}_{ColorKey(border)}_{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = key,
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                // 아래(0)에서 위(height-1)로 갈수록 경사만큼 오른쪽으로 이동
                float t = height <= 1 ? 0f : y / (float)(height - 1);
                float leftEdge = t * slant;
                float rightEdge = width - slant + t * slant;

                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    // 좌/우/상/하 경계까지의 거리 중 가장 바깥값(양수 = 도형 바깥)
                    float distance = Mathf.Max(
                        Mathf.Max(leftEdge - px, px - rightEdge),
                        Mathf.Max(0.5f - (y + 0.5f), (y + 0.5f) - (height - 0.5f)));

                    float outerAlpha = Mathf.Clamp01(0.5f - distance);
                    Color color;
                    if (borderWidth > 0)
                    {
                        float fillBlend = Mathf.Clamp01(0.5f - (distance + borderWidth));
                        color = Color.Lerp(border, fill, fillBlend);
                        color.a *= outerAlpha;
                    }
                    else
                    {
                        color = fill;
                        color.a *= outerAlpha;
                    }

                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // 좌우 경사면을 슬라이스 가장자리로 고정하고 중앙만 늘린다.
            var slice = new Vector4(slant + 2, 0f, slant + 2, 0f);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, slice);
            sprite.name = key;

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 세로 그라데이션 스프라이트(위 → 아래). 대사 영역 하단 스크림에 사용한다.
        /// 가로 1px이므로 Image.type = Simple로 가로 전체에 늘려 쓴다.
        /// </summary>
        public static Sprite VerticalGradient(int height, Color top, Color bottom)
        {
            height = Mathf.Max(height, 2);
            string key = $"vg_{height}_{ColorKey(top)}_{ColorKey(bottom)}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = key,
            };

            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                // y=0이 아래쪽이므로 bottom → top으로 보간한다.
                float t = y / (float)(height - 1);
                // 가장자리로 갈수록 부드럽게 사라지도록 감마를 준다.
                pixels[y] = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
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
