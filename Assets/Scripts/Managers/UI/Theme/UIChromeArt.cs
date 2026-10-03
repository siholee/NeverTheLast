using System.Collections.Generic;
using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>
    /// 생성 원화 기반의 화면 크롬을 임포터 설정과 무관하게 읽는다.
    /// Texture2D를 직접 불러 Sprite를 만들면 새 PNG의 .meta가 아직 없어도 Unity가 기본 텍스처로
    /// 임포트한 뒤 동일한 경계값을 쓸 수 있다.
    /// </summary>
    public static class UIChromeArt
    {
        private const string Root = "UI/Chrome/";
        private static readonly Dictionary<string, Sprite> Cache = new();

        public const string DefaultBoardEnvironmentKey = "UI/Chrome/board_environment_bright_1672x941";

        public static Sprite BoardEnvironment => LoadResource(DefaultBoardEnvironmentKey);
        public static Sprite BoardFloor => Load("board_floor_bright_1672x941");
        public static Sprite CenterDivider => Load("center_divider_bright_2172x724");
        public static Sprite MainMenuBackground => Load("main_menu_sky_citadel_1672x941");

        /// <summary>
        /// 테마 데이터의 Resources 상대 키를 읽는다. 이전처럼 파일명만 준 키도 UI/Chrome 아래에서 찾는다.
        /// </summary>
        public static Sprite LoadBackdrop(string resourceKey)
        {
            string normalized = NormalizeResourceKey(resourceKey);
            Sprite sprite = LoadResource(normalized);
            return sprite != null ? sprite : BoardEnvironment;
        }

        private static Sprite Load(string resource, Vector4 border = default)
            => LoadResource(Root + resource, border);

        private static Sprite LoadResource(string resource, Vector4 border = default)
        {
            resource = NormalizeResourceKey(resource);
            string key = $"{resource}:{border}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return null;

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
            sprite.name = resource;
            Cache[key] = sprite;
            return sprite;
        }

        private static string NormalizeResourceKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DefaultBoardEnvironmentKey;
            string key = value.Trim().Replace('\\', '/');
            if (key.StartsWith("Resources/")) key = key.Substring("Resources/".Length);
            int extension = key.LastIndexOf('.');
            if (extension > key.LastIndexOf('/')) key = key.Substring(0, extension);
            return key.Contains("/") ? key : Root + key;
        }
    }
}
