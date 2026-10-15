using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 특수행동 묶음을 한 번에 예약한다. 같은 묶음의 임시 효과는 마지막 SP가 끝날 때 걷는다.
    ///
    /// 묶음은 <b>유닛 태그</b>(<c>gateTag</c>)로 정한다. 지금은 로카팔라 하나이고 여는 것은 인드라의 초월기뿐이다
    /// (Detail_08 §13). 다른 특수 묶음이 생기면 태그만 다르게 넘기면 된다.
    ///
    /// 표준 순서(<see cref="Codes.Base.SpecialCode.OpenOrder"/>): 디버프 · 버프를 거는 참여자는 음수(바유 -100 · 바루나 -50),
    /// 피해가 주인 참여자는 0 이상(쿠베라 0 · 인드라 +50 · 야마 +75), 회복만 하는 참여자는 맨 끝(찬드라 +100).
    /// </summary>
    public static class SpecialAction
    {
        public const string LokapalaTag = "Lokapala";

        private static readonly HashSet<Unit> Pending = new();
        private static readonly List<Action> BatchCleanups = new();

        /// <summary>지금 진행 중인 묶음을 연 유닛. 묶음이 끝나면 비운다. 아그니의 보류 판정이 읽는다(Detail_08 §17).</summary>
        public static Unit CurrentOpener { get; private set; }

        /// <summary>이 유닛의 특수행동이 진행 중인 묶음에 예약되어 아직 끝나지 않았는가.</summary>
        public static bool IsPending(Unit unit) => unit != null && Pending.Contains(unit);

        public static List<Unit> Participants(Unit opener, string gateTag = LokapalaTag)
        {
            if (opener == null || GridManager.Instance == null) return new List<Unit>();
            List<Unit> allies = opener.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            return allies.Where(unit => unit != null && unit.isActive && unit.IsOnField &&
                                        unit.HasUnitTag(gateTag) && unit.HasSpecialAction).ToList();
        }

        /// <summary>필드의 같은 묶음 SP를 OpenOrder 순으로 예약한다.</summary>
        public static int OpenGate(Unit opener, string gateTag = LokapalaTag)
        {
            if (!DirectAttack.CasterCanContinue(opener, true)) return 0;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return 0;

            int opened = 0;
            foreach (Unit unit in Participants(opener, gateTag).OrderBy(unit => unit.ActiveSpecialCode.OpenOrder))
            {
                if (Pending.Contains(unit)) continue;
                if (!scheduler.EnqueueSpecial(unit, gateTag, unit.ActiveSpecialCode.CodeName, unit.CastSpecialCode)) continue;
                Pending.Add(unit);
                opened++;
            }
            if (opened > 0) CurrentOpener = opener;

            Debug.Log($"[특수행동] {opener.UnitName}이(가) {gateTag} 묶음 {opened}명의 행동을 열었다");
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
            CurrentOpener = null;
            Action[] callbacks = BatchCleanups.ToArray();
            BatchCleanups.Clear();
            foreach (Action callback in callbacks) callback?.Invoke();
        }
    }
}
