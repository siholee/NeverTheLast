using System;
using BaseClasses;
using Effects.Vfx;
using Managers;
using Managers.Stage;
using UnityEngine;

namespace Entities.View
{
    /// <summary>기존 행동/타격 사건만 관측한다. 콜백·판정·대기·원래 몸체 모션은 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class SdFrameRig : MonoBehaviour
    {
        public enum Pose { IDLE, WINDUP, STRIKE, HIT, DOWN }
        // 등록 아군 42명(호루스 제외)의 IDLE 실루엣 표시 높이 중앙값을 유지한다.
        public const float CrownHeightScale = .96299463f;
        public static event Action<SdFrameRig, Pose> PoseChanged;
        private readonly Sprite[] _frames = new Sprite[5];
        private SpriteRenderer _renderer;
        private Unit _unit;
        private SdFrameData _data;
        private float _pixelWorld, _castAt = -1f, _hitUntil = -1f;
        private bool _acting, _down, _flip;
        private float _downAt;
        public const float DownFadeDuration = .6f, DownFadeAlpha = .35f;
        public float DownOpacity => _data?.downFade == true && _down ? FadeOpacity(Time.time - _downAt) : 1f;
        internal static float FadeOpacity(float age) => Mathf.Lerp(1f, DownFadeAlpha, Mathf.Clamp01(age / DownFadeDuration));
        public Pose CurrentPose { get; private set; }
        public SpriteRenderer Renderer => _renderer;
        public Unit BoundUnit => _unit;
        public int UnitId { get; private set; }
        public SdFrameData Profile => _data;
        public bool Ready { get; private set; }
        public Vector3 FeetWorld => Point(_data.faceX, _data.feetY);
        public Vector3 HeadWorld => Point(_data.head.x, _data.head.y);
        public Vector3 ChestWorld => Point(_data.chest.x, _data.chest.y);
        public Vector3 HandWorld => Point(_data.hand.x, _data.hand.y);
        public Bounds HeadBounds
        {
            get
            {
                Vector3 center = HeadWorld;
                var bounds = new Bounds(center, Vector3.zero);
                float radius = Mathf.Max(1f, _data.head.y - _data.topY);
                foreach (float x in new[] { -radius, radius })
                    foreach (float y in new[] { -radius, radius })
                        bounds.Encapsulate(Point(_data.head.x + x, _data.head.y + y));
                return bounds;
            }
        }

        public bool Configure(SdFrameData data, Unit unit, SpriteRenderer renderer, float height, bool flip)
        {
            Ready = false;
            if (data == null) return false;
            bool resetFade = _data?.downFade == true || data.downFade;
            bool changed = _data != data || _unit != unit || (_down && unit != null && unit.isActive && unit.HpCurr > 0);
            if (changed)
                for (int i = 0; i < _frames.Length; i++)
                {
                    _frames[i] = Resources.Load<Sprite>($"Sprite/SD/{data.prefix}_{(Pose)i}");
                    if (_frames[i] == null) return false;
                }
            _data = data; _unit = unit; _renderer = renderer; _flip = flip;
            UnitId = unit != null ? unit.ID : 0;
            _pixelWorld = float.IsNaN(data.crownY)
                ? height / (data.feetY - data.topY)
                : height * CrownHeightScale / (data.feetY - data.crownY);
            if (changed)
            {
                CurrentPose = Pose.IDLE; _acting = _down = false; _castAt = _hitUntil = -1f;
                if (resetFade) { Color color = _renderer.color; color.a = 1f; _renderer.color = color; }
            }
            Ready = true;
            ApplyFrame();
            return true;
        }

        private Vector3 Point(float x, float y) => transform.TransformPoint(new Vector3(
            (x - _data.faceX) * _pixelWorld * (_flip ? -1f : 1f),
            (_data.feetY - y) * _pixelWorld, 0f));

        // Center 피벗·임포트 축소 비율을 원본 캔버스 좌표로 환산한다. 프레임마다 알파 bounds로 재배치하지 않는다.
        private void ApplyFrame()
        {
            Sprite sprite = _frames[(int)CurrentPose];
            _renderer.sprite = sprite;
            // 원본 좌향/우향에 따른 반전을 접점 앵커와 동일하게 적용한다.
            bool flip = _flip;
            _renderer.flipX = flip;
            float sx = 1024f * _pixelWorld / sprite.bounds.size.x;
            float sy = 1280f * _pixelWorld / sprite.bounds.size.y;
            _renderer.transform.localScale = new Vector3(sx, sy, 1f);
            _renderer.transform.localPosition = new Vector3((512f - _data.faceX) * _pixelWorld * (flip ? -1f : 1f),
                (_data.feetY - 640f) * _pixelWorld, 0f);
            _renderer.ResetLocalBounds();
            var material = _renderer.sharedMaterial;
            Rect rect = sprite.textureRect;
            material.SetVector("_SpriteUV", new Vector4(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
                rect.width / sprite.texture.width, rect.height / sprite.texture.height));
            material.SetVector("_ClipV", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_Head", 0f);
        }

        private void SetPose(Pose pose)
        {
            if (!Ready || CurrentPose == pose) return;
            CurrentPose = pose;
            ApplyFrame();
            PoseChanged?.Invoke(this, pose);
        }

        private void OnEnable()
        {
            ActionScheduler.AnyActionStarted += OnStarted;
            ActionScheduler.AnyActionFinished += OnFinished;
            Unit.AnyDamageIncoming += OnImpact;
            Unit.AnyDamageDealt += OnDamage;
            Unit.AnyUnitDied += OnDeath;
            VfxComposer.SlotPlayed += OnSlot;
        }
        private void OnDisable()
        {
            ActionScheduler.AnyActionStarted -= OnStarted;
            ActionScheduler.AnyActionFinished -= OnFinished;
            Unit.AnyDamageIncoming -= OnImpact;
            Unit.AnyDamageDealt -= OnDamage;
            Unit.AnyUnitDied -= OnDeath;
            VfxComposer.SlotPlayed -= OnSlot;
            _acting = false; _castAt = _hitUntil = -1f;
        }
        private bool CanPose => Ready && !_down && _unit != null && !_unit.isControlled;
        private void OnStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (unit != _unit || !CanPose) return;
            _acting = true; _castAt = Time.time;
            if (_hitUntil <= Time.time) SetPose(Pose.WINDUP);
        }
        private void OnFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (unit != _unit || !Ready) return;
            _acting = false; _castAt = -1f;
            if (!_down && _hitUntil <= Time.time) SetPose(Pose.IDLE);
        }
        private void OnImpact(Unit target, DamageContext context)
        {
            if (CanPose && _acting && context.Attacker == _unit && _hitUntil <= Time.time) SetPose(Pose.STRIKE);
        }
        private void OnSlot(VfxSlotPlaybackEvent e)
        {
            if (CanPose && _acting && e.Caster == _unit && e.SlotName == "hit" && _hitUntil <= Time.time)
                SetPose(Pose.STRIKE);
        }
        private void OnDamage(DamageResolvedContext context)
        {
            if (context.Target == _unit && context.DamageDealt > 0) ObserveDamage();
        }
        internal void ObserveDamage()
        {
            if (!CanPose) return;
            // 피격 후에는 대기로 돌아간다. 진행 중 행동의 판정/완료에는 관여하지 않는다.
            _acting = false; _castAt = -1f;
            _hitUntil = Time.time + .42f;
            SetPose(Pose.HIT);
        }
        private void OnDeath(Unit unit, Unit attacker) { if (unit == _unit) ObserveDeath(); }
        internal void ObserveDeath()
        {
            if (!Ready || _down) return;
            _down = true; _acting = false; _hitUntil = _castAt = -1f;
            _downAt = Time.time;
            SetPose(Pose.DOWN);
        }
        private void LateUpdate()
        {
            if (Ready && _data.downFade)
            {
                Color color = _renderer.color; color.a = DownOpacity; _renderer.color = color;
            }
            if (!Ready || _down || Time.timeScale <= 0f) return;
            if (_unit.isControlled)
            {
                _hitUntil = _castAt = -1f; _acting = false;
                SetPose(Pose.IDLE); return;
            }
            if (UltimateCutIn.IsShowing(_unit)) { SetPose(Pose.WINDUP); return; }
            if (_hitUntil > Time.time) return;
            // 마지막 타로부터 0.42초가 지나면 대기로 복귀한다.
            if (CurrentPose == Pose.HIT) { _hitUntil = -1f; SetPose(Pose.IDLE); return; }
            SetPose(_acting ? Time.time - _castAt < .14f ? Pose.WINDUP : Pose.STRIKE : Pose.IDLE);
        }
    }

    /// <summary>실제 사망으로 분리된 SD 표시만 유지한다. 전투 수명/완료 사건에는 관여하지 않는다.</summary>
    internal sealed class SdDownRemnant : MonoBehaviour
    {
        private Unit _unit;
        private SpriteRenderer _renderer;
        private float _started;
        private Action<EventContext> _roundEnded;
        internal Unit Owner => _unit;
        internal SpriteRenderer Renderer => _renderer;

        internal void Configure(Unit unit, SpriteRenderer renderer)
        {
            _unit = unit; _renderer = renderer; _started = Time.time;
            _roundEnded = _ => Destroy(gameObject);
            unit.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEnded);
        }

        private void LateUpdate()
        {
            if (_unit == null || (_unit.isActive && _unit.HpCurr > 0) ||
                GameManager.Instance == null || GameManager.Instance.gameState != BaseEnums.GameState.RoundInProgress)
            { Destroy(gameObject); return; }
            Color color = _renderer.color;
            color.a = SdFrameRig.FadeOpacity(Time.time - _started);
            _renderer.color = color;
        }

        private void OnDestroy()
        {
            if (_unit != null && _roundEnded != null)
                _unit.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEnded);
        }
    }
}
