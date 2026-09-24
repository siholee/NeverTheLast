using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허 계열 궁극기의 공용 뼈대. 부착 원소는 언제나 <b>시전자 자신의 원소</b>이고,
    /// 공허 속성이면 아무것도 붙이지 않는다 — 원소마다 ID를 따로 두지 않기 위한 약속이다.
    /// </summary>
    public abstract class VoidElementalUltimate : SimpleUltimate
    {
        protected VoidElementalUltimate(UltimateCodeContext context, string name, float delay)
            : base(context, name, delay) { }

        /// <summary>시전자 자신의 원소를 대상에게 부착한다. 공허 속성이면 아무 일도 없다.</summary>
        protected void AttachOwnElement(Unit target)
        {
            if (target == null || !target.isActive) return;
            if (!System.Enum.TryParse(Caster.Element, true, out BaseEnums.UnitElement own)) return;
            if (own is BaseEnums.UnitElement.None or BaseEnums.UnitElement.Void) return;

            target.GrantCombatElement(own, Unit.CommonElementAuraDuration, Caster);
        }

        /// <summary>이 유닛 기준 한 방의 피해를 굴린다.</summary>
        protected int RollDamage(int power, BaseEnums.PrimaryStat stat, out bool isCrit)
        {
            isCrit = Random.value <= Caster.CritChanceCurr;
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, stat) * (isCrit ? Caster.CritMultiplierCurr : 1f)));
        }
    }

    /// <summary>
    /// 공허의 학살자 U(1911) — 정밀 포격.
    /// 서로 다른 세 대상을 골라 각각 때리고 자신의 원소를 붙인다. 적이 모자라면 있는 만큼만 친다.
    /// </summary>
    public sealed class VoidSlaughtererPreciseBarrage : VoidElementalUltimate
    {
        private const int TargetCount = 3;
        private const int BasePower = 100;
        private const float IntCoefficient = 1.2f;

        public VoidSlaughtererPreciseBarrage(UltimateCodeContext context)
            : base(context, "정밀 포격", 0.5f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            // 서로 다른 대상이어야 한다. 우선도 순으로 최대 셋을 뽑는다.
            List<Unit> targets = CombatTargets.PickByPriority(Enemies(), TargetCount);
            if (targets == null || targets.Count == 0) return;

            int power = BasePower + Mathf.RoundToInt(Caster.GetBaseInt() * IntCoefficient);
            foreach (Unit target in targets)
            {
                if (target == null || !target.isActive || target.IsUntargetable) continue;

                int damage = RollDamage(power, BaseEnums.PrimaryStat.INT, out bool isCrit);
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
                AttachOwnElement(target);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 세이렌 U(1912) — 돌림노래.
    /// 적 전체를 치되 위력이 <b>자기 중첩</b>에 비례한다. 노래할 때마다 같은 진영 세이렌 모두의
    /// 중첩이 오르므로, 세이렌이 여럿이면 서로를 불려 준다. 행동 제어가 그 사슬을 끊는다.
    /// </summary>
    public sealed class SirenRoundelay : VoidElementalUltimate
    {
        private const int BasePower = 50;

        public SirenRoundelay(UltimateCodeContext context) : base(context, "돌림노래", 0.5f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            // 노래가 먼저다. 이번 시전분까지 포함해 중첩이 오른다.
            SirenChorus.NotifyChorus(Caster);

            int stacks = Mathf.Max(0, Caster.GetCombatResource(OdysseyCombat.ChorusResource));
            int power = BasePower + Mathf.RoundToInt(
                Caster.GetBaseInt() * OdysseyCombat.ChorusIntPerStack * stacks);

            foreach (Unit enemy in Enemies())
            {
                if (enemy == null || !enemy.isActive || enemy.IsUntargetable) continue;

                int damage = RollDamage(power, BaseEnums.PrimaryStat.INT, out bool isCrit);
                enemy.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 케토스 U(1914) — 산탄 포격.
    /// 한 대상을 크게 치고 자신의 원소를 붙인 뒤, 흩어진 파편이 나머지 적을 훑는다.
    /// </summary>
    public sealed class KetosScatterBarrage : VoidElementalUltimate
    {
        private const int MainPower = 100;
        private const float MainIntCoefficient = 1.2f;
        private const int SplashPower = 60;
        private const float SplashIntCoefficient = 0.9f;

        public KetosScatterBarrage(UltimateCodeContext context) : base(context, "산탄 포격", 0.5f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            Unit main = CombatTargets.PickByPriority(Enemies());
            if (main == null) return;

            int mainDamage = RollDamage(
                MainPower + Mathf.RoundToInt(Caster.GetBaseInt() * MainIntCoefficient),
                BaseEnums.PrimaryStat.INT, out bool mainCrit);
            main.TakeDamage(new DamageContext(
                Caster, mainDamage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.Special, DamageTag.NonContactAttack,
                },
                mainCrit));
            AttachOwnElement(main);

            int splashPower = SplashPower + Mathf.RoundToInt(Caster.GetBaseInt() * SplashIntCoefficient);
            foreach (Unit enemy in Enemies().Where(unit => unit != main).ToList())
            {
                if (enemy == null || !enemy.isActive || enemy.IsUntargetable) continue;

                int damage = RollDamage(splashPower, BaseEnums.PrimaryStat.INT, out bool isCrit);
                enemy.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }
}
