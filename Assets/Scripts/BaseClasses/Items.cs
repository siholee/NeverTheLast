using System.Collections.Generic;
using Managers;
using UnityEngine;

namespace BaseClasses
{
    public class ShopItem
    {
        public int ID;
        public string ItemName;
        public string IconPath;
        public Dictionary<int, int> Cost;
        public List<int> SynergyIds; // 시너지 ID 목록
        public bool IsBought;

        public void Initialize(UnitData data)
        {
            ID = data.id;
            ItemName = data.name;
            IconPath = data.portrait;
            // 데이터에 아이콘 경로 추가 필요
            // iconPath = data.IconPath;
            SynergyIds = data.synergies;
            IsBought = false;
            var costList = data.cost;
            var totalCost = data.costAmount;
            var newCost = new Dictionary<int, int>();

            var remainingCost = totalCost;
            for (var i = 0; i < costList.Count - 1; i++)
            {
                var allocatedCost = Random.Range(0, remainingCost + 1);
                newCost[costList[i]] = allocatedCost;
                remainingCost -= allocatedCost;
            }
            newCost[costList[^1]] = remainingCost;
            Cost = newCost;
        }

        public virtual void BuyItem()
        {
            // InventoryManager 에서 처리
        }
    }

    public class DeckUnitItem : MonoBehaviour
    {
        public int id;
        public string itemName;
        public string iconPath;
        public List<int> cost;
        public List<int> synergyIds; // 시너지 ID 목록
        
        public void Initialize(UnitData data)
        {
            id = data.id;
            itemName = data.name;
            // 데이터에 아이콘 경로 추가 필요
            // iconPath = data.IconPath;
            cost = data.cost;
            synergyIds = data.synergies;
        }
        
        public void SummonItem()
        {
            // InventoryManager에서 처리
        }
    }
}