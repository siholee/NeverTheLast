using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Ultimate;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 로키 — 특성 「풀려난 늑대」(Detail_08 §22.2). 동반형 소환자로 펜리르 1기와 함께 싸운다.
    ///
    /// 펜리르(소환수 902, 일반 501): 칸 없음 · 대상 불가 · 고정 속도 90으로 제 차례에 로키 공격력 60% 할퀴기.
    ///   생성 때 로키의 최대 체력 · 공격력 · 방어력 · 특수방어력 60%를 물려받는다(피해는 늘 로키 현재 공격력으로 계산).
    ///
    /// 사냥감 추격: 아군의 <b>실제 추가행동</b>이 사냥감(초월기 표식)에 직접 피해(&gt;0)를 주면 펜리르가 추가행동으로
    ///   로키 공격력 55% 물기를 한 번 예약한다. 한 행동이 여러 번 맞혀도 한 번, 펜리르 자신의 일반 차례 사이 최대 3번이다.
    ///   덧붙는 피해 · 지속피해 · 원소 반응 · 펜리르 자신의 공격은 방아쇠가 아니다.
    /// 자유 추격(초월기가 2 부여, 최대 2): 남아 있으면 <b>먼저</b> 쓰며, 행동 종류(일반 · 특수 · 궁극기)를 가리지 않고 추격을 연다.
    ///   자유 추격도 3번 상한 안에서만 쓴다.
    /// 예약은 수락 때 예산을 잡고, 실행 전에 취소되면 돌려준다. 정지 · 사망 · 라운드 종료에 표식 · 자원 · 구독 · 소환 규칙을 모두 걷는다.
    /// </summary>
    public sealed class LokiUnboundWolf : UniquePassiveCode, ISummonerPolicy
    {
        public const string FenrirKind = "loki_fenrir";
        public const int FenrirId = 902;
        public const float FenrirSpeed = 90f;
        public const float InheritRatio = 0.60f;
        public const float FollowUpRatio = 0.55f;
        public const int FollowUpCap = 3;
        public const int FlexMax = 2;
        public const string FlexResourceId = "loki_fenrir_flex";
        private const int KinshipStatusId = 70802;
        private const string KinshipKey = "loki_fenrir_kinship";

        public enum HuntEvent { Accepted, Fired, Cancelled, CapReached }

        /// <summary>추격 흐름(로키, 사냥감, 종류, 자유 추격 사용 여부). 검증 · 연출이 읽는다.</summary>
        public static event Action<Unit, Unit, HuntEvent, bool> AnyHunt;

        private sealed class Reservation
        {
            public int Window;
            public bool Flex;
            public bool Settled;
        }

        private readonly List<Reservation> _pending = new();
        private readonly HashSet<int> _handledActions = new();
        private Action<EventContext> _onDeath;
        private Unit _fenrir;
        private bool _subscribed;
        private int _window;
        private int _windowUsed;
        private int _serial;
        private int _lastStartedActionId;

        /// <summary>특성 추격의 공격 형태(물기 = 찌르기). 보상 풀이 "찌르기 공격이 있는가"를 이 코드에서 읽는다.</summary>
        public override int AttackForm => DamageTag.Pierce;

        public LokiUnboundWolf(PassiveCodeContext context) : base(context)
        {
            CodeName = "풀려난 늑대";
            CombatResourceLabels.Register(FlexResourceId, "자유 추격");
        }

        // ── 소환 규칙 ────────────────────────────────────────────

        public SummonStyle Style => SummonStyle.Companion;
        public int Cap => 1;

        /// <summary>펜리르 정의. 최대 체력은 만들 때의 로키 최대 체력 60%다.</summary>
        public SummonSpec Spec => new()
        {
            Id = FenrirId,
            Kind = FenrirKind,
            Name = "펜리르",
            Element = "Anemo",
            PortraitKey = "FENRIR_PORTRAIT",
            Hp = Mathf.Max(1f, Mathf.Round((Caster != null ? Caster.HpMax : 1) * InheritRatio)),
            Speed = FenrirSpeed,
            NormalCodeId = FenrirClaw.CodeId,
            Targetable = false,
        };

        /// <summary>동반형 1기 — 상한에서는 새로 만들지 않는다.</summary>
        public void OnOverflow(Unit owner, Unit oldest) { }

        // ── 조회 ─────────────────────────────────────────────────

        public Unit Fenrir => _fenrir != null && _fenrir.isActive ? _fenrir : null;
        public int Flex => Caster != null ? Caster.GetCombatResource(FlexResourceId) : 0;
        /// <summary>이번 펜리르 차례 구간에 잡힌 추격 수(예약 + 실행).</summary>
        public int WindowUsed => _windowUsed;
        public int PendingCount => _pending.Count(reservation => !reservation.Settled);
        public bool Subscribed => _subscribed;

        private bool Working => _subscribed && Caster != null && Caster.isActive && Caster.IsOnField;
        private LokiTwilight Ultimate => Caster?.ActiveUltimateCode as LokiTwilight;

        // ── 수명 ─────────────────────────────────────────────────

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            _subscribed = true;
            _pending.Clear();
            _handledActions.Clear();
            _window = 0;
            _windowUsed = 0;
            _lastStartedActionId = 0;
            Caster.SetCombatResourceMaximum(FlexResourceId, FlexMax, resetCurrent: true);
            Summons.Register(Caster, this);
            SummonFenrir();
            Unit.AnyDamageDealt += OnAnyDamageDealt;
            Unit.AnyUnitDied += OnAnyUnitDied;
            ActionScheduler.AnyActionStarted += OnAnyActionStarted;
            _onDeath = _ => Teardown(ownerDied: true);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            Ultimate?.InstallMark();
        }

        /// <summary>정지(라운드 종료 · 외부 정리). 펜리르는 사망 판정 없이 물러난다 — 아누비스 영혼 등 사망 보상을 주지 않는다.</summary>
        public override void StopCode() => Teardown(ownerDied: false);

        /// <summary>
        /// 표식 · 예약 · 자원 · 구독 · 소환 규칙을 걷는다. 로키가 실제로 쓰러진 경우에는 펜리르를 건드리지 않는다 —
        /// 이어지는 공통 사망 경로(<see cref="Summons"/>)가 펜리르의 실제 사망을 처리한다.
        /// </summary>
        private void Teardown(bool ownerDied)
        {
            Ultimate?.RemoveMark();
            if (!_subscribed) return;
            _subscribed = false;
            Unit.AnyDamageDealt -= OnAnyDamageDealt;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            ActionScheduler.AnyActionStarted -= OnAnyActionStarted;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _onDeath = null;
            foreach (Reservation reservation in _pending) reservation.Settled = true;
            _pending.Clear();
            _handledActions.Clear();
            _windowUsed = 0;
            if (Caster != null)
            {
                Caster.AddCombatResource(FlexResourceId, -Flex);
                Summons.Unregister(Caster);
            }
            if (!ownerDied) RetireFenrir();
            _fenrir = null;
        }

        /// <summary>
        /// 펜리르를 사망 판정 없이 전장에서 물린다(라운드 종료 정리와 같은 비사망 경로). 진행 중인 추격 · 할퀴기 코루틴을
        /// 멈추고 행동 순서에서 빠지게 한 뒤 소환수 목록에서 지운다. 다음 시작(CastCode)은 새 펜리르로 깨끗이 시작한다.
        /// </summary>
        private void RetireFenrir()
        {
            Unit fenrir = _fenrir;
            if (fenrir == null || !fenrir.isActive) return;
            fenrir.StopAllCoroutines();
            fenrir.isCasting = false;
            fenrir.DeactivateUnit();
            GridManager.Instance?.ForgetSummon(fenrir);
        }

        private void SummonFenrir()
        {
            // 혈맹(계승)은 생성 시점 값이다 — 상태 효과가 펜리르의 스탯 재계산 때만 로키 값을 다시 읽는다.
            Unit existing = Summons.Of(Caster).FirstOrDefault(unit => unit.SummonKind == FenrirKind);
            if (existing != null)
            {
                _fenrir = existing;
                return;
            }
            Unit fenrir = Summons.Create(Caster);
            if (fenrir == null) return;
            _fenrir = fenrir;
            UnitStatus kinship = BuffStatus.Create(KinshipStatusId, KinshipKey, "늑대의 혈맹", Caster, fenrir,
                new KinshipEffect(Caster), duration: 0, isBeneficial: true,
                description: "로키의 공격력 · 방어력 · 특수방어력 60%를 물려받는다.");
            kinship.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            fenrir.AddStatus(kinship);
            // 소환 경로의 표시 부착은 분신 전용이다. 칸 없는 소환수 카드를 같은 표시로 소환자 뒤에 세운다.
            if (fenrir.GetComponent<Entities.View.SummonCardView>() == null)
                fenrir.gameObject.AddComponent<Entities.View.SummonCardView>().Bind(fenrir);
            AudioManager.PlayCue("loki.fenrir.resolve");
        }

        // ── 자유 추격 ────────────────────────────────────────────

        /// <summary>초월기가 부른다. 2로 갱신하며 상한을 넘겨 쌓지 않는다.</summary>
        public void GrantFlex()
        {
            if (!Working) return;
            Caster.AddCombatResource(FlexResourceId, FlexMax - Flex);
        }

        // ── 추격 방아쇠 ──────────────────────────────────────────

        private void OnAnyActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (!_subscribed) return;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            int actionId = scheduler?.CurrentActionId ?? 0;
            // 라운드마다 행동 번호가 1부터 다시 시작한다. 번호가 되돌아가면 지난 시계의 중복 기록을 비운다.
            if (actionId < _lastStartedActionId) _handledActions.Clear();
            _lastStartedActionId = actionId;
            if (unit != null && unit == Fenrir && kind == ActionScheduler.ActionKind.Normal)
            {
                _window++;
                _windowUsed = 0;
            }
            if (Working) Ultimate?.OnActionStarted(actionId);
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || dead != _fenrir) return;
            // 스케줄러는 쓰러진 유닛의 예약을 취소 콜백 없이 걷는다. 잡아 둔 예산은 여기서 돌려준다.
            foreach (Reservation reservation in _pending.ToList()) Refund(reservation);
            _fenrir = null;
        }

        /// <summary>
        /// 펜리르 자신의 공격인가(일반 · 추격 · 나폴레옹 지휘 사격 모두). 피해 명의가 로키라 "로키 명의 + 소환수 표지"와
        /// 현재 실행자를 함께 본다. 로키의 소환수는 펜리르 1기뿐(동반형 상한 1)이라 이 조합이 곧 펜리르다.
        /// 다른 아군 소환수(우치 분신 등)는 명의가 그 소환자라 걸러지지 않고 추가행동 방아쇠가 될 수 있다.
        /// </summary>
        private bool IsFenrirDamage(DamageContext damage, Unit attacker, ActionScheduler scheduler)
            => (attacker == Caster && damage.HasTag(DamageTag.SummonAttack)) ||
               (_fenrir != null && scheduler.ExecutingUnit == _fenrir);

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (!Working || context == null || context.DamageDealt <= 0) return;
            Unit fenrir = Fenrir;
            Unit prey = Ultimate?.MarkedTarget;
            if (fenrir == null || prey == null || context.Target != prey) return;
            Unit attacker = context.Attacker;
            DamageContext damage = context.DamageContext;
            if (attacker == null || attacker.IsEnemy != Caster.IsEnemy || !DirectAttack.IsDirect(damage)) return;

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            // CurrentActionId는 비실행 중 ActionCount를 돌려준다. 직접 탐침 · 효과 콜백을 실제 행동으로 세지 않는다.
            if (scheduler == null || scheduler.ExecutingUnit == null) return;
            if (IsFenrirDamage(damage, attacker, scheduler)) return;
            int actionId = scheduler.CurrentActionId;
            if (_handledActions.Contains(actionId)) return;

            // 자유 추격이 남아 있으면 추가행동이어도 그것부터 쓴다 — 한 행동이 두 예산을 함께 쓰지 않는다.
            bool flex = Flex > 0;
            if (!flex && !ActionScheduler.IsAdditional(scheduler.ActingKind)) return;
            if (_windowUsed >= FollowUpCap)
            {
                _handledActions.Add(actionId);
                AnyHunt?.Invoke(Caster, prey, HuntEvent.CapReached, flex);
                return;
            }

            var reservation = new Reservation { Window = _window, Flex = flex };
            if (!scheduler.EnqueueAdditional(fenrir, $"fenrir_hunt_{++_serial}", "사냥감 추격",
                    () => Fire(reservation, fenrir), () => CanFire(reservation, fenrir), () => Refund(reservation))) return;
            _handledActions.Add(actionId);
            _pending.Add(reservation);
            _windowUsed++;
            if (flex) Caster.TryConsumeCombatResource(FlexResourceId, 1);
            AnyHunt?.Invoke(Caster, prey, HuntEvent.Accepted, flex);
        }

        /// <summary>예약한 그 펜리르가 아직 이 로키의 동반자이고, 예약이 살아 있고, 행동할 수 있는가.</summary>
        private bool CanFire(Reservation reservation, Unit fenrir)
            => Working && reservation != null && !reservation.Settled && fenrir != null && fenrir == _fenrir &&
               fenrir.isActive && fenrir.IsOnField && !fenrir.isControlled && Ultimate?.MarkedTarget != null;

        private void Refund(Reservation reservation)
        {
            if (reservation == null || reservation.Settled) return;
            reservation.Settled = true;
            _pending.Remove(reservation);
            if (reservation.Window == _window) _windowUsed = Mathf.Max(0, _windowUsed - 1);
            // 상한을 넘겨 돌려주지 않는다(그 사이 초월기가 2로 갱신했으면 그대로 2).
            if (reservation.Flex && Working) Caster.AddCombatResource(FlexResourceId, 1);
            AnyHunt?.Invoke(Caster, Ultimate?.MarkedTarget, HuntEvent.Cancelled, reservation.Flex);
        }

        private void Fire(Reservation reservation, Unit fenrir)
        {
            if (reservation.Settled) return;
            if (!CanFire(reservation, fenrir))
            {
                Refund(reservation);
                return;
            }
            fenrir.isCasting = true;
            fenrir.StartCoroutine(HuntRoutine(fenrir, reservation));
        }

        private IEnumerator HuntRoutine(Unit fenrir, Reservation reservation)
        {
            try
            {
                Unit prey = Ultimate?.MarkedTarget;
                var tags = new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.ContactAttack,
                    DamageTag.Pierce, DamageTag.SummonAttack,
                };
                AttackTargetResolution routing = AttackTargetRouter.Prepare(fenrir,
                    prey != null ? new[] { prey } : Array.Empty<Unit>(), BaseEnums.CodeType.Passive, tags, "사냥감 추격");
                yield return routing.ResolvePreAttack();
                if (prey == null || !CanFire(reservation, fenrir) || !routing.CanContinue || routing.Targets.Count == 0 ||
                    !Targeting.Announce(fenrir, routing.Targets))
                    yield break;

                Unit target = routing.Targets[0];
                // 치명은 로키의 스탯 · 효과로, 행동자 기준 오라(마리 보급의 추가행동 치명 피해)는 실제 행동자인 펜리르로 굴린다.
                bool crit = RollCriticalFor(Caster, fenrir, target, BaseEnums.CodeType.Passive, out float multiplier);
                int damage = Mathf.Max(1, Mathf.RoundToInt(StatDamage(0f, BaseEnums.StatType.Attack, FollowUpRatio) * multiplier));
                var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags, crit);
                AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, CodeName);
                bool landed = false;
                yield return DirectAttack.HitOne("loki.fenrir", fenrir, target, () =>
                {
                    // 정지 · 펜리르 교체 · 제어 뒤 늦게 도착한 정점은 판정하지 않는다.
                    if (!target.isActive || !CanFire(reservation, fenrir)) return;
                    AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
                    target.TakeDamage(context);
                    landed = !context.IsCancelled;
                }, stopOnControl: true, presentation: impact => FenrirPresentation.Strike(fenrir, target, impact));

                if (landed && !reservation.Settled)
                {
                    reservation.Settled = true;
                    _pending.Remove(reservation);
                    AnyHunt?.Invoke(Caster, target, HuntEvent.Fired, reservation.Flex);
                }
            }
            finally
            {
                // 취소 · 중단 · 실패는 모두 여기서 반환한다(이미 정리된 예약은 그대로 둔다).
                if (!reservation.Settled) Refund(reservation);
                if (fenrir != null) fenrir.isCasting = false;
            }
        }

        /// <summary>늑대의 혈맹 — 로키 공격력 · 방어력 · 특수방어력의 60%. 체력은 소환 정의가 만들 때 물려받는다.</summary>
        private sealed class KinshipEffect : BaseEffect
        {
            private readonly Unit _loki;

            public KinshipEffect(Unit loki) : base(0, InheritRatio) => _loki = loki;

            public override bool IsBeneficial => true;

            public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
            {
                if (_loki == null || !_loki.isActive) return 0f;
                return stat switch
                {
                    BaseEnums.StatType.Attack or BaseEnums.StatType.Defense or BaseEnums.StatType.SpecialDefense
                        => _loki.StatValue(stat) * InheritRatio,
                    _ => 0f,
                };
            }
        }
    }
}
