using BaseClasses;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    public class ShopTag: MonoBehaviour
    {
        public TMPro.TextMeshProUGUI itemNameText; // 원소
        public TMPro.TextMeshProUGUI itemPriceText; // 가격
        public TMPro.TextMeshProUGUI itemSynergyText1; // 소속 시너지 (있다면)
        public TMPro.TextMeshProUGUI itemSynergyText2; // 직업 시너지 (있다면)  
        public Image itemTorsoImage;

        public int itemCost; // 아이템 비용
        public int elementId; // 소환할 유닛 ID
    }

}