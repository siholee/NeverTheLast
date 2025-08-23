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
        public Button purchaseButton; // 구매 버튼
        public GameObject soldOutOverlay; // 구매 완료 표시용 오버레이

        private ShopItem _shopItem;
        private bool _isPurchased;

        private void Start()
        {
            // 구매 버튼 이벤트 등록
            if (purchaseButton != null)
            {
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
            }
            else
            {
                // purchaseButton이 없으면 이 GameObject 자체를 버튼으로 사용
                Button button = GetComponent<Button>();
                if (button == null)
                {
                    button = gameObject.AddComponent<Button>();
                }
                button.onClick.AddListener(OnPurchaseClicked);
            }
        }

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

        private void OnPurchaseClicked()
        {
            // 이미 구매된 아이템인지 확인
            if (_isPurchased)
            {
                Debug.Log("이미 구매된 아이템입니다.");
                return;
            }

            // _shopItem이 null인지 확인
            if (_shopItem == null)
            {
                Debug.LogError("ShopItem이 초기화되지 않았습니다.");
                return;
            }

            // InventoryManager 인스턴스 가져오기
            InventoryManager inventoryManager = FindFirstObjectByType<InventoryManager>();
            if (inventoryManager == null)
            {
                Debug.LogError("InventoryManager를 찾을 수 없습니다.");
                return;
            }

            // 비용 확인 및 구매 처리
            var costs = _shopItem.Cost.Where(kvp => kvp.Value > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // 토큰이 충분한지 확인하고 구매 처리
            if (inventoryManager.SpendToken(costs))
            {
                // 구매 성공 - UnitsInHand에 추가
                if (!inventoryManager.UnitsInHand.ContainsKey(_shopItem.ID))
                {
                    inventoryManager.UnitsInHand[_shopItem.ID] = 0;
                }
                inventoryManager.UnitsInHand[_shopItem.ID] += 1;
                
                // 구매 완료 상태로 변경
                SetPurchasedState();
                
                Debug.Log($"{_shopItem.ItemName} 구매 완료! 현재 보유량: {inventoryManager.UnitsInHand[_shopItem.ID]}");
            }
            else
            {
                Debug.Log("토큰이 부족하여 구매할 수 없습니다.");
            }
        }

        private void SetPurchasedState()
        {
            _isPurchased = true;
            
            // 구매 버튼 비활성화
            if (purchaseButton != null)
            {
                purchaseButton.interactable = false;
            }
            else
            {
                Button button = GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = false;
                }
            }
            
            // 구매 완료 오버레이 활성화
            if (soldOutOverlay != null)
            {
                soldOutOverlay.SetActive(true);
            }
            
            // 시각적 피드백 (선택사항)
            Color grayColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            if (itemTorsoImage != null)
            {
                itemTorsoImage.color = grayColor;
            }
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위한 이벤트 해제
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(OnPurchaseClicked);
            }
            else
            {
                Button button = GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveListener(OnPurchaseClicked);
                }
            }
        }
    }
}
