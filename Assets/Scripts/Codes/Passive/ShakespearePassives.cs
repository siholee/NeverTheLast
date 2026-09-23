using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class ShakespeareCombat
    {
        public const string LanguageResource = "shakespeare_language";
        public const int LanguageMaximum = 999;
        public const int LanguageStatusId = 5210;
        public const int FieldStatusId = 5211;
        public const int ReviveStatusId = 5212;
    }

    /// <summary>셰익스피어 P — 소환수의 생성·소멸·행동을 언어 중첩으로 바꾼다.</summary>
    public sealed class ShakespeareLanguageCreator : UniquePassiveCode
    {
        public ShakespeareLanguageCreator(PassiveCodeContext context) : base(context)
        {
            CodeName = "언어의 창조자";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(ShakespeareCombat.LanguageResource,
                ShakespeareCombat.LanguageMaximum, resetCurrent: true);
            Caster.AddStatus(BuffStatus.Create(
                ShakespeareCombat.LanguageStatusId, "shakespeare_language_creator", CodeName,
                Caster, Caster, new ShakespeareLanguageEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "아군 소환수의 생성·소멸·행동마다 언어 1을 얻습니다. 소환수 공격 시 1을 소비해 확정 치명타로 만들고 기존 치명타 확률을 치명타 피해로 전환합니다."));
        }
    }

    internal sealed class ShakespeareLanguageEffect : BaseEffect
    {
        private readonly HashSet<Unit> _observed = new();
        private Action<Unit, Unit> _spawned;
        private Action<Unit, Unit> _died;
        private Action<Unit> _withdrew;
        private Action<EventContext> _acted;

        public ShakespeareLanguageEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _acted = _ => AddLanguage(1);
            _spawned = (owner, summon) =>
            {
                if (!IsAlly(owner) || summon == null) return;
                Observe(summon);
                AddLanguage(1);
            };
            _died = (unit, _) => OnSummonRemoved(unit);
            _withdrew = OnSummonRemoved;
            Unit.AnySummonSpawned += _spawned;
            Unit.AnyUnitDied += _died;
            Unit.AnyUnitWithdrew += _withdrew;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Target))
                if (Summons.IsSummonLike(ally) && ally != Target) Observe(ally);
        }

        private bool IsAlly(Unit unit)
            => Target != null && unit != null && unit.IsEnemy == Target.IsEnemy;

        private void Observe(Unit summon)
        {
            if (summon == null || !_observed.Add(summon)) return;
            summon.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _acted);
            summon.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _acted);
            summon.AddListener(BaseEnums.UnitEventType.OnSpecialActivates, _acted);
        }

        private void OnSummonRemoved(Unit unit)
        {
            if (unit == null || !IsAlly(unit) || !Summons.IsSummonLike(unit) || unit == Target) return;
            AddLanguage(1);
            StopObserving(unit);
        }

        private void StopObserving(Unit summon)
        {
            if (summon == null || !_observed.Remove(summon)) return;
            summon.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _acted);
            summon.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _acted);
            summon.RemoveListener(BaseEnums.UnitEventType.OnSpecialActivates, _acted);
        }

        private void AddLanguage(int amount)
        {
            if (Target == null || !Target.isActive) return;
            Target.AddCombatResource(ShakespeareCombat.LanguageResource, amount);
        }

        public override bool TryConsumeAlliedSummonCritical(Unit owner, Unit summonAttacker)
        {
            if (owner != Target || Target == null || !Target.IsOnField || summonAttacker == null ||
                summonAttacker.IsEnemy != Target.IsEnemy) return false;
            return Target.TryConsumeCombatResource(ShakespeareCombat.LanguageResource, 1);
        }

        public override void OnRemove()
        {
            Unit.AnySummonSpawned -= _spawned;
            Unit.AnyUnitDied -= _died;
            Unit.AnyUnitWithdrew -= _withdrew;
            foreach (Unit summon in _observed.ToList()) StopObserving(summon);
        }
    }

    /// <summary>셰익스피어 U의 3턴 결계. 소환수 피해와 1회 부활을 제공한다.</summary>
    internal sealed class ShakespeareSummonFieldEffect : BaseEffect
    {
        private readonly HashSet<Unit> _warded = new();
        private Action<Unit, Unit> _spawned;

        public ShakespeareSummonFieldEffect() : base(0) { }

        public override void OnApply()
        {
            _spawned = (owner, summon) =>
            {
                if (Target != null && owner != null && owner.IsEnemy == Target.IsEnemy) AddWard(summon);
            };
            Unit.AnySummonSpawned += _spawned;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Target)) AddWard(ally);
        }

        private void AddWard(Unit unit)
        {
            if (Target == null || unit == null || unit.IsEnemy != Target.IsEnemy ||
                !Summons.IsSummonLike(unit) || !_warded.Add(unit)) return;
            unit.AddStatus(BuffStatus.Create(
                ShakespeareCombat.ReviveStatusId,
                $"shakespeare_summon_revival_{Target.GetEntityId()}", "사느냐, 죽느냐",
                Target, unit, new ShakespeareSummonReviveEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "결계가 유지되는 동안 처음 쓰러질 때 최대 체력으로 즉시 부활합니다."));
        }

        public override float AlliedSummonDamageBonusAdditive(Unit unit, Unit summonOwner)
            => unit == Target && Target != null && Target.IsOnField && Summons.IsSummonLike(summonOwner)
                ? 0.25f
                : 0f;

        public override void OnRemove()
        {
            Unit.AnySummonSpawned -= _spawned;
            if (Target == null) return;
            string key = $"shakespeare_summon_revival_{Target.GetEntityId()}";
            foreach (Unit unit in _warded.Where(unit => unit != null).ToList()) unit.RemoveStatusByKey(key);
            _warded.Clear();
        }
    }

    internal sealed class ShakespeareSummonReviveEffect : BaseEffect
    {
        private bool _used;

        public ShakespeareSummonReviveEffect() : base(0) { }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;
            unit.ModifyHp(unit.HpMax, Caster);
            Debug.Log($"[사느냐, 죽느냐] {unit.UnitName}이(가) 한 번 부활했다");
            return true;
        }
    }
}
