using System;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Managers
{
    public class ShopManager: MonoBehaviour
    {
        public ShopItem[] ShopItems;
        public int currentLevel;
        public int maxLevel;
        public int rerollCost;
        
        public Button rerollButton;
        public Button maxLevelUpButton;
        public Button levelUpButton;
        public Button levelDownButton;

        private void Awake()
        {
            currentLevel = 1;
            maxLevel = 1;
            rerollCost = 0;
        }

        private void SetLevelButtonAvailability()
        {
            levelUpButton.interactable = maxLevel > 0;
            levelDownButton.interactable = currentLevel > 1;
            maxLevelUpButton.interactable = currentLevel < maxLevel;
        }
        
        public void OnLevelUpButtonClicked()
        {
            if (currentLevel >= maxLevel) return;
            currentLevel++;
            SetLevelButtonAvailability();
        }
        
        public void OnLevelDownButtonClicked()
        {
            if (currentLevel <= 1) return;
            currentLevel--;
            SetLevelButtonAvailability();
        }

        public void OnRerollButtonClicked()
        {
            if (rerollCost <= 0) return;
        }

        private void RerollShopItems(Dictionary<int, List<ElementData>> elementsByCost)
        {
            var levelCosts = new[] { currentLevel, currentLevel + 1, currentLevel + 2 };
            var levelChances = new[] { 0.5f, 0.3f, 0.2f };

            var elementsByLevel = new List<ElementData>[3];
            for (var i = 0; i < 3; i++)
            {
                if (!elementsByCost.TryGetValue(levelCosts[i], out var list) || list.Count == 0)
                    elementsByLevel[i] = new List<ElementData>();
                else
                    elementsByLevel[i] = new List<ElementData>(list);
            }

            // 레벨별 원소가 없으면 한 단계 아래로 대체
            for (int i = 2; i >= 0; i--)
            {
                if (elementsByLevel[i].Count == 0 && i > 0)
                    elementsByLevel[i] = new List<ElementData>(elementsByLevel[i - 1]);
            }

            var rand = new System.Random();
            foreach (var t in ShopItems)
            {
                // 확률에 따라 레벨 선택
                float r = (float)rand.NextDouble();
                var levelIdx = 0;
                if (r < levelChances[0]) levelIdx = 0;
                else if (r < levelChances[0] + levelChances[1]) levelIdx = 1;
                else levelIdx = 2;

                var candidates = elementsByLevel[levelIdx];
                if (candidates.Count == 0)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                var selected = candidates[rand.Next(candidates.Count)];
                t.Initialize(selected);
            }
        }
    }
}