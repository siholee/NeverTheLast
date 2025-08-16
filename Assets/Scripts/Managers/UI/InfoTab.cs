using UnityEngine;

namespace Managers.UI
{
    public class InfoTab : MonoBehaviour
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
            // ESC 키를 누를 경우 InfoTab 닫기
            if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInfoTab();
                return;
            }
        }
        
        // 버튼 클릭 시 호출될 함수 (버튼의 OnClick 이벤트에 연결)
        public void OnCloseButtonClick()
        {
            CloseInfoTab();
        }
        
        // InfoTab 닫기 기능을 담당하는 메서드
        private void CloseInfoTab()
        {
            gameObject.SetActive(false);
        }
    }
}
