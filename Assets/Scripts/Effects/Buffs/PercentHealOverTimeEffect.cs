using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Effects.Buffs
{
    /// <summary>
    /// 보유자의 턴마다 최대 체력의 일정 비율을 회복하는 재생. 지속피해의 회복판이다.
    ///
    /// 전투가 턴제이므로 초 단위 코루틴 대신 <see cref="BaseEffect.OnOwnerTurn"/>에 얹는다.
    /// 총량이 아니라 <b>턴당 비율</b>을 받는다 — 지속시간은 상태가 들고 있기 때문이다.
    /// </summary>
    public sealed class PercentHealOverTimeEffect : BaseEffect
    {
        private readonly float _percentPerTurn;

        public PercentHealOverTimeEffect(float percentPerTurn) : base(0, percentPerTurn)
        {
            _percentPerTurn = percentPerTurn;
            EffectName = "재생";
            EffectDescription = $"턴마다 최대 체력의 {percentPerTurn:0.#}% 회복";
            Category = BaseEnums.EffectCategory.Positive;
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive || Target.HpCurr <= 0) return;

            int heal = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _percentPerTurn * 0.01f));
            Target.ModifyHp(Target.HpCurr + heal, Caster ?? Target);
        }
    }
}
