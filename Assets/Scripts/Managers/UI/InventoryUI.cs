using BaseClasses;
using Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 인벤토리 패널 UI.
    /// TAB 키로 토글. 왼쪽: 마네킹(7 슬롯), 오른쪽: 가방(20 슬롯).
    ///
    /// 슬롯 배치 (Inspector에서 할당):
    ///   equipSlots[0] = MainWeapon(주무기)
    ///   equipSlots[1] = SubWeapon(보조무기)
    ///   equipSlots[2] = Helmet(투구)
    ///   equipSlots[3] = Necklace(목걸이)
    ///   equipSlots[4] = Ring(반지)
    ///   equipSlots[5] = Armor(갑옷)
    ///   equipSlots[6] = Shoes(신발)
    ///   bagSlots[0..19] = 가방 슬롯
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("패널 루트")]
        [SerializeField] private GameObject inventoryPanel;

        [Header("장비 슬롯 (EquipSlotType 순서대로 7개)")]
        [SerializeField] private Button[] equipSlots = new Button[7];

        [Header("가방 슬롯 (20개)")]
        [SerializeField] private Button[] bagSlots = new Button[20];

        [Header("툴팁")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMPro.TextMeshProUGUI tooltipName;
        [SerializeField] private TMPro.TextMeshProUGUI tooltipDesc;
        [SerializeField] private TMPro.TextMeshProUGUI tooltipStats;
        [SerializeField] private Button equipButton;    // [장착] 버튼
        [SerializeField] private Button unequipButton;  // [해제] 버튼

        private Unit _currentUnit;
        private ItemData _selectedItem;
        private BaseEnums.EquipSlotType _selectedSlot;
        private bool _selectedFromBag;

        private void Awake()
        {
            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);

            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);

            // 장비 슬롯 클릭 이벤트
            for (int i = 0; i < equipSlots.Length; i++)
            {
                int captured = i;
                if (equipSlots[i] != null)
                    equipSlots[i].onClick.AddListener(() => OnEquipSlotClicked((BaseEnums.EquipSlotType)captured));
            }

            // 가방 슬롯 클릭 이벤트
            for (int i = 0; i < bagSlots.Length; i++)
            {
                int captured = i;
                if (bagSlots[i] != null)
                    bagSlots[i].onClick.AddListener(() => OnBagSlotClicked(captured));
            }

            if (equipButton  != null) equipButton.onClick.AddListener(OnEquipClicked);
            if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequipClicked);
        }

        private void Update()
        {
            // TAB 키로 인벤토리 토글
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();
        }

        public void Toggle()
        {
            if (inventoryPanel == null) return;
            bool next = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(next);
            if (next) Refresh();
        }

        public void Open(Unit unit)
        {
            _currentUnit = unit;
            if (inventoryPanel != null) inventoryPanel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            HideTooltip();
        }

        // ── 슬롯 갱신 ──────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_currentUnit == null) return;
            var inv = _currentUnit.Inventory;

            // 장비 슬롯 갱신
            for (int i = 0; i < equipSlots.Length; i++)
            {
                if (equipSlots[i] == null) continue;
                var item = inv.GetEquipped((BaseEnums.EquipSlotType)i);
                var label = equipSlots[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null)
                    label.text = item != null ? item.itemName : $"[{(BaseEnums.EquipSlotType)i}]";
            }

            // 가방 슬롯 갱신
            var bag = inv.Bag;
            for (int i = 0; i < bagSlots.Length; i++)
            {
                if (bagSlots[i] == null) continue;
                var label = bagSlots[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null)
                    label.text = i < bag.Count ? bag[i].itemName : "";
            }
        }

        // ── 클릭 핸들러 ────────────────────────────────────────────────────────

        private void OnEquipSlotClicked(BaseEnums.EquipSlotType slot)
        {
            if (_currentUnit == null) return;
            var item = _currentUnit.Inventory.GetEquipped(slot);
            _selectedSlot = slot;
            _selectedFromBag = false;

            if (item != null)
            {
                _selectedItem = item;
                ShowTooltip(item, canEquip: false, canUnequip: true);
            }
            else
            {
                HideTooltip();
            }
        }

        private void OnBagSlotClicked(int bagIndex)
        {
            if (_currentUnit == null) return;
            var bag = _currentUnit.Inventory.Bag;
            if (bagIndex >= bag.Count)
            {
                HideTooltip();
                return;
            }

            _selectedItem = bag[bagIndex];
            _selectedFromBag = true;
            ShowTooltip(_selectedItem, canEquip: true, canUnequip: false);
        }

        private void OnEquipClicked()
        {
            if (_currentUnit == null || _selectedItem == null || !_selectedFromBag) return;
            _currentUnit.Inventory.Equip(_selectedItem, _selectedItem.SlotType, out _);
            HideTooltip();
            Refresh();
        }

        private void OnUnequipClicked()
        {
            if (_currentUnit == null) return;
            _currentUnit.Inventory.Unequip(_selectedSlot);
            HideTooltip();
            Refresh();
        }

        // ── 툴팁 ───────────────────────────────────────────────────────────────

        private void ShowTooltip(ItemData item, bool canEquip, bool canUnequip)
        {
            if (tooltipPanel == null) return;
            tooltipPanel.SetActive(true);

            if (tooltipName  != null) tooltipName.text  = item.itemName;
            if (tooltipDesc  != null) tooltipDesc.text  = item.description;
            if (tooltipStats != null)
            {
                tooltipStats.text =
                    (item.bonusAtk       != 0    ? $"ATK +{item.bonusAtk}\n"               : "") +
                    (item.bonusDef       != 0    ? $"DEF +{item.bonusDef}\n"               : "") +
                    (item.bonusHp        != 0    ? $"HP +{item.bonusHp}\n"                 : "") +
                    (item.bonusCritChance != 0f  ? $"CRIT +{item.bonusCritChance*100:F0}%\n" : "") +
                    (item.bonusSpeed     != 0f   ? $"SPD +{item.bonusSpeed}\n"             : "");
            }

            if (equipButton  != null) equipButton.gameObject.SetActive(canEquip);
            if (unequipButton != null) unequipButton.gameObject.SetActive(canUnequip);
        }

        private void HideTooltip()
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
            _selectedItem = null;
        }
    }
}
