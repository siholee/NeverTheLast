using System.Linq;
using BaseClasses;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    public class ShopTag : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI itemNameText; // 유닛명
        public TMPro.TextMeshProUGUI itemSynergyText1; // 소속 시너지 (있다면)
        public TMPro.TextMeshProUGUI itemSynergyText2; // 직업 시너지 (있다면)  
        public Image itemTorsoImage;
        public TMPro.TextMeshProUGUI itemCostCountText1;
        public TMPro.TextMeshProUGUI itemCostCountText2;
        public TMPro.TextMeshProUGUI itemCostCountText3;
        public Image itemCostIcon1;
        public Image itemCostIcon2;
        public Image itemCostIcon3;

        private ShopItem _shopItem;

        public void Initialize(ShopItem shopItem)
        {
            _shopItem = shopItem;
            itemNameText.text = shopItem.ItemName;
            string path = $"Sprite/Portraits/{shopItem.IconPath}";
            Sprite sprite = Resources.Load<Sprite>(path);
            itemTorsoImage.sprite = sprite;

            // 시너지 텍스트 설정
            if (shopItem.SynergyIds.Count > 0)
            {
                itemSynergyText1.text = GameManager.Instance.synergyDataList.synergies.First(
                    syn => syn.id == shopItem.SynergyIds[0]).name;
                itemSynergyText1.gameObject.SetActive(true);
            }
            else
            {
                itemSynergyText1.gameObject.SetActive(false);
            }

            if (shopItem.SynergyIds.Count > 1)
            {
                itemSynergyText2.text = GameManager.Instance.synergyDataList.synergies.First(
                    syn => syn.id == shopItem.SynergyIds[1]).name;
                itemSynergyText2.gameObject.SetActive(true);
            }
            else
            {
                itemSynergyText2.gameObject.SetActive(false);
            }

            // 비용 설정
            var costs = shopItem.Cost.Where(kvp => kvp.Value > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var costKeys = costs.Keys.ToArray();
            if (costs.Count > 0)
            {
                itemCostCountText1.text = costs[costKeys[0]].ToString();
                // itemCostIcon1.sprite = GameManager.Instance.resourceTokenDataList.GetTokenIcon(costs[0]);
                itemCostIcon1.gameObject.SetActive(true);
                itemCostCountText1.gameObject.SetActive(true);
            }
            else
            {
                itemCostIcon1.gameObject.SetActive(false);
                itemCostCountText1.gameObject.SetActive(false);
            }

            if (costs.Count > 1)
            {
                itemCostCountText2.text = costs[costKeys[1]].ToString();
                // itemCostIcon2.sprite = GameManager.Instance.resourceTokenDataList.GetTokenIcon(costs[1]);
                itemCostIcon2.gameObject.SetActive(true);
                itemCostCountText2.gameObject.SetActive(true);
            }
            else
            {
                itemCostIcon2.gameObject.SetActive(false);
                itemCostCountText2.gameObject.SetActive(false);
            }

            if (costs.Count > 2)
            {
                itemCostCountText3.text = costs[costKeys[2]].ToString();
                // itemCostIcon3.sprite = GameManager.Instance.resourceTokenDataList.GetTokenIcon(costs[2]);
                itemCostIcon3.gameObject.SetActive(true);
                itemCostCountText3.gameObject.SetActive(true);
            }
            else
            {
                itemCostIcon3.gameObject.SetActive(false);
                itemCostCountText3.gameObject.SetActive(false);
            }
        }
    }
}

