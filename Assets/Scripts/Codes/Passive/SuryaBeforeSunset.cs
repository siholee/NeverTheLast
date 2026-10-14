using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
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
    /// 수리야 — 특성 「해가 지기 전에」(Detail_08 §18.2).
    ///
    ///   · 전투 시작에 공격 가능한 적 하나를 현상수배한다(낮은 체력 비율 → 기본 우선도 → 고정 배치, 효과 명중 공통).
    ///     수배된 적은 <b>아군의 추가행동</b>에 받는 피해 +15%(수리야 자신 포함). 일반 · 궁극기 · 지속피해 · 반격만 있는 공격은 제외.
    ///   · 탄환 2발로 시작한다. 다른 아군의 직접 공격이 수배된 적에게 실제 피해를 주면 탄환 1발을 써서
    ///     공격력 55% 단일 #사격 추가행동으로 응답한다. 실제 행동 하나에 한 번 — 다단 · 범위 · 관통이 여러 번 맞혀도 한 번이다.
    ///     소환수의 행동은 그 행동 하나로 센다(피해는 소환자 명의).
    ///   · 탄환은 예약이 수락될 때 쓴다. 대기 중인 응답은 각자 따로 예약되어 서로를 지우지 않는다.
    ///   · 수배된 적이 쓰러지면 살아 있는 다음 적을 수배하고, 대기 중인 응답은 실행 때 새 표적을 쏜다.
    ///   · 표식과 지원은 탄환이 0발이어도 남는다. 탄환 · 강탄은 전투 자원이라 라운드가 끝나면 사라진다(런 저장 없음).
    /// </summary>
    public sealed class SuryaBeforeSunset : UniquePassiveCode
    {
        public const int WantedStatusId = 70670;
        public const string WantedKey = "surya_wanted";
        public const string BulletResourceId = "surya_bullets";
        public const string EmpoweredResourceId = "surya_empowered";
        public const int MaxBullets = 2;
        public const float ShotRatio = 0.55f;
        public const float EmpoweredShotRatio = 1.10f;
        public const float AdditionalVulnerability = 0.15f;

        public enum ShotKind { Marked, Accepted, NoBullet, Fired, Cancelled }

        /// <summary>표식 · 응답의 흐름. 검증 · 연출이 읽는다. (수리야, 대상, 종류, 강탄 여부)</summary>
        public static event Action<Unit, Unit, ShotKind, bool> AnyShot;

        private readonly HashSet<int> _handledActionIds = new();
        private Unit _wanted;
        private bool _subscribed;
        private bool _needsMark;
        private int _serial;
        private Action<EventContext> _onDeath;

        public SuryaBeforeSunset(PassiveCodeContext context) : base(context)
        {
            CodeName = "해가 지기 전에";
            CombatResourceLabels.Register(BulletResourceId, "탄환");
            CombatResourceLabels.Register(EmpoweredResourceId, "강탄");
        }

        private bool Working => _subscribed && Caster != null && Caster.isActive && Caster.IsOnField;

        /// <summary>지금 유효한 수배 대상. 쓰러졌거나 표식이 사라졌으면 null.</summary>
        public Unit Wanted => IsAliveEnemy(_wanted) && HasMark(_wanted) ? _wanted : null;

        // 사망 이벤트는 DeactivateUnit보다 먼저 나간다. 그 순간의 대상은 아직 isActive이므로 체력으로도 거른다.
        private static bool IsAliveEnemy(Unit unit) => unit != null && unit.isActive && unit.IsOnField && unit.HpCurr > 0;

        public int Bullets => Caster?.GetCombatResource(BulletResourceId) ?? 0;
        public bool Empowered => (Caster?.GetCombatResource(EmpoweredResourceId) ?? 0) > 0;

        private bool HasMark(Unit unit)
            => unit.GetAllStatuses().Any(status => status.Key == WantedKey && status.Caster == Caster);

        public override void CastCode()
        {
            if (Caster == null || _subscribed) return;
            _handledActionIds.Clear();
            _wanted = null;
            Caster.SetCombatResourceMaximum(BulletResourceId, MaxBullets, resetCurrent: true);
            Caster.AddCombatResource(BulletResourceId, MaxBullets);
            Caster.SetCombatResourceMaximum(EmpoweredResourceId, 1, resetCurrent: true);
            Unit.AnyDamageDealt += OnAnyDamageDealt;
            Unit.AnyUnitDied += OnAnyUnitDied;
            ActionScheduler.AnyActionStarted += OnAnyActionStarted;
            _onDeath = _ => { ClearMark(); ClearResources(); };
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _subscribed = true;
            // 적이 아직 등장 중이면 첫 행동이 시작될 때 수배한다.
            _needsMark = true;
            if (CombatTargets.AliveEnemies(Caster).Count > 0) ResolveTarget();
        }

        public override void StopCode()
        {
            ClearMark();
            ClearResources();
            if (!_subscribed) return;
            Unit.AnyDamageDealt -= OnAnyDamageDealt;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            ActionScheduler.AnyActionStarted -= OnAnyActionStarted;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _subscribed = false;
            _handledActionIds.Clear();
        }

        // ── 수배 ────────────────────────────────────────────────

        /// <summary>
        /// 유효한 수배 대상을 돌려준다. 없으면 새로 고르고 수배를 시도한다. 명중에 실패하면 고른 적을 그대로 돌려줘
        /// 일반행동 · 초월기가 그 적을 쏘게 하고(다음 시도는 그 행동 때), 특성 응답은 수배가 붙어 있을 때만 일한다.
        /// </summary>
        public Unit ResolveTarget(Unit exclude = null)
        {
            Unit current = Wanted;
            if (current != null && current != exclude) return current;
            Unit candidate = PickCandidate(exclude);
            if (candidate == null) return null;
            _needsMark = false;
            if (_wanted != null && _wanted != candidate) _wanted.RemoveStatusByKey(WantedKey);
            UnitStatus mark = BuffStatus.Create(WantedStatusId, WantedKey, "현상수배", Caster, candidate,
                new WantedEffect(Caster), duration: 0, stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "수리야의 아군이 가하는 추가행동 피해를 +15% 더 받는다.");
            candidate.AddStatus(mark);
            if (!candidate.GetAllStatuses().Contains(mark)) return candidate;
            _wanted = candidate;
            AnyShot?.Invoke(Caster, candidate, ShotKind.Marked, false);
            return candidate;
        }

        private Unit PickCandidate(Unit exclude)
            => CombatTargets.AliveEnemies(Caster).Where(unit => unit != exclude && IsAliveEnemy(unit))
                .OrderBy(unit => unit.HpMax > 0 ? (float)unit.HpCurr / unit.HpMax : 1f)
                .ThenByDescending(unit => unit.Priority)
                .ThenBy(unit => unit.currentCell != null ? Mathf.Abs(unit.currentCell.xPos) : 99)
                .ThenBy(unit => unit.currentCell != null ? unit.currentCell.yPos : 99)
                .FirstOrDefault();

        private void ClearMark()
        {
            if (_wanted != null) _wanted.RemoveStatusByKey(WantedKey);
            _wanted = null;
        }

        private void OnAnyActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (_needsMark && Working) ResolveTarget();
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || dead != _wanted) return;
            _wanted = null;
            _needsMark = true;
            if (Working) ResolveTarget(dead);
        }

        // ── 탄환 · 응답 사격 ─────────────────────────────────────────

        /// <summary>탄환을 2발까지 채운다(더하지 않는다). 이미 수락된 응답은 복제하지 않는다.</summary>
        internal void Reload()
        {
            if (!Working) return;
            Caster.AddCombatResource(BulletResourceId, MaxBullets - Bullets);
            Effects.Vfx.VedicTrioFx.Reload(Caster, Bullets);
        }

        /// <summary>다음 실제 응답 한 발을 110%로. 미사용 강탄은 겹치지 않고 갱신된다.</summary>
        internal void Empower()
        {
            if (Working) Caster.AddCombatResource(EmpoweredResourceId, 1);
        }

        private void ClearResources()
        {
            if (Caster == null) return;
            Caster.AddCombatResource(BulletResourceId, -Bullets);
            Caster.AddCombatResource(EmpoweredResourceId, -Caster.GetCombatResource(EmpoweredResourceId));
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (!Working || context == null || context.DamageDealt <= 0) return;
            Unit wanted = Wanted;
            if (wanted == null || context.Target != wanted) return;
            Unit attacker = context.Attacker;
            if (attacker == null || attacker == Caster || attacker.IsEnemy != Caster.IsEnemy) return;
            if (!DirectAttack.IsDirect(context.DamageContext)) return;

            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            // CurrentActionId는 비실행 중 ActionCount를 돌려준다. 직접 탐침·효과 콜백을 실제 행동으로 세지 않는다.
            if (scheduler == null || scheduler.ExecutingUnit == null || !_handledActionIds.Add(scheduler.CurrentActionId)) return;
            if (Bullets < 1)
            {
                AnyShot?.Invoke(Caster, wanted, ShotKind.NoBullet, false);
                return;
            }
            if (!scheduler.EnqueueAdditional(Caster, $"surya_shot_{++_serial}", CodeName, Fire, CanFire)) return;
            Caster.TryConsumeCombatResource(BulletResourceId, 1);
            AnyShot?.Invoke(Caster, wanted, ShotKind.Accepted, false);
        }

        private bool CanFire()
            => Working && !Caster.isControlled && CombatTargets.AliveEnemies(Caster).Count > 0;

        private void Fire()
        {
            if (!CanFire()) return;
            Caster.isCasting = true;
            Caster.StartCoroutine(ShotRoutine());
        }

        private IEnumerator ShotRoutine()
        {
            Unit target = ResolveTarget();
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            AttackTargetResolution routing = AttackTargetRouter.Prepare(Caster, target != null ? new[] { target } : Array.Empty<Unit>(),
                BaseEnums.CodeType.Passive, tags, CodeName);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (target == null || !routing.CanContinue || targets.Count == 0 || !Targeting.Announce(Caster, targets))
            {
                AnyShot?.Invoke(Caster, target, ShotKind.Cancelled, false);
                if (Caster != null) Caster.isCasting = false;
                yield break;
            }

            Unit shot = targets[0];
            // 강탄은 예약이 아니라 실제로 쏘는 순간에 쓴다.
            bool empowered = Empowered;
            float ratio = empowered ? EmpoweredShotRatio : ShotRatio;
            bool crit = RollCritical(shot, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(StatDamage(0f, BaseEnums.StatType.Attack, ratio) * multiplier));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags, crit);

            // 응답 사격의 Cast/Hit/Resolve 음향은 연출 입구(VedicTrioFx.SuryaAdditional)가 소유한다 — 여기서 따로 울리지 않는다.
            bool hit = false;
            yield return DirectAttack.HitOne(empowered ? "surya.focused_follow_up" : "surya.follow_up", Caster, shot, () =>
            {
                if (shot.isActive) { if (empowered) Caster.TryConsumeCombatResource(EmpoweredResourceId, 1); shot.TakeDamage(context); hit = true; }
            }, stopOnControl: true,
                presentation: impact => Effects.Vfx.VedicTrioFx.SuryaAdditional(Caster, shot, empowered, impact));
            AnyShot?.Invoke(Caster, shot, hit ? ShotKind.Fired : ShotKind.Cancelled, empowered);
            if (Caster != null) Caster.isCasting = false;
        }

        /// <summary>현상수배 — 수리야의 아군이 가하는 추가행동 피해 +15%. 수리야가 필드에 없으면 효력 0.</summary>
        private sealed class WantedEffect : BaseEffect
        {
            private readonly Unit _surya;

            public WantedEffect(Unit surya) : base(0, AdditionalVulnerability)
            {
                _surya = surya;
                Category = BaseEnums.EffectCategory.Negative;
            }

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            {
                if (unit != Target || !DirectAttack.IsDirect(context) || !context.HasTag(DamageTag.AdditionalAttack)) return 1f;
                if (context.Attacker == null || context.Attacker.IsEnemy == unit.IsEnemy) return 1f;
                return _surya != null && _surya.isActive && _surya.IsOnField ? 1f + AdditionalVulnerability : 1f;
            }
        }
    }
}
