using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Ultimate
{
    /// <summary>마리 U — 아군 전체 피해 +20%, 활 사용자에게 추가 +20% (3턴).</summary>
    public sealed class MarieSongOfRevolution : SimpleUltimate
    {
        private const int StatusId = 6513;

        public MarieSongOfRevolution(UltimateCodeContext context)
            : base(context, "혁명의 노래", 0.35f) { }

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
                    description: ally.HasEquippedBow()
                        ? "가하는 피해 +40% (활 추가 보너스 포함, 3턴)."
                        : "가하는 피해 +20% (3턴)."));
            }
        }
    }

    internal sealed class MarieRevolutionEffect : BaseEffect
    {
        public MarieRevolutionEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? (attacker.HasEquippedBow() ? 1.4f : 1.2f) : 1f;
    }
}
