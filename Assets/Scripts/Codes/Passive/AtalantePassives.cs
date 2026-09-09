using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class AtalanteStatusIds
    {
        public const int Innate = 5200;
        public const int Agility = 5201;
        public const int GlassCannon = 5202;
        public const int Hunter = 5203;
        public const int Venom = 5204;
        public const int Sniper = 5205;
        public const int HuntressBlessing = 5206;
    }

    /// <summary>적의 부정 상태 1개당 방어력 5% 무시(최대 20%).</summary>
    public sealed class AtalanteWeaknessTracker : UniquePassiveCode
    {
        public AtalanteWeaknessTracker(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "약점 추적";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                AtalanteStatusIds.Innate, "atalante_innate", CodeName,
                Caster, Caster, new AtalanteDebuffPenetrationEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "대상의 디버프 1개당 방어력을 5% 무시합니다(최대 20%)."));
        }
    }

    /// <summary>Lv.4: 장궁·단궁·쇠뇌 숙련. 실제 장비 판정은 Unit에서 수행한다.</summary>
    /// <summary>
    /// 궁수 — 장궁·단궁·쇠뇌 중 하나를 <b>장착했고 숙련도 갖췄을 때</b> STR +5%.
    /// 숙련만 주던 예전 사양에서 바뀌었다(바유와 공유하는 공용 코드).
    /// </summary>
    public sealed class AtalanteArcher : PassiveCode
    {
        public AtalanteArcher(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "궁수";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                5140, "archer_bow_mastery", CodeName, Caster, Caster,
                new ArcherBowBonusEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "활 계열 무기를 장착하고 숙련되어 있으면 STR +5%."));
        }
    }

    internal sealed class ArcherBowBonusEffect : Effects.Base.BaseEffect
    {
        public ArcherBowBonusEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR && unit.HasEquippedBow() ? 1.05f : 1f;
    }

    public sealed class AtalanteAgility : PassiveCode
    {
        public AtalanteAgility(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "민첩함";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                AtalanteStatusIds.Agility, "atalante_agility", CodeName,
                Caster, Caster, new PrimaryStatMultiplierEffect(1.05f, BaseEnums.PrimaryStat.DEX),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "DEX +5%."));
        }
    }

    /// <summary>Lv.10: 최대 체력 -25%, 방어력 20% 무시.</summary>
    public sealed class AtalanteGlassCannon : PassiveCode
    {
        public AtalanteGlassCannon(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "유리대포";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            var status = BuffStatus.Create(
                AtalanteStatusIds.GlassCannon, "atalante_glass_cannon", CodeName,
                Caster, Caster, new AtalanteGlassCannonEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "최대 체력이 25% 감소하고 적 방어력을 20% 무시합니다.");
            Caster.AddStatus(status);
        }
    }

    /// <summary>Lv.18: 짐승 또는 괴수 대상 피해 +30%.</summary>
    public sealed class AtalanteHunter : PassiveCode
    {
        public AtalanteHunter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사냥꾼";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                AtalanteStatusIds.Hunter, "atalante_hunter", CodeName,
                Caster, Caster, new AtalanteHunterEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "짐승 또는 괴수 분류의 적에게 가하는 피해가 30% 증가합니다."));
        }
    }

    /// <summary>Lv.30: 스킬 공격 적중 시 맹독을 2턴간 부여.</summary>
    public sealed class AtalanteVirulentPoison : PassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AtalanteVirulentPoison(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "맹독";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            BaseEnums.CodeType? codeType = context?.DamageContext?.CodeType;
            if (target == null || !target.isActive || target.IsEnemy == Caster.IsEnemy ||
                codeType == null || codeType == BaseEnums.CodeType.Effect) return;

            var status = new UnitStatus(new StatusDefinition
            {
                Id = AtalanteStatusIds.Venom,
                Key = $"atalante_venom_{Caster.GetEntityId()}",
                Name = "맹독",
                Description = "2턴 동안 턴마다 40 + 시전자 CON의 10%에 해당하는 지속피해를 받습니다.",
                Category = BaseEnums.StatusCategory.Negative,
                StackPolicy = BaseEnums.StatusStackPolicy.Replace,
                Duration = 2,
            }, Caster, target);
            status.AddEffect(new AtalanteVenomEffect());
            target.AddStatus(status);
        }
    }

    /// <summary>Lv.41: 후열에서 적 후열을 공격하면 피해 +20%.</summary>
    public sealed class AtalanteSniper : PassiveCode
    {
        public AtalanteSniper(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "저격수";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                AtalanteStatusIds.Sniper, "atalante_sniper", CodeName,
                Caster, Caster, new AtalanteSniperEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "후열에서 적 후열을 공격할 때 가하는 피해가 20% 증가합니다."));
        }
    }

    /// <summary>Lv.67: 피해를 입은 적 / 지속피해 대상 조건당 피해 +20%.</summary>
    public sealed class AtalanteHuntressBlessing : PassiveCode
    {
        public AtalanteHuntressBlessing(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사냥신의 가호";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                AtalanteStatusIds.HuntressBlessing, "atalante_huntress_blessing", CodeName,
                Caster, Caster, new AtalanteHuntressBlessingEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "최대 체력이 아니거나 지속피해 상태인 적에게 조건당 피해가 20% 증가합니다."));
        }
    }

    internal sealed class AtalanteDebuffPenetrationEffect : BaseEffect
    {
        public AtalanteDebuffPenetrationEffect() : base(0) { }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            int debuffCount = target.GetAllStatuses().Count(status => status.Category == BaseEnums.StatusCategory.Negative);
            return 1f - Mathf.Min(4, debuffCount) * 0.05f;
        }
    }

    internal sealed class AtalanteGlassCannonEffect : BaseEffect
    {
        public AtalanteGlassCannonEffect() : base(0) { }
        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? 0.75f : 1f;
        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? 0.8f : 1f;
    }

    internal sealed class AtalanteHunterEffect : BaseEffect
    {
        public AtalanteHunterEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && (target.HasUnitTag("Beast") || target.HasUnitTag("Monster"))
                ? 1.3f
                : 1f;
    }

    internal sealed class AtalanteSniperEffect : BaseEffect
    {
        public AtalanteSniperEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target?.currentCell == null || attacker.currentCell == null || GridManager.Instance == null)
                return 1f;
            bool attackerRear = attacker.currentCell.xPos == GridManager.Instance.GetRearColumn(attacker.IsEnemy);
            bool targetRear = target.currentCell.xPos == GridManager.Instance.GetRearColumn(target.IsEnemy);
            return attackerRear && targetRear ? 1.2f : 1f;
        }
    }

    internal sealed class AtalanteHuntressBlessingEffect : BaseEffect
    {
        public AtalanteHuntressBlessingEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            float multiplier = target.HpCurr < target.HpMax ? 1.2f : 1f;
            if (target.HasDamageOverTimeStatus()) multiplier *= 1.2f;
            return multiplier;
        }
    }

    internal sealed class AtalanteVenomEffect : BaseEffect
    {
        public override bool IsDamageOverTime => true;

        public AtalanteVenomEffect() : base(0) { }

        /// <summary>대상의 턴마다 한 번 터진다. 턴 하나가 예전 2초에 해당하므로 값도 2배다.</summary>
        public override void OnOwnerTurn() => DealTick();

        public override int EstimateDamagePerTurn() => CalculateDamage();

        private int CalculateDamage()
        {
            if (Caster == null) return 0;
            float multiplier = Caster.GetDamageOverTimeApplicationMultiplier();
            return Mathf.Max(0, Mathf.RoundToInt((40f + Caster.GetBaseCon() * 0.1f) * multiplier));
        }

        private void DealTick()
        {
            if (Caster == null || Target == null || !Target.isActive || Target.HpCurr <= 0) return;
            Target.TakeDamage(new DamageContext(
                Caster, CalculateDamage(), BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SingleTarget, DamageTag.NonContactAttack }));
        }
    }
}
