using System;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace BaseClasses
{
    public enum EquipmentSlot
    {
        Head,
        Necklace,
        Armor,
        Ring,
        Shoes,
        MainHand,
        OffHand,
    }

    public enum ItemRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
    }

    public enum EquipmentProficiency
    {
        None,
        Wand,
        Longbow,
        LightArmor,
        MediumArmor,
        HeavyArmor,
        Shield,
        Sword,
    }

    [Serializable]
    public class EquipmentStatBonus
    {
        public string stat;
        public int amount;
    }

    [Serializable]
    public class EquipmentCodeGrant
    {
        public string slot;
        public int codeId;
        public int stage = 1;
    }

    public class EquippedItem
    {
        public ItemData Data { get; }

        public EquippedItem(ItemData data)
        {
            Data = data;
        }
    }

    public class EquipmentLoadout
    {
        private readonly Dictionary<EquipmentSlot, EquippedItem> _items = new();

        public IReadOnlyDictionary<EquipmentSlot, EquippedItem> Items => _items;

        public bool TryEquip(ItemData itemData, Func<EquipmentProficiency, bool> hasProficiency, out string reason)
        {
            reason = null;
            if (itemData == null)
            {
                reason = "아이템 데이터가 없습니다.";
                return false;
            }

            if (!TryParseSlot(itemData.slot, out EquipmentSlot slot))
            {
                reason = $"알 수 없는 장비 슬롯입니다: {itemData.slot}";
                return false;
            }

            if (itemData.RequiredProficiency != EquipmentProficiency.None && !hasProficiency(itemData.RequiredProficiency))
            {
                reason = $"{itemData.requiredProficiency} 숙련이 없어 장착할 수 없습니다.";
                return false;
            }

            if (slot == EquipmentSlot.OffHand && IsMainHandTwoHanded())
            {
                reason = "양손무장을 장착 중이라 부무장 슬롯을 사용할 수 없습니다.";
                return false;
            }

            if (slot == EquipmentSlot.MainHand && itemData.twoHanded)
            {
                _items.Remove(EquipmentSlot.OffHand);
            }

            _items[slot] = new EquippedItem(itemData);
            return true;
        }

        public IEnumerable<ItemData> GetEquippedItemData()
        {
            return _items.Values.Select(item => item.Data);
        }

        public int GetTotalWeight()
        {
            return _items.Values.Sum(item => Math.Max(0, item.Data.weight));
        }

        public int GetProspectiveWeight(ItemData itemData)
        {
            if (itemData == null || !TryParseSlot(itemData.slot, out EquipmentSlot slot))
            {
                return GetTotalWeight();
            }

            int weight = GetTotalWeight();
            if (_items.TryGetValue(slot, out EquippedItem replaced))
            {
                weight -= Math.Max(0, replaced.Data.weight);
            }
            if (slot == EquipmentSlot.MainHand && itemData.twoHanded &&
                _items.TryGetValue(EquipmentSlot.OffHand, out EquippedItem offHand))
            {
                weight -= Math.Max(0, offHand.Data.weight);
            }
            return Math.Max(0, weight) + Math.Max(0, itemData.weight);
        }

        private bool IsMainHandTwoHanded()
        {
            return _items.TryGetValue(EquipmentSlot.MainHand, out EquippedItem mainHand) && mainHand.Data.twoHanded;
        }

        public static bool TryParseSlot(string value, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.Head;
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.Trim().ToLowerInvariant() switch
            {
                "head" or "helmet" or "투구" => SetSlot(EquipmentSlot.Head, out slot),
                "necklace" or "amulet" or "목걸이" => SetSlot(EquipmentSlot.Necklace, out slot),
                "armor" or "body" or "갑옷" => SetSlot(EquipmentSlot.Armor, out slot),
                "ring" or "반지" => SetSlot(EquipmentSlot.Ring, out slot),
                "shoes" or "boots" or "신발" => SetSlot(EquipmentSlot.Shoes, out slot),
                "mainhand" or "main_hand" or "main hand" or "주무장" => SetSlot(EquipmentSlot.MainHand, out slot),
                "offhand" or "off_hand" or "off hand" or "부무장" => SetSlot(EquipmentSlot.OffHand, out slot),
                _ => Enum.TryParse(value, true, out slot),
            };
        }

        private static bool SetSlot(EquipmentSlot value, out EquipmentSlot slot)
        {
            slot = value;
            return true;
        }
    }
}
