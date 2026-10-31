using BaseClasses;
using Effects.Buffs;
using Entities;

namespace Codes.Ultimate
{
    public sealed class NicolThunderGrid : DotRosterAreaUltimate
    {
        protected override bool AppliesElement => true;
        public const float DefenseCut = .20f;
        public NicolThunderGrid(UltimateCodeContext context) : base(context, "뇌전 포위망", "nicol.ultimate", 1.20f) { }
        protected override void AfterHit(Unit target)
        {
            target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: Caster);
            if (!target.IsOnField) return;
            var status = BuffStatus.Create(71683, "nicol_defense_shred", "유도된 틈", Caster, target,
                new StatModifierEffect(BaseEnums.StatType.Defense, -DefenseCut, percent: true), duration: 2,
                category: BaseEnums.StatusCategory.Negative, description: "방어력 -20%, 자신의 일반행동2회 완료까지.");
            status.NormalActionDuration = true;
            target.TryAddStatus(status);
        }
    }
}
