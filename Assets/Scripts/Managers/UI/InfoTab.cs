using UnityEngine;
using UnityEngine.EventSystems;

namespace Managers.UI
{
    public class InfoTab : MonoBehaviour, IPointerClickHandler
    {
        [Header("Unit Information")]
        public TMPro.TextMeshProUGUI unitNameText; // 유닛명
        public TMPro.TextMeshProUGUI unitLevelText; // 유닛레벨
        public TMPro.TextMeshProUGUI maxHpText; // 최대 체력
        public TMPro.TextMeshProUGUI currentHpText; // 현재 체력
        public TMPro.TextMeshProUGUI atkText; // 공격력
        public TMPro.TextMeshProUGUI defText; // 방어력
        public TMPro.TextMeshProUGUI critPosText; // 크리티컬 확률
        public TMPro.TextMeshProUGUI critDmgText; // 크리티컬 데미지
        public TMPro.TextMeshProUGUI coolDownText; // 쿨다운
        public TMPro.TextMeshProUGUI penetrationText; // 관통력
        
        [Header("Upgrade Information")]
        public TMPro.TextMeshProUGUI upgradePosText; // 업그레이드 확률
        public TMPro.TextMeshProUGUI upgradeFailText; // 업그레이드 실패
        
        void Start()
        {
            // 시작할 때 InfoTab 비활성화
            gameObject.SetActive(false);
        }
        
        void Update()
        {
            // InfoTab이 활성화된 상태에서 외부 클릭 감지
            if (gameObject.activeInHierarchy && Input.GetMouseButtonDown(0))
            {
                // 마우스 위치가 InfoTab 영역 밖인지 확인
                Vector2 mousePosition = Input.mousePosition;
                RectTransform rectTransform = GetComponent<RectTransform>();
                
                if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePosition, Camera.main))
                {
                    // InfoTab 외부 클릭 시 숨기기
                    if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
                    {
                        GameManager.Instance.uiManager.HideInfoTab();
                    }
                }
            }
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            // InfoTab 내부 클릭 시 아무것도 하지 않음 (이벤트 소비)
        }
    }
}
