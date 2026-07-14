using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers.UI;
using UnityEngine;

namespace Managers
{
    public class InventoryManager: MonoBehaviour
    {
        public int rerollTicketCount;
        public int Gold { get; private set; }
        [SerializeField] public List<int> ItemIdsInHand;
        [SerializeField] public IntIntDictionary TokensInHand;

        public ResourcePanel resourcePanel;

        public void Initialize()
        {
            rerollTicketCount = 0;
            Gold = 500;
            var tokensData = GameManager.Instance.resourceTokenDataList;
            if (TokensInHand == null) TokensInHand = new IntIntDictionary();
            TokensInHand.Clear();
            foreach (var token in tokensData.tokens)
            {
                TokensInHand[token.id] = 999;
            }

            rerollTicketCount = 50;

            if (ItemIdsInHand == null) ItemIdsInHand = new List<int>();
            ItemIdsInHand.Clear();
            
            RefreshPanel();
        }
    
        public void AddToken(int tokenId, int amount)
        {
            if (!TokensInHand.TryAdd(tokenId, amount))
            {
                TokensInHand[tokenId] += amount;
            }

            RefreshPanel();
        }

        public bool SpendToken(Dictionary<int, int> tokensToSpend)
        {
            if (tokensToSpend.Any(token => !TokensInHand.ContainsKey(token.Key) || TokensInHand[token.Key] < token.Value))
            {
                return false; // 토큰이 없거나 부족한 경우
            }
            foreach (var token in tokensToSpend)
            {
                TokensInHand[token.Key] -= token.Value;
            }
            RefreshPanel();
            return true;
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            RefreshPanel();
        }

        public bool TrySpendGold(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (Gold < amount) return false;
            Gold -= amount;
            RefreshPanel();
            return true;
        }

        public void RestoreGold(int amount)
        {
            Gold = Mathf.Max(0, amount);
            RefreshPanel();
        }

        public void AddItem(int itemId)
        {
            if (itemId <= 0) return;
            ItemIdsInHand ??= new List<int>();
            ItemIdsInHand.Add(itemId);
            RefreshPanel();
        }

        public bool TryEquipStoredItem(Unit unit, int itemId, out string reason)
        {
            reason = null;
            if (unit == null || ItemIdsInHand == null || !ItemIdsInHand.Contains(itemId))
            {
                reason = "보관 중인 아이템이 아닙니다.";
                return false;
            }
            ItemDataList itemDataList = GameManager.Instance?.itemDataList ?? GameManager.Instance?.dataManager?.FetchItemDataList();
            ItemData selected = itemDataList?.items?.FirstOrDefault(item => item.id == itemId);
            var displaced = new List<int>();
            if (selected != null && EquipmentLoadout.TryParseSlot(selected.slot, out EquipmentSlot selectedSlot))
            {
                foreach (int equippedId in unit.EquippedItemIds)
                {
                    ItemData equipped = itemDataList.items.FirstOrDefault(item => item.id == equippedId);
                    if (equipped == null || !EquipmentLoadout.TryParseSlot(equipped.slot, out EquipmentSlot equippedSlot)) continue;
                    if (equippedSlot == selectedSlot ||
                        (selectedSlot == EquipmentSlot.MainHand && selected.twoHanded && equippedSlot == EquipmentSlot.OffHand))
                    {
                        displaced.Add(equippedId);
                    }
                }
            }
            if (!unit.TryEquipItem(itemId, out reason)) return false;
            ItemIdsInHand.Remove(itemId);
            ItemIdsInHand.AddRange(displaced);
            RefreshPanel();
            return true;
        }

        public void RefreshPanel()
        {
            GameManager.Instance?.uiManager?.UpdateGoldText();
            if (resourcePanel != null)
            {
                resourcePanel.UpdatePanel(TokensInHand, rerollTicketCount, Gold);
            }
        }
    }
}
