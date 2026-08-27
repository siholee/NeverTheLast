using System.Collections.Generic;
using BaseClasses;
using CGT.Pooling;
using UnityEngine;

namespace Effects.Projectiles
{
    /// <summary>
    /// 원소별 투사체 외형.
    ///
    /// Hovl Studio <c>AAA Projectiles Vol 1</c>에서 원소에 맞는 프리팹을 고르고,
    /// 그 위에 <b>원신의 원소 색</b>을 입힌다. 프리팹만으로는 색이 원신과 어긋나므로
    /// 파티클의 <c>startColor</c>를 덮어써 색조를 맞춘다.
    ///
    /// 색은 파티클 시스템의 모듈 값이라 <b>공유 머티리얼을 건드리지 않는다.</b>
    /// 풀에서 재사용될 때마다 다시 칠하면 되므로 인스턴스가 새지도 않는다.
    /// </summary>
    public static class ElementalProjectiles
    {
        private const string ResourceRoot = "SFX/Projectile/";

        /// <summary>원소별 프리팹 이름. Resources/SFX/Projectile/ 아래에 있다.</summary>
        private static readonly Dictionary<BaseEnums.UnitElement, string> PrefabNames = new()
        {
            { BaseEnums.UnitElement.Pyro,    "Projectile 16 fire" },
            { BaseEnums.UnitElement.Hydro,   "Projectile 9 water" },
            { BaseEnums.UnitElement.Dendro,  "Projectile 1 nature arrow" },
            { BaseEnums.UnitElement.Anemo,   "Projectile 14 blue rapid" },
            { BaseEnums.UnitElement.Electro, "Projectile 2 electro" },
            { BaseEnums.UnitElement.Cryo,    "Projectile 26 blue diamond" },
            { BaseEnums.UnitElement.Geo,     "Projectile 4 yellow arrow" },
            { BaseEnums.UnitElement.Void,    "Projectile 17 nova violet" },
            { BaseEnums.UnitElement.None,    "Projectile 10 blue laser" },
        };

        /// <summary>원신 원소 색.</summary>
        private static readonly Dictionary<BaseEnums.UnitElement, Color> Colors = new()
        {
            { BaseEnums.UnitElement.Pyro,    new Color(1.000f, 0.400f, 0.200f) },   // #FF6633 불
            { BaseEnums.UnitElement.Hydro,   new Color(0.169f, 0.694f, 0.945f) },   // #2BB1F1 물
            { BaseEnums.UnitElement.Dendro,  new Color(0.588f, 0.796f, 0.176f) },   // #96CB2D 풀
            { BaseEnums.UnitElement.Anemo,   new Color(0.361f, 0.855f, 0.702f) },   // #5CDAB3 바람
            { BaseEnums.UnitElement.Electro, new Color(0.702f, 0.482f, 0.937f) },   // #B37BEF 번개
            { BaseEnums.UnitElement.Cryo,    new Color(0.600f, 0.898f, 0.965f) },   // #99E5F6 얼음
            { BaseEnums.UnitElement.Geo,     new Color(1.000f, 0.784f, 0.212f) },   // #FFC836 바위
            { BaseEnums.UnitElement.Void,    new Color(0.620f, 0.400f, 0.800f) },   // 공허
            { BaseEnums.UnitElement.None,    Color.white },
        };

        private static readonly Dictionary<BaseEnums.UnitElement, HS_Poolable> Cache = new();

        /// <summary>문자열 원소명을 enum으로 읽는다. 비어 있거나 모르는 값이면 None.</summary>
        public static BaseEnums.UnitElement Parse(string element)
        {
            return !string.IsNullOrWhiteSpace(element) &&
                   System.Enum.TryParse(element, true, out BaseEnums.UnitElement parsed)
                ? parsed
                : BaseEnums.UnitElement.None;
        }

        /// <summary>원소에 맞는 투사체 프리팹. 없으면 null.</summary>
        public static HS_Poolable PrefabFor(BaseEnums.UnitElement element)
        {
            if (Cache.TryGetValue(element, out HS_Poolable cached) && cached != null) return cached;

            if (!PrefabNames.TryGetValue(element, out string name)) return null;

            HS_Poolable prefab = Resources.Load<HS_Poolable>(ResourceRoot + name);
            if (prefab == null)
            {
                Debug.LogWarning($"[ElementalProjectiles] 투사체 프리팹을 찾지 못했습니다: {ResourceRoot}{name}");
                return null;
            }

            Cache[element] = prefab;
            return prefab;
        }

        /// <summary>원소 색.</summary>
        public static Color ColorFor(BaseEnums.UnitElement element)
            => Colors.TryGetValue(element, out Color color) ? color : Color.white;

        /// <summary>궁극기용 파스텔 팔레트. 원소 식별색은 유지하고 흰색을 섞어 채도를 낮춘다.</summary>
        public static Color PastelColorFor(BaseEnums.UnitElement element)
        {
            Color baseColor = ColorFor(element);
            Color pastel = Color.Lerp(baseColor, Color.white, 0.38f);
            pastel.a = baseColor.a;
            return pastel;
        }

        /// <summary>
        /// 투사체 인스턴스의 모든 파티클을 원소 색으로 칠한다.
        ///
        /// <c>startColor</c>만 바꾸므로 머티리얼은 그대로다. 색상 그라디언트를 쓰는 파티클도
        /// 그 위에 곱해지는 형태라 색조가 따라온다.
        /// </summary>
        // Hovl 셰이더가 실제로 쓰는 색 속성은 _StartColor / _EndColor다.
        // (AddTrail·Add_CenterGlow·Blend_CenterGlow 등에서 확인)
        // _TintColor·_Color·_EmissionColor는 다른 셰이더를 위한 보조다.
        private static readonly int[] ColorPropertyIds =
        {
            Shader.PropertyToID("_StartColor"),
            Shader.PropertyToID("_EndColor"),
            Shader.PropertyToID("_TintColor"),
            Shader.PropertyToID("_Color"),
            Shader.PropertyToID("_EmissionColor"),
        };

        [System.ThreadStatic] private static MaterialPropertyBlock _block;

        public static void Tint(GameObject projectile, BaseEnums.UnitElement element)
        {
            if (projectile == null) return;

            Color color = ColorFor(element);

            foreach (ParticleSystem particles in projectile.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                Color original = main.startColor.color;
                // 알파는 원본 연출을 따른다. 색조만 바꾼다.
                main.startColor = new Color(color.r, color.g, color.b, original.a);
            }

            // 눈에 보이는 몸통은 파티클 색이 아니라 머티리얼이 정한다.
            // MaterialPropertyBlock으로 덮어써야 공유 머티리얼을 건드리지 않고,
            // renderer.material처럼 인스턴스를 새로 만들어 새게 하지도 않는다.
            _block ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in projectile.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(_block);
                foreach (int propertyId in ColorPropertyIds) _block.SetColor(propertyId, color);
                renderer.SetPropertyBlock(_block);
            }

            foreach (Light light in projectile.GetComponentsInChildren<Light>(true))
            {
                light.color = color;
            }
        }
    }
}
