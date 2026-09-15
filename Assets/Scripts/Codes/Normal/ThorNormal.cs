using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 토르 N / N+ — 망치질과 뇌전 도약.
    ///
    /// 일반행동을 두 번 하면 세 번째가 대체행동이 된다. 대체행동은 STR 위력 120짜리
    /// 바운스를 다섯 번 튕기며 맞은 적마다 번개를 부착하고, 고유 패시브에 성장을 알린다.
    ///
    /// 대체행동도 일반행동 한 번으로 세므로 <c>NotifyActionResolved</c>를 반드시 부른다.
    /// 박자를 세는 곳은 여기 하나뿐이다.
    /// </summary>
    public sealed class ThorNormalAttack : BaseNormalCode
    {
        /// <summary>몇 번째 일반행동이 대체행동으로 바뀌는가.</summary>
        private const int SubstituteInterval = 3;

        private const int StrikePower = 100;
        private const float StrikeStrCoefficient = 0.8f;
        private const int BouncePower = 120;
        private const int BounceCount = 5;

        private Action<EventContext> _resetHandler;
        private int _actionCount;
        private bool _substitute;
        private bool _registered;

        public ThorNormalAttack(NormalCodeContext context) : base(context)
        {
            CodeName = "망치질";
            Power = StrikePower;
            PowerStatCoefficient = StrikeStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.45f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        public override void CastCode()
        {
            RegisterReset();
            _actionCount++;
            _substitute = _actionCount % SubstituteInterval == 0;
            CodeName = _substitute ? "뇌전 도약" : "망치질";
            base.CastCode();
        }

        private void RegisterReset()
        {
            if (_registered || Caster == null) return;
            _resetHandler = _ => _actionCount = 0;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _resetHandler);
            _registered = true;
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (!_substitute)
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

            ResolveBounce();

            // 대체행동도 일반행동 한 번이다. 성장은 실제로 나갔을 때만 센다.
            ThorMjolnirRhythm.NotifySubstitute(Caster);
            NotifyActionResolved();
            StopCode();
        }

        /// <summary>
        /// 다섯 번 튕긴다. 매번 살아 있는 적 중에서 다시 고르므로 대상이 하나면 한 명을 다섯 번 친다.
        /// 튕길 때마다 번개를 부착해 얼음·물과 만나면 초전도·감전이 열린다.
        /// </summary>
        private void ResolveBounce()
        {
            for (int i = 0; i < BounceCount; i++)
            {
                List<Unit> enemies = Combat.CombatTargets.AliveEnemies(Caster);
                if (enemies.Count == 0) return;

                Unit target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
                bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(BouncePower, BaseEnums.PrimaryStat.STR) * critMultiplier));

                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.NormalAttack,
                    DamageTag.Special, DamageTag.NonContactAttack,
                }, isCrit));

                if (target.isActive)
                {
                    target.GrantCombatElement(
                        BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };

        public override void StopCode()
        {
            base.StopCode();
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _resetHandler);
            _registered = false;
        }
    }
}
