using UnityEngine;
using System.Text;
using StatusEffects.Base;
using Managers;

namespace Managers.UI
{
    public class InfoTab : MonoBehaviour
    {
        [Header("Unit Information")]
        public TMPro.TextMeshProUGUI unitNameText; // 유닛명
        public TMPro.TextMeshProUGUI unitLevelText; // 유닛레벨
        public TMPro.TextMeshProUGUI maxHpText; // 최대 체력
        public TMPro.TextMeshProUGUI currentHpText; // 현재 체력
        public TMPro.TextMeshProUGUI hpText; // 현재 체력
        public TMPro.TextMeshProUGUI atkText; // 공격력
        public TMPro.TextMeshProUGUI defText; // 방어력
        public TMPro.TextMeshProUGUI critPosText; // 크리티컬 확률
        public TMPro.TextMeshProUGUI critDmgText; // 크리티컬 데미지
        public TMPro.TextMeshProUGUI coolDownText; // 쿨다운
        
        [Header("Status Effects")]
        public TMPro.TextMeshProUGUI statusEffectsText; // 상태 효과 목록
        
        [Header("Synergy Information")]
        public GameObject synergyTag1; // 첫 번째 시너지 태그 (Text + Icon)
        public GameObject synergyTag2; // 두 번째 시너지 태그 (Text + Icon)
        public TMPro.TextMeshProUGUI synergyText1; // 첫 번째 시너지 텍스트
        public TMPro.TextMeshProUGUI synergyText2; // 두 번째 시너지 텍스트
        
        [Header("Tab System")]
        public GameObject tab1Content; // 탭 1 컨텐츠 (기본 정보)
        public GameObject tab2Content; // 탭 2 컨텐츠
        public GameObject tab3Content; // 탭 3 컨텐츠
        
        [Header("Upgrade Information")]
        public TMPro.TextMeshProUGUI upgradePosText; // 업그레이드 확률
        public TMPro.TextMeshProUGUI upgradeFailText; // 업그레이드 실패
        
        // 현재 표시 중인 유닛과 탭 상태
        private Entities.Unit currentDisplayedUnit;
        private int currentTab = 1;
        private bool isInfoTabActive = false;
        
        void Start()
        {
            // 시작할 때 InfoTab 비활성화
            gameObject.SetActive(false);
            InitializeTabs();
        }
        
        void Update()
        {
            // ESC 키를 누를 경우 InfoTab 닫기
            if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInfoTab();
                return;
            }
            
            // InfoTab이 활성화되어 있고 표시할 유닛이 있다면 지속적으로 업데이트
            if (isInfoTabActive && currentDisplayedUnit != null && currentDisplayedUnit.isActive)
            {
                UpdateCurrentUnitInfo();
            }
        }
        
        // 탭 시스템 초기화
        private void InitializeTabs()
        {
            // 기본적으로 탭 1만 활성화
            SetActiveTab(1);
        }
        
        // 탭 버튼 클릭 핸들러
        public void OnTabButtonClick(int tabNumber)
        {
            if (tabNumber >= 1 && tabNumber <= 3)
            {
                SetActiveTab(tabNumber);
            }
        }
        
        // 특정 탭을 활성화하고 나머지는 비활성화
        private void SetActiveTab(int tabNumber)
        {
            currentTab = tabNumber;
            
            // 모든 탭 컨텐츠 비활성화
            if (tab1Content != null) tab1Content.SetActive(false);
            if (tab2Content != null) tab2Content.SetActive(false);
            if (tab3Content != null) tab3Content.SetActive(false);
            
            // 선택된 탭만 활성화
            switch (tabNumber)
            {
                case 1:
                    if (tab1Content != null) tab1Content.SetActive(true);
                    break;
                case 2:
                    if (tab2Content != null) tab2Content.SetActive(true);
                    break;
                case 3:
                    if (tab3Content != null) tab3Content.SetActive(true);
                    break;
            }
        }
        
        // InfoTab 열기 (외부에서 호출)
        public void ShowInfoTab(Entities.Unit unit)
        {
            if (unit == null) return;
            
            currentDisplayedUnit = unit;
            isInfoTabActive = true;
            
            // InfoTab 활성화
            gameObject.SetActive(true);
            
            // 기본적으로 탭 1 활성화
            SetActiveTab(1);
            
            // 유닛 정보 업데이트
            UpdateCurrentUnitInfo();
        }
        
        // 버튼 클릭 시 호출될 함수 (버튼의 OnClick 이벤트에 연결)
        public void OnCloseButtonClick()
        {
            CloseInfoTab();
        }
        
        // InfoTab 닫기 기능을 담당하는 메서드
        private void CloseInfoTab()
        {
            isInfoTabActive = false;
            currentDisplayedUnit = null;
            gameObject.SetActive(false);
        }
        
        // 현재 유닛 정보 업데이트 (지속적 업데이트용)
        private void UpdateCurrentUnitInfo()
        {
            if (currentDisplayedUnit == null) return;
            
            // 유닛 기본 정보 업데이트
            if (unitNameText != null) unitNameText.text = currentDisplayedUnit.UnitName;
            if (unitLevelText != null) unitLevelText.text = $"Lv.{currentDisplayedUnit.Level}";
            if (maxHpText != null) maxHpText.text = $"{currentDisplayedUnit.HpMax}";
            if (currentHpText != null) currentHpText.text = $"{currentDisplayedUnit.HpCurr}";
            if (hpText != null) hpText.text  = $"{currentDisplayedUnit.HpCurr}";
            if (atkText != null) atkText.text = $"{currentDisplayedUnit.AtkCurr}";
            if (defText != null) defText.text = $"{currentDisplayedUnit.DefCurr}";
            if (critPosText != null) critPosText.text = $"{(currentDisplayedUnit.CritChanceCurr * 100):F1}%";
            if (critDmgText != null) critDmgText.text = $"{(currentDisplayedUnit.CritMultiplierCurr * 100):F1}%";
            if (coolDownText != null) coolDownText.text = $"{currentDisplayedUnit.normalCooldown:F1}s";
            
            // 상태 효과 정보 업데이트
            UpdateStatusEffects(currentDisplayedUnit);
            
            // 시너지 정보 업데이트
            UpdateSynergyInfo(currentDisplayedUnit);
            
            // 업그레이드 정보 설정 (임시값)
            if (upgradePosText != null) upgradePosText.text = "Upgrade Rate: 50%";
            if (upgradeFailText != null) upgradeFailText.text = "Fail Rate: 50%";
        }
        
        // 상태 효과 정보를 업데이트하는 메서드
        public void UpdateStatusEffects(Entities.Unit unit)
        {
            if (statusEffectsText == null || unit == null)
            {
                if (statusEffectsText != null)
                    statusEffectsText.text = "상태 효과: 없음";
                return;
            }

            StringBuilder statusBuilder = new StringBuilder();
            statusBuilder.AppendLine("<b>상태 효과:</b>");

            bool hasAnyEffect = false;

            // StatusEffects (일반 상태 효과) 처리
            var statusEffects = unit.GetStatusEffects();
            if (statusEffects != null && statusEffects.Count > 0)
            {
                foreach (var effect in statusEffects)
                {
                    hasAnyEffect = true;
                    string effectName = GetEffectDisplayName(effect.Key);
                    string duration = effect.Value.Duration > 0 ? $"{effect.Value.Duration:F1}초" : "영구";
                    string stack = effect.Value.Stack > 1 ? $" (x{effect.Value.Stack})" : "";
                    string description = GetEffectDescription(effect.Value);
                    
                    statusBuilder.AppendLine($"• <color=#ffaa44>{effectName}</color>{stack}");
                    statusBuilder.AppendLine($"  남은시간: {duration}");
                    if (!string.IsNullOrEmpty(description))
                        statusBuilder.AppendLine($"  효과: {description}");
                    statusBuilder.AppendLine();
                }
            }

            // SynergyEffects (시너지 효과) 처리
            var synergyEffects = unit.GetSynergyEffects();
            if (synergyEffects != null && synergyEffects.Count > 0)
            {
                foreach (var synergy in synergyEffects)
                {
                    hasAnyEffect = true;
                    string synergyName = synergy.Value.SynergyName ?? $"시너지 {synergy.Key}";
                    string stack = synergy.Value.Stack > 1 ? $" (x{synergy.Value.Stack})" : "";
                    string description = synergy.Value.SynergyDescription ?? GetSynergyEffectDescription(synergy.Value);
                    
                    statusBuilder.AppendLine($"• <color=#44aaff>{synergyName}</color>{stack}");
                    statusBuilder.AppendLine($"  타입: 시너지");
                    if (!string.IsNullOrEmpty(description))
                        statusBuilder.AppendLine($"  효과: {description}");
                    statusBuilder.AppendLine();
                }
            }

            if (!hasAnyEffect)
            {
                statusBuilder.AppendLine("없음");
            }

            statusEffectsText.text = statusBuilder.ToString().TrimEnd();
        }

        // 상태 효과 이름을 표시용으로 변환
        private string GetEffectDisplayName(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "알 수 없는 효과";
            
            if (identifier.Contains("PoisonEffect")) 
            {
                // 맹독의 경우 개별 인스턴스를 구분할 수 있도록 표시
                return "맹독";
            }
            if (identifier.Contains("Burn")) return "화상";
            if (identifier.Contains("HolyEnchant")) return "신성한 인챈트";
            if (identifier.Contains("HealingModified")) return "치유 변화";
            if (identifier.Contains("Shield")) return "방어막";
            
            return identifier;
        }

        // 상태 효과 설명 생성
        private string GetEffectDescription(StatusEffect effect)
        {
            StringBuilder desc = new StringBuilder();

            // 공격력 수정자
            if (effect.AtkMultiplicativeModifier(null) != 0f)
                desc.Append($"공격력 {(effect.AtkMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.AtkAdditiveModifier(null) != 0)
                desc.Append($"공격력 {effect.AtkAdditiveModifier(null):+0;-0} ");
                
            // 방어력 수정자
            if (effect.DefMultiplicativeModifier(null) != 0f)
                desc.Append($"방어력 {(effect.DefMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.DefAdditiveModifier(null) != 0)
                desc.Append($"방어력 {effect.DefAdditiveModifier(null):+0;-0} ");
                
            // 체력 수정자
            if (effect.HpMultiplicativeModifier(null) != 0f)
                desc.Append($"체력 {(effect.HpMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.HpAdditiveModifier(null) != 0)
                desc.Append($"체력 {effect.HpAdditiveModifier(null):+0;-0} ");
                
            // 치명타 수정자
            if (effect.CritChanceAdditiveModifier(null) != 0f)
                desc.Append($"치명타율 {(effect.CritChanceAdditiveModifier(null) * 100):+0;-0}% ");
            if (effect.CritMultiplierAdditiveModifier(null) != 0f)
                desc.Append($"치명타 배율 {(effect.CritMultiplierAdditiveModifier(null) * 100):+0;-0}% ");
                
            // 코드 가속
            if (effect.CodeAccelerationMultiplicativeModifier(null) != 0f)
                desc.Append($"코드 가속 {(effect.CodeAccelerationMultiplicativeModifier(null) * 100):+0;-0}% ");
                
            // 피해 수정자
            if (effect.ReceivingDamageModifier(null) != 1f)
                desc.Append($"받는 피해 {((effect.ReceivingDamageModifier(null) - 1f) * 100):+0;-0}% ");
                
            // 치유 수정자
            if (effect.HealingReceivedModifier(null) != 1f)
                desc.Append($"치유량 {((effect.HealingReceivedModifier(null) - 1f) * 100):+0;-0}% ");

            // HP 변화 효과 (맹독, 화상 등)
            if (effect is IHpChangeEffect hpChangeEffect)
            {
                if (hpChangeEffect.HpFlatChange() != 0)
                    desc.Append($"초당 {hpChangeEffect.HpFlatChange():+0;-0} HP ");
                if (hpChangeEffect.HpPercentageChange() != 0f)
                    desc.Append($"초당 최대HP의 {(hpChangeEffect.HpPercentageChange() * 100):+0;-0}% ");
            }

            return desc.ToString().Trim();
        }

        // 시너지 효과 설명 생성
        private string GetSynergyEffectDescription(SynergyEffect effect)
        {
            StringBuilder desc = new StringBuilder();
            
            // 공격력 수정자
            if (effect.AtkMultiplicativeModifier(null) != 0f)
                desc.Append($"공격력 {(effect.AtkMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.AtkAdditiveModifier(null) != 0)
                desc.Append($"공격력 {effect.AtkAdditiveModifier(null):+0;-0} ");
                
            // 방어력 수정자
            if (effect.DefMultiplicativeModifier(null) != 0f)
                desc.Append($"방어력 {(effect.DefMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.DefAdditiveModifier(null) != 0)
                desc.Append($"방어력 {effect.DefAdditiveModifier(null):+0;-0} ");
                
            // 체력 수정자
            if (effect.HpMultiplicativeModifier(null) != 0f)
                desc.Append($"체력 {(effect.HpMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.HpAdditiveModifier(null) != 0)
                desc.Append($"체력 {effect.HpAdditiveModifier(null):+0;-0} ");
                
            // 치명타 수정자
            if (effect.CritChanceAdditiveModifier(null) != 0f)
                desc.Append($"치명타율 {(effect.CritChanceAdditiveModifier(null) * 100):+0;-0}% ");
            if (effect.CritMultiplierAdditiveModifier(null) != 0f)
                desc.Append($"치명타 배율 {(effect.CritMultiplierAdditiveModifier(null) * 100):+0;-0}% ");
                
            // 코드 가속
            if (effect.CodeAccelerationMultiplicativeModifier(null) != 0f)
                desc.Append($"코드 가속 {(effect.CodeAccelerationMultiplicativeModifier(null) * 100):+0;-0}% ");

            return desc.ToString().Trim();
        }
        
        // 시너지 정보를 업데이트하는 메서드
        private void UpdateSynergyInfo(Entities.Unit unit)
        {
            if (unit == null) return;
            
            var synergies = unit.Synergies;
            
            // 첫 번째 시너지 처리
            if (synergies.Count > 0)
            {
                // 첫 번째 시너지 활성화
                if (synergyTag1 != null) synergyTag1.SetActive(true);
                
                // 시너지 이름 설정
                if (synergyText1 != null)
                {
                    var synergyData = GetSynergyData(synergies[0]);
                    synergyText1.text = synergyData?.name ?? $"시너지 {synergies[0]}";
                }
            }
            else
            {
                // 첫 번째 시너지 비활성화
                if (synergyTag1 != null) synergyTag1.SetActive(false);
            }
            
            // 두 번째 시너지 처리
            if (synergies.Count > 1)
            {
                // 두 번째 시너지 활성화
                if (synergyTag2 != null) synergyTag2.SetActive(true);
                
                // 시너지 이름 설정
                if (synergyText2 != null)
                {
                    var synergyData = GetSynergyData(synergies[1]);
                    synergyText2.text = synergyData?.name ?? $"시너지 {synergies[1]}";
                }
            }
            else
            {
                // 두 번째 시너지 비활성화 (단일 시너지이거나 시너지가 없는 경우)
                if (synergyTag2 != null) synergyTag2.SetActive(false);
            }
        }
        
        // 시너지 데이터를 가져오는 헬퍼 메서드
        private SynergyData GetSynergyData(int synergyId)
        {
            var gameManager = Managers.GameManager.Instance;
            if (gameManager?.synergyDataList?.synergies == null) return null;
            
            foreach (var synergy in gameManager.synergyDataList.synergies)
            {
                if (synergy.id == synergyId)
                {
                    return synergy;
                }
            }
            
            return null;
        }
    }
}
