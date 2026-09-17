using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 해안 전선 일반행동의 공용 뼈대. 단일 공격은 기본 흐름을 따르고,
    /// 준비 공격이나 열 공격으로 바뀌는 회차만 <see cref="TrySubstitute"/>가 가로챈다.
    /// </summary>
    public abstract class CoastNormal : BaseNormalCode
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly bool _contact;
        private readonly bool _special;

        protected CoastNormal(NormalCodeContext context, string name, BaseEnums.PrimaryStat stat,
            bool contact, bool special) : base(context)
        {
            CodeName = name;
            _stat = stat;
            _contact = contact;
            _special = special;
            CastingDelay = 0.35f;
            CodeTags = new List<int>
            {
                special ? DamageTag.Special : DamageTag.Physical,
                contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
            };
        }

        /// <summary>이번 회차의 단일 공격 위력.</summary>
        protected abstract int SinglePower { get; }

        /// <summary>단일 공격 피해 배율. 해금 패시브의 '피해 +10%'가 쓴다.</summary>
        protected virtual float SingleMultiplier => 1f;

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(SinglePower, _stat) * SingleMultiplier * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            _special ? DamageTag.Special : DamageTag.Physical,
            _contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
        };

        /// <summary>
        /// 시전 직전에 이번 회차를 대체행동으로 바꿀지 정한다. 바꾼다면 시전 지연 뒤 부를 동작을 돌려준다.
        /// 대체행동도 일반행동 한 번이라 끝에 <c>NotifyActionResolved</c>를 낸다.
        /// </summary>
        protected virtual System.Action TrySubstitute() => null;

        protected override IEnumerator SkillCoroutine()
        {
            System.Action substitute = TrySubstitute();
            if (substitute == null)
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

            substitute();
            NotifyActionResolved();
            StopCode();
        }

        /// <summary>대상 목록 각각에 한 번씩. 치명타는 행동 단위로 한 번 굴린다.</summary>
        protected void StrikeAll(IEnumerable<Unit> targets, int flat, int power, BaseEnums.PrimaryStat stat,
            bool contact, bool special, float multiplier, BaseEnums.UnitElement element)
        {
            List<Unit> list = targets.Where(unit => unit != null && unit.isActive).ToList();
            if (list.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt((flat + Caster.SkillDamage(power, stat)) * multiplier * crit));
            int scope = list.Count == 1 ? DamageTag.SingleTarget : DamageTag.MultiTarget;

            foreach (Unit target in list)
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Normal,
                    new List<int>
                    {
                        scope, DamageTag.NormalAttack,
                        special ? DamageTag.Special : DamageTag.Physical,
                        contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
                    },
                    isCrit));
            }

            if (element == BaseEnums.UnitElement.None) return;
            foreach (Unit target in list.Where(unit => unit != null && unit.isActive))
            {
                target.GrantCombatElement(element, Unit.CommonElementAuraDuration, Caster);
            }
        }
    }

    /// <summary>공허의 돌격대장 N — 집게 압착. STR 위력 80, 날선 집게(1811) 피해 +10%.</summary>
    public sealed class CoastPincerCrush : CoastNormal
    {
        public CoastPincerCrush(NormalCodeContext context)
            : base(context, "집게 압착", BaseEnums.PrimaryStat.STR, contact: true, special: false) { }

        protected override int SinglePower => 80;
        protected override float SingleMultiplier
            => Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.SharpPincer) ? 1.1f : 1f;
    }

    /// <summary>
    /// 공허의 선봉대장 N — 등갑 밀치기. STR 위력 70(무거운 등갑 1821: 80).
    /// 되받는 등갑(1823)으로 암초 충돌 뒤 첫 일반행동은 위력 100이 된다.
    /// </summary>
    public sealed class CoastShellShove : CoastNormal
    {
        private bool _recoilThisAction;

        public CoastShellShove(NormalCodeContext context)
            : base(context, "등갑 밀치기", BaseEnums.PrimaryStat.STR, contact: true, special: false) { }

        protected override int SinglePower => _recoilThisAction ? 100
            : Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.HeavyShell) ? 80 : 70;

        protected override System.Action TrySubstitute()
        {
            // 강화는 위력만 바꾸므로 기본 흐름을 그대로 탄다. 여기서 표식만 소모한다.
            _recoilThisAction = Caster.HasStatus(CoastFrontlineIds.RecoilStatus);
            if (_recoilThisAction) Caster.RemoveStatusByKey("coast_recoil_shell");
            return null;
        }
    }

    /// <summary>
    /// 심연의 사냥꾼 N — 핏빛 추격. 상대 후열을 먼저 노리는 DEX 위력 70(깊은 물어뜯기 1832: 80), 접촉 물리.
    /// 후열이 비었으면 보통의 대상 선택으로 돌아간다.
    /// </summary>
    public sealed class CoastBloodChase : CoastNormal
    {
        public CoastBloodChase(NormalCodeContext context)
            : base(context, "핏빛 추격", BaseEnums.PrimaryStat.DEX, contact: true, special: false) { }

        protected override int SinglePower
            => Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.DeepBite) ? 80 : 70;

        protected override List<Unit> SelectTarget()
        {
            List<Unit> available = GetAvailableEnemies();
            List<Unit> rear = CoastFrontline.OpposingRearOrAll(Caster).Where(available.Contains).ToList();
            Unit pick = CombatTargets.PickByPriority(rear.Count > 0 ? rear : available);
            return pick != null ? new List<Unit> { pick } : new List<Unit>();
        }
    }

    /// <summary>
    /// 심연의 공포 N — 심연의 촉수. 서로 다른 둘에게 INT 위력 50(굵은 촉수 1841: 55), 특수·접촉.
    /// 촉수 폭풍을 준비했다면 대체행동으로 상대 전원에게 INT 위력 110(폭풍의 눈 1842: 120)과 바람 부착.
    /// </summary>
    public sealed class CoastAbyssTentacles : CoastNormal
    {
        public CoastAbyssTentacles(NormalCodeContext context)
            : base(context, "심연의 촉수", BaseEnums.PrimaryStat.INT, contact: true, special: true) { }

        protected override int SinglePower
            => Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.ThickTentacle) ? 55 : 50;

        protected override List<Unit> SelectTarget()
            => CombatTargets.PickByPriority(GetAvailableEnemies(), 2);

        protected override System.Action TrySubstitute()
        {
            if (!CoastFrontline.IsCharging(Caster)) return null;
            return () =>
            {
                if (!CoastFrontline.ConsumeCharge(Caster)) return;
                int power = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.EyeOfStorm) ? 120 : 110;
                StrikeAll(CombatTargets.AliveEnemies(Caster), 0, power, BaseEnums.PrimaryStat.INT,
                    contact: true, special: true, 1f, BaseEnums.UnitElement.Anemo);
                Debug.Log($"[촉수 폭풍] {Caster.UnitName}이(가) 상대 전원을 휩쓸었습니다.");
            };
        }
    }

    /// <summary>
    /// 심연의 집정관 N — 방파제 파쇄. 단일 STR 위력 100.
    /// 매 3번째는 상대 전열 전체 STR 위력 75(무거운 꼬리 1851: 85)의 꼬리 휩쓸기.
    /// 대호흡을 준비했다면 해일로 대체한다 — 상대 전원에게 고정 120 + STR 위력 125(큰 해일 1852: +10%).
    /// 해일은 원소를 붙이지 않는다. 물을 연달아 붙이면 정수(INT 버프)가 아군에게 가기 때문에, 대신 위력으로 누른다.
    /// 해일로 바뀐 회차도 순번 하나로 세지만 꼬리 휩쓸기를 겹쳐 쓰지는 않는다. 순번은 브레이크로 초기화하지 않는다.
    /// </summary>
    public sealed class CoastBreakwaterCrush : CoastNormal
    {
        private int _count;

        public CoastBreakwaterCrush(NormalCodeContext context)
            : base(context, "방파제 파쇄", BaseEnums.PrimaryStat.STR, contact: true, special: false) { }

        protected override int SinglePower => 100;

        protected override System.Action TrySubstitute()
        {
            _count++;
            if (CoastFrontline.IsCharging(Caster))
            {
                return () =>
                {
                    if (!CoastFrontline.ConsumeCharge(Caster)) return;
                    float multiplier = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.GreatTide) ? 1.1f : 1f;
                    StrikeAll(CombatTargets.AliveEnemies(Caster), 120, 125, BaseEnums.PrimaryStat.STR,
                        contact: false, special: true, multiplier, BaseEnums.UnitElement.None);
                    Debug.Log($"[해일] {Caster.UnitName}이(가) 해일을 일으켰습니다.");
                };
            }

            if (_count % 3 != 0) return null;
            return () =>
            {
                int power = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.HeavyTail) ? 85 : 75;
                StrikeAll(CoastFrontline.OpposingFrontOrAll(Caster), 0, power, BaseEnums.PrimaryStat.STR,
                    contact: true, special: false, 1f, BaseEnums.UnitElement.None);
                Debug.Log($"[꼬리 휩쓸기] {Caster.UnitName}이(가) 전열을 휩쓸었습니다.");
            };
        }
    }
}
