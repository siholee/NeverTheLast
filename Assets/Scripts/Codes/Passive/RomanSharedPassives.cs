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
    /// <summary>
    /// Lv.20 야습 — 전투 시작 시 <b>자신의</b> 행동 게이지를 50% 채워 둔다.
    ///
    /// 행동 서열은 DEX가 채우는 행동치로 정해지는데, 이 패시브는 그 절반을 미리 지운 채로
    /// 시작한다. 첫 행동이 두 배 빨리 오는 셈이라 **선공 확보용**이다. 이후 턴에는 영향이 없다.
    ///
    /// 조정 자체는 <see cref="Managers.ActionScheduler.BeginRound"/>가 행동치를 채운 뒤에 일어난다.
    /// 패시브가 걸리는 시점(<c>GridManager.OnRoundStart</c>)에 직접 만지면 초기화에 덮인다.
    /// </summary>
    public sealed class NightRaid : PassiveCode
    {
        public NightRaid(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "야습";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            RomanSharedIds.NightRaid, "roman_night_raid", CodeName,
            Caster, Caster, new NightRaidEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "전투 시작 시 자신의 행동 게이지를 50% 채워 첫 행동을 두 배 빨리 가져옵니다."));
    }

    /// <summary>
    /// Lv.12 카리스마 — 전투 종료 후 모든 아군의 획득 EXP +10%.
    /// 실제 배율은 <see cref="RewardModifiers.ExpMultiplier"/>가 필드에서 읽어 더한다.
    /// 선두주자·음유시인과 서로 더해진다.
    /// </summary>
    public sealed class Charisma : PassiveCode
    {
        public const float ExpBonus = 0.10f;

        public Charisma(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "카리스마";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>
    /// 직감 — DEX 집중 훈련 효율 +10%. <c>TrainingManager</c>가 읽는다.
    /// 사바흐의 '손재주'(95)와 같은 효과였으므로 이쪽으로 합쳤다.
    /// </summary>
    public sealed class Intuition : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public Intuition(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "직감";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>Lv.46 전술가 — 필드에 있는 동안 아군 전체가 적 방어력 10%를 무시한다.</summary>
    public sealed class Tactician : PassiveCode
    {
        public Tactician(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "전술가";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            foreach (Unit ally in global::Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit.isActive)
                         .Concat(new[] { Caster })
                         .Distinct())
            {
                ally.AddStatus(BuffStatus.Create(
                    RomanSharedIds.Tactician, $"roman_tactician_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new DefenseIgnoreEffect(0.9f),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace, isBeneficial: true,
                    description: "적 방어력의 10%를 무시합니다."));
            }
        }
    }

    /// <summary>
    /// 심안 — 원소가 부착된 적을 공격하면 대상의 내구를 무시하고, 피해를 준 뒤 부착 원소를 지운다.
    ///
    /// 내구 무시는 공격 코드가 <see cref="DamageTag.DurabilityPenetration"/>을 붙여 처리한다.
    /// 이 패시브는 그 뒤의 '원소 삭제'만 맡는다.
    /// </summary>
    public sealed class MindsEye : PassiveCode
    {
        public MindsEye(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "심안";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            RomanSharedIds.MindsEye, "roman_minds_eye", CodeName,
            Caster, Caster, new MindsEyeEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "원소가 붙은 적에게 내구를 무시하고 피해를 준 뒤 그 원소를 지웁니다."));
    }

    internal static class RomanSharedIds
    {
        public const int NightRaid = 6280;
        public const int Tactician = 6283;
        public const int MindsEye = 6284;
    }

    internal sealed class NightRaidEffect : BaseEffect
    {
        private const float Ratio = 0.5f;

        public NightRaidEffect() : base(0, Ratio) { }

        public override float RoundStartActionAdjustment(Unit unit)
            => unit != null && unit == Target ? Ratio : 0f;   // 양수 = 앞당긴다
    }

    internal sealed class MindsEyeEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;

        public MindsEyeEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.Target == null) return;
                if (!context.Target.HasAnyCombatElement) return;

                foreach (BaseEnums.UnitElement element in Enum.GetValues(typeof(BaseEnums.UnitElement))
                             .Cast<BaseEnums.UnitElement>()
                             .Where(element => element != BaseEnums.UnitElement.None)
                             .ToList())
                {
                    if (context.Target.HasAttachedElement(element))
                    {
                        context.Target.RemoveCombatElement(element);
                    }
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }
}
