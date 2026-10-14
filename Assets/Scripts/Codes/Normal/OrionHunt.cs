using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;
namespace Codes.Normal
{
    public sealed class OrionHunt : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Shot;
        public OrionHunt(NormalCodeContext context) : base(context) => CodeName = "거인의 사냥";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, 1f) + StatDamage(0, BaseEnums.StatType.MaxHp, .02f);
        protected override ProjectileStyle ProjectileFor(Unit target) => OrionSkadiFx.Arrow();
    }
}
