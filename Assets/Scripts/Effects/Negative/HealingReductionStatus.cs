using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;

namespace Effects.Negative
{
    /// <summary>
    /// 상처 벌리기·고통스러운 상처·날카로운 가시가 공유하는 치유량 감소 상태.
    /// 동일한 50% 감소는 중첩하지 않고 기존 남은 시간과 새 지속시간 중 긴 쪽을 유지한다.
    /// </summary>
    public static class HealingReductionStatus
    {
        public const int StatusId = 5900;
        public const string StatusKey = "healing_reduction_50";

        public static void Apply(Unit target, Unit caster, float duration, string sourceName)
        {
            if (target == null || !target.isActive || duration <= 0f) return;

            UnitStatus existing = target.GetAllStatuses()
                .FirstOrDefault(status => status.Key == StatusKey);
            if (existing != null)
            {
                float remaining = existing.Duration <= 0f
                    ? float.PositiveInfinity
                    : existing.Duration - existing.ElapsedTime;
                if (remaining >= duration) return;
                target.RemoveStatusByKey(StatusKey);
            }

            target.AddStatus(Create(target, caster, duration, sourceName));
        }

        private static UnitStatus Create(Unit target, Unit caster, float duration, string sourceName)
        {
            var definition = new StatusDefinition
            {
                Id = StatusId,
                Key = StatusKey,
                Name = "치유량 감소",
                Description = $"{sourceName}: 받는 치유량이 50% 감소합니다.",
                Category = BaseEnums.StatusCategory.Negative,
                StackPolicy = BaseEnums.StatusStackPolicy.Replace,
                Duration = duration,
                IsBeneficial = false,
            };
            var status = new UnitStatus(definition, caster, target);
            status.AddEffect(new HealingReductionEffect(0.5f));
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

        public override float HealingReceivedMultiplierModifier(Unit unit)
            => unit == Target ? _multiplier : 1f;
    }
}
