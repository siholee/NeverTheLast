using Action = System.Action;
using System.Collections.Generic;
using Effects.Projectiles;
using Entities;
using UnityEngine;

namespace Effects
{
    /// <summary>대상 카드 위에 짧고 선명한 베기와 파편 파티클을 재생한다.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashEffect : MonoBehaviour
    {
        public const float ImpactDelay = 0.12f;

        private const float Lifetime = 0.36f;
        private const string TrailPath = "SFX/Combat/SlashTrail";
        private const string SparkPath = "SFX/Combat/SlashSpark";

        private SpriteRenderer _glow;
        private SpriteRenderer _core;
        private SpriteRenderer _echo;
        private Vector3 _glowScale;
        private Vector3 _coreScale;
        private Vector3 _echoScale;
        private float _age;

        public static SlashEffect Play(Unit attacker, Unit target, Color accent)
        {
            if (target == null) return null;

            Vector3 start = CombatVfxAssets.WorldPoint(attacker);
            Vector3 end = CombatVfxAssets.WorldPoint(target);
            Vector3 direction = end - start;

            var root = new GameObject("SlashVfx");
            root.transform.position = end;
            // 공격 방향에 따라 서로 반대 대각선을 사용해 양 진영의 타격 방향이 읽히게 한다.
            float angle = direction.x >= 0f ? -28f : 208f;
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var effect = root.AddComponent<SlashEffect>();
            effect.Build(target, accent);
            return effect;
        }

        private void Build(Unit target, Color accent)
        {
            int layer = CombatVfxAssets.SortingLayer(target);
            float cardScale = Cell.CardSize;

            _glow = CombatVfxAssets.NewRenderer(
                transform, "Glow", CombatVfxAssets.Sprite(TrailPath), accent,
                layer, CardProjectile.OverlaySortingOrder + 2);
            _glowScale = new Vector3(cardScale * 0.92f, cardScale * 0.15f, 1f);

            _core = CombatVfxAssets.NewRenderer(
                transform, "Core", ProjectileShapes.Sliver(320, 8), Color.white,
                layer, CardProjectile.OverlaySortingOrder + 4);
            _coreScale = new Vector3(cardScale * 0.28f, cardScale * 0.12f, 1f);

            _echo = CombatVfxAssets.NewRenderer(
                transform, "Echo", ProjectileShapes.Sliver(250, 6), accent,
                layer, CardProjectile.OverlaySortingOrder + 3);
            _echo.transform.localPosition = new Vector3(0f, -cardScale * 0.045f, 0f);
            _echoScale = new Vector3(cardScale * 0.24f, cardScale * 0.10f, 1f);

            ParticleSystem particles = CombatVfxAssets.NewParticles(
                transform, "SlashParticles", CombatVfxAssets.Texture(SparkPath), layer,
                CardProjectile.OverlaySortingOrder + 5);
            EmitSlashParticles(particles, accent, cardScale);
            ApplyFrame(0f);
        }

        private static void EmitSlashParticles(ParticleSystem particles, Color accent, float cardScale)
        {
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 3;
            sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
            sheet.cycleCount = 1;

            particles.Play(false);
            for (int i = 0; i < 18; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;

                var emit = new ParticleSystem.EmitParams
                {
                    position = Random.insideUnitCircle * (cardScale * 0.08f),
                    velocity = direction * Random.Range(cardScale * 1.1f, cardScale * 2.2f),
                    startLifetime = Random.Range(0.16f, 0.32f),
                    startSize = Random.Range(cardScale * 0.10f, cardScale * 0.23f),
                    startColor = Color.Lerp(Color.white, accent, Random.Range(0.15f, 0.75f)),
                };
                particles.Emit(emit, 1);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);
            ApplyFrame(t);
            if (t >= 1f) Destroy(gameObject);
        }

#if UNITY_EDITOR
        /// <summary>Editor 프리뷰 렌더러가 특정 시점의 베기를 고정해 점검할 때 사용한다.</summary>
        public void SetPreviewTime(float seconds)
        {
            _age = Mathf.Clamp(seconds, 0f, Lifetime);
            ApplyFrame(_age / Lifetime);
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>())
            {
                particles.Simulate(_age, true, true, true);
            }
        }
#endif

        private void ApplyFrame(float t)
        {
            float reveal = Mathf.SmoothStep(0.08f, 1f, Mathf.Clamp01(t / 0.30f));
            float fade = 1f - Mathf.SmoothStep(0.38f, 1f, t);

            _glow.transform.localScale = Vector3.Scale(_glowScale, new Vector3(reveal, 1f, 1f));
            _core.transform.localScale = Vector3.Scale(_coreScale, new Vector3(reveal, 1f, 1f));
            _echo.transform.localScale = Vector3.Scale(_echoScale, new Vector3(reveal, 1f, 1f));

            CombatVfxAssets.SetAlpha(_glow, fade * 0.72f);
            CombatVfxAssets.SetAlpha(_core, fade);
            CombatVfxAssets.SetAlpha(_echo, fade * 0.82f);
        }
    }

    /// <summary>에어본 상태 동안 대상 아래에 풍압 링과 상승 파티클을 유지한다.</summary>
    [DisallowMultipleComponent]
    public sealed class AirborneEffect : MonoBehaviour
    {
        public const float ApexDelay = 0.24f;
        private const float ApexHoldDuration = 0.22f;
        private const float DescentDuration = 0.20f;
        private const string RingPath = "SFX/Combat/AirborneRing";
        private const string SparkPath = "SFX/Combat/SlashSpark";
        private const string AirborneKey = "cc_airborne";

        private static readonly Dictionary<EntityId, AirborneEffect> Live = new();

        private Unit _target;
        private SpriteRenderer _lowerRing;
        private SpriteRenderer _upperRing;
        private ParticleSystem _particles;
        private Transform _portrait;
        private Vector3 _portraitBasePosition;
        private float _fade;
        private float _time;
        private bool _apexSent;
        private event Action ApexReached;

        public static AirborneEffect Attach(Unit target, Action onApex = null)
        {
            if (target == null) return null;
            EntityId id = target.GetEntityId();
            if (Live.TryGetValue(id, out AirborneEffect current) && current != null)
            {
                current._fade = 1f;
                if (current._apexSent) onApex?.Invoke();
                else current.ApexReached += onApex;
                return current;
            }

            var root = new GameObject("AirborneVfx");
            var effect = root.AddComponent<AirborneEffect>();
            effect.ApexReached += onApex;
            effect.Build(target);
            Live[id] = effect;
            return effect;
        }

        private void Build(Unit target)
        {
            _target = target;
            _fade = 1f;
            if (target.currentCell?.portraitRenderer != null)
            {
                _portrait = target.currentCell.portraitRenderer.transform;
                _portraitBasePosition = _portrait.localPosition;
            }
            int layer = CombatVfxAssets.SortingLayer(target);
            Color wind = new(0.56f, 0.86f, 1f, 1f);

            Sprite ring = CombatVfxAssets.Sprite(RingPath);
            _lowerRing = CombatVfxAssets.NewRenderer(
                transform, "LowerWindRing", ring, wind, layer,
                CardProjectile.OverlaySortingOrder + 1);
            _upperRing = CombatVfxAssets.NewRenderer(
                transform, "UpperWindRing", ring, Color.white, layer,
                CardProjectile.OverlaySortingOrder + 2);

            _particles = CombatVfxAssets.NewParticles(
                transform, "RisingWind", CombatVfxAssets.Texture(SparkPath), layer,
                CardProjectile.OverlaySortingOrder + 3);
            ConfigureRisingParticles(_particles, wind);
            _particles.Play();
        }

        private static void ConfigureRisingParticles(ParticleSystem particles, Color wind)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.68f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(Cell.CardSize * 0.08f, Cell.CardSize * 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, wind);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 16f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Cell.CardSize * 0.34f;
            shape.radiusThickness = 1f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // x/y/z 곡선은 같은 MinMaxCurve 모드여야 Editor 시뮬레이션 경고가 나지 않는다.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(Cell.CardSize * 0.42f, Cell.CardSize * 0.72f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(wind, 0f),
                    new GradientColorKey(Color.white, 0.55f),
                    new GradientColorKey(wind, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.18f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = gradient;

            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 3;
            sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
            sheet.cycleCount = 1;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            if (!_apexSent && _time >= ApexDelay)
            {
                _apexSent = true;
                Action callbacks = ApexReached;
                ApexReached = null;
                callbacks?.Invoke();
            }
            bool active = _target != null && _target.isActive && _target.HasStatusKey(AirborneKey);
            _fade = Mathf.MoveTowards(_fade, active ? 1f : 0f, Time.deltaTime / (active ? 0.10f : 0.22f));

            if (_target != null)
            {
                Vector3 anchor = _target.currentCell != null
                    ? _target.currentCell.transform.position
                    : _target.transform.position;
                transform.position = anchor + Vector3.up * (Cell.CardSize * 0.08f);
            }

            if (_portrait != null)
            {
                float lift;
                if (_time < ApexDelay)
                {
                    float t = Mathf.Clamp01(_time / ApexDelay);
                    lift = Mathf.SmoothStep(0f, Cell.CardSize * 0.18f, t);
                }
                else if (_time < ApexDelay + ApexHoldDuration)
                {
                    lift = Cell.CardSize * 0.18f;
                }
                else
                {
                    float t = Mathf.Clamp01(
                        (_time - ApexDelay - ApexHoldDuration) / DescentDuration);
                    lift = Mathf.SmoothStep(Cell.CardSize * 0.18f, 0f, t);
                }
                _portrait.localPosition = _portraitBasePosition + Vector3.up * lift;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 11f);
            ApplyRing(_lowerRing, Cell.CardSize * Mathf.Lerp(0.58f, 0.76f, pulse),
                Cell.CardSize * 0.10f, -Cell.CardSize * 0.34f, _fade * 0.62f);
            ApplyRing(_upperRing, Cell.CardSize * Mathf.Lerp(0.42f, 0.58f, 1f - pulse),
                Cell.CardSize * 0.075f, -Cell.CardSize * 0.16f, _fade * 0.46f);

            var emission = _particles.emission;
            emission.rateOverTime = 16f * _fade;

            if (!active && _fade <= 0f)
            {
                Destroy(gameObject);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor 프리뷰 렌더러에서 에어본의 유지 프레임을 고정한다.</summary>
        public void SetPreviewTime(float seconds)
        {
            _time = Mathf.Max(0f, seconds);
            _fade = 1f;
            if (_target != null)
            {
                Vector3 anchor = _target.currentCell != null
                    ? _target.currentCell.transform.position
                    : _target.transform.position;
                transform.position = anchor + Vector3.up * (Cell.CardSize * 0.08f);
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 11f);
            ApplyRing(_lowerRing, Cell.CardSize * Mathf.Lerp(0.58f, 0.76f, pulse),
                Cell.CardSize * 0.10f, -Cell.CardSize * 0.34f, 0.62f);
            ApplyRing(_upperRing, Cell.CardSize * Mathf.Lerp(0.42f, 0.58f, 1f - pulse),
                Cell.CardSize * 0.075f, -Cell.CardSize * 0.16f, 0.46f);
            _particles.Simulate(_time, true, true, true);
        }
#endif

        private static void ApplyRing(
            SpriteRenderer ring, float width, float height, float y, float alpha)
        {
            if (ring == null || ring.sprite == null) return;
            Vector2 size = ring.sprite.bounds.size;
            ring.transform.localPosition = new Vector3(0f, y, 0f);
            ring.transform.localScale = new Vector3(width / size.x, height / size.y, 1f);
            CombatVfxAssets.SetAlpha(ring, alpha);
        }

        private void OnDestroy()
        {
            if (_portrait != null) _portrait.localPosition = _portraitBasePosition;
            if (_target != null) Live.Remove(_target.GetEntityId());
        }
    }

    internal static class CombatVfxAssets
    {
        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static readonly Dictionary<string, Material> Materials = new();

        public static Vector3 WorldPoint(Unit unit)
        {
            if (unit == null) return Vector3.zero;
            if (unit.currentCell?.portraitRenderer != null)
                return unit.currentCell.portraitRenderer.transform.position;
            return unit.currentCell != null ? unit.currentCell.transform.position : unit.transform.position;
        }

        public static int SortingLayer(Unit unit)
        {
            SpriteRenderer reference = unit?.currentCell != null ? unit.currentCell.portraitRenderer : null;
            return reference != null ? reference.sortingLayerID : 0;
        }

        public static Texture2D Texture(string path)
        {
            if (Textures.TryGetValue(path, out Texture2D cached) && cached != null) return cached;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[CombatVfx] 텍스처를 찾을 수 없습니다: Resources/{path}");
                texture = ProjectileShapes.Core().texture;
            }
            Textures[path] = texture;
            return texture;
        }

        public static Sprite Sprite(string path)
        {
            if (Sprites.TryGetValue(path, out Sprite cached) && cached != null) return cached;
            Texture2D texture = Texture(path);
            var sprite = UnityEngine.Sprite.Create(
                texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = path.Replace('/', '_');
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[path] = sprite;
            return sprite;
        }

        public static SpriteRenderer NewRenderer(
            Transform parent, string name, Sprite sprite, Color color, int sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = order;
            if (sprite?.texture != null &&
                (sprite.texture.name.Contains("SlashTrail") || sprite.texture.name.Contains("AirborneRing")))
            {
                renderer.sharedMaterial = AdditiveMaterial(sprite.texture);
            }
            return renderer;
        }

        public static ParticleSystem NewParticles(
            Transform parent, string name, Texture2D texture, int sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 64;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = order;
            renderer.sharedMaterial = AdditiveMaterial(texture);
            return particles;
        }

        private static Material AdditiveMaterial(Texture2D texture)
        {
            string key = texture != null ? texture.name : "fallback";
            if (Materials.TryGetValue(key, out Material cached) && cached != null) return cached;

            // 재사용한 Hovl 텍스처는 검정을 투명으로 쓰는 additive 에셋이다.
            // 알파 블렌드 셰이더를 쓰면 검은 사각형이 보여 Hovl 원본 셰이더를 우선한다.
            Shader shader = Shader.Find("Hovl/Particles/Add_CenterGlow")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;

            var material = new Material(shader)
            {
                name = $"CombatVfx_{key}",
                mainTexture = texture,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 1.4f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Blend2")) material.SetFloat("_Blend2", 1f);
            if (material.HasProperty("_Usedepth")) material.SetFloat("_Usedepth", 0f);
            Materials[key] = material;
            return material;
        }

        public static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }
    }
}
