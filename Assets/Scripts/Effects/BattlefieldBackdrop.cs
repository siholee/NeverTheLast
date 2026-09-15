using Effects.Projectiles;
using Managers;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// 전장 뒤에 테마 환경광을 깐다.
    ///
    /// 완전한 검정 배경은 카드 대비에는 가장 유리하지만, <b>어디서 싸우는지</b>가 사라진다.
    /// 로마 군단도 공허 얼음도 같은 검은 방에서 싸우는 것처럼 보인다.
    ///
    /// 그래서 명도를 아주 낮게 유지한 채 세 겹만 깐다.
    ///   · 하늘 — 위는 거의 검고 아래로 갈수록 테마 색이 밴다
    ///   · 바닥 — 사선 빗금이 아주 옅게 깔려 평면이 바닥으로 읽힌다
    ///   · 광원 — 전장 가운데에 넓고 부드러운 빛 웅덩이
    ///
    /// 세 겹 모두 카드보다 훨씬 뒤에 그린다. 적 실루엣은 아래쪽이 밝아진 만큼 또렷해진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlefieldBackdrop : MonoBehaviour
    {
        /// <summary>카드(최대 40 남짓)보다 한참 뒤. 전장의 어떤 것보다 먼저 그려진다.</summary>
        private const int BackdropSortingOrder = -900;

        /// <summary>환경광이 실제로 화면에 얹히는 최대 세기. 넘기면 카드 대비가 무너진다.</summary>
        private const float MaxAmbientAlpha = 0.30f;

        private static BattlefieldBackdrop _instance;

        private SpriteRenderer _sky;
        private SpriteRenderer _floor;
        private SpriteRenderer _pool;
        private Camera _camera;
        private int _appliedThemeId = int.MinValue;

        /// <summary>
        /// 카메라에 배경을 붙인다(이미 있으면 그대로 쓴다). 카메라 자식이라 프레이밍을
        /// 다시 잡아도 배경이 따라온다 — 전투 중 빈 칸이 지워질 때마다 카메라가 움직인다.
        /// </summary>
        public static void Ensure()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            if (_instance != null && _instance.gameObject == camera.gameObject) return;
            if (_instance != null) Destroy(_instance);

            _instance = camera.gameObject.GetComponent<BattlefieldBackdrop>()
                        ?? camera.gameObject.AddComponent<BattlefieldBackdrop>();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Build();
        }

        private void Build()
        {
            _sky = NewLayer("BackdropSky", VerticalFade(), BackdropSortingOrder);
            _floor = NewLayer("BackdropFloor", Hatch(), BackdropSortingOrder + 1);
            _pool = NewLayer("BackdropPool", ProjectileShapes.Core(256), BackdropSortingOrder + 2);
        }

        // ── 배경 전용 도형 ───────────────────────────────────────────
        // 투사체 도형과 쓰임이 달라 여기서 만든다. 둘 다 한 번 만들어 두고 계속 쓴다.

        private static Sprite _fade;
        private static Sprite _hatch;

        /// <summary>아래로 갈수록 진해지는 세로 그라데이션. 가로로 늘려 쓴다.</summary>
        private static Sprite VerticalFade()
        {
            if (_fade != null) return _fade;

            const int height = 64;
            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                // y = 0이 아래다. 바닥에서 가장 진하고 위로 갈수록 사라진다.
                float t = 1f - y / (float)(height - 1);
                pixels[y] = new Color(1f, 1f, 1f, Mathf.Pow(t, 1.4f));
            }

            _fade = Build("backdrop_fade", pixels, 1, height);
            return _fade;
        }

        /// <summary>아주 옅은 사선 빗금. 평면이 '바닥'으로 읽히게 하는 결이다.</summary>
        private static Sprite Hatch()
        {
            if (_hatch != null) return _hatch;

            const int width = 256;
            const int height = 128;
            const int period = 24;

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                // 위로 갈수록 빗금이 사라진다. 끝까지 살아 있으면 벽지처럼 보인다.
                float depth = Mathf.Pow(1f - y / (float)(height - 1), 1.6f);
                for (int x = 0; x < width; x++)
                {
                    int phase = (x + y) % period;
                    float line = phase < 2 ? 1f - phase * 0.5f : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, line * depth);
                }
            }

            _hatch = Build("backdrop_hatch", pixels, width, height);
            return _hatch;
        }

        private static Sprite Build(string name, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private SpriteRenderer NewLayer(string label, Sprite sprite, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            // 가장 아래 정렬 레이어에 둔다. 카드가 어떤 레이어를 쓰든 그보다 뒤에 그려야 한다 —
            // sortingOrder만 낮춰 두면 레이어가 다를 때 배경이 카드를 덮는다.
            renderer.sortingLayerID = SortingLayer.layers[0].id;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.orthographic) return;

            RefreshTheme();

            // 화면을 덮을 만큼만 늘린다. 카메라 자식이라 로컬 좌표로 계산하면 된다.
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            // z는 카드(월드 z = 0)보다 뒤다. 카메라가 z = -10에 서므로 로컬 20이 월드 10이다.
            Stretch(_sky, halfWidth * 2f, halfHeight * 2f, 0f, 20f);
            // 바닥은 아래 절반만 차지한다. 위까지 깔면 빗금이 하늘에 뜬다.
            Stretch(_floor, halfWidth * 2f, halfHeight * 1.15f, -halfHeight * 0.42f, 19.9f);
            Stretch(_pool, halfWidth * 1.9f, halfHeight * 1.5f, -halfHeight * 0.12f, 19.8f);
        }

        private void Stretch(SpriteRenderer renderer, float width, float height, float y, float z)
        {
            if (renderer == null || renderer.sprite == null) return;

            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, size.x), height / Mathf.Max(0.0001f, size.y), 1f);
            renderer.transform.localPosition = new Vector3(0f, y, z);
        }

        /// <summary>테마가 바뀌었을 때만 다시 칠한다. 매 프레임 색을 만들 필요가 없다.</summary>
        private void RefreshTheme()
        {
            RoundManager rounds = GameManager.Instance?.RoundManager;
            int themeId = rounds?.CurrentThemeId ?? 0;
            if (themeId == _appliedThemeId) return;

            _appliedThemeId = themeId;
            Color ambient = rounds?.CurrentThemeAmbient ?? new Color(0.13f, 0.15f, 0.20f);

            // 하늘은 아래가 진하다. VerticalFade가 아래로 갈수록 알파를 올려 둔다.
            _sky.color = new Color(ambient.r, ambient.g, ambient.b, MaxAmbientAlpha);
            _floor.color = new Color(ambient.r, ambient.g, ambient.b, 0.13f);
            // 웅덩이는 색을 한 번 더 밝혀 빛으로 읽히게 한다.
            Color glow = Color.Lerp(ambient, Color.white, 0.25f);
            _pool.color = new Color(glow.r, glow.g, glow.b, 0.18f);
        }
    }
}
