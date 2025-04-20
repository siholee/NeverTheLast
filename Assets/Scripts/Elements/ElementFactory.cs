using System.Collections.Generic;
using Managers;
using System.Linq;
using UnityEngine;

namespace Elements
{
    public class ElementFactory
    {
        public List<ElementData> ReloadElementShop(int length, int shopLevel, ElementDataList dataList)
        {
            // 확률 설정
            var probabilities = new Dictionary<int, float>
            {
                { 0, 0.50f }, // shopLevel과 같은 cost
                { 1, 0.25f }, // shopLevel보다 1 높은 cost
                { 2, 0.15f }, // shopLevel보다 2 높은 cost
                { 3, 0.10f }, // shopLevel보다 3 높은 cost
                { 4, 0.05f }  // shopLevel보다 4 높은 cost
            };

            // 결과 리스트
            var result = new List<ElementData>();

            // 확률 기반으로 원소 뽑기
            for (int i = 0; i < length; i++)
            {
                float randomValue = Random.value; // 0.0 ~ 1.0 사이의 랜덤 값
                float cumulativeProbability = 0f;

                // 확률에 따라 cost 결정
                int selectedCost = -1;
                foreach (var kvp in probabilities)
                {
                    cumulativeProbability += kvp.Value;
                    if (!(randomValue <= cumulativeProbability)) continue;
                    selectedCost = shopLevel + kvp.Key;
                    break;
                }

                // 해당 cost의 원소 리스트 가져오기
                if (dataList.elementsByCost.TryGetValue(selectedCost, out var elements) && elements.Count > 0)
                {
                    // 랜덤으로 하나 선택
                    var selectedElement = elements[Random.Range(0, elements.Count)];
                    result.Add(selectedElement);
                }
                else
                {
                    Debug.LogWarning($"Cost {selectedCost}에 해당하는 원소가 없습니다.");
                }
            }

            return result;
        }
    }
}