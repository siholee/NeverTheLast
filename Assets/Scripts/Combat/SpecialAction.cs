using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 로카팔라의 특수행동을 한 묶음으로 예약한다. 같은 묶음의 임시 효과는 마지막 SP가 끝날 때 걷는다.
    /// </summary>
    public static class SpecialAction
    {
        public const string LokapalaTag = "Lokapala";

        private static readonly HashSet<Unit> Pending = new();
        private static readonly List<Action> BatchCleanups = new();

        public static List<Unit> Participants(Unit opener)
        {
            if (opener == null || GridManager.Instance == null) return new List<Unit>();
            List<Unit> allies = opener.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            return allies.Where(unit => unit != null && unit.isActive && unit.IsOnField &&
                                        unit.HasUnitTag(LokapalaTag) && unit.HasSpecialAction).ToList();
        }

        /// <summary>필드의 로카팔라 SP를 OpenOrder 순으로 예약한다. 바유 -100, 찬드라 -50을 표준으로 쓴다.</summary>
        public static int OpenGate(Unit opener)
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return 0;

            int opened = 0;
            foreach (Unit unit in Participants(opener).OrderBy(unit => unit.ActiveSpecialCode.OpenOrder))
            {
                if (Pending.Contains(unit)) continue;
                if (!scheduler.EnqueueSpecial(unit, "lokapala", unit.ActiveSpecialCode.CodeName, unit.CastSpecialCode)) continue;
                Pending.Add(unit);
                opened++;
            }

            Debug.Log($"[특수행동] {opener.UnitName}이(가) 로카팔라 {opened}명의 행동을 열었다");
            return opened;
        }

        public static void RegisterBatchCleanup(Action cleanup)
        {
            if (cleanup != null) BatchCleanups.Add(cleanup);
        }

        public static void NotifyResolved(Unit unit)
        {
            if (unit == null || !Pending.Remove(unit) || Pending.Count > 0) return;
            RunCleanups();
        }

        public static void Reset()
        {
            Pending.Clear();
            RunCleanups();
        }

        private static void RunCleanups()
        {
            Action[] callbacks = BatchCleanups.ToArray();
            BatchCleanups.Clear();
            foreach (Action callback in callbacks) callback?.Invoke();
        }
    }
}
