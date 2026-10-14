using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>소환 피해는 소환자 명의로 기록된다. 태그와 자기 소환수 판정을 함께 읽는다.</summary>
    public static class SummonSupportCombat
    {
        public static IEnumerable<Unit> Allies(Unit source, bool summons = false)
        {
            var grid = GridManager.Instance;
            if (source == null || grid == null) return Enumerable.Empty<Unit>();
            IEnumerable<Unit> pool = grid.heroList.Concat(grid.enemyList);
            if (summons) pool = pool.Concat(grid.summonList);
            return pool.Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == source.IsEnemy).Distinct();
        }

        public static bool IsSummonDamage(DamageContext context)
            => context != null && context.Attacker != null && context.CodeType != BaseEnums.CodeType.Effect &&
               !context.IsDamageOverTime && !context.HasTag(DamageTag.ElementalReaction) && !context.HasTag(DamageTag.BonusDamage) &&
               (context.HasTag(DamageTag.SummonAttack) || context.Attacker.CountsAsSummon);

        public static float PowerRatio(Unit source)
            => source == null ? 0 : source.StatValue(BaseEnums.StatType.SpecialAttack) / UnitStats.ArmorConstant(source.Level);
    }
}
