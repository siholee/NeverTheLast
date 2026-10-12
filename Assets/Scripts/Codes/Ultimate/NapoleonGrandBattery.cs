using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Codes.Special;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 나폴레옹 — 초월기 「아우스터리츠의 태양」(Detail_08 §20.4). 피해 없는 파티 버프.
    ///
    ///   · 필드의 아군 전원(자신 포함) 일반 공격 피해 × (1 + 시전 시점 나폴레옹 치명타 피해 × 30%). 치피 50%면 +15%.
    ///     값은 시전 때 한 번 찍는다. 이 버프는 치명타 피해를 올리지 않으므로 재시전이 자기 값을 키우지 않는다.
    ///     특성 오라(+15%)와는 따로 곱한다.
    ///   · 아군 전원(행동치를 가진 소환수 포함)의 행동 게이지 20% 앞당김.
    ///   · 지속은 나폴레옹의 실제 일반행동 완료 2회. 두 번째 완료가 연 일제사격까지 덮도록, 그 일제사격이 정리될 때 걷는다.
    ///   · 재시전은 같은 출처를 갱신한다(중복 없음). 나폴레옹 사망 · 정지 · 라운드 종료에 걷힌다.
    /// 실제로 버프가 하나라도 붙어야 완료 근거가 생겨 마나 소모가 정상 완료로 기록된다.
    /// </summary>
    public sealed class NapoleonGrandBattery : BaseUltimateCode
    {
        public const int BuffStatusId = 71612;
        public const string BuffKey = "napoleon_austerlitz";
        public const float CritDamageShare = 0.30f;
        public const float AdvanceRatio = 0.20f;
        public const int DurationNormals = 2;

        private readonly List<Unit> _buffed = new();
        private Action<EventContext> _normal;
        private Action<EventContext> _roundEnd;
        private Action<EventContext> _turnStart;
        private Action<Unit, Unit> _death;
        private NapoleonGrandVolley _volley;
        private bool _expireAfterVolley;

        protected override bool RequiresCompletionEvidence => true;

        public NapoleonGrandBattery(UltimateCodeContext context) : base(context) => CodeName = "아우스터리츠의 태양";

        /// <summary>남은 나폴레옹 일반행동 완료 수. 0이면 버프 없음(만료 유예 중이면 1 이상이 아닐 수 있다).</summary>
        public int RemainingNormals { get; private set; }
        /// <summary>마지막 시전이 찍은 일반 공격 피해 증가량(0.15 = +15%).</summary>
        public float SnapshotBonus { get; private set; }
        public bool ExpiryDeferred => _expireAfterVolley;

        protected override IEnumerator Perform()
        {
            float bonus = Mathf.Max(0f, Caster.StatValue(BaseEnums.StatType.CritDamage)) * CritDamageShare;
            ClearBuff();
            SnapshotBonus = bonus;

            var grid = GridManager.Instance;
            List<Unit> allies = grid == null ? new List<Unit>() : grid.heroList.Concat(grid.enemyList)
                .Where(unit => unit != null && unit.isActive && unit.IsOnField && unit.IsEnemy == Caster.IsEnemy)
                .ToList();
            foreach (Unit ally in allies)
            {
                UnitStatus buff = BuffStatus.Create(BuffStatusId, BuffKey, CodeName, Caster, ally,
                    new AusterlitzEffect(Caster, bonus), duration: 0, isBeneficial: true,
                    description: $"일반 공격 피해 +{Mathf.RoundToInt(bonus * 100f)}%. 나폴레옹의 일반행동 {DurationNormals}회 동안 유지.");
                if (ally.TryAddStatus(buff)) _buffed.Add(ally);
            }
            if (_buffed.Count == 0) yield break;
            MarkEffectApplied();

            RemainingNormals = DurationNormals;
            _normal = _ => OnNormalResolved();
            _roundEnd = _ => ClearBuff();
            _turnStart = _ => { if (_expireAfterVolley) ClearBuff(); };
            _death = (unit, attacker) => { if (unit == Caster) ClearBuff(); };
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEnd);
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnStart);
            Unit.AnyUnitDied += _death;
            _volley = Caster.ActiveSpecialCode as NapoleonGrandVolley;
            if (_volley != null) _volley.OrderSettled += OnVolleySettled;

            // 행동치는 아군 행동 순서에 끼는 모든 유닛을 앞당긴다. 스케줄러에 없는 유닛은 무시된다.
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler != null && grid != null)
                foreach (Unit unit in allies.Concat(grid.summonList.Where(summon => summon != null && summon.isActive &&
                             summon.IsOnField && summon.IsEnemy == Caster.IsEnemy && summon.TakesScheduledTurns)))
                    scheduler.AdvanceAction(unit, AdvanceRatio);

            Effects.Vfx.VfxComposer.PlayScreen("napoleon.reload", Caster, Caster);
            Effects.Vfx.VfxComposer.PlaySlot("napoleon.reload", "cast", 0, Caster, Caster,
                Effects.Vfx.VfxSpace.PortraitPoint(Caster), Effects.Vfx.VfxSpace.SortingLayer(Caster));
            foreach(Unit ally in _buffed)
                Effects.Vfx.VfxComposer.PlayAffectedSlot("napoleon.reload", 0, Caster, ally);
            AudioManager.PlayCue("napoleon.ultimate.resolve");
            yield return new WaitForSeconds(0.3f);
        }

        // 자연 완료(Run → StopCode)에서는 버프가 남는다. 밖에서 멈추면(라운드 · 디버그 정리) 버프 · 구독 · 유예를 모두 걷는다.
        protected override void OnStoppedExternally() => ClearBuff();

        private void OnNormalResolved()
        {
            if (RemainingNormals <= 0) return;
            RemainingNormals--;
            if (RemainingNormals > 0) return;
            // 방금 끝난 일반행동이 일제사격을 예약했다면 그 일제사격까지 버프가 남는다.
            if (_volley != null && _volley.HasPendingOrder) _expireAfterVolley = true;
            else ClearBuff();
        }

        private void OnVolleySettled()
        {
            if (_expireAfterVolley) ClearBuff();
        }

        private void ClearBuff()
        {
            if (_normal != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            if (_roundEnd != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEnd);
            if (_turnStart != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnStart);
            if (_death != null) Unit.AnyUnitDied -= _death;
            if (_volley != null) _volley.OrderSettled -= OnVolleySettled;
            _normal = null; _roundEnd = null; _turnStart = null; _death = null; _volley = null;
            foreach (Unit ally in _buffed)
                if (ally != null) ally.RemoveStatusByKey(BuffKey);
            _buffed.Clear();
            RemainingNormals = 0;
            _expireAfterVolley = false;
        }

        /// <summary>일반 공격 피해 배율. 특성 오라와 독립적으로 곱한다.</summary>
        private sealed class AusterlitzEffect : BaseEffect
        {
            private readonly Unit _napoleon;
            private readonly float _bonus;

            public AusterlitzEffect(Unit napoleon, float bonus) : base(0, bonus)
            {
                _napoleon = napoleon;
                _bonus = bonus;
            }

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && _napoleon != null && _napoleon.isActive && NapoleonOrders.IsNormalAttack(context)
                    ? 1f + _bonus : 1f;
        }
    }
}
