using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Ultimate
{
    /// <summary>마리 U — 아군 전체에게 3턴간 일반공격 피해 +20%, DEX +10.</summary>
    public sealed class MarieSongOfRevolution : SimpleUltimate
    {
        private const int StatusId = 6513;

        public MarieSongOfRevolution(UltimateCodeContext context)
            : base(context, "혁명의 노래", 4f, 0.35f) { }

        protected override void Resolve()
        {
            foreach (Unit ally in Passive.MarieCriticalCommand.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    StatusId, $"marie_song_of_revolution_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new MarieRevolutionEffect(),
                    duration: 3,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "일반공격 피해 +20%, DEX +10 (3턴)."));
            }
        }
    }

    internal sealed class MarieRevolutionEffect : BaseEffect
    {
        public MarieRevolutionEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? 10 : 0;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.NormalAttack) == true
                ? 1.2f
                : 1f;
    }
}
