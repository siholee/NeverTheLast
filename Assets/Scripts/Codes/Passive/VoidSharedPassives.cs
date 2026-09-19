using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>공허 계열 여럿이 나눠 쓰는 코드. 한 병종 전용은 각자의 파일에 둔다.</summary>
    public static class VoidSharedCodeIds
    {
        public const int AlphaSpecimen = 1541;
        public const int Storm = 1542;
        public const int UltimateBarrier = 1543;
        public const int VanguardAura = 1544;
        public const int Intimidation = 1545;
        public const int Perfection = 1546;
    }

    public static class VoidSharedStatusIds
    {
        public const int AlphaSpecimen = 7920;
        public const int Storm = 7921;
        public const int UltimateBarrier = 7922;
        public const int VanguardAura = 7923;
        public const int Intimidation = 7924;
        public const int Perfection = 7925;
    }

    /// <summary>
    /// 알파 개체(1541)와 그 금색 상위 코드 완전함(1546).
    ///
    /// CON이 주는 최대 체력을 배로 불리고 <b>한 방에 깎이는 양</b>에 상한을 건다.
    /// 체력 바도 같은 수만큼 칸으로 끊어 그려 남은 단계가 눈에 보이게 한다 —
    /// 페이즈가 있는 거대 개체의 문법이다. 상한이 없으면 광역 폭딜 한 번에
    /// 배로 불린 체력이 통째로 사라져 <b>최대 체력만 키운 것과 다르지 않다.</b>
    /// </summary>
    public sealed class VoidAlphaSpecimen : PersistentStatusPassive
    {
        private readonly int _segments;

        public VoidAlphaSpecimen(PassiveCodeContext context, int statusId, string statusKey,
            string name, int segments, int supersededByCodeId, BaseEnums.CodeGrade grade)
            : base(context, statusId, statusKey, name,
                $"CON이 제공하는 최대 체력이 {segments}배가 되고, 한 번에 최대 체력의 1/{segments}을 넘는 피해를 받지 않습니다.")
        {
            _segments = Mathf.Max(2, segments);
            Transferable = false;
            Grade = grade;
            SupersededByCodeId = supersededByCodeId;
        }

        protected override BaseEffect CreateInitialEffect() => new HpSegmentEffect(_segments);

        public override float PreviewMaxHpMultiplier => _segments;
    }

    internal sealed class HpSegmentEffect : BaseEffect
    {
        private readonly int _segments;
        public HpSegmentEffect(int segments) : base(0, segments) => _segments = segments;

        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? _segments : 1f;
        public override int HpSegmentCount(Unit unit) => unit == Target ? _segments : 0;
        public override float IncomingDamageCapRatio(Unit unit, DamageContext context)
            => unit == Target ? 1f / _segments : 0f;
    }

    /// <summary>궁극기 사용에 반응하는 코드의 뼈대. 발동 시점에 붙는다.</summary>
    public abstract class UltimateReactionPassive : PersistentStatusPassive
    {
        private Action<EventContext> _ultimateHandler;

        protected UltimateReactionPassive(PassiveCodeContext context, int statusId, string statusKey,
            string name, string description) : base(context, statusId, statusKey, name, description)
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _ultimateHandler = _ =>
            {
                if (Caster != null && Registered && Caster.isActive) OnUltimateUsed();
            };
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            _ultimateHandler = null;
        }

        protected abstract void OnUltimateUsed();
    }

    /// <summary>폭풍(1542) — 궁극기를 쓰면 모든 적에게 CON 대결로 기절을 시도한다.</summary>
    public sealed class VoidStorm : UltimateReactionPassive
    {
        public VoidStorm(PassiveCodeContext context)
            : base(context, VoidSharedStatusIds.Storm, "void_storm", "폭풍",
                "궁극기를 사용하면 모든 적에게 CON 대결 확률로 기절을 겁니다.") { }

        protected override void OnUltimateUsed()
        {
            foreach (Unit target in CombatTargets.AliveEnemies(Caster))
            {
                ControlStatuses.ApplyStun(target, Caster);
            }
        }
    }

    /// <summary>궁극의 보호막(1543) — 궁극기를 쓰면 자신에게 CON 비례 보호막.</summary>
    public sealed class VoidUltimateBarrier : UltimateReactionPassive
    {
        private const float ShieldConCoefficient = 1.2f;

        public VoidUltimateBarrier(PassiveCodeContext context)
            : base(context, VoidSharedStatusIds.UltimateBarrier, "void_ultimate_barrier", "궁극의 보호막",
                $"궁극기를 사용하면 자신에게 CON×{ShieldConCoefficient} 보호막을 부여합니다.") { }

        protected override void OnUltimateUsed()
            => Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShieldConCoefficient)), Caster);
    }

    /// <summary>
    /// 공허의 선봉장(1544) — 일반행동을 마칠 때마다 필드의 공허 괴수 전원이 올스탯 +3%.
    /// 하위 코드가 없는 <b>금색 단독</b>이다.
    /// </summary>
    public sealed class VoidVanguardAura : PersistentStatusPassive
    {
        private const float PerStack = 0.03f;

        private Action<EventContext> _actionHandler;
        private int _stacks;

        public VoidVanguardAura(PassiveCodeContext context)
            : base(context, VoidSharedStatusIds.VanguardAura, "void_vanguard_aura", "공허의 선봉장",
                $"일반행동을 마칠 때마다 필드의 공허 괴수 아군 전체가 올스탯 +{PerStack * 100f:F0}%. 중첩됩니다.")
        {
            Transferable = false;
            Grade = BaseEnums.CodeGrade.Enhanced;
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
            float bonus = 1f + PerStack * _stacks;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit.HasUnitTag("VoidMonster")))
            {
                ally.AddStatus(BuffStatus.Create(
                    StatusId, StatusKey, CodeName, Caster, ally,
                    new PrimaryStatMultiplierEffect(bonus),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"올스탯 +{(bonus - 1f) * 100f:F0}% ({_stacks}중첩)"));
            }
        }
    }

    /// <summary>
    /// 위압감(1545) — 필드에 있는 동안 모든 적의 INT 마나 회복 기여분이 20% 줄어든다.
    /// 자기 턴마다 다시 훑어 전투 도중 들어온 적에게도 걸린다.
    /// </summary>
    public sealed class VoidIntimidation : PersistentStatusPassive
    {
        private const float Multiplier = 0.8f;

        private Action<EventContext> _turnHandler;

        public VoidIntimidation(PassiveCodeContext context)
            : base(context, VoidSharedStatusIds.Intimidation, "void_intimidation", "위압감",
                "필드에 있는 동안 모든 적의 INT 마나 회복 효율이 20% 감소합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _turnHandler = _ => ApplyToEnemies();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            ApplyToEnemies();
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            _turnHandler = null;
            foreach (Unit target in CombatTargets.AliveEnemies(Caster))
            {
                target.RemoveStatusByKey(StatusKey);
            }
        }

        private void ApplyToEnemies()
        {
            if (Caster == null || !Registered || !Caster.isActive) return;

            foreach (Unit target in CombatTargets.AliveEnemies(Caster))
            {
                target.AddStatus(BuffStatus.Create(
                    StatusId, StatusKey, CodeName, Caster, target,
                    new ManaEfficiencyEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    isBeneficial: false,
                    description: Description));
            }
        }
    }
}
