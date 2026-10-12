using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Core;
using UnityEngine;

namespace Entities
{
    public partial class Unit
    {
        private List<RewardCodeInstanceData> _rewardSlotInstances = Enumerable.Repeat<RewardCodeInstanceData>(null, RewardCodeSlotCount).ToList();
        private List<RewardCodeInstanceData> _rewardStorageInstances = new();
        private readonly Dictionary<BaseEnums.StatType,float> _nectarStats = new();
        public IReadOnlyList<RewardCodeInstanceData> RewardCodeSlotInstances => _rewardSlotInstances;
        public IReadOnlyList<RewardCodeInstanceData> StoredRewardCodeInstances => _rewardStorageInstances;
        public IEnumerable<RewardCodeInstanceData> OwnedRewardCodeInstances => _rewardSlotInstances.Concat(_rewardStorageInstances).Where(x=>x!=null);
        public RewardCodeInstanceData ActiveRewardInstance(int codeId) => _rewardSlotInstances.FirstOrDefault(x=>x?.codeId==codeId);
        public float NectarStatBonus(BaseEnums.StatType stat) => _nectarStats.TryGetValue(stat,out float value)?value:0;

        private void ResetRewardGrowth()
        {
            _rewardSlotInstances=Enumerable.Repeat<RewardCodeInstanceData>(null,RewardCodeSlotCount).ToList();
            _rewardStorageInstances.Clear(); _nectarStats.Clear();
        }
        private RewardCodeInstanceData NewRewardInstance(int id) => id<=0?null:new()
        {
            codeId=id,instanceId=Guid.NewGuid().ToString("N"),acquiredStage=Mathf.Max(1,Managers.GameManager.Instance?.RoundManager?.Stage??1)
        };
        private bool BuildRewardInstances(IReadOnlyList<int> slots,IReadOnlyList<int> stored,
            IReadOnlyList<RewardCodeInstanceData> slotState,IReadOnlyList<RewardCodeInstanceData> storageState,
            out List<RewardCodeInstanceData> nextSlots,out List<RewardCodeInstanceData> nextStored)
        {
            nextSlots=new();nextStored=new();
            if((slotState==null)!=(storageState==null))return false;
            if(slotState!=null)
            {
                if(slotState.Count!=slots.Count || storageState.Count!=stored.Count)return false;
                for(int i=0;i<slots.Count;i++)if((slotState[i]?.codeId??0)!=slots[i])return false;
                for(int i=0;i<stored.Count;i++)if(storageState[i]?.codeId!=stored[i])return false;
                nextSlots=slotState.ToList();nextStored=storageState.ToList();
            }
            else
            {
                var pool=OwnedRewardCodeInstances.ToList();
                RewardCodeInstanceData Take(int id)
                {
                    if(id<=0)return null;
                    var item=pool.FirstOrDefault(x=>x.codeId==id);
                    if(item!=null)pool.Remove(item);
                    return item??NewRewardInstance(id);
                }
                nextSlots=slots.Select(Take).ToList();nextStored=stored.Select(Take).ToList();
            }
            var all=nextSlots.Concat(nextStored).Where(x=>x!=null).ToList();
            return all.All(x=>!string.IsNullOrEmpty(x.instanceId)) && all.Select(x=>x.instanceId).Distinct().Count()==all.Count;
        }
        private void RestoreRewardGrowth(UnitSaveData saved)
        {
            RewardCodeInstanceData Read(RewardCodeInstanceData old,int id)
            {
                if(id<=0)return null;
                var value=old?.codeId==id?old.Copy():NewRewardInstance(id);
                if(string.IsNullOrEmpty(value.instanceId))value.instanceId=Guid.NewGuid().ToString("N");
                float cap=RewardCodeCatalog.Find(id)?.capacitorCap??0;
                value.charge=float.IsNaN(value.charge)||float.IsInfinity(value.charge)?0:Mathf.Clamp(value.charge,0,cap);
                value.acquiredStage=Mathf.Max(1,value.acquiredStage);
                return value;
            }
            var seen=new HashSet<string>();
            RewardCodeInstanceData Unique(RewardCodeInstanceData value)
            {
                if(value!=null&&!seen.Add(value.instanceId)){value.instanceId=Guid.NewGuid().ToString("N");seen.Add(value.instanceId);}
                return value;
            }
            _rewardSlotInstances=rewardCodeSlots.Select((id,i)=>Unique(Read(saved.activeRewardCodeInstances?.ElementAtOrDefault(i),id))).ToList();
            _rewardStorageInstances=storedRewardCodeIds.Select((id,i)=>Unique(Read(saved.storedRewardCodeInstances?.ElementAtOrDefault(i),id))).ToList();
            _nectarStats.Clear();
            foreach(var bonus in saved.nectarStats??new List<PermanentStatSaveData>())
                if(bonus!=null && Enum.TryParse(bonus.stat,true,out BaseEnums.StatType stat) && (int)stat>=0 && (int)stat<UnitStats.StatCount &&
                    !float.IsNaN(bonus.amount)&&!float.IsInfinity(bonus.amount))_nectarStats[stat]=Mathf.Max(0,bonus.amount);
        }
        private void WriteRewardGrowth(UnitSaveData saved)
        {
            saved.activeRewardCodeInstances=_rewardSlotInstances.Select(x=>x?.Copy()).ToList();
            saved.storedRewardCodeInstances=_rewardStorageInstances.Select(x=>x?.Copy()).ToList();
            saved.nectarStats=_nectarStats.Select(x=>new PermanentStatSaveData{stat=x.Key.ToString(),amount=x.Value}).ToList();
        }
        /// <summary>활성 코드 한 장에만 충전한다. 획득 배율은 기존 영구 중첩과 똑같이 한 번 적용한다.</summary>
        public float GainRewardCharge(int codeId,float amount)
        {
            var instance=ActiveRewardInstance(codeId);var entry=RewardCodeCatalog.Find(codeId);
            if(instance==null || entry==null || entry.capacitorCap<=0 || amount<=0)return 0;
            float multiplier=1;
            foreach(var passive in ActivePassiveCodesResolved())multiplier*=Mathf.Max(0,passive.PermanentStackGainMultiplier("capacitor."+instance.instanceId));
            float before=instance.charge;
            instance.charge=Mathf.Min(entry.capacitorCap,before+amount*multiplier);
            if(instance.charge>before)RefreshAttributes();
            return instance.charge-before;
        }
        internal void ApplyNectar(Managers.ItemData item)
        {
            foreach(var pair in item.nectarFlatStats)
            {
                if(!Enum.TryParse(pair.Key,true,out BaseEnums.StatType stat)||(int)stat<0||(int)stat>=UnitStats.StatCount)continue;
                float value=stat is BaseEnums.StatType.CritRate or BaseEnums.StatType.CritDamage?pair.Value/100:pair.Value;
                _nectarStats[stat]=NectarStatBonus(stat)+Mathf.Max(0,value);
            }
            RefreshAttributes();
        }
        public bool RemoveRewardCodeInstance(string instanceId)
        {
            var si=_rewardSlotInstances.ToList();var bi=_rewardStorageInstances.ToList();
            int index=si.FindIndex(x=>x?.instanceId==instanceId);
            if(index>=0)si[index]=null;
            else {index=bi.FindIndex(x=>x?.instanceId==instanceId);if(index<0)return false;bi.RemoveAt(index);}
            return ApplyRewardCodeInventory(si.Select(x=>x?.codeId??0).ToList(),bi.Select(x=>x.codeId).ToList(),si,bi);
        }
    }
}
