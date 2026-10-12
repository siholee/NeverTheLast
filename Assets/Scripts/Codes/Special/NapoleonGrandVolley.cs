using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>
    /// 나폴레옹 지휘의 대상 선정 · 참여자 규칙(Detail_08 §20). 일반행동 · 특수행동 · 검증이 같은 답을 쓰도록 한곳에 둔다.
    /// </summary>
    public static class NapoleonCommand
    {
        /// <summary>지휘 사격의 최종 피해 배율(컨텍스트 배율로 한 번 곱한다).</summary>
        public const float VolleyRatio = CommandedAttackScope.FinalDamageRatio;

        /// <summary>
        /// 지휘에 응하는 아군. 필드 위 · 행동 가능(제어 · 일반 공격 봉쇄 없음) · 일반행동이 실제로 공격하는(<see cref="NormalCode.CanJoinCommand"/>) 유닛 전원.
        /// 클래스 종류로 가르지 않는다 — 우치 분신(분신 일격)과 펜리르 같은 소환수도 포함된다. 지휘자 자신과
        /// 공격하지 않는 일반행동(우치 분신술 등)은 빠진다. 인원 상한은 없다.
        /// </summary>
        public static List<Unit> Participants(Unit napoleon)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || napoleon == null) return new List<Unit>();
            return grid.heroList.Concat(grid.enemyList).Concat(grid.summonList)
                .Where(unit => unit != null && unit != napoleon && unit.isActive && unit.IsOnField &&
                               unit.IsEnemy == napoleon.IsEnemy && !unit.isControlled && !unit.IsNormalAttackBlocked &&
                               unit.ActiveNormalCode != null && unit.ActiveNormalCode.CanJoinCommand)
                .Distinct()
                .ToList();
        }

        /// <summary>완전 무적. 대상 지정 불가 · 디버그 무적 · 받는 피해 배율 0(무효화 효과)을 같은 선으로 거른다.</summary>
        public static bool IsInvincible(Unit target, Unit napoleon)
        {
            if (target == null || !target.isActive || target.IsUntargetable) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (target.IsEnemy ? Core.DebugMode.EnemyInvincible : Core.DebugMode.AllyInvincible) return true;
#endif
            var probe = new DamageContext(napoleon, 1000, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack });
            float receiving = 1f;
            foreach (Effects.Base.BaseEffect effect in target.CombatEffects())
                receiving *= effect.ReceivingDamageModifier(target, probe);
            return receiving <= 0f;
        }

        /// <summary>
        /// 횟수형 보호막(다음 한 방을 지우는 무효, 보상 코드의 <c>_ward</c> 계열 상태). 이 적들만 맨 뒤로 미룬다.
        /// 피해량 보호막(<see cref="Unit.ShieldCurr"/>)은 후순위가 아니라 유효 체력에 더할 뿐이다.
        /// </summary>
        public static bool IsHitWarded(Unit target)
            => target != null && target.GetAllStatuses().Any(status => status?.Key != null &&
                                                                       status.Key.EndsWith("_ward", StringComparison.Ordinal));

        /// <summary>쓰러뜨리는 데 깎아야 할 양. 현재 체력 + 피해량 보호막.</summary>
        public static int EffectiveHp(Unit target) => target == null ? 0 : target.HpCurr + Mathf.Max(0, target.ShieldCurr);

        /// <summary>
        /// 실제 참여 아군의 추정 일제사격(치명 없음, 최종 배율 · 오라 · 방어 효과 · 경감 포함). 참여자가 없으면 나폴레옹 공격력
        /// 한 발을 같은 공통 추정식에 넣어 방어력만이라도 반영한다.
        /// </summary>
        public static int EstimateVolley(Unit target, IReadOnlyList<Unit> participants, Unit napoleon)
        {
            if (target == null) return 0;
            int total = 0;
            foreach (Unit ally in participants)
                if (ally?.ActiveNormalCode != null) total += ally.ActiveNormalCode.EstimateCommandedDamage(target);
            if (participants.Count > 0 || napoleon == null) return total;
            var probe = new DamageContext(napoleon, Mathf.Max(1, Mathf.RoundToInt(napoleon.StatValue(BaseEnums.StatType.Attack))),
                BaseEnums.CodeType.Normal, new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack })
            {
                OutgoingDamageMultiplier = VolleyRatio,
            };
            return CommandedAttackScope.EstimateFinalDamage(probe, target);
        }

        /// <summary>쓰러뜨리기 쉬운 정도. 유효 체력 ÷ 추정 일제사격 — 1 이하면 이번 일제사격으로 쓰러진다고 본다. 작을수록 쉽다.</summary>
        public static float KillScore(Unit target, IReadOnlyList<Unit> participants, Unit napoleon)
        {
            int volley = EstimateVolley(target, participants, napoleon);
            return volley > 0 ? (float)EffectiveHp(target) / volley : float.MaxValue;
        }

        /// <summary>
        /// 지휘 대상 순위. 우선도(도발 · 전열)는 보지 않는다. 완전 무적은 빼고, 횟수형 보호막이 없는 적이 있으면 그들만 본 뒤
        /// 쓰러뜨리기 쉬운 순 → 유효 체력 → 앞 열 → 위 칸 → 개체 순으로 결정적으로 정렬한다.
        /// </summary>
        public static List<Unit> Rank(Unit napoleon)
        {
            List<Unit> enemies = CombatTargets.AliveEnemies(napoleon)
                .Where(unit => unit.IsOnField && unit.HpCurr > 0 && !IsInvincible(unit, napoleon))
                .ToList();
            if (enemies.Count == 0) return enemies;
            List<Unit> participants = Participants(napoleon);
            List<Unit> open = enemies.Where(unit => !IsHitWarded(unit)).ToList();
            List<Unit> pool = open.Count > 0 ? open : enemies;
            return pool
                .OrderBy(unit => KillScore(unit, participants, napoleon))
                .ThenBy(EffectiveHp)
                .ThenBy(unit => unit.currentCell != null ? Mathf.Abs(unit.currentCell.xPos) : 99)
                .ThenBy(unit => unit.currentCell != null ? unit.currentCell.yPos : 99)
                .ThenBy(unit => unit.GetEntityId())
                .ToList();
        }

        public static Unit PickTarget(Unit napoleon) => napoleon == null ? null : Rank(napoleon).FirstOrDefault();
    }

    /// <summary>
    /// 나폴레옹 — 특수행동 「일제 사격」(SP 161). 일반행동 「포격 지점 지정」만 연다(로카팔라 묶음과 무관).
    ///
    /// 고정된 대상 하나에게 참여 아군 전원이 <b>자기 일반 공격의 실제 전달</b>을 동시에 한 번씩 꽂는다. 원시 피해 · 치명은
    /// 원래 그대로이고, 지휘 표지가 피해 컨텍스트에 최종 배율 50%를 한 번 곱한다. 일반 공격 태그 · 공격 형태 · 적중 후처리
    /// (원소 · 표식 · 종언탄 · 다단 · 분신 소멸)는 그대로지만 참여자에게는 일반행동 시작 · 완료 · 행동치 · 마나 +20 · 턴이 생기지 않는다.
    ///
    /// 대상 교체 · 선제 개입 · 공지는 참여자마다 차례로, 전달은 그 뒤에 동시에 연다. 대상이 도중에 쓰러지면 남은 판정은 버리고
    /// 다른 적으로 옮기지 않는다. 전원의 전달이 끝날 때까지 <c>isCasting</c>을 유지하며, 나폴레옹이 쓰러지거나 제어되거나 밖에서
    /// 멈추면 모든 지휘 표지를 취소한다 — 이미 만든 컨텍스트의 늦은 판정도 열리지 않는다.
    /// </summary>
    public sealed class NapoleonGrandVolley : BaseSpecialCode
    {
        public const string OrderKey = "napoleon_volley";
        private const float CancelGraceSeconds = 2.5f;

        /// <summary>(나폴레옹, 대상, 참여자 수)</summary>
        public static event Action<Unit, Unit, int> AnyVolleyStarted;

        /// <summary>(나폴레옹, 대상, 지휘 건수, 실제 적중 수, 취소 여부)</summary>
        public static event Action<Unit, Unit, int, int, bool> AnyVolleyFinished;

        /// <summary>예약된 일제사격이 실행 · 취소 · 소실로 정리됐다. 초월기 버프의 만료 유예가 듣는다.</summary>
        public event Action OrderSettled;

        private readonly List<NormalCode.CommandedAttack> _attacks = new();
        private Action<EventContext> _turnStart;
        private Unit _listeningUnit;
        private Unit _locked;
        private bool _ordered;
        private bool _performing;

        public NapoleonGrandVolley(SpecialCodeContext context) : base(context) => CodeName = "일제 사격";

        /// <summary>예약됐거나 실행 중인 일제사격이 있는가.</summary>
        public bool HasPendingOrder => _ordered || _performing;
        public Unit LockedTarget => _locked;
        /// <summary>이 코드가 실제로 시작한 일제사격 수. 검증용.</summary>
        public int VolleysPerformed { get; private set; }
        /// <summary>마지막 일제사격의 지휘 표지들. 검증이 취소 뒤 늦은 판정 차단을 확인한다.</summary>
        public IReadOnlyList<CommandedAttackScope> LastScopes { get; private set; } = Array.Empty<CommandedAttackScope>();

        private bool StillCommanding => Caster != null && Caster.isActive && Caster.IsOnField && !Caster.isControlled && Caster.isCasting;

        private bool LockedValid => _locked != null && _locked.isActive && _locked.IsOnField && !_locked.IsUntargetable;

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && LockedValid && NapoleonCommand.Participants(Caster).Count > 0;

        /// <summary>대상을 고정하고 일제사격 하나를 예약한다. 같은 일제사격은 한 번에 하나만 줄 선다.</summary>
        public bool Order(Unit target)
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null || Caster == null || target == null || _performing || _ordered) return false;
            if (NapoleonCommand.Participants(Caster).Count == 0) return false;
            EnsureTurnListener();
            _locked = target;
            _ordered = scheduler.EnqueueSpecial(Caster, OrderKey, CodeName, Caster.CastSpecialCode, CanRun);
            if (!_ordered) _locked = null;
            return _ordered;
        }

        // 특수행동 예약에는 취소 콜백이 없다. 실행 직전 조건이 깨지면 여기서 정리하고, 조용히 버려진 예약은 다음 턴 시작이 걷는다.
        private bool CanRun()
        {
            bool ok = HasValidTarget();
            if (!ok) Settle();
            return ok;
        }

        private void EnsureTurnListener()
        {
            if (Caster == null || _listeningUnit == Caster) return;
            _turnStart = _ => { if (_ordered && !_performing) Settle(); };
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnStart);
            _listeningUnit = Caster;
        }

        private void DetachTurnListener()
        {
            if (_listeningUnit != null && _turnStart != null) _listeningUnit.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnStart);
            _listeningUnit = null;
            _turnStart = null;
        }

        private void Settle()
        {
            if (!_ordered && !_performing) return;
            _ordered = false;
            _performing = false;
            _locked = null;
            OrderSettled?.Invoke();
        }

        protected override IEnumerator Perform()
        {
            Unit target = _locked;
            _ordered = false;
            _performing = true;
            _attacks.Clear();
            List<Unit> participants = NapoleonCommand.Participants(Caster);
            VolleysPerformed++;
            AnyVolleyStarted?.Invoke(Caster, target, participants.Count);
            VfxComposer.PlayScreen("napoleon.follow_up", Caster, target);
            VfxComposer.PlaySlot("napoleon.follow_up", "cast", 0, Caster, target,
                VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));

            // 1) 대상 교체 · 선제 개입 · 공지는 한 명씩. 선제 개입은 스케줄러의 행동 문맥을 잠시 빌리므로 겹치면 안 된다.
            foreach (Unit ally in participants)
            {
                if (!StillCommanding || target == null || !target.isActive) break;
                if (ally == null || !ally.isActive || ally.isControlled || ally.ActiveNormalCode == null) continue;
                NormalCode.CommandedAttack attack = ally.ActiveNormalCode.BeginCommandedAttack(target, Caster);
                if (attack == null) continue;
                _attacks.Add(attack);
                yield return attack.Prepare();
            }
            LastScopes = _attacks.Select(attack => attack.Scope).ToList();

            // 2) 전달은 동시에. 전달 코루틴은 연출 러너가 끝까지 돌려 카드 복귀 · 포커스를 보장하고,
            //    판정은 지휘 표지가 막는다(지휘자 쪽 코루틴이 밖에서 멈춰도 배율 없는 피해가 새지 않는다).
            foreach (NormalCode.CommandedAttack attack in _attacks)
            {
                if (attack.Ready && !attack.Cancelled) VfxRunner.Run(RunDelivery(attack));
                else attack.End();
            }

            bool cancelled = false;
            float grace = 0f;
            while (_attacks.Any(attack => !attack.Ended))
            {
                if (!StillCommanding)
                {
                    if (!cancelled) CancelAll();
                    cancelled = true;
                    grace += Time.deltaTime;
                    if (grace >= CancelGraceSeconds) break;
                }
                yield return null;
            }

            int hits = _attacks.Sum(attack => attack.HitsLanded);
            int count = _attacks.Count;
            if (cancelled) CancelAll();
            _attacks.Clear();
            if (hits > 0 && !cancelled) AudioManager.PlayAction(Caster, SfxActionSlot.Special, SfxCuePhase.Resolve, CodeName);
            AnyVolleyFinished?.Invoke(Caster, target, count, hits, cancelled);
            Settle();
        }

        private static IEnumerator RunDelivery(NormalCode.CommandedAttack attack)
        {
            try
            {
                yield return attack.Deliver();
            }
            finally
            {
                attack.End();
            }
        }

        /// <summary>남은 지휘 표지를 모두 취소한다. 전달은 연출 러너에서 끝까지 돌며 끝날 때 스스로 범위를 닫는다.</summary>
        private void CancelAll()
        {
            foreach (NormalCode.CommandedAttack attack in _attacks) attack.Cancel();
        }

        public override void StopCode()
        {
            CancelAll();
            _attacks.Clear();
            Effects.BattlefieldBackdrop.ClearFocus();
            DetachTurnListener();
            Settle();
            base.StopCode();
        }
    }
}
