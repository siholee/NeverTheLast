using Effects.Projectiles;
using Entities;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// 찌르기(<see cref="AttackVisualForm.Thrust"/>) 연출.
    ///
    /// 베기가 대상 위를 <b>가로지르는 선</b>이라면, 찌르기는 공격 방향을 따라
    /// <b>들어왔다 빠지는 창</b>이다. 같은 근접 공격이라도 무기가 무엇인지 한눈에 갈린다.
    /// 창끝이 카드에 닿는 순간에 맞춰 피해가 들어가므로 지연은 베기와 같은 값을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrustEffect : MonoBehaviour
    {
        /// <summary>창끝이 카드에 닿기까지. 베기와 같아야 타격 타이밍이 흔들리지 않는다.</summary>
        public const float ImpactDelay = SlashEffect.ImpactDelay;

        private const float Lifetime = 0.34f;
        private const int ShardCount = 4;
        private const string TrailPath = "SFX/Combat/SlashTrail";

        private SpriteRenderer _glow;
        private SpriteRenderer _shaft;
        private SpriteRenderer _head;
        private SpriteRenderer _tip;
        private readonly SpriteRenderer[] _shards = new SpriteRenderer[ShardCount];
        private readonly float[] _shardAngles = new float[ShardCount];
        private float _cardScale;
        private float _age;

        public static ThrustEffect Play(Unit attacker, Unit target, Color accent)
        {
            if (target == null) return null;

            Vector3 start = CombatVfxAssets.WorldPoint(attacker);
            Vector3 end = CombatVfxAssets.WorldPoint(target);
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;

            var root = new GameObject("ThrustVfx");
            root.transform.position = end;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var effect = root.AddComponent<ThrustEffect>();
            effect.Build(target, accent);
            return effect;
        }

        private void Build(Unit target, Color accent)
        {
            int layer = CombatVfxAssets.SortingLayer(target);
            _cardScale = Cell.CardSize;

            // 몸통 빛무리. 베기가 가진 것과 같은 텍스처다 — 이것이 없으면 자루가
            // 실 한 올처럼 보인다(첫 판에서 실제로 그랬다).
            _glow = CombatVfxAssets.NewRenderer(
                transform, "Glow", CombatVfxAssets.Sprite(TrailPath), accent,
                layer, CardProjectile.OverlaySortingOrder + 1);
            _glow.transform.localScale = new Vector3(_cardScale * 0.87f, _cardScale * 0.10f, 1f);

            // 자루. 길이는 카드 한 변쯤(스프라이트 3.6유닛 × 0.30 × 카드 크기)에서 멈춘다.
            _shaft = CombatVfxAssets.NewRenderer(
                transform, "Shaft", ProjectileShapes.Sliver(360, 12), Color.white,
                layer, CardProjectile.OverlaySortingOrder + 2);
            _shaft.transform.localScale = new Vector3(_cardScale * 0.30f, _cardScale * 0.24f, 1f);

            // 창날. 자루보다 짧고 넓은 쐐기라야 '찌른다'가 실루엣으로 읽힌다.
            _head = CombatVfxAssets.NewRenderer(
                transform, "Head", ProjectileShapes.Sliver(140, 70), accent,
                layer, CardProjectile.OverlaySortingOrder + 3);
            _head.transform.localScale = new Vector3(_cardScale * 0.128f, _cardScale * 0.088f, 1f);

            _tip = CombatVfxAssets.NewRenderer(
                transform, "Tip", ProjectileShapes.Core(), Color.white,
                layer, CardProjectile.OverlaySortingOrder + 4);
            _tip.transform.localScale = Vector3.one * (_cardScale * 0.175f);

            for (int i = 0; i < ShardCount; i++)
            {
                _shards[i] = CombatVfxAssets.NewRenderer(
                    transform, $"Shard{i}", ProjectileShapes.Shard(70, 5), accent,
                    layer, CardProjectile.OverlaySortingOrder + 3);
                // 관통은 앞으로 뻗는다. 사방으로 흩어지면 둔기 타격처럼 보인다.
                _shardAngles[i] = Random.Range(-26f, 26f);
                _shards[i].transform.localRotation = Quaternion.Euler(0f, 0f, _shardAngles[i]);
            }

            ApplyFrame(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);
            ApplyFrame(t);
            if (t >= 1f) Destroy(gameObject);
        }

#if UNITY_EDITOR
        /// <summary>Editor 프리뷰 렌더러가 특정 시점의 찌르기를 고정해 점검할 때 사용한다.</summary>
        public void SetPreviewTime(float seconds)
        {
            _age = Mathf.Clamp(seconds, 0f, Lifetime);
            ApplyFrame(_age / Lifetime);
        }
#endif

        private void ApplyFrame(float t)
        {
            // 들어올 때는 급하게, 빠질 때는 그보다 느리게. 찌르기의 리듬이다.
            float drive = Mathf.Clamp01(t / 0.34f);
            drive = 1f - (1f - drive) * (1f - drive);
            float withdraw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.45f) / 0.55f));

            float reach = Mathf.Lerp(-_cardScale * 0.62f, _cardScale * 0.06f, drive)
                          - _cardScale * 0.35f * withdraw;
            // 자루와 빛무리는 원점이 가운데라 반 길이만큼 뒤로 물려야 창끝이 tip과 같은 자리에 온다.
            _shaft.transform.localPosition = new Vector3(reach - _cardScale * 0.54f, 0f, 0f);
            _glow.transform.localPosition = new Vector3(reach - _cardScale * 0.56f, 0f, 0f);
            _head.transform.localPosition = new Vector3(reach - _cardScale * 0.07f, 0f, 0f);
            _tip.transform.localPosition = new Vector3(reach, 0f, 0f);

            float fade = 1f - Mathf.SmoothStep(0.4f, 1f, t);
            CombatVfxAssets.SetAlpha(_glow, fade * 0.70f);
            CombatVfxAssets.SetAlpha(_shaft, fade * 0.95f);
            CombatVfxAssets.SetAlpha(_head, fade);
            CombatVfxAssets.SetAlpha(_tip, Mathf.Clamp01(1f - Mathf.Abs(t - 0.34f) * 4.5f));

            float spread = _cardScale * (0.06f + 0.35f * t);
            for (int i = 0; i < ShardCount; i++)
            {
                SpriteRenderer shard = _shards[i];
                shard.transform.localPosition =
                    shard.transform.localRotation * new Vector3(spread, 0f, 0f);
                shard.transform.localScale =
                    new Vector3(_cardScale * (0.14f + 0.16f * t), _cardScale * 0.22f * fade, 1f);
                CombatVfxAssets.SetAlpha(shard, fade * fade);
            }
        }
    }

    /// <summary>
    /// 타격(<see cref="AttackVisualForm.Impact"/>) 연출 — 둔기·맨손·투척의 착탄.
    ///
    /// 베기도 찌르기도 <b>방향이 있는</b> 공격이다. 둔기는 방향이 없다.
    /// 그래서 선 대신 <b>퍼지는 고리</b>와 사방으로 튀는 파편으로 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactEffect : MonoBehaviour
    {
        /// <summary>충격이 카드에 닿기까지. 다른 근접 연출과 같은 값이다.</summary>
        public const float ImpactDelay = SlashEffect.ImpactDelay;

        private const float Lifetime = 0.34f;
        private const int ShardCount = 6;

        private SpriteRenderer _ring;
        private SpriteRenderer _innerRing;
        private SpriteRenderer _core;
        private readonly SpriteRenderer[] _shards = new SpriteRenderer[ShardCount];
        private float _cardScale;
        private float _age;

        public static ImpactEffect Play(Unit attacker, Unit target, Color accent)
        {
            if (target == null) return null;

            Vector3 end = CombatVfxAssets.WorldPoint(target);
            Vector3 start = CombatVfxAssets.WorldPoint(attacker);
            Vector3 direction = end - start;

            var root = new GameObject("ImpactVfx");
            // 때린 쪽으로 조금 치우쳐 터진다. 정중앙이면 누가 때렸는지가 사라진다.
            Vector3 bias = direction.sqrMagnitude > 0.0001f
                ? -direction.normalized * (Cell.CardSize * 0.10f)
                : Vector3.zero;
            root.transform.position = end + bias;

            var effect = root.AddComponent<ImpactEffect>();
            effect.Build(target, accent);
            return effect;
        }

        private void Build(Unit target, Color accent)
        {
            int layer = CombatVfxAssets.SortingLayer(target);
            _cardScale = Cell.CardSize;

            _ring = CombatVfxAssets.NewRenderer(
                transform, "ShockRing", ProjectileShapes.Ring(96, 8), accent,
                layer, CardProjectile.OverlaySortingOrder + 2);
            _innerRing = CombatVfxAssets.NewRenderer(
                transform, "InnerRing", ProjectileShapes.Ring(96, 5), Color.white,
                layer, CardProjectile.OverlaySortingOrder + 3);
            _core = CombatVfxAssets.NewRenderer(
                transform, "Core", ProjectileShapes.Core(), Color.white,
                layer, CardProjectile.OverlaySortingOrder + 4);

            for (int i = 0; i < ShardCount; i++)
            {
                _shards[i] = CombatVfxAssets.NewRenderer(
                    transform, $"Shard{i}", ProjectileShapes.Shard(64, 8), accent,
                    layer, CardProjectile.OverlaySortingOrder + 3);
                // 완전한 등간격은 도장처럼 보인다. 한 칸 안에서 흔들어 둔다.
                float angle = 360f / ShardCount * i + Random.Range(-18f, 18f);
                _shards[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            ApplyFrame(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);
            ApplyFrame(t);
            if (t >= 1f) Destroy(gameObject);
        }

#if UNITY_EDITOR
        /// <summary>Editor 프리뷰 렌더러가 특정 시점의 충격을 고정해 점검할 때 사용한다.</summary>
        public void SetPreviewTime(float seconds)
        {
            _age = Mathf.Clamp(seconds, 0f, Lifetime);
            ApplyFrame(_age / Lifetime);
        }
#endif

        private void ApplyFrame(float t)
        {
            float fade = 1f - Mathf.SmoothStep(0.25f, 1f, t);

            // 바깥 고리가 먼저 크게 퍼지고 안쪽 고리가 뒤따른다.
            ApplyRing(_ring, _cardScale * (0.16f + 0.92f * Mathf.Sqrt(t)), fade * 0.9f);
            ApplyRing(_innerRing, _cardScale * (0.10f + 0.52f * t), fade * 0.7f);

            float flash = Mathf.Clamp01(1f - t * 4f);
            _core.transform.localScale = Vector3.one * (_cardScale * (0.14f + 0.22f * t));
            CombatVfxAssets.SetAlpha(_core, flash);

            float reach = _cardScale * (0.08f + 0.40f * t);
            for (int i = 0; i < ShardCount; i++)
            {
                SpriteRenderer shard = _shards[i];
                shard.transform.localPosition =
                    shard.transform.localRotation * new Vector3(reach, 0f, 0f);
                shard.transform.localScale =
                    new Vector3(_cardScale * (0.20f + 0.14f * t), _cardScale * 0.14f * fade, 1f);
                CombatVfxAssets.SetAlpha(shard, fade * fade);
            }
        }

        private void ApplyRing(SpriteRenderer ring, float diameter, float alpha)
        {
            if (ring == null || ring.sprite == null) return;
            float size = Mathf.Max(0.0001f, ring.sprite.bounds.size.x);
            ring.transform.localScale = Vector3.one * (diameter / size);
            CombatVfxAssets.SetAlpha(ring, alpha);
        }
    }
}
