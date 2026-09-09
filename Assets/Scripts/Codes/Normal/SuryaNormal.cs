using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 수리야 N — 라비 / N+ — 바스카르.
    ///
    /// 라비는 <b>중첩을 전부 태워</b> 무작위 단일 대상을 <c>중첩 ÷ 10</c>회 튕긴다.
    /// 태울 중첩이 10에 못 미쳐 한 번도 튕길 수 없으면 <b>대체행동 바스카르</b>로 바뀌어
    /// 오히려 중첩을 모은다 — 빈손으로 한 턴을 버리지 않게 하는 장치다.
    /// </summary>
    public sealed class SuryaRavi : BaseNormalCode
    {
        /// <summary>라비 한 발의 위력. CON 기반이다.</summary>
        private const int BouncePower = 80;

        /// <summary>한 발을 쏘는 데 필요한 중첩.</summary>
        public const int StacksPerBounce = 10;

        private bool _substitute;

        public SuryaRavi(NormalCodeContext context) : base(context)
        {
            CodeName = "라비";
            CastingDelay = 0.4f;
            Power = BouncePower;
            CodeTags = new List<int> { DamageTag.Special };
        }

        public override void CastCode()
        {
            _substitute = Bounces() <= 0;
            CodeName = _substitute ? "대체행동" : "라비";
            base.CastCode();
        }

        private int Bounces()
            => Caster == null ? 0 : Caster.GetCombatResource(SuryaSavitr.ResourceId) / StacksPerBounce;

        protected override IEnumerator SkillCoroutine()
        {
            if (!_substitute)
            {
                yield return base.SkillCoroutine();
                yield break;
            }

            // ── 대체행동 바스카르 ──
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            // 레벨 ÷ 10. 레벨이 낮아도 빈손으로 돌려보내지 않는다.
            int gain = Mathf.Max(1, Caster.Level / 10);
            Caster.AddCombatResource(SuryaSavitr.ResourceId, gain);
            Debug.Log($"[바스카르] {Caster.UnitName} 중첩 +{gain}");

            NotifyActionResolved();
            StopCode();
        }

        /// <summary>라비는 대상을 스스로 고르므로 여기서는 한 명만 잡아 둔다.</summary>
        protected override List<Unit> SelectTarget()
        {
            List<Unit> pool = CombatTargets.AliveEnemies(Caster);
            return pool.Count == 0 ? pool : new List<Unit> { pool[Random.Range(0, pool.Count)] };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        /// <summary>
        /// 첫 발이 적중한 뒤 남은 발을 이어 쏜다.
        /// 발마다 대상을 다시 뽑으므로 앞 발에 쓰러진 적에게 나머지가 낭비되지 않는다.
        /// </summary>
        protected override IEnumerator ApplyAdditionalEffects(Unit target, DamageContext context)
        {
            int bounces = Bounces();
            Caster.TryConsumeCombatResource(SuryaSavitr.ResourceId,
                Caster.GetCombatResource(SuryaSavitr.ResourceId));   // 중첩은 전부 태운다

            for (int i = 1; i < bounces; i++)
            {
                List<Unit> pool = CombatTargets.AliveEnemies(Caster);
                if (pool.Count == 0) yield break;

                Unit next = pool[Random.Range(0, pool.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));

                next.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Normal, GetDamageTags(), isCrit));
                yield return null;
            }
        }
    }
}
