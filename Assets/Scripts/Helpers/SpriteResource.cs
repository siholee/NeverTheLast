using System;
using System.Collections.Generic;
using UnityEngine;

namespace Helpers
{
    /// <summary>
    /// 데이터에는 <b>스프라이트 키만</b> 두고(예: <c>SEI_PORTRAIT</c>) 실제 Resources 폴더는 여기서 찾는다.
    ///
    /// 스프라이트를 아군/적 분류 폴더로 옮긴 뒤로, 키를 그대로 <c>Resources.Load</c>에 넘기면
    /// 아무것도 나오지 않는다. <b>초상화·스탠딩을 읽는 곳은 반드시 이 클래스를 거친다.</b>
    /// 이미 해석이 끝난 전체 경로(<c>Sprite/...</c>)를 넘겨도 그대로 통과하므로,
    /// 키를 들고 있든 경로를 들고 있든 같은 진입점을 쓸 수 있다.
    ///
    /// 분류되지 않은 개발용 에셋을 위해 옛 평면 경로도 계속 뒤지되 <b>맨 뒤</b>에 둔다.
    /// 실제 자산이 전부 분류 폴더에 있으므로 앞에 두면 조회마다 헛걸음이 한 번 더 붙는다.
    /// </summary>
    public static class SpriteResource
    {
        private static readonly string[] PortraitRoots =
        {
            "Sprite/Portraits/Allies/",
            "Sprite/Portraits/Enemies/Normal/",
            "Sprite/Portraits/Enemies/Elite/",
            "Sprite/Portraits/Enemies/Boss/",
            "Sprite/Portraits/",
        };

        private static readonly string[] StandingRoots =
        {
            "Sprite/Standings/Allies/",
            "Sprite/Standings/Enemies/Normal/",
            "Sprite/Standings/Enemies/Elite/",
            "Sprite/Standings/Enemies/Boss/",
            "Sprite/Standings/",
        };

        private static readonly Dictionary<string, string> PortraitPathCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> StandingPathCache =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public static Sprite LoadPortrait(string key)
            => Load(key, PortraitRoots, PortraitPathCache, out _);

        public static Sprite LoadPortrait(string key, out string resourcePath)
            => Load(key, PortraitRoots, PortraitPathCache, out resourcePath);

        public static Sprite LoadStanding(string key)
            => Load(key, StandingRoots, StandingPathCache, out _);

        public static Sprite LoadStanding(string key, out string resourcePath)
            => Load(key, StandingRoots, StandingPathCache, out resourcePath);

        private static Sprite Load(
            string key,
            IReadOnlyList<string> roots,
            IDictionary<string, string> cache,
            out string resourcePath)
        {
            resourcePath = null;
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
                return null;

            if (normalized.StartsWith("Sprite/", StringComparison.Ordinal))
            {
                resourcePath = normalized;
                return Resources.Load<Sprite>(resourcePath);
            }

            if (cache.TryGetValue(normalized, out string cachedPath))
            {
                Sprite cached = Resources.Load<Sprite>(cachedPath);
                if (cached != null)
                {
                    resourcePath = cachedPath;
                    return cached;
                }

                cache.Remove(normalized);
            }

            for (int i = 0; i < roots.Count; i++)
            {
                string candidate = roots[i] + normalized;
                Sprite sprite = Resources.Load<Sprite>(candidate);
                if (sprite == null)
                    continue;

                cache[normalized] = candidate;
                resourcePath = candidate;
                return sprite;
            }

            // 못 찾았으면 키를 그대로 돌려준다. 예전에는 옛 평면 경로를 붙여 돌려줬는데,
            // 그 경로에 자산이 없는 지금은 로그에 존재하지 않는 위치가 찍혀 원인을 가렸다.
            resourcePath = normalized;
            return null;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            string normalized = key.Trim().Replace('\\', '/').TrimStart('/');
            if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);
            return normalized;
        }
    }
}
