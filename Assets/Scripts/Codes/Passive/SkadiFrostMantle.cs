using System;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>스카디 — 피격 조건 없이 자신의 일반행동으로 쌓는 자기 방호.</summary>
    public sealed class SkadiFrostMantle : UniquePassiveCode
    {
        public const string StatusKey = "skadi_frost_mantle";
        public const float ShieldRatio = .60f, AttackBonus = .15f;
        private bool _working, _startGranted;
        private Action<EventContext> _normal, _death, _round;
        public SkadiFrostMantle(PassiveCodeContext context) : base(context) => CodeName = "혹한의 외투";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _startGranted = false;
            Caster.AddStatus(BuffStatus.Create(70830, StatusKey, CodeName, Caster, Caster,
                new StatModifierEffect(BaseEnums.StatType.Attack, () => _working && Caster.ShieldCurr > 0 ? AttackBonus : 0f, true),
                isBeneficial: true, description: "보호막을 보유한 동안 공격력 +15%. 중복되지 않습니다."));
            _normal = _ => GrantShield(); _death = _ => StopCode();
            _round = _ => StartingShield();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _death);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _round);
            Unit.AnyShieldChanged += ShieldChanged;
            StartingShield();
        }
        private void StartingShield()
        {
            if (_startGranted || !_working || Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            _startGranted = true; GrantShield();
        }
        private void GrantShield()
        {
            if (_working && Caster != null && Caster.isActive && Caster.IsOnField)
                Caster.AddShield(Mathf.RoundToInt(Caster.DefCurr * ShieldRatio), Caster);
        }
        private void ShieldChanged(Unit owner) { if (_working && owner == Caster) Caster.RefreshAttributes(); }
        public override void StopCode()
        {
            _working = false;
            Unit.AnyShieldChanged -= ShieldChanged;
            if (_normal != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            if (_death != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _death);
            if (_round != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundStart, _round);
            Caster?.RemoveStatusByKey(StatusKey);
            _normal = _death = _round = null;
        }
    }
}
