using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 광전사 N — 도끼질. 위력 <c>80 + STR×0.8</c>.
    ///
    /// <b>세 번째 일반행동마다</b> 한 방이 달라진다. 피해가 고정 피해로 바뀌어 방어력 감쇠를
    /// 건너뛰고, 최대 체력의 15%를 낼 수 있으면 태워 소모량에 비례한 추가 피해를 얹고,
    /// 대상에게 불을 부착한다. 볼바가 깔아 둔 얼음 위에 그 불이 얹히면 융해가 터진다.
    ///
    /// 세 번째 타격만 태우는 것은 광전사가 스스로 말라 죽지 않게 하기 위해서다.
    /// 매 타격마다 15%를 내면 볼바 없이는 다섯 번을 못 버틴다.
    /// </summary>
    public sealed class BerserkerAxe : BaseNormalCode
    {
        /// <summary>몇 번째 일반행동마다 고정 피해로 바뀌는가.</summary>
        private const int EmpowerInterval = 3;

        /// <summary>고정 피해 타격이 태우는 최대 체력 비율.</summary>
        private const float HpCostRatio = 0.15f;

        /// <summary>태운 체력 비율에 곱해 추가 피해로 바꾸는 계수.</summary>
        private const float SpentDamageCoefficient = 3f;

        private Action<EventContext> _resetHandler;
        private int _actionCount;
        private bool _empowered;
        private float _spentHpRatio;
        private bool _registered;

        public BerserkerAxe(NormalCodeContext context) : base(context)
        {
            CodeName = "도끼질";
            Power = 80;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash };
        }

        public override void CastCode()
        {
            RegisterReset();
            _actionCount++;
            _empowered = _actionCount % EmpowerInterval == 0;
            base.CastCode();
        }

        /// <summary>라운드가 끝나면 타격 수를 되돌린다. 다음 전투가 3타째부터 시작하면 안 된다.</summary>
        private void RegisterReset()
        {
            if (_registered || Caster == null) return;
            _resetHandler = _ => _actionCount = 0;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _resetHandler);
            _registered = true;
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            _spentHpRatio = 0f;
            if (_empowered && Caster.TryConsumeAttackHp(HpCostRatio, false, out float spent))
            {
                _spentHpRatio = spent;
            }

            float bonus = 1f + _spentHpRatio * SpentDamageCoefficient;
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier * bonus));
        }

        protected override DamageContext CreateDamageContext(int damage, List<int> damageTags, bool isCrit)
        {
            DamageContext context = base.CreateDamageContext(damage, damageTags, isCrit);
            context.SelfHpSpentRatio = _spentHpRatio;
            return context;
        }

        protected override List<int> GetDamageTags()
        {
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.NormalAttack,
                DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
            };
            if (_empowered) tags.Add(DamageTag.TrueDamage);
            return tags;
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            // 부착은 피해 뒤에 온다. 얼음이 이미 붙어 있으면 이 순간 융해가 터지고,
            // 되짚는 대상이 방금 들어간 고정 피해라 3타째가 그대로 증폭된다.
            if (!_empowered || target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
        }

        public override void StopCode()
        {
            base.StopCode();
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundStart, _resetHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _resetHandler);
            _registered = false;
        }
    }

    /// <summary>
    /// 볼바 N — 서리의 노래. 체력이 가장 낮은 아군 하나를 <c>CON×0.6</c>만큼 치유한다.
    ///
    /// 프레이아와 같이 <b>적을 때리지 않는 일반행동</b>이라 적 타겟팅 흐름을 쓸 수 없다.
    /// 시전 지연만 공유하고 해결부는 새로 짠다.
    /// </summary>
    public sealed class VolvaFrostSong : BaseNormalCode
    {
        private const int HealPower = 60;

        public VolvaFrostSong(NormalCodeContext context) : base(context)
        {
            CodeName = "서리의 노래";
            Power = HealPower;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Special };
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            Unit patient = Allies()
                .OrderBy(unit => unit.HpMax <= 0 ? 1f : (float)unit.HpCurr / unit.HpMax)
                .FirstOrDefault();
            if (patient == null)
            {
                StopCode();
                yield break;
            }

            int heal = Mathf.Max(1, Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON));
            patient.ModifyHp(patient.HpCurr + heal, Caster);
            Debug.Log($"[서리의 노래] {patient.UnitName}을(를) {heal}만큼 치유했습니다.");

            NotifyActionResolved();
            StopCode();
        }

        /// <summary>치유는 적이 없어도 성립한다. 살아 있는 아군이 하나라도 있으면 시전한다.</summary>
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Allies().Count > 0;

        private List<Unit> Allies() => Combat.CombatTargets.AliveAlliesIncludingSelf(Caster);
    }
}
