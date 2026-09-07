using System.Linq;
using BaseClasses;
using Effects.Buffs;
using Entities;

namespace Effects.Neutral
{
    /// <summary>
    /// 도발 공용 규칙.
    ///
    /// 도발은 수르트·스카디처럼 여러 코드가 돌려 쓰는 상태다. 코드마다 상태 ID와 키를
    /// 따로 잡으면 "지금 도발 중인가"를 물을 때마다 판정이 갈라지므로,
    /// <b>ID·키·판정을 한 곳에 모아 둔다.</b>
    /// </summary>
    public static class Taunt
    {
        /// <summary>5930은 아스완의 출혈이 쓰고 있어 5940으로 띄웠다.</summary>
        public const int StatusId = 5940;
        public const string StatusKey = "taunt";

        /// <summary>도발 중인가. 부여자가 누구든 도발 효과 하나라도 붙어 있으면 참이다.</summary>
        public static bool Has(Unit unit)
            => unit?.ActiveStatuses?.Any(status =>
                status.Effects.Any(effect => effect.EffectObject is TauntEffect)) == true;

        /// <summary>대상을 도발 상태로 만든다. 우선도가 <paramref name="priorityBonus"/>만큼 오른다.</summary>
        public static void Apply(Unit target, Unit caster, int durationTurns, int priorityBonus = 1)
        {
            if (target == null || durationTurns <= 0) return;

            target.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, "도발", caster, target,
                new TauntEffect(0, priorityBonus),
                duration: durationTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Neutral,
                isBeneficial: true,
                description: $"{durationTurns}턴 동안 도발 상태가 되어 우선도가 {priorityBonus} 증가합니다."));
        }
    }
}
