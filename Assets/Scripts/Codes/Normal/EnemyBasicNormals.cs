using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 적 공용 일반행동 — 1000번대(공용 블록). 새 로스터가 들어오기 전까지 시험용 적이 쓴다.
    /// 둘 다 공격력을 계수로 쓰고 방어력으로 경감된다.
    /// </summary>
    public static class EnemyBasicNormalIds
    {
        public const int Charge = 1001;
        public const int Shot = 1002;
    }

    /// <summary>「돌격」 — 짧은 후퇴·부분 전진이 시전을 대신하고 대상 초상화에서 한 번 피격한다. 공격력의 100%.</summary>
    public sealed class EnemyCharge : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Strike;

        private const float AttackRatio = 1.0f;

        public EnemyCharge(NormalCodeContext context) : base(context)
        {
            CodeName = "돌격";
        }

        protected override bool IsContact => true;

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            if (EnemyNormalFx.UsesGroundRake(Caster))
            {
                DamageContext rakeContext = BuildContext(target);
                yield return EnemyNormalFx.GroundRake(Caster, target, () => Hit(target, rakeContext));
                yield break;
            }

            if (EnemyNormalFx.UsesHeavyStrike(Caster))
            {
                DamageContext heavyContext = BuildContext(target);
                yield return EnemyNormalFx.HeavyStrike(Caster, target, () => Hit(target, heavyContext));
                yield break;
            }

            DamageContext vanguardContext = BuildContext(target);
            yield return EnemyNormalFx.VanguardStrike(Caster, target,
                () => Hit(target, vanguardContext));
        }
    }

    /// <summary>「사격」 — 한 번 시전하고 대상 초상화의 단일 피격 정점에서 판정한다. 공격력의 90%.</summary>
    public sealed class EnemyShot : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Shot;

        private const float AttackRatio = 0.9f;

        public EnemyShot(NormalCodeContext context) : base(context)
        {
            CodeName = "사격";
        }

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return EnemyNormalFx.MirroredVolley(Caster, target, () => Hit(target, context));
        }

    }
}
