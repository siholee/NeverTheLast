using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>Hovl의 발사광 · 비행체 · 착탄 프리팹 한 벌과 카드 전장용 보정값.</summary>
    public sealed class HovlProjectileSet
    {
        public string FlashPath;
        public string ProjectilePath;
        public string HitPath;
        public Color Tint = Color.white;
        public float Alpha = 0.9f;
        public float FlashScale = 0.30f;
        public float ProjectileScale = 0.34f;
        public float HitScale = 0.44f;
        public float RotationOffset;
        public Color UnderlayColor = new(0.10f, 0.23f, 0.34f, 0.16f);
        public float UnderlaySize = 0.19f;
        public float UnderlayAspect = 3.2f;

        public HovlProjectileSet Clone(float scale = 1f)
        {
            return new HovlProjectileSet
            {
                FlashPath = FlashPath,
                ProjectilePath = ProjectilePath,
                HitPath = HitPath,
                Tint = Tint,
                Alpha = Alpha,
                FlashScale = FlashScale * scale,
                ProjectileScale = ProjectileScale * scale,
                HitScale = HitScale * scale,
                RotationOffset = RotationOffset,
                UnderlayColor = UnderlayColor,
                UnderlaySize = UnderlaySize * scale,
                UnderlayAspect = UnderlayAspect,
            };
        }
    }

    /// <summary>
    /// Hovl Studio 프리팹을 카드 전장 좌표계에 맞춰 재생한다.
    /// 원본의 Rigidbody/충돌/이동 스크립트는 끄고, 위치와 적중 시점은 <see cref="ProjectileFx"/>가 소유한다.
    /// 인스턴스는 경로별 풀로 되돌려 7연사에서도 대형 프리팹을 매번 만들지 않는다.
    /// </summary>
    public static class HovlFx
    {
        private const string Root = "VfxPrefabs/Hovl/";

        public const string NatureFlash = Root + "Flash 1 nature arrow";
        public const string NatureProjectile = Root + "Projectile 1 nature arrow";
        public const string NatureHit = Root + "Hit 1 nature arrow";
        public const string ElectroFlash = Root + "Flash 2 electro";
        public const string ElectroProjectile = Root + "Projectile 2 electro";
        public const string ElectroHit = Root + "Hit 2 electro";
        public const string WaterFlash = Root + "Flash 9 water";
        public const string WaterProjectile = Root + "Projectile 9 water";
        public const string WaterHit = Root + "Hit 9 water";
        public const string RapidFlash = Root + "Flash 14 blue rapid";
        public const string RapidProjectile = Root + "Projectile 14 blue rapid";
        public const string RapidHit = Root + "Hit 14 blue rapid";
        public const string FireFlash = Root + "Flash 16 fire";
        public const string FireProjectile = Root + "Projectile 16 fire";
        public const string FireHit = Root + "Hit 16 fire";
        public const string VioletNovaFlash = Root + "Flash 17 nova violet";
        public const string VioletNovaProjectile = Root + "Projectile 17 nova violet";
        public const string VioletNovaHit = Root + "Hit 17 nova violet";
        public const string OrangeNovaFlash = Root + "Flash 18 nova orange";
        public const string OrangeNovaProjectile = Root + "Projectile 18 nova orange";
        public const string OrangeNovaHit = Root + "Hit 18 nova orange";
        public const string BlueDiamondFlash = Root + "Flash 26 blue crystal";
        public const string BlueDiamondProjectile = Root + "Projectile 26 blue diamond";
        public const string BlueDiamondHit = Root + "Hit 26 blue crystal";
        public const string OrangeExplosionHit = Root + "Hit 25 orange explosion";
        public const string DragonPunchFlash = Root + "Dragon punch flash";

        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly Dictionary<string, Stack<HovlPoolItem>> Pools = new();
        private static readonly HashSet<HovlPoolItem> ActiveItems = new();
        private static HovlPoolHost _host;

        /// <summary>기본 원소 투사체 매핑. 색만 바꾸지 않고 실루엣도 원소군에 맞는 Hovl 세트를 고른다.</summary>
        public static HovlProjectileSet ForElement(BaseEnums.UnitElement element, float scale = 1f)
        {
            Color tint = ElementPalette.ColorFor(element);
            HovlProjectileSet set = element switch
            {
                BaseEnums.UnitElement.Pyro => Set(FireFlash, FireProjectile, FireHit, tint, 0.92f),
                BaseEnums.UnitElement.Hydro => Set(WaterFlash, WaterProjectile, WaterHit, tint, 0.88f),
                BaseEnums.UnitElement.Electro => Set(ElectroFlash, ElectroProjectile, ElectroHit, tint, 0.92f),
                BaseEnums.UnitElement.Dendro => Set(NatureFlash, NatureProjectile, NatureHit, tint, 0.88f),
                BaseEnums.UnitElement.Anemo => Set(NatureFlash, NatureProjectile, NatureHit, tint, 0.82f),
                BaseEnums.UnitElement.Cryo => Set(BlueDiamondFlash, BlueDiamondProjectile, BlueDiamondHit, tint, 0.88f),
                // nova Flash의 billboard Shockwave는 2D 전장에서 점 입자로 풀어진다. 메시형 Dragon Punch Flash로
                // 발사 실루엣을 만들고, 비행체·착탄은 각 원소 nova 세트를 그대로 쓴다.
                BaseEnums.UnitElement.Geo => Set(DragonPunchFlash, OrangeNovaProjectile, OrangeNovaHit,
                    new Color(1f, 0.68f, 0.18f), 0.90f),
                BaseEnums.UnitElement.Void => Set(DragonPunchFlash, VioletNovaProjectile, VioletNovaHit,
                    new Color(0.62f, 0.24f, 0.88f), 0.90f),
                _ => Set(RapidFlash, RapidProjectile, RapidHit, new Color(0.56f, 0.74f, 0.88f), 0.78f),
            };

            // 포커스 딤이 착탄 순간의 명도 대비를 맡는다. 비가산 받침은 비행 방향을 잃지 않을 만큼만 남겨
            // 밝은 판에 갈색·보라 얼룩이 잔류하지 않게 한다.
            set.UnderlayColor = element switch
            {
                BaseEnums.UnitElement.Pyro => new Color(0.52f, 0.10f, 0.06f, 0.17f),
                BaseEnums.UnitElement.Geo => new Color(0.13f, 0.28f, 0.39f, 0.14f),
                BaseEnums.UnitElement.Void => new Color(0.20f, 0.055f, 0.36f, 0.18f),
                BaseEnums.UnitElement.Electro => new Color(0.16f, 0.10f, 0.46f, 0.17f),
                BaseEnums.UnitElement.Dendro => new Color(0.06f, 0.30f, 0.20f, 0.15f),
                BaseEnums.UnitElement.Anemo => new Color(0.04f, 0.31f, 0.35f, 0.14f),
                BaseEnums.UnitElement.Hydro => new Color(0.05f, 0.22f, 0.48f, 0.15f),
                BaseEnums.UnitElement.Cryo => new Color(0.09f, 0.28f, 0.45f, 0.14f),
                _ => new Color(0.12f, 0.27f, 0.38f, 0.14f),
            };
            return set.Clone(scale);
        }

        private static HovlProjectileSet Set(string flash, string projectile, string hit, Color tint, float alpha)
            => new()
            {
                FlashPath = flash,
                ProjectilePath = projectile,
                HitPath = hit,
                Tint = tint,
                Alpha = alpha,
            };

        /// <summary>단발 프리팹을 재생하고 안전시간 뒤 풀로 돌려보낸다.</summary>
        public static GameObject Play(string resourcePath, Vector3 position, float scale, int sortingLayer,
            int sortingOrder, float safeLifetime = 2.2f)
            => Play(resourcePath, position, Vector3.right, scale, sortingLayer, sortingOrder,
                Color.white, 1f, safeLifetime);

        public static GameObject Play(string resourcePath, Vector3 position, Vector3 direction, float scale,
            int sortingLayer, int sortingOrder, Color tint, float alpha = 1f, float safeLifetime = 2.2f)
        {
            GameObject instance = Spawn(resourcePath, position, direction, scale, sortingLayer, sortingOrder,
                tint, alpha, worldSpaceParticles: false);
            if (instance != null) Host.RecycleAfter(instance, safeLifetime);
            return instance;
        }

        /// <summary>
        /// 밝은 전장용 발사광. Hovl Flash를 주인공으로 두고, 진행 방향의 짙은 알파 받침 한 장만 뒤에 깐다.
        /// 가산광 자체를 키우면 흰 바닥에서 더 날아가므로 채도는 유지하고 명도 대비를 받침에서 만든다.
        /// </summary>
        public static GameObject PlayFlash(HovlProjectileSet set, Vector3 position, Vector3 direction,
            float cardSize, int sortingLayer, int sortingOrder, float scale = 1f, float alpha = 1f)
        {
            if (set == null) return null;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Color shadow = set.UnderlayColor;
            shadow.a *= 0.28f * Mathf.Clamp01(alpha);
            float width = set.UnderlaySize * cardSize * scale;
            SpriteFx.Play(VfxArt.Streak, position - direction.normalized * width * 0.18f, shadow, false,
                new Vector2(width * 0.42f, width * 0.065f),
                new Vector2(width * 1.72f, width * 0.15f), 0.19f,
                angle + 180f, 0f, 0.01f, 0.34f, SpriteFx.Ease.OutCubic,
                sortingLayer, sortingOrder - 1);
            return Play(set.FlashPath, position, direction, set.FlashScale * cardSize * scale * 1.04f,
                sortingLayer, sortingOrder, set.Tint, Mathf.Min(1f, set.Alpha * alpha * 1.05f), 1.4f);
        }

        /// <summary>
        /// 밝은 전장용 착탄광. Hovl Hit 아래의 청회/원소색 알파 파동이 흰 판에서도 착탄 크기를 남긴다.
        /// </summary>
        public static GameObject PlayHit(HovlProjectileSet set, Vector3 position, Vector3 direction,
            float cardSize, int sortingLayer, int sortingOrder, float scale = 1f, float alpha = 1f)
        {
            if (set == null) return null;
            Color shadow = set.UnderlayColor;
            shadow.a *= 0.24f * Mathf.Clamp01(alpha);
            float size = set.UnderlaySize * cardSize * scale;
            SpriteFx.Play(VfxSpace.Ring, position, shadow, false,
                new Vector2(size * 0.34f, size * 0.34f),
                new Vector2(size * 1.82f, size * 1.82f), 0.25f,
                0f, 0f, 0.006f, 0.32f, SpriteFx.Ease.OutCubic,
                sortingLayer, sortingOrder - 1);
            return Play(set.HitPath, position, direction, set.HitScale * cardSize * scale * 1.10f,
                sortingLayer, sortingOrder, set.Tint, Mathf.Min(1f, set.Alpha * alpha * 1.10f), 2.2f);
        }

        /// <summary>우리 이동 루트가 매 프레임 옮길 비행체를 꺼낸다.</summary>
        public static GameObject SpawnProjectile(HovlProjectileSet set, Vector3 position, Vector3 direction,
            int sortingLayer, int sortingOrder)
        {
            if (set == null || string.IsNullOrEmpty(set.ProjectilePath)) return null;
            GameObject instance = Spawn(set.ProjectilePath, position, direction, set.ProjectileScale * VfxSpace.CardSize,
                sortingLayer, sortingOrder, set.Tint, set.Alpha, worldSpaceParticles: false);
            // 코루틴 소유 유닛이 중간에 사라져도 풀 밖에 영구 잔류하지 않는 최종 안전망이다.
            if (instance != null) Host.RecycleAfter(instance, 5f);
            return instance;
        }

        public static void MoveProjectile(GameObject instance, Vector3 position, Vector3 direction, float rotationOffset = 0f)
        {
            if (instance == null) return;
            instance.transform.position = position;
            instance.transform.rotation = Rotation(direction, rotationOffset);
        }

        /// <summary>방출은 멈추되 이미 나온 트레일은 짧게 살려 순간이동처럼 끊기지 않게 한다.</summary>
        public static void StopAndRelease(GameObject instance, float trailTail = 0.28f)
        {
            if (instance == null) return;
            HovlPoolItem item = instance.GetComponent<HovlPoolItem>();
            if (item == null)
            {
                Object.Destroy(instance);
                return;
            }

            item.StopEmitting();
            Host.RecycleAfter(instance, Mathf.Max(0.02f, trailTail));
        }

        public static void Release(GameObject instance)
        {
            if (instance == null) return;
            HovlPoolItem item = instance.GetComponent<HovlPoolItem>();
            if (item == null)
            {
                Object.Destroy(instance);
                return;
            }

            item.Generation++;
            ActiveItems.Remove(item);
            item.StopAndClear();
            item.transform.SetParent(Host.transform, false);
            item.gameObject.SetActive(false);
            if (!Pools.TryGetValue(item.ResourcePath, out Stack<HovlPoolItem> pool))
            {
                pool = new Stack<HovlPoolItem>();
                Pools[item.ResourcePath] = pool;
            }
            pool.Push(item);
        }

        /// <summary>스테이지 재배치 전에 이전 전투에서 아직 수명 중인 Hovl 인스턴스를 전부 회수한다.</summary>
        public static void ClearTransient()
        {
            Effects.BattlefieldBackdrop.ClearFocus();
            if (ActiveItems.Count == 0) return;
            var snapshot = new List<HovlPoolItem>(ActiveItems);
            foreach (HovlPoolItem item in snapshot)
            {
                if (item != null) Release(item.gameObject);
            }
        }

        private static GameObject Spawn(string resourcePath, Vector3 position, Vector3 direction, float scale,
            int sortingLayer, int sortingOrder, Color tint, float alpha, bool worldSpaceParticles)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            HovlPoolItem item = null;
            if (Pools.TryGetValue(resourcePath, out Stack<HovlPoolItem> pool))
            {
                while (pool.Count > 0 && item == null) item = pool.Pop();
            }

            if (item == null)
            {
                GameObject prefab = Load(resourcePath);
                if (prefab == null) return null;
                GameObject go = Object.Instantiate(prefab);
                go.name = "Hovl_" + prefab.name;
                item = go.AddComponent<HovlPoolItem>();
                item.Initialize(resourcePath, resourcePath.Contains("/Projectile "));
            }

            item.Generation++;
            item.transform.SetParent(null, false);
            item.transform.position = position;
            item.transform.rotation = Rotation(direction, 0f);
            item.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            item.gameObject.SetActive(true);
            ActiveItems.Add(item);
            item.Play(sortingLayer, sortingOrder, tint, alpha, worldSpaceParticles);
            return item.gameObject;
        }

        private static Quaternion Rotation(Vector3 direction, float offset)
        {
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.right;
            direction.z = 0f;
            return Quaternion.FromToRotation(Vector3.forward, direction.normalized)
                   * Quaternion.AngleAxis(offset, Vector3.forward);
        }

        private static GameObject Load(string resourcePath)
        {
            if (Prefabs.TryGetValue(resourcePath, out GameObject cached)) return cached;
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            Prefabs[resourcePath] = prefab;
            if (prefab == null) Debug.LogWarning($"[HovlFx] Resources 프리팹을 찾지 못했습니다: {resourcePath}");
            return prefab;
        }

        private static HovlPoolHost Host
        {
            get
            {
                if (_host != null) return _host;
                var go = new GameObject("HovlFxPool") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<HovlPoolHost>();
                return _host;
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class HovlPoolItem : MonoBehaviour
    {
        private static readonly int TintColor = Shader.PropertyToID("_TintColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private ParticleSystem[] _systems;
        private ParticleSystemRenderer[] _renderers;
        private ParticleSystemSimulationSpace[] _simulationSpaces;
        private float[] _simulationSpeeds;
        private float[] _startSizeMultipliers;
        private float[] _startSpeedMultipliers;
        private float[] _startLifetimeMultipliers;
        private TrailRenderer[] _trails;

        public string ResourcePath { get; private set; }
        public int Generation { get; set; }

        public void Initialize(string resourcePath, bool stripEmbeddedImpact)
        {
            ResourcePath = resourcePath;

            // 원본 transform형 프리팹의 이동/충돌/자동 풀 스크립트가 우리 적중 계약을 침범하지 못하게 한다.
            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != this) behaviour.enabled = false;
            }
            foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = false;

            if (stripEmbeddedImpact)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child == transform) continue;
                    if (child.name.StartsWith("Flash ") || child.name.StartsWith("Hit "))
                        child.gameObject.SetActive(false);
                }
            }

            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _simulationSpaces = new ParticleSystemSimulationSpace[_systems.Length];
            _simulationSpeeds = new float[_systems.Length];
            _startSizeMultipliers = new float[_systems.Length];
            _startSpeedMultipliers = new float[_systems.Length];
            _startLifetimeMultipliers = new float[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem.MainModule main = _systems[i].main;
                _simulationSpaces[i] = main.simulationSpace;
                _simulationSpeeds[i] = main.simulationSpeed;
                _startSizeMultipliers[i] = main.startSizeMultiplier;
                _startSpeedMultipliers[i] = main.startSpeedMultiplier;
                _startLifetimeMultipliers[i] = main.startLifetimeMultiplier;

                string lowerName = _systems[i].name.ToLowerInvariant();
                string resourceName = resourcePath[(resourcePath.LastIndexOf('/') + 1)..].ToLowerInvariant();
                bool looseParticle = lowerName.Contains("spark") || lowerName.Contains("debris")
                                     || lowerName.Contains("trail");
                bool planarNovaNoise = (resourcePath.EndsWith("Hit 18 nova orange")
                                        || resourcePath.EndsWith("Hit 17 nova violet"))
                                       && (lowerName.Contains("lightning") || lowerName.Contains("wind"));
                bool novaSingle = resourcePath.EndsWith("Hit 18 nova orange")
                                  || resourcePath.EndsWith("Hit 17 nova violet")
                                  || resourcePath.EndsWith("Flash 18 nova orange")
                                  || resourcePath.EndsWith("Flash 17 nova violet");
                // nova의 루트/Glow/Darkness 빌보드는 2D로 눕히면 단색 전구가 되어 Hovl 고리 실루엣을 가린다.
                // ShockWave/SubShockWave는 남겨 원본 애니메이션을 주인공으로 쓴다.
                bool novaBulb = novaSingle && (lowerName == resourceName || lowerName == "glow" || lowerName == "darkness");
                bool crystalBulb = (resourcePath.EndsWith("Hit 26 blue crystal")
                                    || resourcePath.EndsWith("Flash 26 blue crystal"))
                                   && lowerName == "glow";
                bool dragonFlashNoise = resourcePath.EndsWith("Dragon punch flash")
                                        && !lowerName.Contains("magic circle");
                if (looseParticle || planarNovaNoise || novaBulb || crystalBulb || dragonFlashNoise)
                    _systems[i].gameObject.SetActive(false);
            }
        }

        public void Play(int sortingLayer, int sortingOrder, Color tint, float alpha, bool worldSpaceParticles)
        {
            sortingLayer = VfxSpace.ResolveSortingLayer(sortingLayer);
            var block = new MaterialPropertyBlock();
            Color shaded = new(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
            foreach (ParticleSystemRenderer renderer in _renderers)
            {
                renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = sortingOrder;
                block.Clear();
                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty(TintColor)) block.SetColor(TintColor, shaded);
                else if (material != null && material.HasProperty(ColorProperty)) block.SetColor(ColorProperty, shaded);
                renderer.SetPropertyBlock(block);
            }

            foreach (TrailRenderer trail in _trails)
            {
                trail.sortingLayerID = sortingLayer;
                trail.sortingOrder = sortingOrder - 1;
                trail.Clear();
                trail.emitting = true;
            }

            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (!system.gameObject.activeInHierarchy) continue;
                ParticleSystem.MainModule main = system.main;
                bool projectile = ResourcePath.Contains("/Projectile ");
                bool anchoredNovaHit = ResourcePath.EndsWith("Hit 18 nova orange")
                                       || ResourcePath.EndsWith("Hit 17 nova violet");
                main.loop = false;
                main.useUnscaledTime = false;
                // Time.timeScale이 전투 배속과 히트스톱의 주인이므로 simulationSpeed에 다시 곱하지 않는다.
                main.simulationSpeed = _simulationSpeeds[i];
                main.simulationSpace = worldSpaceParticles ? ParticleSystemSimulationSpace.World : _simulationSpaces[i];
                // Hovl 데모의 낙하·충돌 입자는 3D 바닥을 전제로 한다. 카드 전장에서는 바닥에 점으로
                // 남으므로 중력과 파티클 충돌을 끄고, 색은 머티리얼 MPB뿐 아니라 입자 시작색에도 넣는다.
                main.gravityModifier = 0f;
                string lowerName = system.name.ToLowerInvariant();
                bool projectileGlow = ResourcePath.Contains("/Projectile ")
                                      && (lowerName.Contains("glow") || lowerName.Contains("shine"));
                main.startSizeMultiplier = _startSizeMultipliers[i] * (projectileGlow ? 0.62f : 1f);
                // 흩어지는 Sparks/Glow 계열은 Initialize에서 제거하고, 남은 Hovl 고리·결정의 원래 속도는 살린다.
                // 단발 수명만 줄여 다음 행동까지 잔상이 남지 않게 한다.
                main.startSpeedMultiplier = anchoredNovaHit ? 0f : _startSpeedMultipliers[i];
                main.startLifetimeMultiplier = _startLifetimeMultipliers[i]
                                               * (projectile ? 1f : anchoredNovaHit ? 0.24f : 0.62f);
                Color particleTint = lowerName.Contains("glow") || lowerName.Contains("shine") || lowerName.Contains("flash")
                    ? Color.Lerp(shaded, Color.white, 0.42f)
                    : shaded;
                particleTint.a = Mathf.Clamp01(alpha);
                main.startColor = particleTint;
                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = false;
                ParticleSystem.ExternalForcesModule externalForces = system.externalForces;
                externalForces.enabled = false;
                main.stopAction = ParticleSystemStopAction.None;
                system.Clear(true);
                system.Play(true);
            }
        }

        public void StopEmitting()
        {
            foreach (ParticleSystem system in _systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (TrailRenderer trail in _trails) trail.emitting = false;
        }

        public void StopAndClear()
        {
            foreach (ParticleSystem system in _systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (TrailRenderer trail in _trails)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }
    }

    internal sealed class HovlPoolHost : MonoBehaviour
    {
        public void RecycleAfter(GameObject instance, float seconds)
        {
            if (instance == null) return;
            HovlPoolItem item = instance.GetComponent<HovlPoolItem>();
            if (item == null) return;
            int generation = item.Generation;
            StartCoroutine(Recycle(instance, item, generation, Mathf.Max(0.02f, seconds)));
        }

        private static IEnumerator Recycle(GameObject instance, HovlPoolItem item, int generation, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (instance != null && item != null && item.Generation == generation)
                HovlFx.Release(instance);
        }
    }
}
