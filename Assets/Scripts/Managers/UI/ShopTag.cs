using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers;
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
        private int _slotIndex = -1; // 슬롯 인덱스 추가

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

        public void Initialize(ShopItem shopItem, int slotIndex = -1)
        {
            _shopItem = shopItem;
            _slotIndex = slotIndex;
            _isPurchased = false; // 초기화 시 구매 상태 리셋
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
            
            // 반복적으로 비용 재설정
            bool needsReroll = true;
            int maxAttempts = 10; // 무한 루프 방지
            int attempts = 0;
            
            while (needsReroll && attempts < maxAttempts)
            {
                attempts++;
                
                // 1. 비용 설정 (첫 번째 반복이 아닌 경우 새로 생성)
                if (attempts > 1)
                {
                    // 원본 비용 구조를 유지하면서 새로운 값으로 재설정
                    var originalCosts = shopItem.Cost.Where(kvp => kvp.Value > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    int totalOriginal = originalCosts.Values.Sum();
                    
                    costs = new Dictionary<int, int>();
                    foreach (var kvp in originalCosts)
                    {
                        // 각 토큰에 대해 랜덤한 비율로 재분배 (최소 1은 보장)
                        costs[kvp.Key] = Mathf.Max(1, UnityEngine.Random.Range(1, totalOriginal));
                    }
                    
                    // 총합을 원래 총합과 비슷하게 조정
                    int currentTotal = costs.Values.Sum();
                    float scaleFactor = (float)totalOriginal / currentTotal;
                    var scaledCosts = new Dictionary<int, int>();
                    foreach (var kvp in costs)
                    {
                        scaledCosts[kvp.Key] = Mathf.Max(1, Mathf.RoundToInt(kvp.Value * scaleFactor));
                    }
                    costs = scaledCosts;
                }
                
                // 2. 60% 이상 차지하는 토큰이 있는지 확인
                int totalCost = costs.Values.Sum();
                bool hasExcessiveToken = false;
                
                if (totalCost > 0)
                {
                    foreach (var kvp in costs)
                    {
                        float percentage = (float)kvp.Value / totalCost;
                        if (percentage >= 0.6f)
                        {
                            hasExcessiveToken = true;
                            break;
                        }
                    }
                }
                
                // 3. 60% 이상 차지하는 토큰이 있는 경우
                if (hasExcessiveToken)
                {
                    // 3-1. 10% 확률로 비용설정 종료
                    if (UnityEngine.Random.Range(0f, 1f) <= 0.1f)
                    {
                        needsReroll = false;
                    }
                    // 3-2. 90% 확률로 다시 1로 반복
                    else
                    {
                        needsReroll = true;
                    }
                }
                else
                {
                    // 60% 이상 차지하는 토큰이 없으면 종료
                    needsReroll = false;
                }
            }
            
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

            // GridManager 인스턴스 가져오기
            GridManager gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                Debug.LogError("GridManager를 찾을 수 없습니다.");
                return;
            }

            // 벤치에 빈 셀이 있는지 확인
            if (!gridManager.HasAvailableBenchSlot())
            {
                Debug.Log("벤치가 가득 차서 유닛을 구매할 수 없습니다.");
                return;
            }

            // InventoryManager 인스턴스 가져오기
            InventoryManager inventoryManager = FindFirstObjectByType<InventoryManager>();
            if (inventoryManager == null)
            {
                Debug.LogError("InventoryManager를 찾을 수 없습니다.");
                return;
            }

            // ShopManager 인스턴스 가져오기
            ShopManager shopManager = FindFirstObjectByType<ShopManager>();
            if (shopManager == null)
            {
                Debug.LogError("ShopManager를 찾을 수 없습니다.");
                return;
            }

            // 비용 확인 및 구매 처리
            var costs = _shopItem.Cost.Where(kvp => kvp.Value > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // 토큰이 충분한지 확인하고 구매 처리
            if (inventoryManager.SpendToken(costs))
            {
                // GridManager의 SpawnUnit을 사용하여 벤치에 유닛 생성
                gridManager.SpawnUnit(0, 0, false, _shopItem.ID, true); // 벤치에 스폰
                
                Debug.Log($"{_shopItem.ItemName} 구매 완료! 벤치에 배치되었습니다.");
                
                // 구매한 슬롯을 새로운 유닛으로 교체
                ReplaceWithNewUnit(shopManager);
            }
            else
            {
                Debug.Log("토큰이 부족하여 구매할 수 없습니다.");
            }
        }

        private void ReplaceWithNewUnit(ShopManager shopManager)
        {
            // 슬롯 인덱스가 유효하지 않은 경우 자동으로 찾기
            if (_slotIndex == -1)
            {
                ShopTag[] shopTags = shopManager.shopTagParent.GetComponentsInChildren<ShopTag>();
                for (int i = 0; i < shopTags.Length; i++)
                {
                    if (shopTags[i] == this)
                    {
                        _slotIndex = i;
                        break;
                    }
                }
            }

            if (_slotIndex == -1)
            {
                Debug.LogError("슬롯 인덱스를 찾을 수 없습니다.");
                return;
            }

            // 해당 슬롯을 새로운 유닛으로 교체
            int currentTier = shopManager.currentTier;
            shopManager.RerollSingleSlot(currentTier, _slotIndex);
            shopManager.RefreshSingleShopTag(currentTier, _slotIndex);
            
            Debug.Log($"슬롯 {_slotIndex}이(가) 새로운 유닛으로 교체되었습니다.");
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
