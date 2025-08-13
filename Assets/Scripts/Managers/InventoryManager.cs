using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class InventoryManager: MonoBehaviour
    {
        public int rerollTicketCount;
        public Dictionary<int, int> TokensInHand;
        public Dictionary<int, int> UnitsInHand;

        public void Initialize()
        {
            rerollTicketCount = 0;
            var tokensData = GameManager.Instance.resourceTokenDataList;
            TokensInHand = new Dictionary<int, int>();
            foreach (var token in tokensData.tokens)
            {
                TokensInHand[token.id] = 0;
            }
            var unitsData = GameManager.Instance.unitDataList;
            UnitsInHand = new Dictionary<int, int>();
            foreach (var unit in unitsData.units)
            {
                UnitsInHand[unit.id] = 0;
            }
        }
    }
}