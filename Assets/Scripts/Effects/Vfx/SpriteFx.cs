using System.Collections;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 전투 배속이 짧은 연출 한 장을 통째로 건너뛰지 않게 하는 표시 시간 하한이다.
    /// 게임 판정 시간은 건드리지 않고, VFX가 사용하는 스케일된 초만 현재 배속의 프레임 수만큼 보장한다.
    /// </summary>
    public static class VfxTiming
    {
        private const float ReferenceFrame = 1f / 60f;
        private static int _captureClockFrame=-1;
        private static float _captureClock;
        private static bool _capturing;
        /// <summary>고정 프레임 검수에서는 PNG 저장의 벽시계 비용을 표시 수명에 섞지 않는다. 평상시에는 Unity 실시간 그대로다.</summary>
        public static float ReadableDeltaTime => Time.captureDeltaTime>0f?Time.captureDeltaTime:Time.unscaledDeltaTime;
        public static float ReadableTime
        {
            get
            {
                if(Time.captureDeltaTime<=0f){_capturing=false;return Time.unscaledTime;}
                if(!_capturing){_captureClock=Time.unscaledTime;_captureClockFrame=Time.frameCount;_capturing=true;}
                if(_captureClockFrame!=Time.frameCount){_captureClock+=ReadableDeltaTime*Mathf.Max(1,Time.frameCount-_captureClockFrame);_captureClockFrame=Time.frameCount;}
                return _captureClock;
            }
        }

        public static float AtLeastFrames(float authoredSeconds, int frameCount = 2)
        {
            float speed = Mathf.Clamp(Time.timeScale, 1f, 8f);
            return Mathf.Max(authoredSeconds, ReferenceFrame * speed * Mathf.Max(1, frameCount));
        }
    }

    /// <summary>
    /// 스프라이트 한 장의 짧은 연출 — 커지며(또는 줄며) 돌고, 나타났다 사라진다.
    ///
    /// <see cref="ImpactFx.Pulse"/>는 균일 배율 · 선형 사그라듦뿐이라 마법진이 펼쳐지는 모양,
    /// 바닥에 눌린 충격파(가로로 긴 타원), 튀어나왔다 가라앉는 문양처럼 <b>곡선이 필요한</b> 연출을 못 했다.
    /// 시간은 전투 시간을 따른다(배속 · 타격 정지가 그대로 걸린다).
    /// </summary>
    public sealed class SpriteFx : MonoBehaviour
    {
        public enum Ease { OutCubic, OutBack, Linear, InCubic }

        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _from;
        private Vector2 _to;
        private float _life;
        private float _age;
        private float _spin;
        private float _fadeIn;
        private float _fadeOutFrom;
        private Ease _ease;
        private Transform _follow;
        private Vector3 _followOffset;
        private bool _unscaledLife;

        /// <summary>
        /// 한 장을 띄운다. 크기는 월드 단위(가로 · 세로), 페이드는 수명 비율이다.
        /// <paramref name="fadeOutFrom"/> 이후로 서서히 사라진다.
        /// </summary>
        public static SpriteFx Play(Sprite sprite, Vector3 position, Color color, bool additive,
            Vector2 fromSize, Vector2 toSize, float life, float rotation = 0f, float spin = 0f,
            float fadeIn = 0.08f, float fadeOutFrom = 0.45f, Ease ease = Ease.OutCubic,
            int sortingLayer = 0, int order = VfxSpace.OverlaySortingOrder + 10, bool preserveInk = false)
        {
            if (sprite == null) return null;
            var go = new GameObject("SpriteFx");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            var fx = go.AddComponent<SpriteFx>();
            fx._renderer = VfxSpace.NewSprite(go.transform, "Sprite", sprite, Color.clear, sortingLayer, order, additive);
            // 작은 기호의 먹선은 파스텔 몸체와 별개다. 명시한 비가산 받침만 원래 군청 대비를 보존한다.
            fx._color = additive ? BattleVfxStyle.Light(color) : preserveInk ? color : BattleVfxStyle.Body(color, color);
            fx._from = fromSize;
            fx._to = toSize;
            // 8배속에서도 생성 프레임 하나로 끝나지 않아 코어와 비가산 외곽을 함께 읽을 수 있다.
            fx._life = VfxTiming.AtLeastFrames(Mathf.Max(0.01f, life), 2);
            fx._spin = spin;
            fx._fadeIn = Mathf.Clamp01(fadeIn);
            fx._fadeOutFrom = Mathf.Clamp01(fadeOutFrom);
            fx._ease = ease;
            fx.Apply(0f);
            return fx;
        }

        /// <summary>균일 크기 편의판.</summary>
        public static SpriteFx Play(Sprite sprite, Vector3 position, Color color, bool additive,
            float fromSize, float toSize, float life, float rotation = 0f, float spin = 0f,
            float fadeIn = 0.08f, float fadeOutFrom = 0.45f, Ease ease = Ease.OutCubic,
            int sortingLayer = 0, int order = VfxSpace.OverlaySortingOrder + 10, bool preserveInk = false)
            => Play(sprite, position, color, additive, Vector2.one * fromSize, Vector2.one * toSize, life,
                rotation, spin, fadeIn, fadeOutFrom, ease, sortingLayer, order, preserveInk);

        /// <summary>대상(카드)을 따라다닌다. 카드가 흔들리거나 날아가도 표식이 떨어지지 않는다.</summary>
        public SpriteFx Follow(Transform target)
        {
            _follow = target;
            _followOffset = target != null ? transform.position - target.position : Vector3.zero;
            return this;
        }

        /// <summary>배속이 개입 도중 바뀌어도 짧은 의미 신호를 읽을 시간을 보장한다. 일시정지는 따른다.</summary>
        public SpriteFx HoldUnscaled(float seconds)
        {
            _unscaledLife=true;_life=Mathf.Max(.01f,seconds);return this;
        }

        private void Update()
        {
            if(_unscaledLife&&Time.timeScale<=0f)return;
            _age += _unscaledLife?VfxTiming.ReadableDeltaTime:Time.deltaTime;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }
            Apply(_age / _life);
        }

        private void Apply(float t)
        {
            if (_follow != null) transform.position = _follow.position + _followOffset;
            float k = _ease switch
            {
                Ease.OutBack => OutBack(t),
                Ease.Linear => t,
                Ease.InCubic => t * t * t,
                _ => 1f - (1f - t) * (1f - t) * (1f - t),
            };
            Vector2 size = Vector2.LerpUnclamped(_from, _to, k);
            _renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (_spin != 0f) transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            float alpha = _fadeIn > 0f ? Mathf.Clamp01(t / _fadeIn) : 1f;
            if (t > _fadeOutFrom) alpha *= 1f - Mathf.Clamp01((t - _fadeOutFrom) / Mathf.Max(0.001f, 1f - _fadeOutFrom));
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }

    /// <summary>
    /// 호출한 쪽이 코루틴을 돌릴 MonoBehaviour를 갖고 있지 않을 때(정적 연출 함수) 대신 돌린다.
    /// 씬이 바뀌면 함께 사라져 진행 중이던 연출도 걷힌다.
    /// </summary>
    public sealed class VfxRunner : MonoBehaviour
    {
        private static VfxRunner _instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (routine == null) return null;
            if (_instance == null) _instance = new GameObject("VfxRunner").AddComponent<VfxRunner>();
            return _instance.StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
