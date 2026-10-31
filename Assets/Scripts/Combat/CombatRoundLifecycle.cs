using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;

namespace Combat
{
    /// <summary>게임 화면과 독립 실행 호스트가 공유하는 개전·종료 수명. UI와 저장은 호출자가 맡는다.</summary>
    public static class CombatRoundLifecycle
    {
        public static void Begin(IReadOnlyList<Unit> heroes, IReadOnlyList<Unit> enemies)
        {
            foreach (Unit unit in heroes.Concat(enemies).Where(unit => unit != null && unit.isActive && !unit.IsBench))
                unit.PrepareRoundResources();
            foreach (Unit hero in heroes)
                if (hero != null && hero.isActive)
                    hero.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(hero));
            foreach (Unit enemy in enemies.ToList()) BeginEnemy(enemy);
        }

        public static void BeginEnemy(Unit enemy)
        {
            if (enemy == null || !enemy.IsOnField || !enemy.IsEnemy) return;
            enemy.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(enemy));
            EnemyCombatBudget.Apply(enemy);
        }

        public static void End(IEnumerable<Unit> heroes, IEnumerable<Unit> enemies)
        {
            Battlefield.Clear();
            Summons.ClearAll();
            foreach (Unit unit in heroes.Concat(enemies).Where(unit => unit != null).ToList())
                unit.Invoke(BaseEnums.UnitEventType.OnRoundEnd, new EventContext(unit));
            PartyTreasury.EndBattle();
        }
    }
}
