using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Effects.Vfx;
using Helpers;
using Managers;
using Managers.Stage;
using UnityEngine;

namespace Entities.View
{
    /// <summary>칸·Unit·행동을 만들지 않는 Stage 소환수의 표시. 시간은 기존 콜백을 관측할 뿐이다.</summary>
    public sealed class StageSummonVisual : MonoBehaviour
    {
        public static event Action<StageSummonVisual, string> MotionObserved;
        private static readonly Dictionary<Unit, float> DelayedSpawns = new();
        private readonly Sprite[] _frames = new Sprite[5];
        private Unit _owner, _summon;
        private SpriteRenderer _renderer;
        private SdFrameData _data;
        private SummonStageData _placement;
        private StageFigureDataList _catalog;
        private readonly Vector3[] _homes = new Vector3[3];
        private readonly float[] _widths = new float[3];
        private readonly float[] _heights = new float[3];
        private readonly Vector2[] _centers = new Vector2[3];
        private float _companionScale = 1f;
        public float CompanionScale => _companionScale;
        public float RearClearanceX { get; private set; }
        private static readonly Dictionary<Sprite, Rect> FrontBodyRects = new();
        public Bounds PlacementBounds
        {
            get
            {
                Rect rect = FrontBodyRect(_frames[0]);
                var sprite = _renderer.sprite;
                float x0 = rect.xMin, x1 = rect.xMax;
                if (_renderer.flipX) { x0 = 1f - rect.xMax; x1 = 1f - rect.xMin; }
                var bounds = new Bounds(_renderer.transform.TransformPoint(sprite.bounds.min + Vector3.Scale(sprite.bounds.size, new Vector3(x0, rect.yMin, 0))), Vector3.zero);
                bounds.Encapsulate(_renderer.transform.TransformPoint(sprite.bounds.min + Vector3.Scale(sprite.bounds.size, new Vector3(x1, rect.yMax, 0))));
                return bounds;
            }
        }
        public const int SpiritSortingOrder = 20000;
        private bool _surround, _acting;
        private float _castAt, _hitUntil = -1;
        private bool _fallback, _detached, _retiring, _guard, _rain, _flying, _returning, _swarm;
        private bool _dashing, _arrived, _unfolded, _reslotting, _returnStarted;
        private string _kind;
        private float _born, _motionAt, _windup, _duration, _arc, _height, _slotAt;
        private int _index = -1;
        private Vector3 _from, _to, _slotFrom;
        private Action<EventContext> _roundEnded;
        public SdFrameRig.Pose Pose { get; private set; }
        public Unit Owner => _owner;
        public Unit Summon => _summon;
        public SpriteRenderer Renderer => _renderer;
        public string Kind => _kind;
        public bool Detached => _detached;
        public bool IsSwarm => _swarm;
        public bool IsSurround => _surround;
        public bool IsFrontCompanion => !_surround && _placement.front > 0f;
        public bool DrawsInFront => _surround || IsFrontCompanion || _placement.drawInFront;
        public float Height => _height;
        public Vector3 Feet => transform.position;
        public float Depth => _owner != null && _owner.currentCell != null ? _owner.currentCell.transform.lossyScale.y : 1f;
        private float Facing => _owner != null && _owner.IsEnemy ? -1f : 1f;
        private float Px => StageLayout.WorldHeight / StageLayout.HeightPx;
        private UnitCardView OwnerView => CardMotion.ViewOf(_owner);
        private bool Stage => VisualModeState.Current == VisualMode.Stage;

        public static StageSummonVisual Create(Unit owner, Unit summon, string kind)
        {
            var visual = new GameObject("StageSummon_" + kind).AddComponent<StageSummonVisual>();
            visual.transform.SetParent(GridManager.Instance.Field, false);
            visual._owner = owner; visual._summon = summon; visual._kind = kind;
            visual._swarm = Summons.StyleOf(owner) == SummonStyle.Swarm;
            visual._surround = visual._swarm || Summons.StyleOf(owner) == SummonStyle.Cohort;
            visual._roundEnded = _ => Destroy(visual.gameObject);
            owner.AddListener(BaseEnums.UnitEventType.OnRoundEnd, visual._roundEnded);
            visual._born = Time.time;
            if (kind == "uchi_clone" && DelayedSpawns.Remove(owner, out float spawnAt)) visual._born = spawnAt;
            var catalog = DataManager.LoadStageFigureDataList();
            visual._catalog = catalog;
            var profile = catalog.figures.SingleOrDefault(f => f.sprite == kind) ??
                (visual._swarm ? catalog.figures.Single(f => f.sprite == "uchi_clone") : null);
            if (profile == null) throw new InvalidOperationException("소환수 Stage 프로필 누락: " + kind);
            visual._data = profile.sd;
            visual._placement = profile.summon ?? catalog.swarm;
            for (int i = 0; i < 5; i++) visual._frames[i] = Resources.Load<Sprite>($"Sprite/SD/{visual._data.prefix}_{(SdFrameRig.Pose)i}");
            visual._fallback = visual._frames.Any(f => f == null);
            if (visual._fallback)
            {
                Sprite source = SpriteResource.LoadStanding(visual._data.fallbackSprite);
                if (source == null) throw new InvalidOperationException("소환수 SD/임시 원화 누락: " + kind);
                for (int i = 0; i < 5; i++) visual._frames[i] = source;
            }
            visual._renderer = new GameObject("Figure").AddComponent<SpriteRenderer>();
            visual._renderer.transform.SetParent(visual.transform, false);
            visual.SetPose(SdFrameRig.Pose.IDLE);
            visual.Refresh(); visual.Trace("spawn_start");
            return visual;
        }

        public static StageSummonVisual Sirius(Unit owner) => FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None)
            .FirstOrDefault(v => v._owner == owner && v._kind == "sirius");

        public Vector3 Home(int index)
        {
            var figure = OwnerView?.StageProxy;
            Vector3 feet = figure != null ? figure.transform.position : _owner.transform.position;
            if (!_surround)
            {
                Vector3 nominal = feet + new Vector3((_placement.front - _placement.back) * Facing,
                    _placement.up + _placement.hover, 0) * (Px * Depth);
                if (!IsFrontCompanion) return nominal;
                return FrontCompanionHome();
            }
            var siblings = Summons.Of(_owner);
            var camera = Camera.main;
            for (int i = 0; i < 3; i++)
            {
                var profile = i < siblings.Count ? _catalog.figures.FirstOrDefault(p => p.sprite == siblings[i].SummonKind) : null;
                var data = profile?.sd ?? _data;
                var placement = profile?.summon ?? _placement;
                float height = (figure != null ? figure.BodyWorldHeight : _height / _placement.size) * placement.size;
                float pixel = height / (data.feetY - data.topY);
                // FullRect SD 표시 박스 전체를 보호하므로 투명 여백/비대칭 얼굴 기준도 화면 밖으로 잘리지 않는다.
                _widths[i] = 1024f * pixel;
                _heights[i] = height;
                float centerOffset = (512f - data.faceX) * pixel * Facing;
                _centers[i] = new Vector2(centerOffset, height * .5f);
                var slot = placement.slots[i];
                Vector3 nominal = feet + new Vector3(slot.x * Facing, placement.hover - slot.y, 0) * (Px * Depth);
                _homes[i] = ClampHome(nominal, _widths[i], centerOffset, camera);
                bool pulled = Mathf.Abs(_homes[i].x - nominal.x) > .000001f;
                bool horizontalClear = true;
                for (int j = 0; j < i; j++)
                    horizontalClear &= Mathf.Abs(_homes[i].x + _centers[i].x - _homes[j].x - _centers[j].x) >= .6f * Mathf.Max(_widths[i], _widths[j]);
                if ((pulled && !horizontalClear) || CrowdedHome(i, _homes[i]))
                {
                    var alternative = feet + new Vector3(215f * Facing, placement.hover + 24f, 0) * (Px * Depth);
                    var resolved = ClampHome(alternative, _widths[i], centerOffset, camera);
                    bool blocked = Mathf.Abs(resolved.x - alternative.x) > .000001f || CrowdedHome(i, resolved);
                    _homes[i] = blocked
                        ? ClampHome(feet + new Vector3(215f * Facing, placement.hover - 40f, 0) * (Px * Depth), _widths[i], centerOffset, camera)
                        : resolved;
                }
            }
            return _homes[Mathf.Clamp(index, 0, 2)];
        }

        private Vector3 FrontCompanionHome()
        {
            var companions = FindObjectsByType<StageSummonVisual>()
                .Where(v => v._owner != null && v._owner.IsOnField && v._owner.IsEnemy == _owner.IsEnemy &&
                    v.IsFrontCompanion && !v._detached && !v._retiring)
                .OrderByDescending(v => v.OwnerView.StageProxy.transform.position.y).ThenBy(v => v.Owner.ID).ToArray();
            var points = new Vector3[companions.Length];
            var lowered = new bool[companions.Length];
            for (int i = 0; i < companions.Length; i++)
            {
                companions[i]._companionScale = 1f;
                companions[i].RearClearanceX = 0f;
                points[i] = companions[i].FrontPoint(false);
            }
            // 위 칸 주인만 아래로 옮긴다. 얼굴을 피해 위로 올리는 경로는 없다.
            for (int pass = 0; pass < companions.Length; pass++)
                for (int i = 0; i < companions.Length; i++)
                    for (int j = i + 1; j < companions.Length; j++)
                    {
                        if (!FrontCrowded(companions[i], points[i], companions[j], points[j])) continue;
                        lowered[i] = true;
                        points[i] = companions[i].FrontPoint(true);
                        if (FrontCrowded(companions[i], points[i], companions[j], points[j]))
                        {
                            companions[i]._companionScale = .9f;
                            points[i] = companions[i].FrontPoint(true);
                        }
                    }
            for (int i = 0; i < companions.Length; i++)
                if (companions[i].FrontFaceCrowded(points[i]))
                {
                    // 아래 칸 얼굴의 앞 여유도 확보한다. 위 칸 동반체만 허용된 0.9배로 줄인다.
                    companions[i]._companionScale = .9f;
                    points[i] = companions[i].FrontPoint(lowered[i]);
                }
            // 뒤 배치를 복원한 펜리르는 고정한다. 앞 동반체가 닿을 때만 기존 아래 보정을 적용한다.
            var rearCompanions = FindObjectsByType<StageSummonVisual>().Where(v => v._owner != null &&
                v._owner.IsOnField && v._owner.IsEnemy == _owner.IsEnemy && !v._surround &&
                !v.IsFrontCompanion && !v._detached && !v._retiring).ToArray();
            foreach (var rear in rearCompanions)
            {
                rear._height = rear.OwnerView.StageProxy.BodyWorldHeight * rear._placement.size;
                Vector3 rearPoint = rear.OwnerView.StageProxy.transform.position + new Vector3(
                    (rear._placement.front - rear._placement.back) * rear.Facing,
                    rear._placement.up + rear._placement.hover, 0) * rear.Px * rear.Depth;
                for (int i = 0; i < companions.Length; i++)
                {
                    if (!FrontCrowded(companions[i], points[i], rear, rearPoint)) continue;
                    lowered[i] = true;
                    points[i] = companions[i].FrontPoint(true);
                    if (FrontCrowded(companions[i], points[i], rear, rearPoint))
                    {
                        companions[i]._companionScale = .9f;
                        points[i] = companions[i].FrontPoint(true);
                    }
                    if (FrontCrowded(companions[i], points[i], rear, rearPoint))
                    {
                        var companion = companions[i];
                        float a = companion._height / (companion._data.feetY - companion._data.topY);
                        float b = rear._height / (rear._data.feetY - rear._data.topY);
                        Rect ar = FrontBodyRect(companion._frames[0]), br = FrontBodyRect(rear._frames[0]);
                        float center = points[i].x + (ar.center.x * 1024f - companion._data.faceX) * a * companion.Facing;
                        float rearCenter = rearPoint.x + (br.center.x * 1024f - rear._data.faceX) * b * rear.Facing;
                        float gap = .6f * 1024f * Mathf.Max(a * ar.width, b * br.width) + .001f;
                        Vector3 clear = points[i];
                        clear.x += Mathf.Sign(center - rearCenter) * (gap - Mathf.Abs(center - rearCenter));
                        clear = companion.ClampHome(clear, ar.width * 1024f * a,
                            (ar.center.x * 1024f - companion._data.faceX) * a * companion.Facing, Camera.main);
                        // 주인 앞 배치는 유지한다. 뒤 동반체와의 최소 몸 간격만 더 확보한다.
                        if ((clear.x - companion.OwnerView.StageProxy.transform.position.x) * companion.Facing > 0f)
                        {
                            companion.RearClearanceX = clear.x - points[i].x;
                            points[i] = clear;
                        }
                    }
                }
            }
            for (int i = 0; i < companions.Length; i++)
                if (companions[i] == this) return points[i];
            return FrontPoint(false);
        }

        private Vector3 FrontPoint(bool lowered)
        {
            var figure = OwnerView.StageProxy;
            float body = figure.BodyWorldHeight;
            _height = body * _placement.size * _companionScale;
            float pixel = _height / (_data.feetY - _data.topY);
            Rect rect = FrontBodyRect(_frames[0]);
            float width = rect.width * 1024f * pixel, offset = (rect.center.x * 1024f - _data.faceX) * pixel * Facing;
            Vector3 feet = figure.transform.position;
            float up = lowered ? -body * .35f : 10f * Px * Depth;
            if (!lowered && _companionScale < 1f) up = body * .5f - _height;
            // 허리는 발~머리 높이의 중간. 큰 동반체도 머리 윗선이 허리 위로 오르지 않는다.
            up = Mathf.Min(up, body * .5f - _height);
            Vector3 point = feet + new Vector3(150f * Px * Depth * Facing, up, 0);
            return ClampHome(point, width, offset, Camera.main);
        }

        private static bool FrontCrowded(StageSummonVisual a, Vector3 pa, StageSummonVisual b, Vector3 pb)
        {
            float ax = a._height / (a._data.feetY - a._data.topY), bx = b._height / (b._data.feetY - b._data.topY);
            Rect ar = FrontBodyRect(a._frames[0]), br = FrontBodyRect(b._frames[0]);
            return Mathf.Abs(pa.x + (ar.center.x * 1024f - a._data.faceX) * ax * a.Facing - pb.x - (br.center.x * 1024f - b._data.faceX) * bx * b.Facing) < .6f * 1024f * Mathf.Max(ax * ar.width, bx * br.width) &&
                Mathf.Abs(pa.y + a._height * .5f - pb.y - b._height * .5f) < .5f * Mathf.Max(a._height, b._height);
        }

        private bool FrontFaceCrowded(Vector3 point)
        {
            float pixel = _height / (_data.feetY - _data.topY);
            Rect rect = FrontBodyRect(_frames[0]);
            float center = point.x + (rect.center.x * 1024f - _data.faceX) * pixel * Facing;
            float half = rect.width * 1024f * pixel * .5f;
            float ownerY = OwnerView.StageProxy.transform.position.y;
            var allies = _owner.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            foreach (var ally in allies)
            {
                if (ally == _owner || !ally.IsOnField || ally.currentCell == null) continue;
                var figure = ally.currentCell.Card.StageProxy;
                if (figure.transform.position.y >= ownerY) continue;
                Vector3 face = figure.Frames.HeadWorld;
                float body = figure.BodyWorldHeight;
                if (Mathf.Abs(center - face.x) < half + body * .16f &&
                    point.y < face.y + body * .14f && point.y + _height > face.y - body * .14f) return true;
            }
            return false;
        }

        private static Rect FrontBodyRect(Sprite sprite)
        {
            if (FrontBodyRects.TryGetValue(sprite, out Rect cached)) return cached;
            int width = Mathf.RoundToInt(sprite.rect.width), height = Mathf.RoundToInt(sprite.rect.height);
            var target = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            var prior = RenderTexture.active;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(sprite.texture, target); RenderTexture.active = target;
                copy.ReadPixels(sprite.rect, 0, 0, false);
                var pixels = copy.GetPixels32();
                int left = width, right = -1, bottom = height, top = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a >= 129)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                // 압축 노이즈를 제외한 실제 몸 경계. 텍스처·메시·알파 자체는 바꾸지 않는다.
                cached = right >= left ? Rect.MinMaxRect(left / (float)width, bottom / (float)height, (right + 1f) / width, (top + 1f) / height) : new Rect(0, 0, 1, 1);
                FrontBodyRects[sprite] = cached;
                return cached;
            }
            finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(target); Destroy(copy); }
        }

        private bool CrowdedHome(int index, Vector3 point)
        {
            for (int j = 0; j < index; j++)
            {
                Vector2 delta = (Vector2)(point - _homes[j]) + _centers[index] - _centers[j];
                if (Mathf.Abs(delta.x) < .6f * Mathf.Max(_widths[index], _widths[j]) &&
                    Mathf.Abs(delta.y) < .5f * Mathf.Max(_heights[index], _heights[j])) return true;
            }
            return false;
        }

        private Vector3 ClampHome(Vector3 point, float width, float centerOffset, Camera camera)
        {
            if (camera == null) return point;
            float depth = camera.WorldToViewportPoint(Vector3.zero).z;
            float px = camera.orthographicSize * 2f / camera.pixelHeight;
            float left = camera.ViewportToWorldPoint(new Vector3(0,.5f,depth)).x + width * .5f + (Facing > 0 ? 140f : 24f) * px - centerOffset;
            float right = camera.ViewportToWorldPoint(new Vector3(1,.5f,depth)).x - width * .5f - (Facing < 0 ? 140f : 24f) * px - centerOffset;
            float middle = camera.ViewportToWorldPoint(new Vector3(.5f,.5f,depth)).x;
            if (Facing > 0) right = Mathf.Min(right, middle - width * .5f - centerOffset);
            else left = Mathf.Max(left, middle + width * .5f - centerOffset);
            point.x = Mathf.Clamp(point.x,left,right);
            return point;
        }

        public Vector3 TargetFront(Unit target, float stop)
        {
            var figure = CardMotion.ViewOf(target)?.StageProxy;
            if (figure == null) return Feet;
            float depth = target.currentCell.transform.lossyScale.y;
            return figure.transform.position + Vector3.left * (Facing * stop * Px * depth);
        }

        public void BeginAdvance(Unit target, bool fenrir = false, float stagger = 0f)
        {
            _motionAt = Time.time + stagger; _from = Feet;
            _windup = fenrir ? .03f : .08f;
            _duration = fenrir ? .10f : .24f - stagger;
            _arc = fenrir ? 40f * Px * Depth : 0f;
            _to = TargetFront(target, fenrir ? 150f : 130f);
            _flying = true; _returning = false;
            _dashing = _arrived = _returnStarted = false;
            SetPose(SdFrameRig.Pose.WINDUP); Trace("windup");
        }

        public void Strike()
        {
            if (_retiring) return;
            if (_flying) { transform.position = _to; _flying = false; }
            SetPose(SdFrameRig.Pose.STRIKE); Trace("strike");
            if (_kind == "fenrir") { _returning = true; _motionAt = Time.time; _from = Feet; }
        }

        public void Overflow()
        {
            DelayedSpawns[_owner] = Time.time + .24f;
            transform.position += Vector3.right * (Facing * _height * .12f);
            SetPose(SdFrameRig.Pose.STRIKE); Trace("overflow_strike");
        }

        public void Guard()
        {
            _guard = _detached = true; _flying = _returning = false;
            _motionAt = Time.time; _from = Feet;
            _to = OwnerView.StageProxy.transform.position + Vector3.right * (Facing * 72f * Px * Depth);
            Trace("guard_start");
        }

        public void Depart()
        {
            if (_guard) return; // 실제 사망과 분리된 표시 수명이다.
            if (_kind != "uchi_clone" && _kind != "fenrir" && _summon != null)
            {
                _retiring = _detached = true; _acting = false;
                _motionAt = Time.time; SetPose(SdFrameRig.Pose.DOWN); Trace("down"); return;
            }
            if (_kind == "uchi_clone") Paper();
            Destroy(gameObject);
        }

        public void RetireSirius()
        {
            if (_retiring) { Destroy(gameObject); return; }
            if (_owner != null && (!_owner.isActive || _owner.HpCurr <= 0 || _owner.IsDying))
            { _retiring = true; _motionAt = Time.time; SetPose(SdFrameRig.Pose.DOWN); Trace("down"); }
            else Destroy(gameObject);
        }

        private void OnEnable()
        {
            SdFrameRig.PoseChanged += OwnerPose;
            ActionScheduler.AnyActionStarted += ActionStarted;
            ActionScheduler.AnyActionFinished += ActionFinished;
            Unit.AnyDamageIncoming += OnImpact;
            Unit.AnyDamageDealt += OnDamage;
        }
        private void OnDisable()
        {
            SdFrameRig.PoseChanged -= OwnerPose;
            ActionScheduler.AnyActionStarted -= ActionStarted;
            ActionScheduler.AnyActionFinished -= ActionFinished;
            Unit.AnyDamageIncoming -= OnImpact;
            Unit.AnyDamageDealt -= OnDamage;
        }
        private void OnDestroy()
        {
            if (_owner != null && _roundEnded != null) _owner.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEnded);
        }
        private void ActionStarted(Unit actor, ActionScheduler.ActionKind kind, string label)
        {
            if (actor == _summon && _kind != "uchi_clone" && _kind != "fenrir")
            {
                _acting = true; _castAt = Time.time;
                if (_hitUntil <= Time.time) { SetPose(SdFrameRig.Pose.WINDUP); Trace("windup"); }
            }
            if (_kind != "sirius" || actor != _owner) return;
            _rain = label == "시리우스 · 화살비";
            if (_rain) { SetPose(SdFrameRig.Pose.WINDUP); Trace("rain_windup"); }
        }
        private void ActionFinished(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (actor == _summon && _kind != "uchi_clone" && _kind != "fenrir")
            {
                _acting = false;
                if (!_retiring && _hitUntil <= Time.time) { SetPose(SdFrameRig.Pose.IDLE); Trace("idle"); }
            }
            if (_kind == "fenrir" && actor == _summon && _flying)
            { _flying = false; _returning = true; _motionAt = Time.time - .20f; _from = Feet; }
            if (_kind == "sirius" && actor == _owner) { _rain = false; if (!_retiring) SetPose(SdFrameRig.Pose.IDLE); }
        }
        private void OnImpact(Unit target, DamageContext context)
        {
            // 수호령 피해 귀속은 주인이다. 실제 실행 중인 소환수와 피해 순간을 함께 확인한다.
            if (_surround && !_swarm && _acting && !_retiring &&
                GameManager.Instance.ActionScheduler.ExecutingUnit == _summon && context.Attacker == _owner)
            { SetPose(SdFrameRig.Pose.STRIKE); Trace("strike"); }
        }
        private void OnDamage(DamageResolvedContext context)
        {
            if (context.Target != _summon || context.DamageDealt <= 0 || _retiring || _summon.IsUntargetable) return;
            _acting = false; _hitUntil = Time.time + .42f;
            SetPose(SdFrameRig.Pose.HIT); Trace("hit");
        }
        private void OwnerPose(SdFrameRig rig, SdFrameRig.Pose pose)
        {
            if (_kind != "sirius" || rig.BoundUnit != _owner) return;
            // 주인 피격은 동반체의 피격이 아니다. 화살비와 실제 사망만 공유한다.
            if (pose == SdFrameRig.Pose.DOWN || (_rain && pose is SdFrameRig.Pose.WINDUP or SdFrameRig.Pose.STRIKE) || pose == SdFrameRig.Pose.IDLE)
            { SetPose(pose); Trace("owner_" + pose.ToString().ToLowerInvariant()); }
        }

        private void LateUpdate()
        {
            if (_owner == null || (_summon == null && _kind != "sirius" && !_detached)) { Destroy(gameObject); return; }
            if (_retiring) { if (Time.time - _motionAt >= .42f) Destroy(gameObject); return; }
            if (Time.timeScale <= 0f)
            {
                _renderer.enabled = Stage && OwnerView?.StageProxy != null;
                return;
            }
            if (Time.timeScale > 0 && _kind == "thoth_scribe")
            {
                if (_hitUntil <= Time.time)
                    SetPose(_acting ? Time.time - _castAt < .14f ? SdFrameRig.Pose.WINDUP : SdFrameRig.Pose.STRIKE : SdFrameRig.Pose.IDLE);
            }
            if (_detached && (!_owner.isActive || GameManager.Instance.gameState != BaseClasses.BaseEnums.GameState.RoundInProgress))
            { Destroy(gameObject); return; }
            Refresh();
        }

        private void Refresh()
        {
            var view = OwnerView;
            bool visible = Stage && view?.StageProxy != null;
            _renderer.enabled = visible;
            if (!visible) return;
            int index = _summon != null ? Mathf.Max(0, Summons.Of(_owner).IndexOf(_summon)) : 0;
            _height = view.StageProxy.BodyWorldHeight * _placement.size * _companionScale;
            if (index != _index)
            {
                _reslotting = _index >= 0;
                _slotFrom = _index < 0 ? Home(index) : Feet; _slotAt = Time.time; _index = index;
                Trace("slot_" + index);
            }
            _renderer.sortingLayerID = view.PortraitRenderer.sortingLayerID;
            _renderer.sortingOrder = view.PortraitRenderer.sortingOrder + (_guard ? 1 : DrawsInFront ? 1 + index : -1 - index);
            if (_placement.aboveUnits)
            {
                _renderer.sortingLayerID = SortingLayer.NameToID("CombatHud");
                _renderer.sortingOrder = SpiritSortingOrder + index;
            }
            float age = Time.time - _motionAt;
            if (_guard)
            {
                transform.position = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0, 1, age / .12f));
                if (age >= .12f && Pose != SdFrameRig.Pose.HIT) { SetPose(SdFrameRig.Pose.HIT); Trace("guard_hit"); }
                if (age >= .37f) { Paper(); Destroy(gameObject); return; }
            }
            else if (_flying)
            {
                if (!_dashing && age >= _windup) { _dashing = true; Trace("dash_start"); }
                float t = Mathf.Clamp01((age - _windup) / (_duration - _windup));
                transform.position = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (_arc * Mathf.Sin(Mathf.PI * t));
                if (!_arrived && t >= 1) { _arrived = true; Trace("arrive"); }
            }
            else if (_returning)
            {
                if (!_returnStarted && age >= .20f) { _returnStarted = true; Trace("return_start"); }
                float t = Mathf.Clamp01((age - .20f) / .32f);
                transform.position = Vector3.Lerp(_from, Home(index), Mathf.SmoothStep(0, 1, t)) + Vector3.up * (50f * Px * Depth * Mathf.Sin(Mathf.PI * t));
                if (age >= .52f) { _returning = false; SetPose(SdFrameRig.Pose.IDLE); Trace("return_idle"); }
            }
            else transform.position = Vector3.Lerp(_slotFrom, Home(index), Mathf.SmoothStep(0, 1, (Time.time - _slotAt) / .24f));
            if (_reslotting && Time.time - _slotAt >= .24f) { _reslotting = false; Trace("reslot_end"); }
            float unfold = Mathf.SmoothStep(0, 1, (Time.time - _born) / .30f);
            _renderer.color = _swarm ? new Color(.88f, 1f, .96f, .88f * unfold)
                : new Color(1, 1, 1, (_surround ? 1f : .88f) * unfold);
            ApplyFrame(unfold);
            if (!_unfolded && Time.time - _born >= .30f) { _unfolded = true; Trace("spawn_end"); }
        }

        private void SetPose(SdFrameRig.Pose pose)
        {
            bool changed = Pose != pose;
            Pose = pose;
            if (_renderer != null) ApplyFrame(1f);
            if (changed && _renderer != null) Trace("pose_" + pose.ToString().ToLowerInvariant());
        }
        private void ApplyFrame(float unfold)
        {
            Sprite sprite = _frames[(int)Pose]; _renderer.sprite = sprite;
            _renderer.flipX = Facing < 0;
            float height = _height;
            if (_fallback)
            {
                float k = height / sprite.bounds.size.y;
                _renderer.transform.localScale = new Vector3(k, k * unfold, 1);
                _renderer.transform.localPosition = new Vector3(0, height * .5f * unfold, 0);
            }
            else
            {
                float px = height / (_data.feetY - _data.topY);
                _renderer.transform.localScale = new Vector3(1024f * px / sprite.bounds.size.x, 1280f * px * unfold / sprite.bounds.size.y, 1);
                _renderer.transform.localPosition = new Vector3((512 - _data.faceX) * px * Facing, (_data.feetY - 640) * px * unfold, 0);
            }
        }
        private void Paper()
        {
            UchiFx.PaperDeparture(_renderer.transform.position, _renderer.sprite, _renderer.transform.lossyScale,
                _renderer.color, _owner, _renderer.sortingLayerID, _renderer.sortingOrder, _renderer.flipX);
            Trace("depart");
        }
        private void Trace(string phase)
        {
            MotionObserved?.Invoke(this, phase);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise(FormattableString.Invariant($"stage_summon|{_kind}|{phase}|{_owner?.GetInstanceID()}|{Time.time:F6}|{Feet.x:F6}|{Feet.y:F6}|{Pose}|{_renderer?.sortingOrder}"));
#endif
        }
    }
}
