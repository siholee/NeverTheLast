using StatusEffects.Base;
using Entities;

namespace StatusEffects.Passive
{
    /// <summary>
    /// 피그말리온의 패시브: 갈라테아
    /// 방어력 10% 증가
    /// </summary>
    public class GalateaPassive : StatusEffect
    {
        private const float DefenseBonus = 0.1f; // 10% 방어력 증가
        
        public GalateaPassive(Unit caster) : base(caster, "galatea_passive")
        {
            Stack = 1;
            Duration = 0f; // 영구 효과
        }
        
        public override float DefMultiplicativeModifier(Unit unit)
        {
            return DefenseBonus;
        }
    }
}
