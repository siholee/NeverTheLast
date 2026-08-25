using System.Collections.Generic;
using UnityEngine;

namespace Effects.Projectiles
{
    /// <summary>
    /// 투사체용 도형을 코드로 만든다. 아트 에셋을 쓰지 않는다.
    ///
    /// 파티클 스프레이 대신 <b>또렷한 실루엣 하나</b>를 쓰기 위한 것이다.
    /// 파티클이 많으면 화려해 보이지만 형태가 뭉개져 값싸 보인다 —
    /// 명일방주 계열은 반대로 단순한 도형을 짧고 세게 쓴다.
    ///
    /// 만들어진 스프라이트는 흰색이다. 색은 SpriteRenderer의 tint로 입힌다.
    /// 같은 파라미터는 캐시해 재사용하므로 텍스처가 계속 늘어나지 않는다.
    /// </summary>
    public static class ProjectileShapes
    {
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>
        /// 날이 선 마름모. 진행 방향으로 길쭉하다.
        /// </summary>
        /// <param name="length">긴 축(px).</param>
        /// <param name="width">짧은 축(px).</param>
        public static Sprite Sliver(int length = 150, int width = 20)
        {
            length = Mathf.Max(length, 8);
            width = Mathf.Max(width, 4);

            string key = $"sv_{length}_{width}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[length * width];
            float halfWidth = width * 0.5f;

            for (int y = 0; y < width; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    // 양 끝이 바늘처럼 모이고 앞쪽 3분의 1 지점이 가장 두껍다.
                    // 두께가 얇을수록(150:20) 실루엣이 또렷해진다 — 두꺼우면 물방울처럼 뭉툭해진다.
                    float along = (x + 0.5f) / length;
                    const float peak = 0.68f;
                    float taper = along < peak
                        ? along / peak
                        : (1f - along) / (1f - peak);
                    taper = Mathf.Pow(Mathf.Clamp01(taper), 0.65f);

                    float allowed = halfWidth * Mathf.Clamp01(taper);
                    float distance = Mathf.Abs(y + 0.5f - halfWidth);

                    // 가장자리 1px만 부드럽게 깎아 계단이 보이지 않게 한다.
                    float alpha = Mathf.Clamp01(allowed - distance);
                    pixels[y * length + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Build(key, pixels, length, width);
        }

        /// <summary>
        /// 뒤로 흐르는 잔광. 왼쪽이 투명하고 오른쪽으로 갈수록 진해진다.
        /// 진행 방향으로 늘려 쓴다.
        /// </summary>
        public static Sprite Streak(int length = 220, int width = 22)
        {
            length = Mathf.Max(length, 8);
            width = Mathf.Max(width, 2);

            string key = $"st_{length}_{width}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[length * width];
            float halfWidth = width * 0.5f;

            for (int y = 0; y < width; y++)
            {
                // 가운데가 가장 진하고 위아래로 갈수록 사라진다.
                float across = 1f - Mathf.Abs(y + 0.5f - halfWidth) / halfWidth;
                across = Mathf.Clamp01(across);
                across *= across;

                for (int x = 0; x < length; x++)
                {
                    float along = (x + 0.5f) / length;
                    // 꼬리는 천천히, 머리 쪽은 급하게 진해진다.
                    float alpha = across * along * along;
                    pixels[y * length + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Build(key, pixels, length, width);
        }

        /// <summary>가운데가 밝은 둥근 점. 착탄 순간의 핵.</summary>
        public static Sprite Core(int diameter = 64)
        {
            diameter = Mathf.Max(diameter, 8);

            string key = $"co_{diameter}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[diameter * diameter];
            float radius = diameter * 0.5f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                    // 가장자리로 갈수록 급격히 사라져 흐릿한 원반이 되지 않게 한다.
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha * alpha;
                    pixels[y * diameter + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Build(key, pixels, diameter, diameter);
        }

        /// <summary>가느다란 직선 조각. 착탄에서 사방으로 뻗는 파편에 쓴다.</summary>
        public static Sprite Shard(int length = 64, int width = 6)
        {
            length = Mathf.Max(length, 4);
            width = Mathf.Max(width, 2);

            string key = $"sh_{length}_{width}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var pixels = new Color[length * width];
            float halfWidth = width * 0.5f;

            for (int y = 0; y < width; y++)
            {
                float across = Mathf.Clamp01(halfWidth - Mathf.Abs(y + 0.5f - halfWidth));

                for (int x = 0; x < length; x++)
                {
                    // 바깥쪽 끝이 가늘어지며 사라진다.
                    float along = 1f - (x + 0.5f) / length;
                    pixels[y * length + x] = new Color(1f, 1f, 1f, across * along);
                }
            }

            return Build(key, pixels, length, width);
        }

        private static Sprite Build(string key, Color[] pixels, int width, int height)
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
                texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[key] = sprite;
            return sprite;
        }
    }
}
