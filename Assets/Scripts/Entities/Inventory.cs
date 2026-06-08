using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;

namespace Entities
{
    /// <summary>
    /// 유닛 인벤토리: 7-슬롯 장비 + 20-칸 가방.
    /// 장비 보너스는 Unit.AttributesUpdate()에서 집계됨.
    /// </summary>
    public class Inventory
    {
        public const int SlotCount   = 7;   // EquipSlotType 열거형 개수와 일치
        public const int BagCapacity = 20;

        private readonly ItemData[] _equipped = new ItemData[SlotCount];
        private readonly List<ItemData> _bag  = new List<ItemData>(BagCapacity);
        private readonly Unit _owner;

        public Inventory(Unit owner)
        {
            _owner = owner;
        }

        // ── 장비 슬롯 접근 ──────────────────────────────────────────────────────

        public ItemData GetEquipped(BaseEnums.EquipSlotType slot)
            => _equipped[(int)slot];

        /// <summary>
        /// 아이템을 장비 슬롯에 장착한다.
        /// 슬롯에 이미 아이템이 있으면 displaced로 반환 (bag에 넣거나 드롭).
        /// 슬롯 타입이 다르면 장착하지 않고 displaced = item 반환.
        /// </summary>
        public void Equip(ItemData item, BaseEnums.EquipSlotType slot, out ItemData displaced)
        {
            displaced = null;
            if (item == null) return;

            // 슬롯 타입 불일치 방어
            if (item.SlotType != slot)
            {
                UnityEngine.Debug.LogWarning($"[Inventory] {item.itemName} 은 {slot} 슬롯에 장착 불가 (슬롯 타입: {item.SlotType})");
                displaced = item;
                return;
            }

            // 가방에서 장착할 아이템 먼저 제거 (슬롯을 확보해야 교체 시 드롭 오류 방지)
            _bag.Remove(item);

            displaced = _equipped[(int)slot];
            _equipped[(int)slot] = item;

            // 기존 아이템 가방으로 이동 (가방이 가득 차면 드롭)
            if (displaced != null)
            {
                if (!AddToBag(displaced))
                    UnityEngine.Debug.LogWarning($"[Inventory] 가방이 가득 차 {displaced.itemName} 드롭됨");
            }

            _owner.AttributesUpdate();
        }

        /// <summary>슬롯의 장비 해제 → 가방으로 이동</summary>
        public void Unequip(BaseEnums.EquipSlotType slot)
        {
            ItemData item = _equipped[(int)slot];
            if (item == null) return;

            _equipped[(int)slot] = null;
            if (!AddToBag(item))
                UnityEngine.Debug.LogWarning($"[Inventory] 가방이 가득 차 {item.itemName} 드롭됨");

            _owner.AttributesUpdate();
        }

        // ── 가방 접근 ───────────────────────────────────────────────────────────

        public IReadOnlyList<ItemData> Bag => _bag;

        public bool AddToBag(ItemData item)
        {
            if (_bag.Count >= BagCapacity) return false;
            _bag.Add(item);
            return true;
        }

        public void RemoveFromBag(ItemData item)
        {
            _bag.Remove(item);
        }

        public void SwapBagSlots(int a, int b)
        {
            if (a < 0 || a >= _bag.Count || b < 0 || b >= _bag.Count) return;
            (_bag[a], _bag[b]) = (_bag[b], _bag[a]);
        }

        // ── 집계 보너스 (Unit.AttributesUpdate()에서 호출) ──────────────────────

        public int   TotalAtkBonus  => SumEquipped(i => i.bonusAtk);
        public int   TotalDefBonus  => SumEquipped(i => i.bonusDef);
        public int   TotalHpBonus   => SumEquipped(i => i.bonusHp);
        public float TotalCritBonus => SumEquippedF(i => i.bonusCritChance);
        public float TotalSpdBonus  => SumEquippedF(i => i.bonusSpeed);

        private int   SumEquipped(Func<ItemData, int> fn)
            => _equipped.Where(i => i != null).Sum(fn);

        private float SumEquippedF(Func<ItemData, float> fn)
            => _equipped.Where(i => i != null).Sum(fn);
    }
}
