using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>쿠베라 — 일반행동 「금화 투척」. 우선도 단일 적에게 공격력 90%, 보물 +1(Detail_08 §11.2).</summary>
    public sealed class KuberaCoinToss : BaseNormalCode
    {
        private const float AttackRatio = 0.90f;

        public override int AttackForm => DamageTag.Shot;

        public KuberaCoinToss(NormalCodeContext context) : base(context) => CodeName = "금화 투척";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Effects.Vfx.LokapalaFx.Normal("kubera.coin_toss", Caster, target,
                () => Hit(target, context));
        }

        // 소환사가 없어도 8행동이면 초월기가 돈다 — 혼자서는 느린 서브딜러다.
        protected override void OnAttackResolved(Unit target, DamageContext context) => Caster?.AddUltimateResource(1);
    }
}
