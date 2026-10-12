using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>쿠베라 — 일반행동 「금화 투척」. 우선도 단일 적에게 공격력 90%, 공용 재보 +1(Detail_08 §11.2).</summary>
    public sealed class KuberaCoinToss : BaseNormalCode
    {
        private const float AttackRatio = 0.90f;

        public override int AttackForm => DamageTag.Shot;

        public KuberaCoinToss(NormalCodeContext context) : base(context) => CodeName = "금화 투척";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            yield return Effects.Vfx.LokapalaFx.Normal("kubera.coin_toss", Caster, target, () =>
            {
                if (!Combat.DirectAttack.CasterCanContinue(Caster, true) || target == null || !target.isActive) return;
                bool treasury = !IsCommanded && Combat.PartyTreasury.Charge(Caster, 1) > 0;
                bool crit = RollCritical(target, out float multiplier);
                if (treasury && crit) multiplier += Codes.Passive.KuberaPatronage.AdditionalCritDamage(Caster) + Codes.Passive.Reward.TreasuryRewardCode.CritDamageBonus(Caster);
                var context = new DamageContext(Caster, UnityEngine.Mathf.RoundToInt(BaseDamage(target) * multiplier),
                    BaseEnums.CodeType.Normal, GetDamageTags(), crit) { IsTreasuryAttack = treasury };
                StampCommand(context); Hit(target, context);
            });
        }


    }
}
