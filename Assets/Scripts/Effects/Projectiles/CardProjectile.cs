using System;
using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Projectiles
{
    public enum ProjectileVisualStyle
    {
        Bolt,
        Arrow,
        UltimateArrow,
    }

    /// <summary>
    /// 카드 게임식 투사체.
    ///
    /// 파티클 에셋(Hovl)을 쓰지 않는다. 날이 선 도형 하나에 잔광을 달아 쏘고,
    /// 착탄은 파편 몇 조각과 짧은 섬광으로 끝낸다. 파티클을 뿌리는 대신
    /// <b>실루엣과 타이밍</b>으로 타격감을 만드는 쪽이다.
    ///
    /// 정렬 순서는 카드보다 훨씬 높게(<see cref="OverlaySortingOrder"/>) 잡는다.
    /// 하스스톤처럼 투사체는 언제나 카드 <b>위</b>를 지나가야 한다 —
    /// 카드 뒤로 숨으면 무슨 일이 일어났는지 읽히지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardProjectile : MonoBehaviour
    {
        /// <summary>카드(최대 36)보다 훨씬 위. 전장의 모든 것을 덮는다.</summary>
        public const int OverlaySortingOrder = 1000;

        /// <summary>동시에 살아 있을 수 있는 투사체 수. 넘으면 가장 오래된 것부터 걷는다.</summary>
        private const int MaxConcurrent = 16;

        private const int ShardCount = 5;
        private const float ImpactTime = 0.22f;

        private static readonly List<CardProjectile> Live = new();

        private SpriteRenderer _head;
        private SpriteRenderer _streak;
        private SpriteRenderer _upperFletching;
        private SpriteRenderer _lowerFletching;
        private SpriteRenderer _core;
        private readonly SpriteRenderer[] _shards = new SpriteRenderer[ShardCount];
        private readonly float[] _shardAngles = new float[ShardCount];

        private Unit _from;
        private Unit _to;
        private Vector3 _start;
        private Vector3 _end;
        private float _duration;
        private float _timer;
        private float _impact = -1f;
        private float _scale = 1f;
        private int _sortingLayer;
        private ProjectilePathType _path;
        private ProjectilePathData _data;
        private ProjectileVisualStyle _style;

        /// <summary>머리가 대상에 닿는 순간 한 번 부른다. 피해는 여기에 맞춰 들어간다.</summary>
        private Action _onImpact;

        /// <summary>
        /// 투사체 하나를 쏜다.
        /// </summary>
        /// <param name="color">원소 색. 몸통과 잔광에 입힌다.</param>
        /// <param name="scale">전장 배율. 칸 크기에 맞춰 키운다.</param>
        public static void Fire(Unit from, Unit to, Color color, float duration,
            ProjectilePathType path, ProjectilePathData data, float scale = 1f, Action onImpact = null,
            ProjectileVisualStyle style = ProjectileVisualStyle.Bolt)
            => Fire(from, to, color, duration, path, data, scale, null, onImpact, style);

        /// <summary>
        /// 시작점을 직접 지정해 쏜다. 허공에 열린 차원문처럼 <b>시전자와 발사 위치가 다른</b> 연출용이다.
        /// <paramref name="from"/>은 색과 정렬을 고르는 데만 쓴다.
        /// </summary>
        public static void FireFrom(Vector3 origin, Unit from, Unit to, Color color, float duration,
            ProjectilePathType path, ProjectilePathData data, float scale = 1f, Action onImpact = null,
            ProjectileVisualStyle style = ProjectileVisualStyle.Bolt)
            => Fire(from, to, color, duration, path, data, scale, origin, onImpact, style);

        private static void Fire(Unit from, Unit to, Color color, float duration,
            ProjectilePathType path, ProjectilePathData data, float scale, Vector3? origin, Action onImpact,
            ProjectileVisualStyle style)
        {
            if (from == null || to == null) return;

            Trim();

            var go = new GameObject("CardProjectile");
            var projectile = go.AddComponent<CardProjectile>();
            projectile._onImpact = onImpact;
            projectile.Setup(from, to, color, Mathf.Max(0.05f, duration), path, data, scale, origin, style);
            Live.Add(projectile);
        }

        private static void Trim()
        {
            Live.RemoveAll(item => item == null);
            while (Live.Count >= MaxConcurrent)
            {
                CardProjectile oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest == null) continue;

                // 자리를 비우려고 앞당겨 걷는 것뿐이다. 착탄은 알려 주고 보낸다 —
                // 그러지 않으면 이 투사체를 기다리던 피해가 통째로 사라진다.
                oldest.NotifyImpact();
                Destroy(oldest.gameObject);
            }
        }

        /// <summary>착탄을 한 번만 알린다.</summary>
        private void NotifyImpact()
        {
            Action callback = _onImpact;
            _onImpact = null;
            callback?.Invoke();
        }

        private void Setup(Unit from, Unit to, Color color, float duration,
            ProjectilePathType path, ProjectilePathData data, float scale, Vector3? origin,
            ProjectileVisualStyle style)
        {
            _from = from;
            _to = to;
            _duration = duration;
            _path = path;
            _data = data;
            _style = style;
            _scale = Mathf.Max(0.2f, scale) * (style == ProjectileVisualStyle.UltimateArrow ? 1.45f : 1f);
            _start = origin ?? from.transform.position;
            _end = to.transform.position;

            transform.position = _start;

            // 카드와 같은 정렬 레이어에 있어야 sortingOrder 비교가 의미를 갖는다.
            // 다른 레이어면 order를 아무리 올려도 카드 뒤로 숨을 수 있다.
            SpriteRenderer reference = to.currentCell != null ? to.currentCell.portraitRenderer : null;
            _sortingLayer = reference != null ? reference.sortingLayerID : 0;

            // 잔광이 몸통 뒤에 깔리도록 먼저 만든다(같은 정렬 순서면 나중 것이 위).
            _streak = NewRenderer("Streak", ProjectileShapes.Streak(), color, 0.5f, OverlaySortingOrder);
            _streak.transform.localScale = new Vector3(_scale * 1.5f, _scale * 0.7f, 1f);
            // 잔광 스프라이트는 가운데가 원점이라 그대로 두면 절반이 머리 앞으로 튀어나온다.
            // 뒤로 반 칸 밀어 꼬리로만 보이게 한다.
            float streakLength = ProjectileShapes.Streak().bounds.size.x * _scale * 1.5f;
            _streak.transform.localPosition = new Vector3(-streakLength * 0.5f, 0f, 0f);

            _head = NewRenderer("Head", ProjectileShapes.Sliver(), color, 1f, OverlaySortingOrder + 1);
            _head.transform.localScale = style == ProjectileVisualStyle.Bolt
                ? Vector3.one * _scale
                : new Vector3(_scale * 1.18f, _scale * 0.48f, 1f);

            if (style != ProjectileVisualStyle.Bolt)
            {
                // 긴 몸통 뒤에 두 개의 깃을 붙여 작은 화면에서도 '화살' 실루엣이 읽히게 한다.
                Sprite fletching = ProjectileShapes.Shard(42, 8);
                _upperFletching = NewRenderer("UpperFletching", fletching, color, 0.9f,
                    OverlaySortingOrder + 1);
                _lowerFletching = NewRenderer("LowerFletching", fletching, color, 0.9f,
                    OverlaySortingOrder + 1);
                float rear = -ProjectileShapes.Sliver().bounds.size.x * _scale * 0.42f;
                _upperFletching.transform.localPosition = new Vector3(rear, _scale * 0.10f, 0f);
                _lowerFletching.transform.localPosition = new Vector3(rear, -_scale * 0.10f, 0f);
                _upperFletching.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
                _lowerFletching.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);

                if (style == ProjectileVisualStyle.UltimateArrow)
                {
                    _streak.transform.localScale = new Vector3(_scale * 2.15f, _scale * 0.92f, 1f);
                }
            }

            // 착탄 조각은 미리 만들어 두고 꺼 둔다. 터질 때 켜기만 하면 된다.
            _core = NewRenderer("Core", ProjectileShapes.Core(), Color.white, 0f, OverlaySortingOrder + 3);
            _core.enabled = false;

            for (int i = 0; i < ShardCount; i++)
            {
                _shards[i] = NewRenderer($"Shard{i}", ProjectileShapes.Shard(), color, 0f,
                    OverlaySortingOrder + 2);
                _shards[i].enabled = false;
                // 진행 방향을 중심으로 부채꼴로 흩어진다. 완전한 방사형보다 방향감이 산다.
                _shardAngles[i] = UnityEngine.Random.Range(-62f, 62f);
            }

            Aim();
        }

        private SpriteRenderer NewRenderer(string label, Sprite sprite, Color color, float alpha, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(color.r, color.g, color.b, alpha);
            renderer.sortingLayerID = _sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            if (_impact >= 0f)
            {
                AdvanceImpact();
                return;
            }

            // 유닛이 사라졌으면(사망 등) 마지막으로 알던 자리에서 터뜨린다.
            if (_to != null) _end = _to.transform.position;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            Vector3 previous = transform.position;
            transform.position = PointAt(t);
            Aim(transform.position - previous);

            if (t < 1f) return;

            BeginImpact();
        }

        /// <summary>진행도에 따른 위치. 직선은 도착이 빨라지도록 가속을 먹인다.</summary>
        private Vector3 PointAt(float t)
        {
            if (_path == ProjectilePathType.ParabolicArc)
            {
                Vector3 flat = Vector3.Lerp(_start, _end, t);
                // 가운데에서 가장 높이 뜬다.
                flat.y += _data.arcHeight * 4f * t * (1f - t);
                return flat;
            }

            float eased = t * t * (3f - 2f * t) * 0.35f + t * t * 0.65f;
            return Vector3.Lerp(_start, _end, eased);
        }

        private void Aim(Vector3 direction = default)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = _end - _start;
            if (direction.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void BeginImpact()
        {
            _impact = 0f;
            NotifyImpact();

            _head.enabled = false;
            _streak.enabled = false;
            if (_upperFletching != null) _upperFletching.enabled = false;
            if (_lowerFletching != null) _lowerFletching.enabled = false;

            _core.enabled = true;
            for (int i = 0; i < ShardCount; i++) _shards[i].enabled = true;

            // 착탄 지점에서 회전을 풀어 파편이 화면 기준으로 흩어지게 한다.
            Vector3 direction = _end - _start;
            float baseAngle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;

            transform.position = _end;
            transform.rotation = Quaternion.identity;

            for (int i = 0; i < ShardCount; i++)
            {
                _shards[i].transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + _shardAngles[i]);
            }
        }

        private void AdvanceImpact()
        {
            _impact += Time.deltaTime;
            float t = Mathf.Clamp01(_impact / ImpactTime);

            if (t >= 1f)
            {
                Live.Remove(this);
                Destroy(gameObject);
                return;
            }

            // 핵은 아주 짧게 번쩍이고 사라진다 — 오래 남으면 뿌옇게 보인다.
            float coreFade = Mathf.Clamp01(1f - t * 3f);
            Color coreColor = _core.color;
            coreColor.a = coreFade;
            _core.color = coreColor;
            _core.transform.localScale = Vector3.one * _scale * (0.6f + 1.5f * t);

            // 파편은 밖으로 뻗으며 잦아든다.
            float reach = _scale * (0.35f + 2.2f * t);
            float fade = Mathf.Clamp01(1f - t);
            for (int i = 0; i < ShardCount; i++)
            {
                SpriteRenderer shard = _shards[i];
                Color color = shard.color;
                color.a = fade * fade;
                shard.color = color;

                shard.transform.localPosition =
                    shard.transform.localRotation * new Vector3(reach, 0f, 0f);
                shard.transform.localScale = new Vector3(_scale * (0.8f + t), _scale * fade, 1f);
            }
        }

        private void OnDestroy() => Live.Remove(this);
    }
}
