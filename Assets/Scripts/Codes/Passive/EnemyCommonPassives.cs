using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    internal static class EnemyCommonCodeNames
    {
        public static string Passive(int codeId)
        {
            return codeId switch
            {
                101 => "북방의 기세",
                102 => "혹한의 강인함",
                103 => "약탈자의 숙련",
                104 => "노르드의 노련함",
                105 => "태양의 전사",
                106 => "신전의 가호",
                107 => "흑요석 숙련",
                108 => "의식의 박자",
                109 => "검투사의 기백",
                110 => "관중의 함성",
                111 => "투기장 무기술",
                112 => "백전노장",
                _ => "적 전투 훈련",
            };
        }
    }

    /// <summary>
    /// 같은 테마의 적들이 공유하는 단순 성장 패시브.
    /// 조건부 기믹 없이 피해량, 생존력, 코드 가속만 보정한다.
    /// </summary>
    public sealed class EnemyCommonPassive : PassiveCode
    {
        private readonly int _codeId;

        public EnemyCommonPassive(PassiveCodeContext context, int codeId) : base(context)
        {
            _codeId = codeId;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = EnemyCommonCodeNames.Passive(codeId);
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            int growthStep = (_codeId - 101) % 4;
            BaseEffect effect = growthStep switch
            {
                0 => new EnemyCommonOutgoingDamageEffect(1.05f),
                1 => new EnemyCommonReceivingDamageEffect(0.95f),
                2 => new EnemyCommonOutgoingDamageEffect(1.10f),
                3 => new EnemyCommonCodeAccelerationEffect(0.10f),
                _ => null,
            };

            if (effect == null) return;

            Caster.AddStatus(BuffStatus.Create(
                6100 + _codeId,
                $"enemy_common_passive_{_codeId}",
                CodeName,
                Caster,
                Caster,
                effect,
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true));
        }
    }

    internal sealed class EnemyCommonOutgoingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;

        public EnemyCommonOutgoingDamageEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _multiplier : 1f;
    }

    internal sealed class EnemyCommonReceivingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;

        public EnemyCommonReceivingDamageEffect(float multiplier) : base(0, multiplier)
        {
            _multiplier = multiplier;
        }

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target ? _multiplier : 1f;
    }

    internal sealed class EnemyCommonCodeAccelerationEffect : BaseEffect
    {
        private readonly float _amount;

        public EnemyCommonCodeAccelerationEffect(float amount) : base(0, amount)
        {
            _amount = amount;
        }

        public override float CodeAccelerationAdditiveModifier(Unit unit)
            => unit == Target ? _amount : 0f;
    }
}
