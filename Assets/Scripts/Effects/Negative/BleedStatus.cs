using System;
using BaseClasses;
using Effects.Buffs;
using Entities;

namespace Effects.Negative
{
    /// <summary>
    /// 여러 계열이 돌려 쓰는 '출혈' 상태.
    ///
    /// 예전에는 아스완(5930, 최대 체력 1%)과 공허의 괴조(7893, 3%)가 <b>서로 다른 상태 ID</b>로
    /// 같은 이름을 쓰고 있었다. 그래서 "출혈을 부여하면" 같은 조건을 거는 코드가 어느 쪽을
    /// 봐야 하는지 알 수 없었다. 하나로 합치고 <b>턴당 비율만 부여자가 정한다.</b>
    ///
    /// 재부여는 지속시간만 연장하므로(<see cref="BaseEnums.StatusStackPolicy.ExtendDuration"/>)
    /// 먼저 걸린 쪽의 비율이 남는다. 출혈끼리 비율을 다투게 만들지 않기 위한 선택이다.
    /// </summary>
    public static class BleedStatus
    {
        /// <summary>아스완이 쓰던 번호를 그대로 승계한다. 5920~5922는 빙결·에어본·기절이 쓴다.</summary>
        public const int StatusId = 5930;
        public const string StatusKey = "bleed";

        /// <summary>
        /// 출혈이 실제로 걸린 순간을 알린다. 공허의 늑대 '피 냄새'처럼
        /// <b>부여 행위 자체</b>에 반응하는 코드가 구독한다. 구독자는 반드시 해제해야 한다.
        /// </summary>
        public static event Action<Unit, Unit, int> Applied;

        /// <summary>턴당 최대 체력 <paramref name="maxHpPercent"/>%를 깎는 출혈을 건다.</summary>
        public static void Apply(Unit target, Unit caster, int turns, float maxHpPercent, string sourceName)
        {
            if (target == null || !target.isActive || turns <= 0) return;

            target.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, "출혈",
                caster, target, new PercentDamageOverTimeEffect(0, maxHpPercent),
                duration: turns,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: $"{sourceName}: {turns}턴 동안 자기 턴 시작마다 "
                             + $"최대 체력의 {maxHpPercent:0.#}%에 해당하는 고정 지속피해를 받습니다."));

            Applied?.Invoke(caster, target, turns);
        }
    }
}
