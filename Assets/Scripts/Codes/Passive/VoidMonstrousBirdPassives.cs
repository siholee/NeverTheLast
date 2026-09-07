using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidMonstrousBirdCodeIds
    {
        public const int Rebirth = 1450;
        public const int Acceleration = 1451;
        public const int SharpBeak = 1452;
    }

    public static class VoidMonstrousBirdStatusIds
    {
        public const int Rebirth = 7890;
        public const int Acceleration = 7891;
        public const int SharpBeak = 7892;
    }

    /// <summary>전투 중 한 번 치명 피해를 막고 즉시 최대 체력으로 부활한다.</summary>
    public sealed class VoidMonstrousBirdRebirth : PersistentStatusPassive
    {
        public VoidMonstrousBirdRebirth(PassiveCodeContext context)
            : base(context, VoidMonstrousBirdStatusIds.Rebirth, "void_monstrous_bird_rebirth",
                "괴조의 부활", "전투 중 한 번 치명적인 피해를 입으면 최대 체력으로 부활합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidMonstrousBirdRebirthEffect();
    }

    internal sealed class VoidMonstrousBirdRebirthEffect : BaseEffect
    {
        private Action<EventContext> _afterDamageHandler;
        private bool _used;
        private bool _restorePending;
        private bool _revivalGuardActive;

        public VoidMonstrousBirdRebirthEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _used = false;
            _restorePending = false;
            _revivalGuardActive = false;
            _afterDamageHandler = OnAfterDamageTaken;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterDamageHandler);
        }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;
            _restorePending = true;
            _revivalGuardActive = true;
            unit.AddUntargetableSource();
            return true;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            if (!_restorePending || Target == null || context?.Grantee != Target || !Target.isActive) return;
            _restorePending = false;
            Target.ModifyHp(Target.HpMax, Target);
            Target.RemoveUntargetableSource();
            _revivalGuardActive = false;
            Debug.Log($"[괴조의 부활] {Target.UnitName}이(가) 최대 체력으로 부활했습니다.");
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterDamageHandler);
            if (_revivalGuardActive && Target != null) Target.RemoveUntargetableSource();
            _afterDamageHandler = null;
            _restorePending = false;
            _revivalGuardActive = false;
        }
    }

    /// <summary>
    /// 일반행동을 마칠 때마다 이번 전투 동안 DEX +1. 중첩 제한은 없다.
    ///
    /// DEX는 행동 속도라 <b>빨라질수록 더 자주 쌓는 되먹임</b>이 있다. 다만 속도가
    /// <c>1 + DEX×0.01</c>이라 기본 DEX가 이미 큰 상태에서는 한 중첩의 수익이 계속 줄어든다.
    /// 철벽(CON)의 절반값을 쓰는 것은 그 되먹임을 감안한 몫이다.
    /// </summary>
    public sealed class VoidMonstrousBirdAcceleration : PersistentStatusPassive
    {
        private const int DexPerAction = 1;
        private Action<EventContext> _actionHandler;
        private int _stacks;

        public VoidMonstrousBirdAcceleration(PassiveCodeContext context)
            : base(context, VoidMonstrousBirdStatusIds.Acceleration, "void_monstrous_bird_acceleration",
                "가속", $"일반행동을 할 때마다 이번 전투 동안 DEX +{DexPerAction}. 중첩됩니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _stacks = 0;
            _actionHandler = OnNormalActionResolved;
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            _actionHandler = null;
            _stacks = 0;
        }

        private void OnNormalActionResolved(EventContext context)
        {
            if (Caster == null || !Registered || context?.Grantee != Caster || !Caster.isActive) return;

            _stacks++;
            int bonus = DexPerAction * _stacks;
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, bonus),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"이번 전투 동안 DEX +{bonus} ({_stacks}중첩)"));
        }
    }

    /// <summary>단일 공격 적중 시 현재 LUK% 확률로 최대 체력 3% 출혈을 3턴 부여한다.</summary>
    public sealed class VoidMonstrousBirdSharpBeak : PersistentStatusPassive
    {
        public VoidMonstrousBirdSharpBeak(PassiveCodeContext context)
            : base(context, VoidMonstrousBirdStatusIds.SharpBeak, "void_monstrous_bird_sharp_beak",
                "날카로운 부리", "단일 공격 적중 시 LUK% 확률로 대상에게 출혈을 3턴 부여합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidMonstrousBirdSharpBeakEffect();
    }

    internal sealed class VoidMonstrousBirdSharpBeakEffect : BaseEffect
    {
        private const int BleedTurns = 3;
        private const float BleedMaxHpPercent = 3f;
        private Action<DamageResolvedContext> _damageHandler;

        public VoidMonstrousBirdSharpBeakEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _damageHandler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            Unit victim = context?.Target;
            // damage?.CodeType == Effect는 damage가 null일 때 false라 단락되지 않는다.
            // 형제 코드(파쇄·파괴광선)와 같이 null이면 곧바로 빠져나가도록 먼저 거른다.
            if (context?.Attacker != Target || victim == null || !victim.isActive ||
                context.DamageDealt <= 0 || damage == null ||
                damage.CodeType == BaseEnums.CodeType.Effect ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;

            float chance = Mathf.Clamp01(Target.GetBaseLuk() * 0.01f);
            if (UnityEngine.Random.value >= chance) return;

            BleedStatus.Apply(victim, Target, BleedTurns, BleedMaxHpPercent, "날카로운 부리");
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            _damageHandler = null;
        }
    }
}
