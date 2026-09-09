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
        public const string OrionSiriusResource = "orion_sirius_charge";
        public const int OrionSiriusMaximum = 5;
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

    /// <summary>오리온 P — 피해를 받거나 체력을 소비한 행동마다 시리우스 충전 1스택.</summary>
    public sealed class OrionGeoAffinity : UniquePassiveCode, ICounterAttackProvider
    {
        private Action<EventContext> _damageHandler;
        private Action<Unit, int> _hpSpentHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastChargeAction = int.MinValue;
        private bool _registered;

        public OrionGeoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "시리우스";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            Caster.SetCombatResourceMaximum(
                GreekHeroCombat.OrionSiriusResource,
                GreekHeroCombat.OrionSiriusMaximum,
                true);
            _lastChargeAction = int.MinValue;
            _damageHandler = OnDamageTaken;
            _hpSpentHandler = OnHpSpent;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Unit.AnyHpSpent += _hpSpentHandler;
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Unit.AnyHpSpent -= _hpSpentHandler;
            _damageHandler = null;
            _hpSpentHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnDamageTaken(EventContext context)
        {
            if (context?.Grantee == Caster && context.DmgCtx?.ResolvedDamage > 0)
                TryGainCharge();
        }

        private void OnHpSpent(Unit unit, int amount)
        {
            if (unit == Caster && amount > 0) TryGainCharge();
        }

        private void TryGainCharge()
        {
            if (Caster == null || !Caster.isActive) return;
            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastChargeAction == action) return;
            _lastChargeAction = action;

            int charge = Caster.AddCombatResource(GreekHeroCombat.OrionSiriusResource, 1);
            if (charge < GreekHeroCombat.OrionSiriusMaximum) return;

            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "orion_sirius", CodeName, ResolveSirius);
        }

        private void ResolveSirius()
        {
            if (Caster == null || !Caster.isActive ||
                !Caster.TryConsumeCombatResource(
                    GreekHeroCombat.OrionSiriusResource,
                    GreekHeroCombat.OrionSiriusMaximum)) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(120, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f) *
                Combat.Summons.DamageMultiplier(Caster)));
            foreach (Unit enemy in global::Target.GetAllEnemies(Caster)
                         .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList())
            {
                enemy.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.AdditionalAttack, DamageTag.CounterAttack,
                        DamageTag.SummonAttack, DamageTag.Physical, DamageTag.ContactAttack,
                    },
                    isCrit));
            }
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
            Caster, Caster, new HeavyArmorDurabilityEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "숙련된 중갑 아이템이 제공하는 내구도가 20% 증가합니다."));
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


    /// <summary>
    /// 팔랑크스 — 모든 Greek 유닛이 하드코딩으로 가지는 진형 패시브다.
    ///
    /// 재설계 전에는 '전열의 다른 전열 아군 수만큼 STR'이었지만,
    /// 이제는 <b>같은 패시브를 든 아군 수</b> n에 비례한다.
    ///   · 전투 시작 시 CON 위력 <c>30 × n</c> 보호막
    ///   · 상시 가하는 피해 <c>+5% × n</c> (n은 매 질의마다 다시 세므로 아군이 쓰러지면 즉시 줄어든다)
    ///
    /// 코드 용량을 차지하지 않고 전수도 되지 않는다.
    /// </summary>
    public sealed class GreekPhalanx : PassiveCode
    {
        public const int ShieldPowerPerAlly = 30;
        public const float DamageBonusPerAlly = 0.05f;

        public GreekPhalanx(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "팔랑크스";
            IgnoresActivationChance = true;
            Transferable = false;
            IgnoresCodeCapacity = true;   // 진영 공통 코드라 용량을 먹지 않는다
        }

        public override void CastCode()
        {
            if (Caster == null || !Caster.isActive) return;

            Caster.AddStatus(BuffStatus.Create(
                GreekHeroStatusIds.TheseusPhalanx, "greek_phalanx", CodeName,
                Caster, Caster, new GreekPhalanxEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "팔랑크스를 가진 아군 하나당 가하는 피해 +5%. 전투 시작 시 인원수에 비례한 보호막."));

            int count = GreekPhalanxEffect.HolderCount(Caster);
            int shield = Mathf.Max(1, Caster.SkillDamage(ShieldPowerPerAlly * count, BaseEnums.PrimaryStat.CON));
            Caster.AddShield(shield, Caster);
        }
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
            description: "턴마다 CON만큼 회복합니다. 물 또는 풀 원소가 있으면 2배가 되며 중첩되지 않습니다."));
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

    internal sealed class HeavyArmorDurabilityEffect : BaseEffect
    {
        public HeavyArmorDurabilityEffect() : base(0) { }
        public override float EquipmentDurabilityMultiplierModifier(Unit unit, Managers.ItemData item)
            => unit == Target && item != null &&
               item.RequiredProficiency == EquipmentProficiency.HeavyArmor &&
               unit.HasProficiency(EquipmentProficiency.HeavyArmor)
                ? 1.20f : 1f;
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

    /// <summary>팔랑크스 상시 효과 — 보유 아군 수에 비례해 가하는 피해가 늘어난다.</summary>
    internal sealed class GreekPhalanxEffect : BaseEffect
    {
        public GreekPhalanxEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            return 1f + GreekPhalanx.DamageBonusPerAlly * HolderCount(attacker);
        }

        /// <summary>필드에 살아 있는 팔랑크스 보유 아군 수(자신 포함). 실시간으로 다시 센다.</summary>
        public static int HolderCount(Unit unit)
        {
            if (unit == null) return 0;
            var allies = global::Target.GetAllAllies(unit)
                .Where(ally => ally != null && ally.isActive)
                .ToList();
            if (!allies.Contains(unit)) allies.Add(unit);
            return allies.Count(ally => ally.ActivePassiveCodes.Any(code => code is GreekPhalanx));
        }
    }

    internal sealed class TheseusSelfHealingEffect : BaseEffect
    {
        public TheseusSelfHealingEffect() : base(0) { }

        /// <summary>턴마다 CON의 2배를 회복한다. 턴 하나가 예전 2초에 해당한다.</summary>
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            bool doubled = Target.HasCombatElement(BaseEnums.UnitElement.Hydro) ||
                           Target.HasCombatElement(BaseEnums.UnitElement.Dendro);
            int heal = Mathf.Max(1, Target.GetBaseCon()) * 2 * (doubled ? 2 : 1);
            Target.ModifyHp(Target.HpCurr + heal, Caster ?? Target);
        }
    }

    internal sealed class OverhealShieldEffect : BaseEffect
    {
        private readonly float _ratio;
        public OverhealShieldEffect(float ratio) : base(0, ratio) => _ratio = ratio;
        public override float OverhealShieldConversionModifier(Unit unit) => unit == Target ? _ratio : 0f;
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
