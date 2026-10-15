using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Special;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 아그니 — 특성 「끝까지 태워 주지」(Detail_08 §17.2). 다른 필드 아군(자신 · 소환수 제외)이 <b>초월기를 실제로 마칠 때마다</b>
    /// 적 전체에 공격력 45% 추가행동을 예약하고, 적마다 피해 → 그 적에게 불 부착 → 다음 적 순으로 처리한다.
    ///
    ///   · 같은 아군에게서는 아그니의 일반행동 사이에 한 번만 받는다. 예약이 수락되는 순간 그 아군의 몫을 쓴다.
    ///   · 표(티켓)마다 따로 예약해 여러 건이 대기해도 하나도 잃거나 겹치지 않는다.
    ///   · 실제 궁극기 완료 = <see cref="Unit.AnyUltimateCast"/> 뒤의 <see cref="ActionScheduler.AnyActionFinished"/>(Ultimate).
    ///     궁극기 피해 판정이 붙은 특수행동(바즈라) · 아그니 자신의 추공은 궁극기 행동이 아니므로 부르지 않는다.
    ///   · 초월기 「내가 판을 키워 주지」가 켠 판돈(<see cref="StakesActive"/>) 동안 이 추공은 <c>AdditionalAttack + UltAttack</c> 판정과
    ///     궁극기 치명 질의를 받는다. 실제 행동은 끝까지 추가행동이다.
    ///   · 특수행동 「제일 먼저 태울 곳」이 같은 묶음을 연 인드라의 표 한 장을 보류했다가 집중 포격으로 쓴다(<see cref="AgniFirstToBurn"/>).
    /// </summary>
    public sealed class AgniBurnToTheEnd : UniquePassiveCode
    {
        public const float FollowUpRatio = 0.45f;
        public const float FocusedCenterRatio = 1.80f;
        public const int StakesNormalActions = 3;
        public const float UltimateDamageBonus = 1.20f;
        public const int StakesStatusId = 70620;
        public const string StakesKey = "agni_raise_stakes";
        public const string StakesResourceId = "agni_stakes";

        public enum TicketKind { Accepted, Rejected, Held, ReleasedFocused, ReleasedBasic, Dropped, Fired, CountsReset }

        /// <summary>표의 흐름(수락 · 거절 · 보류 · 해제 · 발사). 검증 · 연출이 읽는다. (아그니, 준 아군 · 대상, 종류)</summary>
        public static event Action<Unit, Unit, TicketKind> AnyTicket;

        private readonly HashSet<Unit> _usedAllies = new();
        private readonly HashSet<Unit> _castingUltimates = new();
        private bool _subscribed;
        private int _serial;
        private int _stakesLeft;
        private Action<EventContext> _onNormalResolved;
        private Action<EventContext> _onDeath;

        public AgniBurnToTheEnd(PassiveCodeContext context) : base(context)
        {
            CodeName = "끝까지 태워 주지";
            CombatResourceLabels.Register(StakesResourceId, "판돈");
        }

        /// <summary>
        /// 추가 공격이 이미 #주문으로 피해를 준다. 보상 후보 조회(<see cref="AttackForms.Of"/>)가 그 형태를 볼 수 있게 선언만 한다 —
        /// 피해 · 행동 · 연출은 바뀌지 않는다.
        /// </summary>
        public override int AttackForm => DamageTag.Spell;

        /// <summary>초월기의 판돈이 남아 있는가(아그니 일반행동 완료로만 줄어든다).</summary>
        public bool StakesActive => _stakesLeft > 0 && Working;
        public int StakesLeft => _stakesLeft;
        public bool HasUsed(Unit ally) => ally != null && _usedAllies.Contains(ally);

        private bool Working => _subscribed && Caster != null && Caster.isActive && Caster.IsOnField;

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            _usedAllies.Clear();
            _castingUltimates.Clear();
            _stakesLeft = 0;
            Unit.AnyUltimateCast += OnUltimateCast;
            ActionScheduler.AnyActionStarted += OnActionStarted;
            ActionScheduler.AnyActionFinished += OnActionFinished;
            _onNormalResolved = _ => OnOwnNormalResolved();
            _onDeath = _ => OnOwnDeath();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _onNormalResolved);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _subscribed = true;
        }

        public override void StopCode()
        {
            EndStakes();
            if (!_subscribed) return;
            Unit.AnyUltimateCast -= OnUltimateCast;
            ActionScheduler.AnyActionStarted -= OnActionStarted;
            ActionScheduler.AnyActionFinished -= OnActionFinished;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _onNormalResolved);
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _subscribed = false;
            _usedAllies.Clear();
            _castingUltimates.Clear();
        }

        // ── 트리거 ────────────────────────────────────────────────

        private bool IsTriggerAlly(Unit unit)
            => unit != null && Caster != null && unit != Caster && !unit.IsSummon && unit.IsEnemy == Caster.IsEnemy;

        private void OnUltimateCast(Unit unit)
        {
            if (IsTriggerAlly(unit)) _castingUltimates.Add(unit);
        }

        // 시전 도중 쓰러진 궁극기는 끝 알림 없이 스케줄러에서 빠진다. 그 유닛이 새 행동을 열면 지난 기록을 버려
        // 시작하지 않은 궁극기를 완료로 오인하지 않는다(새 궁극기라면 시작 직후 다시 기록된다).
        private void OnActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label) => _castingUltimates.Remove(unit);

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (kind != ActionScheduler.ActionKind.Ultimate || !_castingUltimates.Remove(unit)) return;
            if (!unit.isActive || !unit.IsOnField) return;
            if (unit.ActiveUltimateCode is BaseUltimateCode ultimate && !ultimate.CompletedNormally) return;
            OnAllyUltimateCompleted(unit);
        }

        private void OnAllyUltimateCompleted(Unit ally)
        {
            if (!Working) return;
            if (_usedAllies.Contains(ally))
            {
                AnyTicket?.Invoke(Caster, ally, TicketKind.Rejected);
                return;
            }

            // 인드라가 연 묶음에 아그니 특수행동이 실제로 예약되어 있으면 그 인드라 초월기의 표 한 장만 특수행동 끝까지 보류한다.
            if (Caster.ActiveSpecialCode is AgniFirstToBurn special && SpecialAction.IsPending(Caster) &&
                SpecialAction.CurrentOpener == ally && special.TryHold(this))
            {
                _usedAllies.Add(ally);
                AnyTicket?.Invoke(Caster, ally, TicketKind.Held);
                return;
            }

            if (!EnqueueTicket(null)) return;
            _usedAllies.Add(ally);
            AnyTicket?.Invoke(Caster, ally, TicketKind.Accepted);
        }

        /// <summary>보류해 둔 표를 돌려받는다. 중심이 있으면 집중 포격, 없으면 기본 추공으로 예약한다.</summary>
        internal void ReleaseHeld(Unit center)
        {
            if (!Working || !EnqueueTicket(center))
            {
                AnyTicket?.Invoke(Caster, center, TicketKind.Dropped);
                return;
            }
            AnyTicket?.Invoke(Caster, center, center != null ? TicketKind.ReleasedFocused : TicketKind.ReleasedBasic);
        }

        private bool EnqueueTicket(Unit center)
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return false;
            return scheduler.EnqueueAdditional(Caster, $"agni_follow_up_{++_serial}", CodeName, () => Fire(center), CanFire);
        }

        private bool CanFire()
            => Working && !Caster.isControlled && CombatTargets.AliveEnemies(Caster).Count > 0;

        // ── 판돈(초월기) ─────────────────────────────────────────────

        /// <summary>초월기가 실제로 판돈을 건다. 다시 걸면 3회로 갱신하고 같은 원천은 겹치지 않는다. 아군별 표를 모두 되돌린다.</summary>
        internal void StartStakes()
        {
            if (!Working) return;
            _stakesLeft = StakesNormalActions;
            Caster.SetCombatResourceMaximum(StakesResourceId, StakesNormalActions);
            Caster.AddCombatResource(StakesResourceId, StakesNormalActions);
            foreach (Unit ally in FieldAllies())
            {
                ally.AddStatus(BuffStatus.Create(StakesStatusId, StakesKey, "판을 키워 주지", Caster, ally,
                    new StakesEffect(this), duration: 0, stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Positive, isBeneficial: true,
                    description: "궁극기 피해 +20%. 아그니가 일반행동을 세 번 마치면 끝난다."));
            }
            // 이미 지나간 아군 초월기를 다시 재생하지 않는다 — 다음 실제 완료부터 새 표를 받는다. 대기 중인 표는 그대로다.
            _usedAllies.Clear();
            AnyTicket?.Invoke(Caster, Caster, TicketKind.CountsReset);
        }

        private void EndStakes()
        {
            _stakesLeft = 0;
            if (Caster != null && Caster.GetCombatResourceMaximum(StakesResourceId) > 0)
                Caster.AddCombatResource(StakesResourceId, -Caster.GetCombatResource(StakesResourceId));
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return;
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            foreach (Unit ally in pool.Where(unit => unit != null)) ally.RemoveStatusByKey(StakesKey);
        }

        private void OnOwnNormalResolved()
        {
            // 일반행동 한 번이 다음 구간을 연다 — 아군별 표가 다시 1장씩 생긴다.
            _usedAllies.Clear();
            if (_stakesLeft <= 0) return;
            _stakesLeft--;
            Caster.AddCombatResource(StakesResourceId, -1);
            if (_stakesLeft <= 0) EndStakes();
        }

        private void OnOwnDeath()
        {
            EndStakes();
            _usedAllies.Clear();
            _castingUltimates.Clear();
        }

        private List<Unit> FieldAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsSummon).ToList();
        }

        // ── 추가행동 본체 ─────────────────────────────────────────────

        private void Fire(Unit center)
        {
            if (!CanFire()) return;
            Caster.isCasting = true;
            Caster.StartCoroutine(FireRoutine(center));
        }

        private IEnumerator FireRoutine(Unit center)
        {
            bool stakes = StakesActive;
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.AdditionalAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            if (stakes) tags.Add(DamageTag.UltAttack);

            AttackTargetResolution routing = AttackTargetRouter.Prepare(Caster, CombatTargets.AliveEnemies(Caster),
                BaseEnums.CodeType.Passive, tags, CodeName);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Targeting.Announce(Caster, targets))
            {
                if (Caster != null) Caster.isCasting = false;
                yield break;
            }

            // 보류됐다 풀린 표는 실행 때 중심을 다시 확인한다. 그사이 쓰러졌으면 같은 규칙으로 다시 고른다(재료 이동은 다시 하지 않는다).
            bool focused = center != null;
            if (focused && (!center.isActive || !targets.Contains(center)))
                center = AgniFirstToBurn.SelectCenter(targets, out _, out _);
            focused = center != null;
            List<Unit> ordered = focused && center != null
                ? new[] { center }.Concat(targets.Where(unit => unit != center)).ToList()
                : targets;

            // 추공의 Cast/Hit/Resolve 음향은 연출 입구(VedicTrioFx.AgniAdditional)가 소유한다 — 여기서 따로 울리지 않는다.
            AnyTicket?.Invoke(Caster, center, TicketKind.Fired);
            yield return DirectAttack.HitEach(focused ? "agni.focused_follow_up" : "agni.follow_up", Caster, ordered,
                target => Strike(target, focused && target == center ? FocusedCenterRatio : FollowUpRatio, tags, stakes),
                stopOnControl: true,
                presentation: (liveTargets, impact) => Effects.Vfx.VedicTrioFx.AgniAdditional(Caster, liveTargets, center, focused, impact));
            if (Caster != null) Caster.isCasting = false;
        }

        /// <summary>한 적의 피해 → 그 적의 불 부착. 다음 적의 피해가 증폭 반응의 "방금 그 공격"을 덮어쓰지 않는다.</summary>
        private void Strike(Unit target, float ratio, List<int> tags, bool stakes)
        {
            if (!DirectAttack.CasterCanContinue(Caster, true) || target == null || !target.isActive) return;
            bool crit = RollCritical(target, stakes ? BaseEnums.CodeType.Ultimate : BaseEnums.CodeType.Passive, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(StatDamage(0f, BaseEnums.StatType.Attack, ratio) * multiplier));
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, new List<int>(tags), crit));
            if (target.isActive && DirectAttack.CasterCanContinue(Caster, true)) target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster);
        }

        /// <summary>판돈 — 판돈이 남아 있는 동안 궁극기 피해 +20%. 같은 키라 재시전으로 겹치지 않는다.</summary>
        private sealed class StakesEffect : BaseEffect
        {
            private readonly AgniBurnToTheEnd _source;
            public StakesEffect(AgniBurnToTheEnd source) : base(0, AgniBurnToTheEnd.UltimateDamageBonus) => _source = source;
            public override bool IsBeneficial => true;

            public override float UltimateDamageBonus(Unit attacker)
                => attacker == Target && _source.StakesActive ? AgniBurnToTheEnd.UltimateDamageBonus - 1f : 0f;
        }
    }
}
