using BaseClasses;
using Entities;
using StatusEffects.Base;

namespace StatusEffects.Effects
{
    /// <summary>
    /// DoT 반응 지속 피해 효과 (Burning = Pyro, Electrocharged = Electro).
    /// 매 턴 Attacker.AtkCurr * DamageMultiplier 만큼 피해.
    /// PersistsAcrossRounds = true.
    /// BattleManager.TurnCleanup에서 Tick()을 호출해 DoT 적용.
    /// </summary>
    public class BurningDoT : StatusEffect, ITemporalEffect
    {
        public Unit Source { get; }
        public float DamageMultiplier { get; }
        /// <summary>DoT 피해 원소 타입 (Burning = Pyro, Electrocharged = Electro)</summary>
        public BaseEnums.ElementType Element { get; }

        private int _turnsRemaining;

        public BurningDoT(Unit source, int turns = 2, float damageMultiplier = 0.25f,
                          BaseEnums.ElementType element = BaseEnums.ElementType.Pyro)
        {
            Source = source;
            DamageMultiplier = damageMultiplier;
            Element = element;
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

        /// <summary>DoT 피해 계산 (매 턴 BattleManager에서 호출)</summary>
        public int CalculateTick()
        {
            if (Source == null || !Source.isActive) return 0;
            return (int)(Source.AtkCurr * DamageMultiplier);
        }
    }
}
