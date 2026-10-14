using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
namespace Combat
{
    /// <summary>아군 전용 전투 자원. 예약도 상한을 점유하고, 전투 세대가 바뀌면 오래된 탄창은 무효다.</summary>
    public static class PartyTreasury
    {
        public const int Capacity = 12;
        public static int Available { get; private set; }
        public static int Reserved { get; private set; }
        public static int Total => Available + Reserved;
        public static bool Active { get; private set; }
        public static bool RoyalPurchaseEntitled { get; private set; }
        private static int _generation;
        public static event Action<Unit, int> Changed;
        public static event Action BalanceChanged;
        public static event Action<Unit, int, ActionScheduler.ActionKind> SpentActionFinished;
        private static readonly Dictionary<(Unit actor, int action), int> ActionSpending = new();
        private static readonly HashSet<(Unit actor, int code)> OpeningGrants = new();
        private static int ActionId => GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
        public static bool CanUse(Unit actor) => Active && actor != null && !actor.IsEnemy && !actor.IsSummon && actor.IsOnField && actor.isActive;
        public static void BeginBattle()
        {
            Clear(); Active = true;
            ActionScheduler.AnyActionStarted += OnActionStarted;
            ActionScheduler.AnyActionFinished += ResolveAction;
            RoyalPurchaseEntitled = GridManager.Instance?.heroList.Any(u => u != null && u.IsOnField && !u.IsEnemy &&
                u.ActivePassiveCodes.OfType<Codes.Passive.MansaRoyalWarrant>().Any()) == true;
            if (RoyalPurchaseEntitled) GameManager.Instance?.AddBattleGoldBonus(.25f);
        }
        public static void EndBattle()
        {
            ActionScheduler.AnyActionStarted -= OnActionStarted;
            ActionScheduler.AnyActionFinished -= ResolveAction;
            Active = false; Available = Reserved = 0; _generation++;
            ActionSpending.Clear(); OpeningGrants.Clear(); BalanceChanged?.Invoke();
        }
        public static void Clear() { EndBattle(); RoyalPurchaseEntitled = false; }
        public static bool ClaimOpeningGrant(Unit actor, int code)
            => CanUse(actor) && OpeningGrants.Add((actor, code));
        private static void OnActionStarted(Unit actor, ActionScheduler.ActionKind kind, string label)
            => ActionSpending.Remove((actor, ActionId));
        private static void RecordSpending(Unit actor, int amount)
        {
            var key = (actor, ActionId);
            ActionSpending.TryGetValue(key, out int spent); ActionSpending[key] = spent + amount;
        }
        // 스케줄러의 종료 통지와 같은 입구. 예약·미발사 반환·추가 생산은 소비로 세지 않는다.
        internal static void ResolveAction(Unit actor, ActionScheduler.ActionKind kind)
        {
            var key = (actor, ActionId);
            if (!ActionSpending.TryGetValue(key, out int spent)) return;
            ActionSpending.Remove(key);
            if (CanUse(actor) && spent > 0) SpentActionFinished?.Invoke(actor, spent, kind);
        }
        public static int Charge(Unit actor, int amount)
        {
            if (!CanUse(actor) || amount <= 0) return 0;
            int given = Math.Min(amount, Capacity - Total); Available += given;
            if (given > 0) { Changed?.Invoke(actor, given); BalanceChanged?.Invoke(); }
            return given;
        }
        public static bool Spend(Unit actor, int amount)
        {
            if (!CanUse(actor) || amount <= 0 || Available < amount) return false;
            Available -= amount; RecordSpending(actor, amount);
            Changed?.Invoke(actor, -amount); BalanceChanged?.Invoke(); return true;
        }
        public static Magazine Reserve(Unit actor)
        {
            if (!CanUse(actor) || Available == 0) return null;
            var ammo = new Magazine(actor, Available, _generation);
            Reserved += Available; Available = 0; BalanceChanged?.Invoke(); return ammo;
        }
        public sealed class Magazine : IDisposable
        {
            private readonly Unit _owner; private readonly int _battle;
            public int Remaining { get; private set; }
            internal Magazine(Unit owner, int amount, int battle) { _owner = owner; Remaining = amount; _battle = battle; }
            public bool SpendOne()
            {
                if (_battle != _generation || Remaining == 0 || !CanUse(_owner)) return false;
                Remaining--; Reserved--; RecordSpending(_owner, 1);
                Changed?.Invoke(_owner, -1); BalanceChanged?.Invoke(); return true;
            }
            public void Dispose()
            {
                if (_battle == _generation && Remaining > 0)
                { Reserved -= Remaining; Available += Remaining; BalanceChanged?.Invoke(); }
                Remaining = 0;
            }
        }
    }
}
