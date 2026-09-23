using System;
using System.Collections.Generic;
using System.Linq;
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
        public const int FoxBead = 4513;
        public const int NotreDame = 4514;
        public const int LesMiserables = 4412;
        public const int NamelessMercenaryShield = 4413;
        public const int CursedCrown = 4334;
        public const int JustinianCrown = 4515;
        public const int ReplicatorCloak = 4335;
        public const int GoldenArmor = 4336;
        public const int ManekiNeko = 4337;
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
        public const int FoxBead = 6455;
        public const int Grace = 6456;
        public const int LesMiserables = 6457;
        public const int NamelessMercenaryShield = 6458;
        public const int CursedCrown = 6459;
        public const int ImperialEcho = 6460;
        public const int SpellReplication = 6461;
        public const int GoldenArmor = 6462;
        public const int ManekiNeko = 6463;
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

    /// <summary>여우구슬 — 거느린 소환수로 LUK을, 필드 전체의 소환수로 INT를 불린다.</summary>
    public sealed class FoxBeadItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_fox_bead";
        public FoxBeadItemPassive(PassiveCodeContext context) : base(context, "여우구슬") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.FoxBead, new FoxBeadSummonScalingEffect(),
            "자신이 거느린 소환수 1기마다 LUK +3%, 필드 위 모든 소환수 1기마다 INT +2를 얻습니다.");
    }

    internal sealed class FoxBeadSummonScalingEffect : BaseEffect
    {
        private const float LukRatePerOwnSummon = 0.03f;
        private const int IntPerFieldSummon = 2;

        public FoxBeadSummonScalingEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.LUK
                ? 1f + LukRatePerOwnSummon * CountSummons(unit)
                : 1f;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.INT
                ? IntPerFieldSummon * CountFieldSummons(unit)
                : 0;

        private static int CountSummons(Unit owner)
        {
            if (owner?.ActiveSummons == null) return 0;

            int count = 0;
            foreach (Unit summon in owner.ActiveSummons)
            {
                if (summon != null && summon.isActive) count++;
            }
            return count;
        }

        /// <summary>
        /// 양 진영을 통틀어 살아 있는 소환수의 수. 소환수 자신은 아무것도 거느리지 않으므로
        /// 주인 쪽에서만 세면 목록에 소환수가 섞여 있어도 이중으로 세지 않는다.
        /// 세는 동안 스탯을 읽지 않아 스탯 계산과 서로를 부르지 않는다.
        /// </summary>
        private static int CountFieldSummons(Unit unit)
        {
            if (unit == null) return 0;

            int count = 0;
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(unit))
            {
                count += CountSummons(ally);
            }
            foreach (Unit enemy in Combat.CombatTargets.AliveEnemies(unit))
            {
                count += CountSummons(enemy);
            }
            return count;
        }
    }

    /// <summary>은총 — 자가치유의 금색 상위. 매 턴 CON×2를 회복한다.</summary>
    public sealed class GraceItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_grace";
        public GraceItemPassive(PassiveCodeContext context) : base(context, "은총")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            Caster?.RemoveStatusByKey("theseus_self_healing");
            AddPermanentStatus(NewItemStatusIds.Grace, new GraceRegenerationEffect(),
                "자신의 턴마다 CON×2만큼 회복합니다.");
        }
    }

    internal sealed class GraceRegenerationEffect : BaseEffect
    {
        public GraceRegenerationEffect() : base(0) { }
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            int healing = Mathf.Max(1, Target.GetBaseCon() * 2);
            Target.ModifyHp(Target.HpCurr + healing, Caster ?? Target);
        }
    }

    /// <summary>레미제라블 — 보유 소환수 하나마다 그 소환수들이 가하는 피해 +10%.</summary>
    public sealed class LesMiserablesItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_les_miserables";
        public LesMiserablesItemPassive(PassiveCodeContext context) : base(context, "레미제라블") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.LesMiserables, new LesMiserablesEffect(),
            "자신이 보유한 소환수 하나마다 자신의 소환수가 가하는 피해 +10%.");
    }

    internal sealed class LesMiserablesEffect : BaseEffect
    {
        public LesMiserablesEffect() : base(0) { }
        public override float SummonDamageMultiplierModifier(Unit unit)
        {
            if (unit == null || unit != Target) return 1f;
            int count = unit.ActiveSummons.Count(summon => summon != null && summon.isActive);
            return 1f + count * 0.10f;
        }
    }

    /// <summary>이름모를 용병의 방패 — 내구로 완전히 막은 타격마다 영속 내구 중첩 +1.</summary>
    public sealed class NamelessMercenaryShieldItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_nameless_mercenary_shield";
        public NamelessMercenaryShieldItemPassive(PassiveCodeContext context)
            : base(context, "이름모를 용병의 방패") { }

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.NamelessMercenaryShield,
            new NamelessMercenaryShieldEffect(NewItemIds.NamelessMercenaryShield),
            "내구로 타격을 완전히 상쇄할 때마다 영속 중첩 +1. 중첩마다 내구 +1. 장착 해제·소유자 변경 시 초기화됩니다.");
    }

    internal sealed class NamelessMercenaryShieldEffect : BaseEffect
    {
        private readonly int _itemId;
        private Action<EventContext> _handler;

        public NamelessMercenaryShieldEffect(int itemId) : base(0) => _itemId = itemId;

        public override void OnApply()
        {
            _handler = context =>
            {
                if (Target == null || context?.DmgCtx == null || context.DmgCtx.IsCancelled ||
                    !context.DmgCtx.DurabilityFullyAbsorbed) return;
                Target.AddPersistentEquipmentStack(_itemId, 1);
            };
            Target?.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override int DurabilityAdditiveModifier(Unit unit)
            => unit == Target ? unit.GetPersistentEquipmentStack(_itemId) : 0;
    }

    /// <summary>저주받은 왕관 — 치명타가 대상 내구 50%를 무시한다.</summary>
    public sealed class CursedCrownItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_cursed_crown";
        public CursedCrownItemPassive(PassiveCodeContext context) : base(context, "저주받은 왕관") { }

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.CursedCrown, new CursedCrownEffect(),
            "치명타는 대상 내구의 50%를 무시합니다. 이 장비를 착용한 채 전투에서 패배하면 LIFE 감소량이 2배가 됩니다.");
    }

    internal sealed class CursedCrownEffect : BaseEffect
    {
        public CursedCrownEffect() : base(0) { }

        public override int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && context?.IsCrit == true
                ? Mathf.CeilToInt(target.DurabilityCurr * 0.5f)
                : 0;
    }

    /// <summary>주문 복제 — 공격 코드의 순수 피해 부분만 20% 위력으로 즉시 반복한다.</summary>
    public sealed class SpellReplicationItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_spell_replication";

        public SpellReplicationItemPassive(PassiveCodeContext context) : base(context, "주문 복제")
            => SupersededByCodeId = 461;

        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.SpellReplication, new ReplicatedAttackEffect(0.20f),
            "공격 코드의 피해를 20% 위력으로 즉시 한 번 반복합니다. 부가 효과는 반복하지 않습니다.");
    }

    /// <summary>제국의 메아리 — 주문 복제의 강화 등급. 반복 위력이 40%다.</summary>
    public sealed class ImperialEchoItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_imperial_echo";

        public ImperialEchoItemPassive(PassiveCodeContext context) : base(context, "제국의 메아리")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            Caster?.RemoveStatusByKey("item_spell_replication");
            AddPermanentStatus(NewItemStatusIds.ImperialEcho, new ReplicatedAttackEffect(0.40f),
                "공격 코드의 피해를 40% 위력으로 즉시 한 번 반복합니다. 부가 효과는 반복하지 않습니다.");
        }
    }

    internal sealed class ReplicatedAttackEffect : BaseEffect
    {
        private readonly float _powerRatio;
        private Action<DamageResolvedContext> _damageHandler;

        public ReplicatedAttackEffect(float powerRatio) : base(0, powerRatio)
            => _powerRatio = Mathf.Clamp01(powerRatio);

        public override void OnApply()
        {
            _damageHandler = OnDamageDealt;
            Target?.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
        }

        private void OnDamageDealt(DamageResolvedContext resolved)
        {
            DamageContext source = resolved?.DamageContext;
            if (Target == null || resolved?.Attacker != Target || resolved.Target == null ||
                !resolved.Target.isActive || source == null || source.IsCancelled || source.Damage <= 0 ||
                source.DamageTags?.Contains(DamageTag.ReplicatedAttack) == true ||
                source.CodeType is not (BaseEnums.CodeType.Normal or BaseEnums.CodeType.Ultimate or BaseEnums.CodeType.Special))
                return;

            List<int> tags = source.DamageTags != null
                ? new List<int>(source.DamageTags)
                : new List<int>();
            tags.Add(DamageTag.ReplicatedAttack);
            int repeatedPower = Mathf.Max(1, Mathf.RoundToInt(source.Damage * _powerRatio));
            var repeated = new DamageContext(Target, repeatedPower, source.CodeType, tags,
                source.IsCrit, source.Penetration, source.DurabilityPenetration)
            {
                OutgoingDamageMultiplier = source.OutgoingDamageMultiplier,
            };
            resolved.Target.TakeDamage(repeated);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            _damageHandler = null;
        }
    }

    public sealed class GoldenArmorItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_golden_armor";
        public GoldenArmorItemPassive(PassiveCodeContext context) : base(context, "황금 갑주") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.GoldenArmor, new GoldScaledDamageEffect(),
            "현재 파티 골드 1당 가하는 피해가 0.1% 증가합니다.");
    }

    internal sealed class GoldScaledDamageEffect : BaseEffect
    {
        public GoldScaledDamageEffect() : base(0, 0.001f) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            int gold = Mathf.Max(0, global::Managers.GameManager.Instance?.inventoryManager?.Gold ?? 0);
            return 1f + gold * 0.001f;
        }
    }

    public sealed class ManekiNekoItemPassive : ItemStatusPassive
    {
        public const float GoldBonus = 0.20f;
        protected override string StatusKey => "item_maneki_neko";
        public ManekiNekoItemPassive(PassiveCodeContext context) : base(context, "마네키네코") { }
        public override void CastCode() => AddPermanentStatus(
            NewItemStatusIds.ManekiNeko, new MarkerBuffEffect(),
            "전투 후 획득하는 골드가 20% 증가합니다.");
    }
}
