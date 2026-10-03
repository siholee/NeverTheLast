using System.Collections.Generic;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>파티클 한 번 뿌리기의 모양. 값은 전부 월드 단위 · 초 · 도(°)다.</summary>
    public struct ParticleSpec
    {
        public Sprite Sprite;
        /// <summary>빛(섬광 · 불티)이면 가산, 물체(돌 · 먼지)면 알파로 그린다.</summary>
        public bool Additive;
        public Color StartColor;
        public Color EndColor;
        public Vector2 Lifetime;
        public Vector2 Speed;
        /// <summary>뿜는 방향의 중심 각도(0 = 오른쪽, 90 = 위).</summary>
        public float Angle;
        /// <summary>중심 각도에서 좌우로 퍼지는 폭. 180이면 사방이다.</summary>
        public float Spread;
        public Vector2 StartSize;
        /// <summary>수명이 끝날 때의 크기 배율.</summary>
        public float EndSizeScale;
        /// <summary>아래로 당기는 힘(월드/초²).</summary>
        public float Gravity;
        /// <summary>속도 감쇠(초당 비율). 3이면 1초에 거의 멈춘다.</summary>
        public float Drag;
        /// <summary>회전 속도 범위(도/초).</summary>
        public Vector2 Spin;
        /// <summary>진행 방향으로 늘여 그린다(불티 · 잔상). 0이면 늘이지 않는다.</summary>
        public float Stretch;
        /// <summary>뿜는 자리를 이 반지름 안에서 흩는다.</summary>
        public float SpawnRadius;
        /// <summary>수명 앞쪽에서 서서히 나타나는 비율(0~1). 0이면 처음부터 다 보인다.</summary>
        public float FadeIn;
    }

    /// <summary>
    /// 스프라이트 렌더러 풀로 도는 가벼운 파티클.
    ///
    /// 유니티 ParticleSystem을 코드로 조립하면 텍스처 시트 · 재질 · 정렬을 맞추느라 설정이 길어지고
    /// 빌드마다 셰이더 누락을 걱정해야 한다. 전투 연출에 필요한 것은 "뿜고 · 떨어지고 · 사그라드는"
    /// 몇 가지뿐이라 직접 굴린다. 시간은 전투 시간(<see cref="Time.deltaTime"/>)을 따른다 —
    /// 배속과 타격 정지(HitStop)가 연출에도 그대로 걸린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxParticles : MonoBehaviour
    {
        private const int MaxParticles = 640;

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public float EndSize;
            public float Rotation;
            public float Spin;
            public float Gravity;
            public float Drag;
            public float Stretch;
            public float FadeIn;
            public Color Start;
            public Color End;
        }

        private static VfxParticles _instance;
        private readonly List<Particle> _live = new();
        private readonly Stack<SpriteRenderer> _pool = new();

        private static VfxParticles Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("VfxParticles");
                _instance = go.AddComponent<VfxParticles>();
                return _instance;
            }
        }

        /// <summary><paramref name="count"/>개를 한꺼번에 뿜는다.</summary>
        public static void Burst(Vector3 position, ParticleSpec spec, int count, int sortingLayer, int order)
        {
            if (count <= 0 || spec.Sprite == null) return;
            Instance.Emit(position, spec, count, sortingLayer, order);
        }

        private void Emit(Vector3 origin, ParticleSpec spec, int count, int sortingLayer, int order)
        {
            for (int i = 0; i < count; i++)
            {
                if (_live.Count >= MaxParticles) Recycle(0);

                float angle = (spec.Angle + Random.Range(-spec.Spread, spec.Spread)) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector2 jitter = spec.SpawnRadius > 0f ? Random.insideUnitCircle * spec.SpawnRadius : Vector2.zero;
                float size = Random.Range(spec.StartSize.x, spec.StartSize.y);

                SpriteRenderer renderer = Rent();
                renderer.sprite = spec.Sprite;
                renderer.sharedMaterial = spec.Additive ? VfxSpace.Additive : VfxSpace.Alpha;
                renderer.sortingLayerID = VfxSpace.ResolveSortingLayer(sortingLayer);
                renderer.sortingOrder = order;

                _live.Add(new Particle
                {
                    Renderer = renderer,
                    Position = origin + (Vector3)jitter,
                    Velocity = direction * Random.Range(spec.Speed.x, spec.Speed.y),
                    Life = VfxTiming.AtLeastFrames(Mathf.Max(0.01f, Random.Range(spec.Lifetime.x, spec.Lifetime.y)), 2),
                    Size = size,
                    EndSize = size * (spec.EndSizeScale <= 0f ? 1f : spec.EndSizeScale),
                    Rotation = Random.Range(0f, 360f),
                    Spin = Random.Range(spec.Spin.x, spec.Spin.y) * (Random.value < 0.5f ? -1f : 1f),
                    Gravity = spec.Gravity,
                    Drag = spec.Drag,
                    Stretch = spec.Stretch,
                    FadeIn = Mathf.Clamp01(spec.FadeIn),
                    Start = spec.StartColor,
                    End = spec.EndColor,
                });
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Particle p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life || p.Renderer == null)
                {
                    Recycle(i);
                    continue;
                }

                p.Velocity.y -= p.Gravity * dt;
                if (p.Drag > 0f) p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Position += p.Velocity * dt;
                p.Rotation += p.Spin * dt;

                float t = p.Age / p.Life;
                float size = Mathf.Lerp(p.Size, p.EndSize, t);
                Color color = Color.Lerp(p.Start, p.End, t);
                if (p.FadeIn > 0f && t < p.FadeIn) color.a *= t / p.FadeIn;

                Transform tr = p.Renderer.transform;
                tr.position = p.Position;
                if (p.Stretch > 0f && p.Velocity.sqrMagnitude > 0.0001f)
                {
                    float heading = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg;
                    tr.rotation = Quaternion.Euler(0f, 0f, heading);
                    tr.localScale = new Vector3(size * (1f + p.Velocity.magnitude * p.Stretch), size, 1f);
                }
                else
                {
                    tr.rotation = Quaternion.Euler(0f, 0f, p.Rotation);
                    tr.localScale = new Vector3(size, size, 1f);
                }
                p.Renderer.color = color;
                _live[i] = p;
            }
        }

        private SpriteRenderer Rent()
        {
            SpriteRenderer renderer = _pool.Count > 0 ? _pool.Pop() : null;
            if (renderer == null)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
            }
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private void Recycle(int index)
        {
            SpriteRenderer renderer = _live[index].Renderer;
            _live.RemoveAt(index);
            if (renderer == null) return;
            renderer.gameObject.SetActive(false);
            _pool.Push(renderer);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
