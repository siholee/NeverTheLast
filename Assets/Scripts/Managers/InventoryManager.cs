using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class InventoryManager: MonoBehaviour
    {
        public int currentGold;
        public Dictionary<int, int> ElementsInHand;
        public Dictionary<int, int> UnitsInHand;

        private void Awake()
        {
            currentGold = 0;
            var elementsData = GameManager.Instance.elementDataList;
            foreach (var element in elementsData.elementsByCost.SelectMany(eList => eList.Value))
            {
                ElementsInHand[element.id] = 0;
            }
            var unitsData = GameManager.Instance.unitDataList;
            foreach (var unit in unitsData.units)
            {
                UnitsInHand[unit.id] = 0;
            }
        }
    }
}