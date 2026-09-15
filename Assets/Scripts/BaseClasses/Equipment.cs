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

    /// <summary>
    /// 장비 숙련.
    ///
    /// <b>완드는 없앴다.</b> 지팡이·완드 형태의 무기는 창 계열이 받는다 —
    /// 양손 지팡이는 장창(<see cref="Spear"/>), 한손 완드는 단창(<see cref="Shortspear"/>)이다.
    /// 분류를 따로 두면 법사가 들 수 있는 무기 칸만 늘고 실제로 고를 것은 없었다.
    /// </summary>
    public enum EquipmentProficiency
    {
        None,
        Dagger,
        Orb,
        Greatsword,
        Longbow,
        Shortbow,
        Crossbow,
        LightArmor,
        MediumArmor,
        HeavyArmor,
        Shield,
        Longsword,
        Mace,
        Spear,

        /// <summary>단창 — 한손 창. 장창(Spear)과 달리 부무장 슬롯을 비우지 않는다.</summary>
        Shortspear,
    }

    public static class EquipmentProficiencyRules
    {
        public static bool IsWeapon(this EquipmentProficiency proficiency)
        {
            return proficiency is EquipmentProficiency.Dagger
                or EquipmentProficiency.Orb
                or EquipmentProficiency.Greatsword
                or EquipmentProficiency.Longbow
                or EquipmentProficiency.Shortbow
                or EquipmentProficiency.Crossbow
                or EquipmentProficiency.Shield
                or EquipmentProficiency.Longsword
                or EquipmentProficiency.Mace
                or EquipmentProficiency.Spear
                or EquipmentProficiency.Shortspear;
        }

        public static bool IsBow(this EquipmentProficiency proficiency)
        {
            return proficiency is EquipmentProficiency.Longbow
                or EquipmentProficiency.Shortbow
                or EquipmentProficiency.Crossbow;
        }
    }

    /// <summary>
    /// 장비의 <c>statBonuses</c>가 5스탯 대신 실을 수 있는 부가 수치.
    ///
    /// 장비 한 점은 <b>어떤 수치를 올릴지만</b> 고르고 그 크기는 티어가 정한다
    /// (Detail_14 §2). 5스탯이 아닌 것을 고르면 여기의 항목이 된다.
    /// </summary>
    public enum EquipmentSecondaryStat
    {
        None,

        /// <summary>치명타 확률(%p).</summary>
        CritRate,

        /// <summary>치명타 피해(%p).</summary>
        CritDamage,

        /// <summary>내구 — 받는 피해에서 고정으로 깎는 값. 방어구의 분류 내구에 더해진다.</summary>
        Durability,
    }

    [Serializable]
    public class EquipmentStatBonus
    {
        public string stat;
        public int amount;

        /// <summary>5스탯이면 그 값, 아니면 false. 부가 수치는 <see cref="Secondary"/>가 받는다.</summary>
        public bool TryGetPrimary(out BaseEnums.PrimaryStat primary)
        {
            primary = default;
            return !string.IsNullOrWhiteSpace(stat) && Enum.TryParse(stat, true, out primary);
        }

        public EquipmentSecondaryStat Secondary => EquipmentStatKeys.ParseSecondary(stat);
    }

    /// <summary>
    /// 데이터에 적히는 스탯 키를 해석하고 화면에 쓸 이름을 낸다.
    /// 기획서가 한국어로 적혀 있어 한국어 표기도 함께 받는다.
    /// </summary>
    public static class EquipmentStatKeys
    {
        public static EquipmentSecondaryStat ParseSecondary(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return EquipmentSecondaryStat.None;

            return key.Trim().Replace("_", string.Empty).ToLowerInvariant() switch
            {
                "critrate" or "critchance" or "치명타확률" or "치확" => EquipmentSecondaryStat.CritRate,
                "critdamage" or "critdmg" or "치명타피해" or "치피" => EquipmentSecondaryStat.CritDamage,
                "durability" or "내구" or "내구도" => EquipmentSecondaryStat.Durability,
                _ => EquipmentSecondaryStat.None,
            };
        }

        /// <summary>툴팁·보상 화면에 그대로 찍는 이름. 5스탯은 영문 약어를 유지한다.</summary>
        public static string DisplayName(string key)
        {
            return ParseSecondary(key) switch
            {
                EquipmentSecondaryStat.CritRate => "치명타 확률",
                EquipmentSecondaryStat.CritDamage => "치명타 피해",
                EquipmentSecondaryStat.Durability => "내구",
                _ => key,
            };
        }

        /// <summary>치명타 계열은 %p 단위라 접미사가 붙는다.</summary>
        public static string DisplaySuffix(string key)
        {
            return ParseSecondary(key) switch
            {
                EquipmentSecondaryStat.CritRate or EquipmentSecondaryStat.CritDamage => "%",
                _ => string.Empty,
            };
        }

        /// <summary>"치명타 피해 +10%" 꼴의 한 줄.</summary>
        public static string Describe(EquipmentStatBonus bonus)
            => bonus == null
                ? string.Empty
                : $"{DisplayName(bonus.stat)} +{bonus.amount}{DisplaySuffix(bonus.stat)}";
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

        public bool TryEquip(ItemData itemData, out string reason)
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

        /// <summary>장착 중인 아이템 하나를 벗긴다. 착용 중이 아니면 false.</summary>
        public bool Unequip(int itemId)
        {
            EquipmentSlot? found = null;
            foreach (KeyValuePair<EquipmentSlot, EquippedItem> pair in _items)
            {
                if (pair.Value?.Data == null || pair.Value.Data.id != itemId) continue;

                found = pair.Key;
                break;
            }

            if (found == null) return false;

            _items.Remove(found.Value);
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
