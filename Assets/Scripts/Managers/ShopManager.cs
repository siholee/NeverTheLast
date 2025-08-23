using System;
using System.Collections.Generic;
using BaseClasses;
using JetBrains.Annotations;
using Managers.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Managers
{
    public class ShopManager: MonoBehaviour
    {
        public ShopItem[] ShopItemsTier1;
        public ShopItem[] ShopItemsTier2;
        public ShopItem[] ShopItemsTier3;
        public int currentTier;
        public int maxTier;
        
        public Button rerollButton;
        public Button levelUpButton;
        public Button levelDownButton;

        public Transform shopTagParent;

        private void Awake()
        {
            currentTier = 1;
            maxTier = 3;
            ShopItemsTier1 = new ShopItem[5];
            ShopItemsTier2 = new ShopItem[5];
            ShopItemsTier3 = new ShopItem[5];
        }

        private void SetLevelButtonAvailability()
        {
            levelUpButton.interactable = maxTier > currentTier;
            levelDownButton.interactable = currentTier > 1;
            rerollButton.interactable = GameManager.Instance.inventoryManager.rerollTicketCount > 0;
        }
        
        public void OnLevelUpButtonClicked()
        {
            if (currentTier >= maxTier) return;
            currentTier++;
            ShowShopItems(currentTier);
            SetLevelButtonAvailability();
        }
        
        public void OnLevelDownButtonClicked()
        {
            if (currentTier <= 1) return;
            currentTier--;
            ShowShopItems(currentTier);
            SetLevelButtonAvailability();
        }

        public void OnRerollButtonClicked()
        {
            if (GameManager.Instance.inventoryManager.rerollTicketCount <= 0)
            {
                Debug.LogWarning("Reroll 티켓이 부족합니다.");
                return;
            }
            GameManager.Instance.inventoryManager.rerollTicketCount--;
            RerollShopItems(currentTier);
            ShowShopItems(currentTier);
            SetLevelButtonAvailability();
        }

        public void ShowShopItems(int tier)
        {
            ShopItem[] targetShopArray = tier switch
            {
                1 => ShopItemsTier1,
                2 => ShopItemsTier2,
                3 => ShopItemsTier3,
                _ => null
            };
            ShopTag[] targetShopTagArray = shopTagParent.GetComponentsInChildren<ShopTag>();
            for (var i = 0; i < 5; i++)
            {
                targetShopTagArray[i].Initialize(targetShopArray[i], i);
            }
        }

        public void RerollShopItems(int tier)
        {
            // GameManager에서 unitDataList 가져오기
            var unitDataList = GameManager.Instance.unitDataList;
            if (unitDataList?.units == null)
            {
                Debug.LogError("UnitDataList가 없습니다.");
                return;
            }

            // 지정된 tier의 유닛들만 필터링
            var tierUnits = new List<UnitData>();
            foreach (var unit in unitDataList.units)
            {
                if (unit.tier == tier)
                {
                    tierUnits.Add(unit);
                }
            }

            if (tierUnits.Count == 0)
            {
                Debug.LogWarning($"Tier {tier}에 해당하는 유닛이 없습니다.");
                return;
            }

            // 해당 tier의 상점 배열 선택
            ShopItem[] targetShopArray = tier switch
            {
                1 => ShopItemsTier1,
                2 => ShopItemsTier2,
                3 => ShopItemsTier3,
                _ => null
            };

            if (targetShopArray == null)
            {
                Debug.LogError($"Tier {tier}에 해당하는 상점 배열이 없습니다.");
                return;
            }

            // 5개의 무작위 유닛 선택하여 ShopItem 생성
            for (var i = 0; i < 5; i++)
            {
                // 무작위 유닛 선택
                var randomIndex = UnityEngine.Random.Range(0, tierUnits.Count);
                UnitData selectedUnit = tierUnits[randomIndex];
                targetShopArray[i] = new ShopItem();
                targetShopArray[i].Initialize(selectedUnit);
            }
        }

        public void RerollSingleSlot(int tier, int slotIndex)
        {
            // GameManager에서 unitDataList 가져오기
            var unitDataList = GameManager.Instance.unitDataList;
            if (unitDataList?.units == null)
            {
                Debug.LogError("UnitDataList가 없습니다.");
                return;
            }

            // 지정된 tier의 유닛들만 필터링
            var tierUnits = new List<UnitData>();
            foreach (var unit in unitDataList.units)
            {
                if (unit.tier == tier)
                {
                    tierUnits.Add(unit);
                }
            }

            if (tierUnits.Count == 0)
            {
                Debug.LogWarning($"Tier {tier}에 해당하는 유닛이 없습니다.");
                return;
            }

            // 해당 tier의 상점 배열 선택
            ShopItem[] targetShopArray = tier switch
            {
                1 => ShopItemsTier1,
                2 => ShopItemsTier2,
                3 => ShopItemsTier3,
                _ => null
            };

            if (targetShopArray == null || slotIndex < 0 || slotIndex >= targetShopArray.Length)
            {
                Debug.LogError($"유효하지 않은 슬롯 인덱스: {slotIndex}");
                return;
            }

            // 해당 슬롯에 새로운 무작위 유닛 배치
            var randomIndex = UnityEngine.Random.Range(0, tierUnits.Count);
            UnitData selectedUnit = tierUnits[randomIndex];
            targetShopArray[slotIndex] = new ShopItem();
            targetShopArray[slotIndex].Initialize(selectedUnit);
        }

        public void RefreshSingleShopTag(int tier, int slotIndex)
        {
            ShopItem[] targetShopArray = tier switch
            {
                1 => ShopItemsTier1,
                2 => ShopItemsTier2,
                3 => ShopItemsTier3,
                _ => null
            };

            if (targetShopArray == null || slotIndex < 0 || slotIndex >= targetShopArray.Length)
            {
                Debug.LogError($"유효하지 않은 슬롯 인덱스: {slotIndex}");
                return;
            }

            ShopTag[] shopTags = shopTagParent.GetComponentsInChildren<ShopTag>();
            if (slotIndex < shopTags.Length)
            {
                shopTags[slotIndex].Initialize(targetShopArray[slotIndex]);
            }
        }
    }
}
