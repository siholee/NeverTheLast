using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public static class GenericGiantStatusIds
    {
        public const int Bulwark = 7850;
        public const int FrostCore = 7851;
        public const int Shatter = 7852;
        public const int StunningBlow = 7853;
        public const int CoupDeGrace = 7854;
        public const int Adaptability = 7855;
        public const int ShatteredArmor = 7856;
    }

    /// <summary>파멸·종말의 거인 — 전투 시작 시 최대 체력 비례 방어막.</summary>
    public sealed class GenericGiantBulwark : PersistentStatusPassive
    {
        private readonly float _maxHpRatio;

        public GenericGiantBulwark(PassiveCodeContext context, float maxHpRatio)
            : base(context, GenericGiantStatusIds.Bulwark, "generic_giant_bulwark", "거인의 방벽",
                $"전투 시작 시 최대 체력의 {maxHpRatio * 100f:F0}%만큼 방어막을 얻습니다.")
        {
            _maxHpRatio = maxHpRatio;
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new GiantBulwarkEffect(_maxHpRatio);
    }

    internal sealed class GiantBulwarkEffect : BaseEffect
    {
        private readonly float _ratio;
        public GiantBulwarkEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override void OnApply()
        {
            if (Target == null) return;
            Target.AddShield(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _ratio)), Caster ?? Target);
        }
    }

    /// <summary>혹한의 거인 — 자기 턴마다 얼음을 부착하고 Snow 필드에서 STR·CON 레벨 성장량 +50%.</summary>
    public sealed class FrostGiantCore : PersistentStatusPassive
    {
        public FrostGiantCore(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.FrostCore, "frost_giant_core", "혹한의 노심",
                "턴 시작 시 얼음 원소를 부착합니다. 얼음이 부착된 채 눈 필드에 있으면 STR·CON의 레벨 성장량이 1.5배가 됩니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new FrostGiantCoreEffect();
    }

    internal sealed class FrostGiantCoreEffect : BaseEffect
    {
        public FrostGiantCoreEffect() : base(0) { }

        public override void OnOwnerTurn()
        {
            Target?.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster ?? Target);
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || (stat != BaseEnums.PrimaryStat.STR && stat != BaseEnums.PrimaryStat.CON)) return 0;
            if (!unit.HasAttachedElement(BaseEnums.UnitElement.Cryo)) return 0;
            if (GameManager.Instance?.RoundManager?.CurrentThemeHasTag("Snow") != true) return 0;
            return Mathf.RoundToInt(unit.GetLevelGrowthStatValue(stat) * 0.5f);
        }
    }

    public sealed class GiantShatter : PersistentStatusPassive
    {
        public GiantShatter(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.Shatter, "giant_shatter", "파쇄",
                "단일 대상 궁극기 적중 후 대상의 방어력이 3턴 동안 20% 감소합니다.")
        {
            SupersededByCodeId = VoidPrismCodeIds.DestructionRay;
        }

        protected override BaseEffect CreateInitialEffect() => new GiantShatterEffect();
    }

    internal sealed class GiantShatterEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;
        public GiantShatterEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            if (context?.Attacker != Target || context.Target == null || context.DamageDealt <= 0 ||
                damage?.CodeType != BaseEnums.CodeType.Ultimate ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;

            context.Target.AddStatus(BuffStatus.Create(
                GenericGiantStatusIds.ShatteredArmor, "giant_shattered_armor", "파쇄 — 방어 붕괴",
                Target, context.Target, new GiantArmorShredEffect(),
                duration: 3,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "방어력이 20% 감소합니다."));
        }
    }

    internal sealed class GiantArmorShredEffect : BaseEffect
    {
        public GiantArmorShredEffect() : base(0, 0.8f) { }
        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? 0.8f : 1f;
    }

    public sealed class GiantStunningBlow : PersistentStatusPassive
    {
        public GiantStunningBlow(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.StunningBlow, "giant_stunning_blow", "강타",
                "단일 대상 궁극기 적중 후 대상을 1턴 기절시킵니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantStunningBlowEffect();
    }

    internal sealed class GiantStunningBlowEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;
        public GiantStunningBlowEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            if (context?.Attacker != Target || context.Target == null || context.DamageDealt <= 0 ||
                damage?.CodeType != BaseEnums.CodeType.Ultimate ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;
            ControlStatuses.ApplyFixedStun(context.Target, Target, 1);
        }
    }

    /// <summary>행동불능 적을 치명타로 공격할 때 치명타 배율 자체에 +0.4.</summary>
    public sealed class GiantCoupDeGrace : PersistentStatusPassive
    {
        public GiantCoupDeGrace(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.CoupDeGrace, "giant_coup_de_grace", "병상첨병",
                "행동불능 상태의 적을 공격할 때 치명타 피해가 40% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantCoupDeGraceEffect();
    }

    internal sealed class GiantCoupDeGraceEffect : BaseEffect
    {
        public GiantCoupDeGraceEffect() : base(0, 0.4f) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || !target.isControlled || context?.IsCrit != true) return 1f;
            float normalCrit = Mathf.Max(1f, attacker.CritMultiplierCurr);
            return (normalCrit + 0.4f) / normalCrit;
        }
    }

    public sealed class GiantAdaptability : PersistentStatusPassive
    {
        public GiantAdaptability(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.Adaptability, "giant_adaptability", "적응력",
                "물리·접촉 기술에 적중당할 때마다 이번 전투 동안 STR이 1% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantAdaptabilityEffect();
    }

    internal sealed class GiantAdaptabilityEffect : BaseEffect
    {
        private Action<EventContext> _handler;
        private int _stacks;
        public GiantAdaptabilityEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _stacks = 0;
            _handler = OnAfterDamageTaken;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
            _stacks = 0;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            List<int> tags = damage?.DamageTags;
            if (context?.Grantee != Target || damage == null || damage.ResolvedDamage <= 0 || tags == null ||
                !tags.Contains(DamageTag.Physical) || !tags.Contains(DamageTag.ContactAttack)) return;
            _stacks++;
            Target.RefreshAttributes();
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR ? 1f + _stacks * 0.01f : 1f;
    }
}
