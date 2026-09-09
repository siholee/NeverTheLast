using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Neutral;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 수르트 N / N+ — 도발 여부에 따라 단일 공격 또는 방어막·도발 행동으로 바뀐다.
    /// 도발은 스카디와 같은 방식으로 <b>자신에게</b> 걸어 우선도를 올린다.
    /// </summary>
    public sealed class SurtrScorchingSlash : BaseNormalCode
    {
        private const int NormalPower = 60;
        private const int TauntDurationTurns = 3;
        private const float ShieldFlat = 80f;
        private const float ShieldStrCoefficient = 1.5f;

        private bool _empowered;

        public SurtrScorchingSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "일반행동";
            CastingDelay = 0.4f;
            MaxStage = 1;
            Power = NormalPower;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.Slash };
        }

        public override void CastCode()
        {
            _empowered = !Taunt.Has(Caster);
            CodeName = _empowered ? "강화 일반행동" : "일반행동";
            base.CastCode();
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (!_empowered)
            {
                yield return base.SkillCoroutine();
                yield break;
            }

            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            int shield = Mathf.Max(1, Mathf.RoundToInt(
                ShieldFlat + Caster.GetBaseStr() * ShieldStrCoefficient));
            Caster.AddShield(shield, Caster);
            Taunt.Apply(Caster, Caster, TauntDurationTurns);

            NotifyActionResolved();
            StopCode();
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.Physical,
            DamageTag.ContactAttack,
            DamageTag.Slash,
        };

        // 도발 중이 아니면 적이 없어도 방어막·도발 행동은 성립한다.
        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && (!Taunt.Has(Caster) || base.HasValidTarget());
    }
}
