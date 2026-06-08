using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    /// <summary>
    /// Superconduct 반응 디버프: 대상의 물리 방어력을 40% 감소 (DefMultiplicative -0.4).
    /// 2턴 지속. 라운드 종료 후에도 유지 (PersistsAcrossRounds = true).
    /// </summary>
    public class SuperconductDebuff : StatusEffect, ITemporalEffect
    {
        private int _turnsRemaining;

        public SuperconductDebuff(int turns = 2)
        {
            _turnsRemaining = turns;
            Duration = turns;
            Stack = 1;
        }

        public override bool PersistsAcrossRounds => true;

        /// <summary>물리 방어력 -40%</summary>
        public override float DefMultiplicativeModifier(Unit unit) => -0.4f;

        /// <summary>BattleManager TurnCleanup에서 매 턴 호출</summary>
        public void UpdateDuration(float delta)
        {
            _turnsRemaining -= (int)delta;
        }

        public bool IsExpired => _turnsRemaining <= 0;
    }
}
