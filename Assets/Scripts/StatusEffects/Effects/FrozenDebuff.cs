using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    /// <summary>
    /// Frozen 반응 디버프: 대상을 1턴 행동불능으로 만드는 마커.
    /// 실제 행동불능 처리는 BattleManager에서 Unit.ControlStarts()로 처리.
    /// PersistsAcrossRounds = true.
    /// </summary>
    public class FrozenDebuff : StatusEffect, ITemporalEffect
    {
        private int _turnsRemaining;

        public FrozenDebuff(int turns = 1)
        {
            _turnsRemaining = turns;
            Duration = turns;
            Stack = 1;
        }

        public override bool PersistsAcrossRounds => true;

        public void UpdateDuration(float delta)
        {
            _turnsRemaining -= (int)delta;
        }

        public bool IsExpired => _turnsRemaining <= 0;
    }
}
