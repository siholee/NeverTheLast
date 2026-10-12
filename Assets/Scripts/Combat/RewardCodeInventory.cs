using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>준비 중 코드 이동. 양쪽 활성 슬롯을 먼저 검사해 교환 실패로 코드를 잃지 않는다.</summary>
    public static partial class RewardCodeInventory
    {
        public sealed class Drag
        {
            public Unit Owner;
            public bool Stored;
            public int Index;
            public int CodeId;
            public string InstanceId;
        }

        private static bool Prepare(Drag drag, Unit target, bool toStorage, int slot,
            out List<int> fromSlots, out List<int> fromStore, out List<int> toSlots, out List<int> toStore)
        {
            fromSlots = fromStore = toSlots = toStore = null;
            if (GameManager.Instance?.gameState != BaseClasses.BaseEnums.GameState.Preparation ||
                drag?.Owner == null || target == null || drag.Owner.IsSummon || target.IsSummon ||
                drag.Owner.IsEnemy || target.IsEnemy || !AttackForms.PartyUnits().Contains(drag.Owner) ||
                !AttackForms.PartyUnits().Contains(target)) return false;
            fromSlots = drag.Owner.RewardCodeSlots.ToList(); fromStore = drag.Owner.StoredRewardCodeIds.ToList();
            bool same = drag.Owner == target;
            toSlots = same ? fromSlots : target.RewardCodeSlots.ToList();
            toStore = same ? fromStore : target.StoredRewardCodeIds.ToList();
            List<int> source = drag.Stored ? fromStore : fromSlots;
            if (drag.Index < 0 || drag.Index >= source.Count || source[drag.Index] != drag.CodeId || drag.CodeId <= 0) return false;
            var holding=drag.Stored?drag.Owner.StoredRewardCodeInstances.ElementAtOrDefault(drag.Index):drag.Owner.RewardCodeSlotInstances.ElementAtOrDefault(drag.Index);
            if(!string.IsNullOrEmpty(drag.InstanceId) && holding?.instanceId!=drag.InstanceId)return false;
            if (toStorage)
            {
                if (same && drag.Stored) return false;
                if (drag.Stored) fromStore.RemoveAt(drag.Index); else fromSlots[drag.Index] = 0;
                toStore.Add(drag.CodeId);
            }
            else
            {
                if (slot < 0 || slot >= Unit.RewardCodeSlotCount || (same && !drag.Stored && slot == drag.Index)) return false;
                int displaced = toSlots[slot];
                if (drag.Stored)
                {
                    fromStore.RemoveAt(drag.Index);
                    if (displaced > 0) fromStore.Insert(drag.Index, displaced);
                }
                else fromSlots[drag.Index] = displaced;
                toSlots[slot] = drag.CodeId;
            }
            return Unit.ValidRewardCodeInventory(fromSlots, fromStore) && Unit.ValidRewardCodeInventory(toSlots, toStore);
        }

        public static bool CanMove(Drag drag, Unit target, bool toStorage, int slot = -1)
            => Prepare(drag, target, toStorage, slot, out _, out _, out _, out _);

        public static bool TryMove(Drag drag, Unit target, bool toStorage, int slot = -1)
        {
            if (!Prepare(drag, target, toStorage, slot, out var fs, out var fb, out var ts, out var tb)) return false;
            var oldSlots = drag.Owner.RewardCodeSlots.ToList(); var oldStore = drag.Owner.StoredRewardCodeIds.ToList();
            var oldSI=drag.Owner.RewardCodeSlotInstances.ToList();var oldBI=drag.Owner.StoredRewardCodeInstances.ToList();
            var fsi=oldSI.ToList();var fbi=oldBI.ToList();
            bool same=drag.Owner==target;
            var tsi=same?fsi:target.RewardCodeSlotInstances.ToList();
            var tbi=same?fbi:target.StoredRewardCodeInstances.ToList();
            var moving=drag.Stored?fbi[drag.Index]:fsi[drag.Index];
            if(toStorage)
            {
                if(drag.Stored)fbi.RemoveAt(drag.Index);else fsi[drag.Index]=null;
                tbi.Add(moving);
            }
            else
            {
                var displaced=tsi[slot];
                if(drag.Stored){fbi.RemoveAt(drag.Index);if(displaced!=null)fbi.Insert(drag.Index,displaced);}
                else fsi[drag.Index]=displaced;
                tsi[slot]=moving;
            }
            if (!drag.Owner.ApplyRewardCodeInventory(fs, fb,fsi,fbi)) return false;
            if (!same && !target.ApplyRewardCodeInventory(ts, tb,tsi,tbi))
            { drag.Owner.ApplyRewardCodeInventory(oldSlots, oldStore,oldSI,oldBI); return false; }
            GameManager.Instance.runManager?.SaveCurrentRun();
            return true;
        }
    }
}

