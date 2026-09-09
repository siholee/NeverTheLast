using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;

namespace Codes.Passive
{
    public sealed class HuntersVenomPassive : PassiveCode
    {
        public HuntersVenomPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사냥꾼의 독";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                5002, "hunters_venom_innate", CodeName,
                Caster, Caster, new DamageOverTimeApplicationEffect(1.2f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "부여하는 지속 피해가 20% 증가합니다."));
        }
    }

    public sealed class GalateaPassive : UniquePassiveCode
    {
        public GalateaPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "갈라테아";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.GrantUnitTag("Summon");
        }
    }

    public sealed class ArmorTrainingPassive : PassiveCode
    {
        public ArmorTrainingPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "갑옷 숙련";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                5010, "armor_training", CodeName,
                Caster, Caster, new ReceivingDamageMultiplierEffect(0.97f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "받는 피해가 3% 감소합니다."));
        }
    }
}
