using System.Linq;
using Codes.Base;
using Core;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    public static partial class RewardCodeInventory
    {
        // 취득 당시 시세를 고정한다. 오래 보관하거나 무료로 옮겨 판매가를 부풀릴 수 없다.
        public static int SalePrice(RewardCodeInstanceData holding)
        {
            var entry=RewardCodeCatalog.Find(holding?.codeId??0);if(entry==null)return 0;
            float basis=entry.shopPrice>0?entry.shopPrice*.25f:20*Mathf.Pow(2,Mathf.Clamp(entry.tier-1,0,4));
            float charged=entry.capacitorCap>0?Mathf.Clamp01(holding.charge/entry.capacitorCap):0;
            return Mathf.Max(1,Mathf.RoundToInt(basis*Mathf.Max(1,holding.acquiredStage)*(1+charged)));
        }
        public static string ChargeSummary(RewardCodeInstanceData holding)
        {
            var entry=RewardCodeCatalog.Find(holding?.codeId??0);
            if(entry==null || entry.capacitorCap<=0)return "";
            return ChargeBrief(holding) + $" · 판매 {SalePrice(holding):N0} G\n";
        }
        /// <summary>좁은 카드에서는 저장량·현재 스탯을 먼저 읽고 판매 가격은 상세/상점에서 확인한다.</summary>
        public static string ChargeBrief(RewardCodeInstanceData holding)
        {
            var entry=RewardCodeCatalog.Find(holding?.codeId??0);
            if(entry==null || entry.capacitorCap<=0)return "";
            string stat=System.Enum.TryParse(entry.capacitorStat,true,out BaseClasses.BaseEnums.StatType kind)
                ? Entities.UnitStats.DisplayName(kind) : entry.capacitorStat;
            string gain=(holding.charge*entry.capacitorPerCharge).ToString("0.##");
            return $"저장 {holding.charge:0.##}/{entry.capacitorCap:0} · {stat} +{gain}{(entry.capacitorStat.StartsWith("Crit")?"%p":"")}";
        }
        public static bool TrySell(Unit owner,string instanceId,out int gold)
        {
            gold=0;
            var game=GameManager.Instance;
            if(game?.gameState!=BaseClasses.BaseEnums.GameState.Preparation || !AttackForms.PartyUnits().Contains(owner))return false;
            var holding=owner.OwnedRewardCodeInstances.FirstOrDefault(x=>x.instanceId==instanceId);
            if(holding==null || game.inventoryManager==null)return false;
            int price=SalePrice(holding);
            if(!owner.RemoveRewardCodeInstance(instanceId))return false;
            game.inventoryManager.AddGold(price);gold=price;
            game.runManager?.SaveCurrentRun();return true;
        }
    }
}
