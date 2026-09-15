using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>
    /// 상처 벌리기·고통스러운 상처·날카로운 가시·발드르의 살해자·천둥의 신이 공유하는 치유량 감소 상태.
    ///
    /// <b>중첩되지 않는다.</b> 키가 하나뿐이라 새로 걸면 기존 것과 견주어
    /// <b>더 강한 감소</b>가 이기고, 세기가 같으면 <b>남은 턴이 긴 쪽</b>이 남는다.
    /// 감소폭이 코드마다 달라 세기를 먼저 보는 것이 옳다 — 25%짜리가 50%짜리를 덮어쓰면
    /// 약한 코드를 뒤에 던지는 것이 상대에게 이득이 되어 버린다.
    /// </summary>
    public static class HealingReductionStatus
    {
        public const int StatusId = 5900;
        public const string StatusKey = "healing_reduction";

        /// <summary>감소폭을 적지 않은 옛 호출부가 쓰던 기본값.</summary>
        public const float DefaultReduction = 0.5f;

        /// <summary>
        /// <paramref name="turns"/>는 턴 수, <paramref name="reduction"/>은 깎는 비율(0.25 = 25% 감소)이다.
        /// </summary>
        public static void Apply(
            Unit target, Unit caster, int turns, string sourceName, float reduction = DefaultReduction)
        {
            if (target == null || !target.isActive || turns <= 0) return;

            float strength = Mathf.Clamp01(reduction);
            if (strength <= 0f) return;

            UnitStatus existing = target.GetAllStatuses()
                .FirstOrDefault(status => status.Key == StatusKey);
            if (existing != null)
            {
                float existingStrength = ExistingStrength(existing);
                if (existingStrength > strength) return;
                if (Mathf.Approximately(existingStrength, strength) && existing.RemainingTurns >= turns) return;
                target.RemoveStatusByKey(StatusKey);
            }

            target.AddStatus(Create(target, caster, turns, sourceName, strength));
        }

        private static float ExistingStrength(UnitStatus status)
        {
            foreach (var effect in status.Effects)
            {
                if (effect.EffectObject is HealingReductionEffect healing) return healing.Reduction;
            }
            return 0f;
        }

        private static UnitStatus Create(
            Unit target, Unit caster, int turns, string sourceName, float reduction)
        {
            var definition = new StatusDefinition
            {
                Id = StatusId,
                Key = StatusKey,
                Name = "치유량 감소",
                Description = $"{sourceName}: 받는 치유량이 {reduction * 100f:0.#}% 감소합니다.",
                Category = BaseEnums.StatusCategory.Negative,
                StackPolicy = BaseEnums.StatusStackPolicy.Replace,
                Duration = turns,
                IsBeneficial = false,
            };
            var status = new UnitStatus(definition, caster, target);
            status.AddEffect(new HealingReductionEffect(1f - reduction));
            return status;
        }
    }

    internal sealed class HealingReductionEffect : BaseEffect
    {
        private readonly float _multiplier;

        public HealingReductionEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
            Category = BaseEnums.EffectCategory.Negative;
        }

        /// <summary>깎는 비율. 세기를 견주는 쪽이 읽는다.</summary>
        public float Reduction => 1f - _multiplier;

        public override float HealingReceivedMultiplierModifier(Unit unit)
        {
            if (unit != Target) return 1f;

            // 저항이 있으면 깎는 몫 자체를 줄인다. 배율을 거꾸로 올리면
            // 감소가 걸려 있지 않을 때도 치유가 늘어난다.
            float effective = Reduction * (1f - unit.HealingReductionResistance);
            return 1f - effective;
        }
    }
}
