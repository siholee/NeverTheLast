using System;
using System.Collections.Generic;
using UnityEngine;

namespace Entities.View
{
    /// <summary>
    /// 카드 연출용 텍스처를 코드로 그린다. 지금은 파괴 연출의 <b>금 간 자국</b> 하나다.
    ///
    /// 2026-09 개편 때 이 클래스가 하스스톤식 카드(판 · 창 · 금테 · 보석 · 리본)를 그렸으나,
    /// 카드 외형을 옛 디자인(자유롭게 선 원화 + 옅은 컷코너 테두리)으로 되돌리며 그 부분을 걷어냈다.
    /// </summary>
    public static class CardArt
    {
        private const int TexWidth = 256;
        private const int TexHeight = 256;

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>
        /// 금 간 자국. 가운데에서 뻗는 들쭉날쭉한 선 여러 갈래다. 파괴 연출이 카드 위에 덮는다.
        /// </summary>
        public static Sprite Cracks(int variant)
        {
            variant = Mathf.Abs(variant) % 3;
            string key = "cracks_" + variant;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            int w = TexWidth, h = TexHeight;
            var alpha = new float[w * h];
            var rng = new System.Random(7919 * (variant + 1));
            int branches = 7 + variant;
            for (int b = 0; b < branches; b++)
            {
                float angle = (float)(b / (double)branches * Math.PI * 2 + rng.NextDouble() * 0.6);
                float x = w * 0.5f + (float)(rng.NextDouble() - 0.5) * w * 0.12f;
                float y = h * 0.52f + (float)(rng.NextDouble() - 0.5) * h * 0.12f;
                float thickness = 2.6f;
                for (int step = 0; step < 90; step++)
                {
                    angle += (float)(rng.NextDouble() - 0.5) * 0.7f;
                    x += Mathf.Cos(angle) * 2.2f;
                    y += Mathf.Sin(angle) * 2.2f;
                    if (x < 0 || y < 0 || x >= w || y >= h) break;
                    Stamp(alpha, w, h, x, y, thickness);
                    thickness = Mathf.Max(0.8f, thickness - 0.022f);
                    // 가지를 친다.
                    if (rng.NextDouble() < 0.035)
                    {
                        float ba = angle + (rng.NextDouble() < 0.5 ? -0.9f : 0.9f);
                        float bx = x, by = y;
                        for (int s = 0; s < 22; s++)
                        {
                            ba += (float)(rng.NextDouble() - 0.5) * 0.6f;
                            bx += Mathf.Cos(ba) * 2f;
                            by += Mathf.Sin(ba) * 2f;
                            if (bx < 0 || by < 0 || bx >= w || by >= h) break;
                            Stamp(alpha, w, h, bx, by, 1f);
                        }
                    }
                }
            }

            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Mathf.Clamp01(alpha[i]);
                pixels[i] = new Color(0.06f, 0.03f, 0.02f, a);
            }
            return Store(key, pixels, w, h);
        }

        // ── 그리기 도구 ──────────────────────────────────────────────

        private static void Stamp(float[] alpha, int w, int h, float cx, float cy, float radius)
        {
            int r = Mathf.CeilToInt(radius + 1f);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                int i = y * w + x;
                alpha[i] = Mathf.Max(alpha[i], a);
            }
        }

        private static Sprite Store(string key, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "CardArt_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);   // 파괴 연출이 조각을 떠야 하므로 읽기 가능으로 남긴다

            // 세로 한 변이 월드 1이 되게 한다. 몸체 크기는 트랜스폼 배율로 맞춘다.
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), height);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
