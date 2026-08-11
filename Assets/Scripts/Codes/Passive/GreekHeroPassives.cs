using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public static class GreekHeroCombat
    {
        // 0.25 스택을 정수 1칸으로 표현한다. 12칸 = 최대 3스택, 강화 공격은 4칸을 소비한다.
        public const string TheseusCodeWeaveResource = "theseus_code_weave_quarters";
        public const int TheseusResourceMaximum = 12;
        public const int TheseusEnhancedAttackCost = 4;
    }

    public static class GreekHeroStatusIds
    {
        public const int OrionGeoAffinity = 150;
        public const int OrionHeavyInfantry = 151;
        public const int OrionSentinel = 152;
        public const int TheseusHydroAffinity = 153;
        public const int TheseusPhalanx = 154;
        public const int TheseusSelfHealing = 155;
        public const int TheseusOverheal = 156;
        public const int OrionArmorBreak = 157;
    }

    /// <summary>오리온 고유 패시브: 바위 원소 보유 중 매초 CON +1(라운드 동안 누적).</summary>
    public sealed class OrionGeoAffinity : UniquePassiveCode
    {
        public OrionGeoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 바위";
            IgnoresActivationChance = true;
            TransferVersionCodeId = 231;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                GreekHeroStatusIds.OrionGeoAffinity, "orion_geo_affinity", CodeName,
                Caster, Caster, new OrionGeoAffinityEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "바위 원소를 보유한 동안 매초 CON이 1 증가합니다."));
        }
    }

    public sealed class OrionHeavyInfantry : PassiveCode
    {
        public OrionHeavyInfantry(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "중갑병";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.OrionHeavyInfantry, "orion_heavy_infantry", CodeName,
            Caster, Caster, new EquippedStatEffect(EquipmentProficiency.HeavyArmor, BaseEnums.PrimaryStat.STR, 4),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "중갑 착용 중 STR +4."));
    }

    /// <summary>활 계열 무기로 단일 대상 피해를 입혔을 때 STR만큼 고정 피해를 추가한다.</summary>
    public sealed class OrionMarksman : PassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public OrionMarksman(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "명사수";
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
            if (context?.Target == null || !context.Target.isActive || !Caster.HasEquippedBow()) return;
            DamageContext original = context.DamageContext;
            if (original == null || original.CodeType == BaseEnums.CodeType.Effect ||
                original.DamageTags == null ||
                !original.DamageTags.Contains(DamageTag.SingleTarget) ||
                original.DamageTags.Contains(DamageTag.TrueDamage)) return;

            context.Target.TakeDamage(new DamageContext(
                Caster,
                Mathf.Max(1, Caster.GetBaseStr()),
                BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage },
                false));
        }
    }

    public sealed class OrionSentinel : PassiveCode
    {
        public OrionSentinel(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "파수꾼";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.OrionSentinel, "orion_sentinel", CodeName,
            Caster, Caster, new OrionSentinelEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "전열에 있을 때 필드의 후열 아군 하나당 STR +1."));
    }

    /// <summary>테세우스 고유 패시브. 일반 코드 +0.25, 궁극기 코드 +1(궁극기는 +0.25를 중복 획득하지 않음).</summary>
    public sealed class TheseusCodeWeave : UniquePassiveCode
    {
        private Action<EventContext> _normalHandler;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public TheseusCodeWeave(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "코드 직조";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(GreekHeroCombat.TheseusCodeWeaveResource, GreekHeroCombat.TheseusResourceMaximum, true);
            if (_registered) return;
            _normalHandler = _ => Caster.AddCombatResource(GreekHeroCombat.TheseusCodeWeaveResource, 1);
            _ultimateHandler = _ => Caster.AddCombatResource(GreekHeroCombat.TheseusCodeWeaveResource, 4);
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _normalHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _normalHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            _registered = false;
        }
    }

    public sealed class TheseusHydroAffinity : PassiveCode
    {
        public TheseusHydroAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 물";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.TheseusHydroAffinity, "theseus_hydro_affinity", CodeName,
            Caster, Caster, new TheseusHydroAffinityEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "물 원소 보유 중 올스탯 +1, 마나 회복 효율 +20%."));
    }

    public sealed class TheseusFighter : PassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public TheseusFighter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "투사";
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
            List<int> tags = context?.DamageContext?.DamageTags;
            if (context == null || context.DamageDealt <= 0 || tags == null ||
                !tags.Contains(DamageTag.Physical) || !tags.Contains(DamageTag.ContactAttack)) return;
            Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(context.DamageDealt * 0.25f), Caster);
        }
    }

    public sealed class TheseusPhalanx : PassiveCode
    {
        public TheseusPhalanx(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "팔랑크스";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.TheseusPhalanx, "theseus_phalanx", CodeName,
            Caster, Caster, new TheseusPhalanxEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "전열에 있을 때 자신 외 전열 아군 하나당 STR +1."));
    }

    public sealed class TheseusSelfHealing : PassiveCode
    {
        public TheseusSelfHealing(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "자가치유";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.TheseusSelfHealing, "theseus_self_healing", CodeName,
            Caster, Caster, new TheseusSelfHealingEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "매초 CON만큼 회복합니다. 물 또는 풀 원소가 있으면 2배가 되며 중첩되지 않습니다."));
    }

    public sealed class TheseusOverheal : PassiveCode
    {
        public TheseusOverheal(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "과다치유";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            GreekHeroStatusIds.TheseusOverheal, "theseus_overheal", CodeName,
            Caster, Caster, new OverhealShieldEffect(0.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "최대 체력을 넘긴 회복량의 25%를 보호막으로 전환합니다."));
    }

    internal sealed class OrionGeoAffinityEffect : BaseEffect
    {
        private float _elapsed;
        private int _conStacks;
        public OrionGeoAffinityEffect() : base(0) { }

        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || !Target.isActive || !Target.HasCombatElement(BaseEnums.UnitElement.Geo)) return;
            _elapsed += deltaTime;
            int gained = Mathf.FloorToInt(_elapsed);
            if (gained <= 0) return;
            _elapsed -= gained;
            _conStacks += gained;
            Target.RefreshAttributes();
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.CON ? _conStacks : 0;
    }

    internal sealed class EquippedStatEffect : BaseEffect
    {
        private readonly EquipmentProficiency _proficiency;
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;
        public EquippedStatEffect(EquipmentProficiency proficiency, BaseEnums.PrimaryStat stat, int amount) : base(0, amount)
        {
            _proficiency = proficiency;
            _stat = stat;
            _amount = amount;
        }
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == _stat && unit.HasEquippedProficiency(_proficiency) ? _amount : 0;
    }

    internal sealed class OrionSentinelEffect : BaseEffect
    {
        public OrionSentinelEffect() : base(0) { }
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != BaseEnums.PrimaryStat.STR || !GreekHeroPosition.IsFront(unit)) return 0;
            int rearColumn = GridManager.Instance.GetRearColumn(unit.IsEnemy);
            return global::Target.GetAllAllies(unit).Count(ally => ally != null && ally.isActive &&
                ally.currentCell != null && ally.currentCell.xPos == rearColumn);
        }
    }

    internal sealed class TheseusHydroAffinityEffect : BaseEffect
    {
        public TheseusHydroAffinityEffect() : base(0) { }
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && unit.HasCombatElement(BaseEnums.UnitElement.Hydro) ? 1 : 0;
        public override float ManaRecoveryMultiplierModifier(Unit unit)
            => unit == Target && unit.HasCombatElement(BaseEnums.UnitElement.Hydro) ? 1.2f : 1f;
    }

    internal sealed class TheseusPhalanxEffect : BaseEffect
    {
        public TheseusPhalanxEffect() : base(0) { }
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != BaseEnums.PrimaryStat.STR || !GreekHeroPosition.IsFront(unit)) return 0;
            int frontColumn = GridManager.Instance.GetFrontColumn(unit.IsEnemy);
            return global::Target.GetAllAllies(unit).Count(ally => ally != null && ally != unit && ally.isActive &&
                ally.currentCell != null && ally.currentCell.xPos == frontColumn);
        }
    }

    internal sealed class TheseusSelfHealingEffect : BaseEffect
    {
        private float _elapsed;
        public TheseusSelfHealingEffect() : base(0) { }
        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || !Target.isActive) return;
            _elapsed += deltaTime;
            int ticks = Mathf.FloorToInt(_elapsed);
            if (ticks <= 0) return;
            _elapsed -= ticks;
            bool doubled = Target.HasCombatElement(BaseEnums.UnitElement.Hydro) ||
                           Target.HasCombatElement(BaseEnums.UnitElement.Dendro);
            int heal = Mathf.Max(1, Target.GetBaseCon()) * ticks * (doubled ? 2 : 1);
            Target.ModifyHp(Target.HpCurr + heal, Caster ?? Target);
        }
    }

    internal sealed class OverhealShieldEffect : BaseEffect
    {
        private readonly float _ratio;
        public OverhealShieldEffect(float ratio) : base(0, ratio) => _ratio = ratio;
        public override float OverhealShieldConversionModifier(Unit unit) => unit == Target ? _ratio : 0f;
    }

    public sealed class ArmorBreakEffect : BaseEffect
    {
        private readonly float _multiplier;
        public ArmorBreakEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? _multiplier : 1f;
    }

    internal static class GreekHeroPosition
    {
        public static bool IsFront(Unit unit)
        {
            return unit != null && unit.currentCell != null && GridManager.Instance != null &&
                   unit.currentCell.xPos == GridManager.Instance.GetFrontColumn(unit.IsEnemy);
        }
    }
}
