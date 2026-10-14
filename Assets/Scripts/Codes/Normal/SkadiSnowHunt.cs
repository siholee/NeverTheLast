using BaseClasses;
using Codes.Base;
using Entities;
namespace Codes.Normal
{
    public sealed class SkadiSnowHunt : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Shot;
        public SkadiSnowHunt(NormalCodeContext context) : base(context) => CodeName = "설원의 사냥꾼";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, 1.10f);
        protected override void OnAttackResolved(Unit target, DamageContext context)
        { if (target != null && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Cryo, source: Caster); }
        protected override Effects.Vfx.ProjectileStyle ProjectileFor(Unit target) => Effects.Vfx.OrionSkadiFx.Arrow(true);
    }
}
