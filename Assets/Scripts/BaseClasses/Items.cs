using Managers;
using UnityEngine;

namespace BaseClasses
{
    public class ShopItem: MonoBehaviour
    {
        public int id;
        public string itemName;
        public string description;
        public string iconPath;
        public int cost;
        public bool isBought;

        public void Initialize(ElementData data)
        {
            id = data.id;
            itemName = data.name;
            description = data.description;
            // 데이터에 아이콘 경로 추가 필요
            // iconPath = data.IconPath;
            cost = data.cost;
            isBought = false;
        }

        public void BuyItem()
        {
            // InventoryManager 에서 처리
        }
    }

    public class DeckElementItem: MonoBehaviour
    {
        public int id;
        public string itemName;
        public string description;
        public string iconPath;
        public int cost;
        public int count;
        
        public void Initialize(ElementData data)
        {
            id = data.id;
            itemName = data.name;
            description = data.description;
            // 데이터에 아이콘 경로 추가 필요
            // iconPath = data.IconPath;
            cost = data.cost;
            count = 0;
        }

        public void SynthesizeItem()
        {
            // InventoryManager에서 처리
        }
    }

    public class DeckUnitItem : MonoBehaviour
    {
        public int id;
        public string itemName;
        public string description;
        public string iconPath;
        public int cost;
        public int count;
        
        public void Initialize(RawElement data)
        {
            id = data.Id;
            itemName = data.Name;
            description = data.Description;
            // 데이터에 아이콘 경로 추가 필요
            // iconPath = data.IconPath;
            cost = data.Cost;
            count = 0;
        }
        
        public void SynthesizeItem()
        {
            // InventoryManager에서 처리
        }
        
        public void SummonItem()
        {
            // InventoryManager에서 처리
        }
    }
}