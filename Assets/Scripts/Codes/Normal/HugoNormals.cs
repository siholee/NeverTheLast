using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>위고 N — 아군 전체를 100 + CON×0.5 치유한다.</summary>
    public sealed class HugoPartyHeal : BaseNormalCode
    {
        public HugoPartyHeal(NormalCodeContext context) : base(context)
        {
            CodeName = "함께 걷는 길";
            Power = 0;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }

            int healing = Mathf.Max(1, 100 + Mathf.RoundToInt(Caster.GetBaseCon() * 0.5f));
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
                ally.ModifyHp(ally.HpCurr + healing, Caster);

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>팡세 N — 체력 비율이 가장 낮은 아군 하나를 500 + INT×1.2 치유한다.</summary>
    public sealed class PenseeHeal : BaseNormalCode
    {
        public PenseeHeal(NormalCodeContext context) : base(context)
        {
            CodeName = "사색의 손길";
            Power = 0;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }

            Unit patient = CombatTargets.AliveAlliesIncludingSelf(Caster)
                .OrderBy(unit => unit.HpMax <= 0 ? 1f : unit.HpCurr / (float)unit.HpMax)
                .ThenBy(unit => unit.HpCurr)
                .FirstOrDefault();
            if (patient != null)
            {
                int healing = Mathf.Max(1, 500 + Mathf.RoundToInt(Caster.GetBaseInt() * 1.2f));
                patient.ModifyHp(patient.HpCurr + healing, Caster.SummonOwner ?? Caster);
            }

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }
}
