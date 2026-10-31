using System.Linq;
using Codes.Base;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>고유 행동과 활성 패시브만 조회한다. 보관 코드·남에게 받은 잔여 보호막은 능력이 아니다.</summary>
    public static class ShieldCapability
    {
        public static int StrongestBaseAmount(Unit holder)
            => holder == null ? 0 : holder.ActivePassiveCodesResolved().Cast<Code>()
                .Concat(new Code[] { holder.ActiveNormalCode, holder.ActiveUltimateCode, holder.ActiveSpecialCode })
                .Where(code => code != null).Select(code => code.BaseShieldAmount).DefaultIfEmpty(0).Max();

        public static void GrantParty(Unit source, int amount)
        {
            var grid = GridManager.Instance;
            if (source == null || !source.IsOnField || source.HpCurr <= 0 || grid == null || amount <= 0) return;
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Concat(grid.summonList).Distinct()
                         .Where(u => u != null && u.isActive && u.IsOnField && u.HpCurr > 0 && u.IsEnemy == source.IsEnemy))
                ally.AddShield(amount, source);
        }
    }
}
