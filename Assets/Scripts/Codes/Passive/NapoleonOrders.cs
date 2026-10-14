using System;
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

namespace Codes.Passive
{
    /// <summary>
    /// 나폴레옹 — 특성 「대육군의 지휘」(Detail_08 §20).
    ///
    ///   · 전투 시작 때 자신의 행동 게이지가 25% 앞당겨진다. 스케줄러가 라운드 시작 패시브 <b>뒤에</b> 행동치를 초기화하므로
    ///     직접 AdvanceAction하지 않고 자기 오라의 <see cref="BaseEffect.RoundStartActionAdjustment"/>로 넘긴다.
    ///   · 필드에 있는 동안 아군 전원(자신 포함)의 일반 공격 피해 +15%. 일반 공격 태그가 붙은 직접 피해만 받으며
    ///     지휘 사격 · 수리야의 석양처럼 일반 공격 판정을 받는 특수행동 피해도 포함한다. 소환수 피해는 소환자 명의라 소환자의 오라를 쓴다.
    ///   · 오라는 같은 키 하나라 겹치지 않고, 나폴레옹이 쓰러지거나 라운드가 끝나면 걷힌다.
    /// </summary>
    public sealed class NapoleonOrders : UniquePassiveCode
    {
        public const int AuraStatusId = 71611;
        public const string AuraKey = "napoleon_grand_army";
        public const float OpeningAdvance = 0.25f;
        public const float NormalAttackBonus = 0.15f;

        private readonly List<Unit> _auraHolders = new();
        private Action<EventContext> _onDeath;
        private bool _working;

        public NapoleonOrders(PassiveCodeContext context) : base(context) => CodeName = "대육군의 지휘";

        private bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;

        /// <summary>일반 공격 판정을 받는 직접 피해인가. 오라와 초월기 버프가 같은 선을 쓴다.</summary>
        public static bool IsNormalAttack(DamageContext context)
            => DirectAttack.IsDirect(context) && context.HasTag(DamageTag.NormalAttack);

        public override void CastCode()
        {
            if (Caster == null || _working) return;
            _working = true;
            _onDeath = _ => ClearAura();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            ActionScheduler.AnyActionStarted += OnAnyActionStarted;
            ApplyAura();
        }

        public override void StopCode()
        {
            ClearAura();
            if (!_working) return;
            _working = false;
            ActionScheduler.AnyActionStarted -= OnAnyActionStarted;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _onDeath);
            _onDeath = null;
        }

        /// <summary>필드의 아군 전원에게 오라를 건다. 이미 받은 아군은 건너뛴다(중복 없음).</summary>
        private void ApplyAura()
        {
            if (!Working || GridManager.Instance == null) return;
            foreach (Unit ally in GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList)
                         .Where(unit => unit != null && unit.isActive && unit.IsOnField && unit.IsEnemy == Caster.IsEnemy))
            {
                if (ally.HasStatusKey(AuraKey)) continue;
                UnitStatus aura = BuffStatus.Create(AuraStatusId, AuraKey, CodeName, Caster, ally,
                    new GrandArmyEffect(Caster), duration: 0, isBeneficial: true,
                    description: "나폴레옹이 필드에 있는 동안 일반 공격 피해 +15%.");
                if (ally.TryAddStatus(aura) && !_auraHolders.Contains(ally)) _auraHolders.Add(ally);
            }
        }

        private void ClearAura()
        {
            foreach (Unit holder in _auraHolders)
                if (holder != null) holder.RemoveStatusByKey(AuraKey);
            _auraHolders.Clear();
        }

        // 라운드 시작 순서와 무관하게 늦게 선 아군도 첫 행동 전에 오라를 받는다.
        private void OnAnyActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (unit != null && unit.IsEnemy == Caster?.IsEnemy && !unit.IsSummon && !unit.HasStatusKey(AuraKey)) ApplyAura();
        }

        /// <summary>대육군의 지휘 — 보유자의 일반 공격 피해 +15%, 나폴레옹 본인은 개전 행동 게이지 25% 선행.</summary>
        private sealed class GrandArmyEffect : BaseEffect
        {
            private readonly Unit _napoleon;

            public GrandArmyEffect(Unit napoleon) : base(0, NormalAttackBonus) => _napoleon = napoleon;

            public override bool IsBeneficial => true;

            private bool Active => _napoleon != null && _napoleon.isActive && _napoleon.IsOnField;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => Active && attacker == Target && IsNormalAttack(context) ? 1f + NormalAttackBonus : 1f;

            // 스케줄러는 모든 보유자의 효과를 모든 참가자에게 묻는다. 나폴레옹 자신의 오라만 자신에게 답해 한 번만 적용한다.
            public override float RoundStartActionAdjustment(Unit unit)
                => Active && Target == _napoleon && unit == _napoleon ? OpeningAdvance : 0f;
        }
    }
}
