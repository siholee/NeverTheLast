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
        [SerializeField] public IntIntDictionary TokensInHand;
        [SerializeField] public List<Unit> UnitsInHand;

        public ResourcePanel resourcePanel;

        public void Initialize()
        {
            rerollTicketCount = 0;
            var tokensData = GameManager.Instance.resourceTokenDataList;
            if (TokensInHand == null) TokensInHand = new IntIntDictionary();
            TokensInHand.Clear();
            foreach (var token in tokensData.tokens)
            {
                TokensInHand[token.id] = 50;
            }
            
            if (UnitsInHand == null) UnitsInHand = new List<Unit>();
            UnitsInHand.Clear();
            
            resourcePanel.UpdatePanel(TokensInHand, rerollTicketCount);
        }
    
        public void AddToken(int tokenId, int amount)
        {
            if (!TokensInHand.TryAdd(tokenId, amount))
            {
                TokensInHand[tokenId] += amount;
            }

            resourcePanel.UpdatePanel(TokensInHand, rerollTicketCount);
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
            resourcePanel.UpdatePanel(TokensInHand, rerollTicketCount);
            return true;
        }

        public void AddUnit(Unit unit)
        {
            if (unit != null)
            {
                UnitsInHand.Add(unit);
            }
        }

        public bool RemoveUnit(Unit unit)
        {
            return UnitsInHand.Remove(unit);
        }

        public Unit RemoveUnitById(int unitId)
        {
            var unit = UnitsInHand.FirstOrDefault(u => u.ID == unitId);
            if (unit != null)
            {
                UnitsInHand.Remove(unit);
            }
            return unit;
        }

        public List<Unit> GetUnitsById(int unitId)
        {
            return UnitsInHand.Where(u => u.ID == unitId).ToList();
        }

        public int GetUnitCountById(int unitId)
        {
            return UnitsInHand.Count(u => u.ID == unitId);
        }

        public bool HasUnit(int unitId)
        {
            return UnitsInHand.Any(u => u.ID == unitId);
        }
    }
}
