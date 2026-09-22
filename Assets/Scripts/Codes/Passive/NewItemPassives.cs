using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class NewItemIds
    {
        public const int SaintCatherineSword = 4510;
        public const int SalvationBanner = 4328;
        public const int GuardianTalisman = 4329;
        public const int HeavenlyFruit = 4330;
        public const int SpiritBlessingRing = 4331;
        public const int KittyHawkMiracle = 4332;
        public const int AlternatingCurrentDevice = 4411;
        public const int WinterTriangle = 4511;
        public const int Iliad = 4333;
        public const int GoldenFleece = 4512;
    }

    internal static class NewItemStatusIds
    {
        public const int Conquest = 6445;
        public const int BombardMe = 6446;
        public const int GuardianTalisman = 6447;
        public const int HeavenlyFruit = 6448;
        public const int SpiritBlessing = 6449;
        public const int KittyHawk = 6450;
        public const int AlternatingCurrent = 6451;
        public const int WinterTriangle = 6452;
        public const int Iliad = 6453;
        public const int GoldenFleece = 6454;
    }

    /// <summary>정복 — 승승장구의 금색 상위. 처치마다 물리 피해 +10%.</summary>
    public sealed class ConquestItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_conquest";

        public ConquestItemPassive(PassiveCodeContext context) : base(context, "정복")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            // 해금 패시브가 먼저 발동해도 장비의 강화 등급이 그 상태를 걷어 낸다.
            Caster?.RemoveStatusByKey("thor_momentum");
            AddPermanentStatus(NewItemStatusIds.Conquest,
                new KillStackTaggedDamageEffect(DamageTag.Physical, 0.10f),
                "적 처치마다 물리 태그로 가하는 피해 +10%. 중첩됩니다.");
        }
    }

    /// <summary>내 머리 위에 포격을 — 궁극기마다 최대 체력 5%를 낼 수 있으면 피해 +25%.</summary>
    public sealed class BombardMeItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_bombard_me";
        public BombardMeItemPassive(PassiveCodeContext context) : base(context, "내 머리 위에 포격을") { }

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.BombardMe, new BombardMeEffect(),
            "궁극기 발동 시 최대 체력 5%를 소모할 수 있으면 소모하고 해당 궁극기 피해 +25%.");
    }

    internal sealed class BombardMeEffect : BaseEffect
    {
        public BombardMeEffect() : base(0) { }

        public override float PrepareUltimateDamageMultiplier(Unit unit)
        {
            if (unit == null || unit != Target) return 1f;
            return unit.TryConsumeAttackHp(0.05f, false, out _) ? 1.25f : 1f;
        }
    }

    /// <summary>수호부 — 야타의 거울과 같지만 발동한 장비는 파괴된다.</summary>
    public sealed class GuardianTalismanItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_guardian_talisman";
        private bool _triggered;

        public GuardianTalismanItemPassive(PassiveCodeContext context) : base(context, "수호부") { }

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.GuardianTalisman,
            new YataReviveEffect(() =>
            {
                _triggered = true;
                Caster?.ScheduleEquippedItemDestruction(NewItemIds.GuardianTalisman);
            }),
            "치명 피해를 막고 2턴 경직 후 최대 체력으로 부활하며 수호부가 파괴됩니다.");

        public override void StopCode()
        {
            // 발동 뒤에는 장비만 없어지고 이미 시작한 2턴 부활은 끝까지 진행한다.
            if (!_triggered) base.StopCode();
        }
    }

    /// <summary>천상의 과일 — HP 25% 이하에서 50%를 회복하고 파괴된다.</summary>
    public sealed class HeavenlyFruitItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_heavenly_fruit";
        public HeavenlyFruitItemPassive(PassiveCodeContext context) : base(context, "천상의 과일") { }

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.HeavenlyFruit,
            new HeavenlyFruitEffect(NewItemIds.HeavenlyFruit),
            "체력이 25% 이하가 되면 최대 체력 50%를 회복하고 장비가 파괴됩니다.");
    }

    internal sealed class HeavenlyFruitEffect : BaseEffect
    {
        private readonly int _itemId;
        private Action<EventContext> _handler;
        private Action<Unit, int> _hpSpentHandler;
        private bool _used;

        public HeavenlyFruitEffect(int itemId) : base(0) => _itemId = itemId;

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => TryActivate();
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _hpSpentHandler = (unit, _) =>
            {
                if (unit == Target) TryActivate();
            };
            Unit.AnyHpSpent += _hpSpentHandler;
        }

        public override void OnRemove()
        {
            if (Target != null && _handler != null)
                Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            if (_hpSpentHandler != null) Unit.AnyHpSpent -= _hpSpentHandler;
            _handler = null;
            _hpSpentHandler = null;
        }

        private void TryActivate()
        {
            if (_used || Target == null || !Target.isActive || Target.HpMax <= 0 ||
                Target.HpCurr > Target.HpMax * 0.25f) return;

            _used = true;
            int heal = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * 0.50f));
            Target.ModifyHp(Target.HpCurr + heal, Target);
            Target.ScheduleEquippedItemDestruction(_itemId);
        }
    }

    public sealed class SpiritBlessingRingItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_spirit_blessing";
        public SpiritBlessingRingItemPassive(PassiveCodeContext context) : base(context, "정령의 축복") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.SpiritBlessing, new TurnRegenerationEffect(1f / 16f),
            "자신의 턴이 시작될 때 최대 체력의 1/16을 회복합니다.");
    }

    internal sealed class TurnRegenerationEffect : BaseEffect
    {
        private readonly float _ratio;
        public TurnRegenerationEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive || Target.HpCurr >= Target.HpMax) return;
            Target.ModifyHp(Target.HpCurr + Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _ratio)), Target);
        }
    }

    public sealed class KittyHawkMiracleItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_kittyhawk_miracle";
        public KittyHawkMiracleItemPassive(PassiveCodeContext context) : base(context, "키티호크의 기적") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.KittyHawk, new TargetPriorityEffect(-1), "자신의 타겟 우선도 -1.");
    }

    internal sealed class TargetPriorityEffect : BaseEffect
    {
        private readonly int _amount;
        public TargetPriorityEffect(int amount) : base(0, amount) => _amount = amount;
        public override int TargetPriorityAdditiveModifier(Unit unit) => unit == Target ? _amount : 0;
    }

    public sealed class AlternatingCurrentDeviceItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_alternating_current_device";
        public AlternatingCurrentDeviceItemPassive(PassiveCodeContext context) : base(context, "교류 발전 장치") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.AlternatingCurrent, new ElementalOwnerDamageEffect(BaseEnums.UnitElement.Electro, 1.07f),
            "장착자가 번개 원소일 경우 가하는 피해 +7%.");
    }

    internal sealed class ElementalOwnerDamageEffect : BaseEffect
    {
        private readonly BaseEnums.UnitElement _element;
        private readonly float _multiplier;
        public ElementalOwnerDamageEffect(BaseEnums.UnitElement element, float multiplier) : base(0, multiplier)
        {
            _element = element;
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && attacker.HasCombatElement(_element) ? _multiplier : 1f;
    }

    public sealed class WinterTriangleItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_winter_triangle";
        public WinterTriangleItemPassive(PassiveCodeContext context) : base(context, "겨울의 대삼각형") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.WinterTriangle,
            new TaggedDamageMultiplierEffect(DamageTag.CounterAttack, 1.25f),
            "반격 태그로 가하는 피해 +25%.");
    }

    internal sealed class TaggedDamageMultiplierEffect : BaseEffect
    {
        private readonly int _tag;
        private readonly float _multiplier;
        public TaggedDamageMultiplierEffect(int tag, float multiplier) : base(0, multiplier)
        {
            _tag = tag;
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(_tag) == true ? _multiplier : 1f;
    }

    public sealed class IliadItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_iliad";
        public IliadItemPassive(PassiveCodeContext context) : base(context, "일리아스") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.Iliad, new GreekInitialStatEffect(1.5f),
            "그리스 속성이라면 성장·훈련을 제외한 초기 5대 스탯이 1.5배가 됩니다.");
    }

    internal sealed class GreekInitialStatEffect : BaseEffect
    {
        private readonly float _multiplier;
        public GreekInitialStatEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float InitialPrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && unit.HasUnitTag("Greek") ? _multiplier : 1f;
    }

    /// <summary>콜키스의 황금양모 — 착용자의 현재 LUK만큼 내구도를 더한다.</summary>
    public sealed class GoldenFleeceItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_golden_fleece";
        public GoldenFleeceItemPassive(PassiveCodeContext context) : base(context, "황금양모의 광휘") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.GoldenFleece, new LuckDurabilityEffect(),
            "착용자의 LUK만큼 내구도를 얻습니다.");
    }

    internal sealed class LuckDurabilityEffect : BaseEffect
    {
        public LuckDurabilityEffect() : base(0) { }
        public override int DurabilityAdditiveModifier(Unit unit)
            => unit == Target ? Mathf.Max(0, unit.GetBaseLuk()) : 0;
    }
}
