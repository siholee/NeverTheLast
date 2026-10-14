using System.Collections.Generic;
using UnityEngine;

namespace Managers.UI.Theme
{
    public enum UIIcon { Party, Shop, Supply, Rest, Menu, Guide, Close, Play, Arrow, Heart, Coin, Route, Check, Battle, Boss, Event, Elite, Swift, Burst, Precision, Support, Control, Shield, Infusion, Armor, Counter, Summon, Growth, Flame, Dot, Pause, Lexicon, FastForward, Flask }

    /// <summary>한 가지 획 폭을 사용하는 작은 기능 아이콘. 캐시된 스프라이트를 재사용한다.</summary>
    public static class UILineIcons
    {
        private static readonly Dictionary<(UIIcon, int), Sprite> Cache = new();
        public static Sprite Get(UIIcon icon) => Get(icon, 96);

        /// <summary>큰 로비 기호는 더 높은 해상도로 그려 확대 시 획이 흐려지지 않는다.</summary>
        public static Sprite Get(UIIcon icon, int resolution)
        {
            int size = Mathf.Clamp(resolution, 96, 256);
            var key = (icon, size);
            if (Cache.TryGetValue(key, out Sprite found) && found != null) return found;
            var paths = new List<Vector2[]>();
            void Path(params float[] xy)
            {
                var points = new Vector2[xy.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                paths.Add(points);
            }
            void Circle(float cx, float cy, float r)
            {
                var points = new Vector2[33];
                for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI / 16f; points[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); }
                paths.Add(points);
            }
            switch (icon)
            {
                case UIIcon.Party: Circle(12, 19, 4); Circle(23, 18, 3); Path(4,5,4,8,6,12,12,14,18,12,20,8,20,5); Path(22,12,27,10,28,7,28,5); break;
                case UIIcon.Shop: Path(5,18,5,5,27,5,27,18); Path(3,18,7,26,25,26,29,18,3,18); Path(12,5,12,13,20,13,20,5); Path(10,26,9,18); Path(22,26,23,18); break;
                case UIIcon.Supply: Path(5,8,5,24,27,24,27,8,5,8); Path(3,8,29,8,29,4,3,4,3,8); Path(12,24,12,17,20,17,20,24); break;
                case UIIcon.Rest: Path(25,26,19,26,13,23,10,18,10,12,13,7,19,4,25,4,20,8,18,13,18,18,20,23,25,26); Path(4,24,7,24); Path(5.5f,22.5f,5.5f,25.5f); break;
                case UIIcon.Menu: Path(5,24,27,24); Path(5,16,27,16); Path(5,8,27,8); Circle(12,24,2); Circle(22,16,2); Circle(10,8,2); break;
                case UIIcon.Lexicon: Path(7,4,25,4,25,28,7,28,7,4); Path(4,8,10,8); Path(4,16,10,16); Path(4,24,10,24); Path(14,21,21,21); Path(14,15,21,15); break;
                case UIIcon.Guide: Path(16,5,11,8,4,8,4,26,11,26,16,23,21,26,28,26,28,8,21,8,16,5,16,23); break;
                case UIIcon.Close: Path(8,8,24,24); Path(8,24,24,8); break;
                case UIIcon.FastForward: Path(5,8,13,16,5,24); Path(17,8,25,16,17,24); break;
                case UIIcon.Pause: Path(11,7,11,25); Path(21,7,21,25); break;
                case UIIcon.Play: Path(10,6,25,16,10,26,10,6); break;
                case UIIcon.Arrow: Path(5,16,27,16); Path(18,25,27,16,18,7); break;
                case UIIcon.Heart: Path(16,5,5,16,4,21,7,26,12,27,16,23,20,27,25,26,28,21,27,16,16,5); break;
                case UIIcon.Coin: Circle(16,16,11); Path(16,22,22,16,16,10,10,16,16,22); break;
                case UIIcon.Battle: Path(7,5,23,21,26,27,20,24,5,8); Path(4,12,12,4); Path(5,27,11,24,27,8); Path(20,4,28,12); break;
                case UIIcon.Boss: Path(5,9,3,24,11,18,16,27,21,18,29,24,27,9,5,9); Path(6,5,26,5); break;
                case UIIcon.Event: Path(9,5,26,5,26,24,9,24,6,21,6,9,9,5); Path(10,28,25,28); Path(12,18,21,18); Path(12,12,19,12); break;
                case UIIcon.Elite: Path(16,28,28,16,16,4,4,16,16,28); Path(16,23,23,16,16,9,9,16,16,23); break;
                case UIIcon.Swift: Path(4,22,15,22,20,16,15,10,4,10); Path(13,26,24,16,13,6); break;
                case UIIcon.Burst: Path(16,29,19,21,27,24,23,17,29,13,21,11,22,3,16,8,10,3,11,11,3,13,9,17,5,24,13,21,16,29); break;
                case UIIcon.Precision: Circle(16,16,9); Circle(16,16,3); Path(16,3,16,8); Path(16,24,16,29); Path(3,16,8,16); Path(24,16,29,16); break;
                case UIIcon.Support: Path(5,9,12,9,17,7,27,13,28,17,24,18,19,14,13,14); Path(5,5,5,17); Path(4,17,12,19,18,17,21,14); break;
                case UIIcon.Control: Path(4,22,10,28,16,22,10,16,4,22); Path(16,10,22,16,28,10,22,4,16,10); Path(10,22,22,10); break;
                case UIIcon.Shield: Path(16,28,27,24,26,13,22,7,16,3,10,7,6,13,5,24,16,28); Path(10,16,14,12,22,21); break;
                case UIIcon.Infusion: Path(16,29,25,16,26,11,23,6,16,3,9,6,6,11,7,16,16,29); Path(11,11,12,8,16,7); break;
                case UIIcon.Armor: Path(7,27,12,27,16,23,20,27,25,27,28,21,24,18,24,5,8,5,8,18,4,21,7,27); Path(13,16,19,16); break;
                case UIIcon.Counter: Path(26,10,26,19,22,24,8,24); Path(15,31,8,24,15,17); Path(5,8,19,8); break;
                case UIIcon.Summon: Circle(16,24,4); Path(12,19,10,10,22,10,20,19); Path(7,4,7,8,25,8,25,4,7,4); break;
                case UIIcon.Growth: Path(16,4,16,18); Path(16,13,10,13,5,18,4,25,10,25,15,21,16,13); Path(16,18,16,24,21,28,28,28,27,22,22,18,16,18); break;
                case UIIcon.Flame: Path(16,29,20,23,19,18,25,22,28,14,26,8,22,4,11,4,5,9,4,15,9,24,10,17,16,29); break;
                case UIIcon.Dot: Circle(16,16,11); Path(16,24,16,16,21,13); Circle(5,5,1); Circle(16,2,1); Circle(27,5,1); break;
                case UIIcon.Check: Path(6,16,13,9,26,24); break;
                case UIIcon.Flask: Path(12,28,20,28,20,19,26,9,25,5,7,5,6,9,12,19,12,28); Path(10,14,22,14); Path(11,28,21,28); break;
                default: Path(6,5,6,12,12,18,22,18,26,22,26,27); Circle(6,5,2); Circle(26,27,2); break;
            }
            float pixelScale = size / 32f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new((x + 0.5f) / pixelScale, (y + 0.5f) / pixelScale);
                float distance = float.MaxValue;
                foreach (Vector2[] path in paths) for (int i = 1; i < path.Length; i++)
                {
                    Vector2 a = path[i - 1], b = path[i], d = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(d.sqrMagnitude, 0.001f));
                    distance = Mathf.Min(distance, Vector2.Distance(p, a + d * t));
                }
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((0.95f - distance) * pixelScale));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UIIcon_" + icon, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), 100);
            sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite; return sprite;
        }
    }
}
