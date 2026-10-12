using System;
using Effects;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>체력을 자원으로 지불한다. 피해·보호막·경감·피격 마나·반격을 발생시키지 않는다.</summary>
    public static class HealthCost
    {
        public static event Action<Unit, int> AnyPaid;
        public static int Pay(Unit unit, int amount)
        {
            if (unit == null || !unit.isActive || amount <= 0) return 0;
            int paid = Mathf.Min(amount, Mathf.Max(0, unit.HpCurr - 1));
            if (paid == 0) return 0;
            unit.ModifyHp(unit.HpCurr - paid);
            DamagePopup.Show(unit, paid, false, new Color(1f,.52f,.48f), CombatNumberKind.HealthCost, .2f);
            AnyPaid?.Invoke(unit, paid);
            return paid;
        }
    }
}
