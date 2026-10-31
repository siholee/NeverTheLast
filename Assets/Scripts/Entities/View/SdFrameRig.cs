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
        public static event Action<SdFrameRig, Pose> PoseChanged;
        private readonly Sprite[] _frames = new Sprite[5];
        private SpriteRenderer _renderer;
        private Unit _unit;
        private SdFrameData _data;
        private float _pixelWorld, _castAt = -1f, _hitUntil = -1f;
        private bool _acting, _down;
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

        public bool Configure(SdFrameData data, Unit unit, SpriteRenderer renderer, float height)
        {
            Ready = false;
            if (data == null) return false;
            bool changed = _data != data || _unit != unit || (_down && unit != null && unit.isActive && unit.HpCurr > 0);
            if (changed)
                for (int i = 0; i < _frames.Length; i++)
                {
                    _frames[i] = Resources.Load<Sprite>($"Sprite/SD/{data.prefix}_{(Pose)i}");
                    if (_frames[i] == null) return false;
                }
            _data = data; _unit = unit; _renderer = renderer;
            UnitId = unit != null ? unit.ID : 0;
            _pixelWorld = height / (data.feetY - data.topY);
            if (changed) { CurrentPose = Pose.IDLE; _acting = _down = false; _castAt = _hitUntil = -1f; }
            Ready = true;
            ApplyFrame();
            return true;
        }

        private Vector3 Point(float x, float y) => transform.TransformPoint(new Vector3(
            (x - _data.faceX) * _pixelWorld * (_unit != null && _unit.IsEnemy ? -1f : 1f),
            (_data.feetY - y) * _pixelWorld, 0f));

        // Center 피벗·임포트 축소 비율을 원본 캔버스 좌표로 환산한다. 프레임마다 알파 bounds로 재배치하지 않는다.
        private void ApplyFrame()
        {
            Sprite sprite = _frames[(int)CurrentPose];
            _renderer.sprite = sprite;
            bool flip = _unit != null && _unit.IsEnemy;
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
            if (!Ready) return;
            _down = true; _acting = false; _hitUntil = _castAt = -1f;
            SetPose(Pose.DOWN);
        }
        private void LateUpdate()
        {
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
}
