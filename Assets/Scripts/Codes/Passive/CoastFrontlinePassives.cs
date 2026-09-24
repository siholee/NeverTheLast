using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 일본 해안 전선 적 코드(1800~1852). 패시브·일반행동·궁극기가 같은 번호대를 슬롯별로 나눠 쓴다.
    /// 설계 원본은 <c>Theme_Japan_Coastal_Frontline.md</c>다.
    /// </summary>
    public static class CoastFrontlineCodeIds
    {
        public const int VoidArmor = 1800;

        public const int SharpPincer = 1811;
        public const int TidalPincerUp = 1812;
        public const int TwinPincer = 1813;

        public const int HeavyShell = 1821;
        public const int ReefSurge = 1822;
        public const int RecoilShell = 1823;

        public const int BloodScent = 1831;
        public const int DeepBite = 1832;
        public const int BloodTideUp = 1833;

        public const int ThickTentacle = 1841;
        public const int EyeOfStorm = 1842;

        public const int ArchonDominion = 1850;
        public const int HeavyTail = 1851;
        public const int GreatTide = 1852;
    }

    // ══════════════════════════════════════════════════════════════
    // 공용
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 공허의 갑주(1800). 입장 시 STR +40%와 최대 체력 기준 20% 보호막을 두르고,
    /// 실제 행동불능에 들어가는 순간 둘 다 벗는다. 일반·엘리트는 다시 두르지 않는다.
    ///
    /// 알파 개체처럼 최대 체력을 바꾸는 패시브가 같은 라운드 시작에 붙으므로 한 프레임 뒤에 잰다.
    /// </summary>
    public sealed class CoastVoidArmor : PassiveCode
    {
        public CoastVoidArmor(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "공허의 갑주";
            IgnoresActivationChance = true;
            IsUniquePassive = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Unit owner = Caster;
            SkyFrontline.RunNextFrame(owner, () =>
            {
                if (owner.isActive) CoastFrontline.EquipArmor(owner, owner.HpMax);
            });
        }
    }

    /// <summary>이름만 있는 해금 패시브. 효과는 해당 코드가 <see cref="Unit.HasLearnedPassiveCode"/>로 읽는다.</summary>
    public sealed class CoastMarkerPassive : PassiveCode
    {
        public CoastMarkerPassive(PassiveCodeContext context, string name) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() { }
    }

    // ══════════════════════════════════════════════════════════════
    // 심연의 사냥꾼
    // ══════════════════════════════════════════════════════════════

    /// <summary>피 냄새(1831). 현재 체력 50% 이하인 대상에게 주는 피해 +15%.</summary>
    public sealed class CoastBloodScent : PersistentStatusPassive
    {
        public CoastBloodScent(PassiveCodeContext context)
            : base(context, 9804, "coast_blood_scent", "피 냄새",
                "현재 체력 50% 이하인 대상에게 주는 피해가 15% 증가합니다.")
        {
            Transferable = false;
            // 끝없는 허기(1906)를 배우면 이쪽은 발동하지 않는다.
            SupersededByCodeId = 1906;
        }

        protected override BaseEffect CreateInitialEffect() => new BloodScentEffect();
    }

    internal sealed class BloodScentEffect : BaseEffect
    {
        public BloodScentEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.HpMax > 0 && target.HpCurr * 2 <= target.HpMax
                ? 1.15f
                : 1f;
    }

    // ══════════════════════════════════════════════════════════════
    // 심연의 집정관
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 심연의 집정권(1850). 체력이 여섯 구간으로 나뉘고, 한 번의 피해는 현재 구간의 끝까지만 체력을 깎는다.
    /// 경계에 닿을 때마다(브레이크) 공허의 갑주를 새 구간 체력 기준으로 다시 두른다. 마지막 구간의 끝은 실제 사망이다.
    /// </summary>
    public sealed class CoastArchonDominion : PersistentStatusPassive
    {
        public CoastArchonDominion(PassiveCodeContext context)
            : base(context, CoastFrontlineIds.ArchonStatus, "coast_archon_dominion", "심연의 집정권",
                "체력이 6구간으로 나뉩니다. 한 번의 피해는 현재 구간의 끝까지만 체력을 깎고, " +
                "구간이 부서질 때마다 공허의 갑주를 다시 두릅니다. 이미 행동불능이면 새 갑주는 곧바로 벗겨집니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new ArchonDominionEffect();
    }

    internal sealed class ArchonDominionEffect : BaseEffect
    {
        public const int Segments = 6;

        private Action<EventContext> _damageHandler;
        private Action<EventContext> _actionHandler;
        private int _phase;

        public ArchonDominionEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        /// <summary>입장 후 마친 일반행동 수. 대호흡은 2회를 넘긴 뒤부터 쓴다.</summary>
        public int NormalActions { get; private set; }

        public static ArchonDominionEffect Of(Unit unit)
            => unit?.GetStatus(CoastFrontlineIds.ArchonStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<ArchonDominionEffect>().FirstOrDefault();

        public override void OnApply()
        {
            if (Target == null) return;
            _damageHandler = OnAfterDamageTaken;
            _actionHandler = _ => NormalActions++;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            Target.SetCombatResourceMaximum(CoastFrontlineResources.Phase, Segments, resetCurrent: true);
            Target.AddCombatResource(CoastFrontlineResources.Phase, Segments);

            Unit owner = Target;
            SkyFrontline.RunNextFrame(owner, () =>
            {
                if (owner.isActive) CoastFrontline.EquipArmor(owner, SegmentHp(owner));
            });
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
        }

        private static int SegmentHp(Unit unit) => Mathf.Max(1, Mathf.RoundToInt(unit.HpMax / (float)Segments));

        /// <summary>
        /// 현재 구간의 하한. 최대 체력에서 매번 다시 재므로, 최대 체력이 바뀌어도 지금 구간 번호는 그대로다.
        /// 마지막 구간은 0 — 실제로 쓰러질 수 있다.
        /// </summary>
        private int Floor(Unit unit)
            => _phase >= Segments - 1 ? 0 : Mathf.RoundToInt(unit.HpMax * (Segments - 1 - _phase) / (float)Segments);

        public override int HpSegmentCount(Unit unit) => unit == Target ? Segments : 0;

        public override int HpLossFloor(Unit unit, DamageContext context)
            => unit == Target ? Floor(unit) : 0;

        /// <summary>
        /// 브레이크는 <b>한 번의 피해에 한 구간</b>만 진행한다. 체력 손실이 경계에서 잘리므로 보통은 정확히 닿는다.
        /// 피해 처리 안에서 곧바로 갑주를 다시 두르므로, 같은 공격의 뒤따르는 제어가 새 갑주를 벗길 수 있다.
        /// </summary>
        private void OnAfterDamageTaken(EventContext context)
        {
            Unit owner = Target;
            if (owner == null || !owner.isActive || owner.HpCurr <= 0) return;
            if (_phase >= Segments - 1 || owner.HpCurr > Floor(owner)) return;

            _phase++;
            owner.AddCombatResource(CoastFrontlineResources.Phase, -1);
            Debug.Log($"[심연의 집정권] {owner.UnitName} 구간 브레이크 — {_phase + 1}/{Segments}구간 진입");
            CoastFrontline.EquipArmor(owner, SegmentHp(owner));
        }
    }
}
