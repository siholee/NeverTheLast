using System;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;

namespace Effects.Buffs
{
    /// <summary>
    /// 자주 쓰는 버프 상태 빌더. 상태 하나 + 효과 하나 조합을 만든다.
    /// </summary>
    public static class BuffStatus
    {
        /// <summary>
        /// 단일 효과 버프 상태 생성.
        /// <paramref name="duration"/>은 <b>턴</b> 수이며 0 이하면 무한(라운드 종료까지)이다.
        /// </summary>
        public static UnitStatus Create(
            int id, string key, string name, Unit caster, Unit owner, BaseEffect effect,
            int duration = -1,
            BaseEnums.StatusStackPolicy stackPolicy = BaseEnums.StatusStackPolicy.Replace,
            BaseEnums.StatusCategory category = BaseEnums.StatusCategory.Positive,
            bool isBeneficial = false,
            string description = "")
        {
            var status = new UnitStatus(new StatusDefinition
            {
                Id = id,
                Key = key,
                Name = name,
                Description = description,
                Category = category,
                StackPolicy = stackPolicy,
                Duration = duration,
                IsBeneficial = isBeneficial,
            }, caster, owner);
            if (effect != null) status.AddEffect(effect);
            return status;
        }
    }

    /// <summary>수치 효과 없이 상태를 화면에 표시하기 위한 표식용 효과.</summary>
    public class MarkerBuffEffect : BaseEffect
    {
        public MarkerBuffEffect() : base(0) { }
    }

    /// <summary>
    /// 표식이면서, <b>상태가 사라지는 순간</b> 콜백을 부른다.
    /// 코드가 단 이벤트 훅의 수명을 상태의 턴 수명에 묶으면 만료 · 교체 · 라운드 종료가 한 경로로 정리된다.
    /// </summary>
    public sealed class LifetimeMarkerEffect : BaseEffect
    {
        private Action _onRemove;
        public LifetimeMarkerEffect(Action onRemove) : base(0) { _onRemove = onRemove; }

        public override void OnRemove()
        {
            Action callback = _onRemove;
            _onRemove = null;
            callback?.Invoke();
        }
    }
}
