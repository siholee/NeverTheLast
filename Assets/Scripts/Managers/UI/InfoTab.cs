using UnityEngine;
using System.Text;
using System.Collections.Generic;
using StatusEffects.Base;
using Entities.Status;
using Managers;

namespace Managers.UI
{
    public class InfoTab : MonoBehaviour
    {
        [Header("Unit Information")]
        public TMPro.TextMeshProUGUI unitNameText;
        public TMPro.TextMeshProUGUI unitLevelText;
        public TMPro.TextMeshProUGUI maxHpText;
        public TMPro.TextMeshProUGUI currentHpText;
        public TMPro.TextMeshProUGUI atkText;
        public TMPro.TextMeshProUGUI defText;
        public TMPro.TextMeshProUGUI critPosText;
        public TMPro.TextMeshProUGUI critDmgText;
        public TMPro.TextMeshProUGUI coolDownText;
        
        [Header("Status Effects")]
        public TMPro.TextMeshProUGUI statusEffectsText;
        
        [Header("Synergy Information")]
        public GameObject synergyTag1;
        public GameObject synergyTag2;
        public TMPro.TextMeshProUGUI synergyText1;
        public TMPro.TextMeshProUGUI synergyText2;
        
        [Header("Tab System")]
        public GameObject tab1Content;
        public GameObject tab2Content;
        public GameObject tab3Content;
        
        [Header("Upgrade Information")]
        public TMPro.TextMeshProUGUI upgradePosText;
        public TMPro.TextMeshProUGUI upgradeFailText;
        
        private Entities.Unit currentDisplayedUnit;
        private int currentTab = 1;
        private bool isInfoTabActive = false;
        
        void Start()
        {
            gameObject.SetActive(false);
            InitializeTabs();
        }
        
        void Update()
        {
            if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInfoTab();
                return;
            }
            
            if (isInfoTabActive && currentDisplayedUnit != null && currentDisplayedUnit.isActive)
            {
                UpdateCurrentUnitInfo();
            }
        }
        
        private void InitializeTabs()
        {
            SetActiveTab(1);
        }
        
        public void OnTabButtonClick(int tabNumber)
        {
            if (tabNumber >= 1 && tabNumber <= 3)
            {
                SetActiveTab(tabNumber);
            }
        }
        
        private void SetActiveTab(int tabNumber)
        {
            currentTab = tabNumber;
            
            if (tab1Content != null) tab1Content.SetActive(false);
            if (tab2Content != null) tab2Content.SetActive(false);
            if (tab3Content != null) tab3Content.SetActive(false);
            
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
        
        public void ShowInfoTab(Entities.Unit unit)
        {
            if (unit == null) return;
            
            currentDisplayedUnit = unit;
            isInfoTabActive = true;
            gameObject.SetActive(true);
            SetActiveTab(1);
            UpdateCurrentUnitInfo();
        }
        
        public void OnCloseButtonClick()
        {
            CloseInfoTab();
        }
        
        private void CloseInfoTab()
        {
            isInfoTabActive = false;
            currentDisplayedUnit = null;
            gameObject.SetActive(false);
        }
        
        private void UpdateCurrentUnitInfo()
        {
            if (currentDisplayedUnit == null) return;
            
            if (unitNameText != null) unitNameText.text = currentDisplayedUnit.UnitName;
            if (unitLevelText != null) unitLevelText.text = $"Lv.{currentDisplayedUnit.Level}";
            if (maxHpText != null) maxHpText.text = $"{currentDisplayedUnit.HpMax}";
            if (currentHpText != null) currentHpText.text = $"{currentDisplayedUnit.HpCurr}";
            if (atkText != null) atkText.text = $"{currentDisplayedUnit.AtkCurr}";
            if (defText != null) defText.text = $"{currentDisplayedUnit.DefCurr}";
            if (critPosText != null) critPosText.text = $"{(currentDisplayedUnit.CritChanceCurr * 100):F1}%";
            if (critDmgText != null) critDmgText.text = $"{(currentDisplayedUnit.CritMultiplierCurr * 100):F1}%";
            if (coolDownText != null) coolDownText.text = $"{currentDisplayedUnit.normalCooldown:F1}s";
            
            UpdateStatusEffects(currentDisplayedUnit);
            UpdateSynergyInfo(currentDisplayedUnit);
            
            if (upgradePosText != null) upgradePosText.text = "Upgrade Rate: 50%";
            if (upgradeFailText != null) upgradeFailText.text = "Fail Rate: 50%";
        }
        
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

            // 새로운 Status 시스템 - List로 변경됨
            var statuses = unit.GetStatuses();
            if (statuses != null && statuses.Count > 0)
            {
                foreach (var status in statuses)
                {
                    hasAnyEffect = true;
                    
                    string categoryColor = GetCategoryColor(status.Category);
                    string duration = status.Duration > 0 ? $"{(status.Duration - status.ElapsedTime):F1}초" : "영구";
                    
                    statusBuilder.AppendLine($"• <color={categoryColor}>{status.StatusName}</color>");
                    statusBuilder.AppendLine($"  남은시간: {duration}");
                    
                    if (!string.IsNullOrEmpty(status.StatusDescription))
                        statusBuilder.AppendLine($"  효과: {status.StatusDescription}");
                    
                    if (status.Effects != null && status.Effects.Count > 0)
                        statusBuilder.AppendLine($"  효과 수: {status.Effects.Count}");
                    
                    statusBuilder.AppendLine();
                }
            }

            // 기존 StatusEffect 시스템 (하위 호환)
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

            // 시너지 효과 (기존 시스템)
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

        private string GetCategoryColor(BaseClasses.BaseEnums.StatusCategory category)
        {
            switch (category)
            {
                case BaseClasses.BaseEnums.StatusCategory.Positive:
                    return "#44ff44";
                case BaseClasses.BaseEnums.StatusCategory.Negative:
                    return "#ff4444";
                case BaseClasses.BaseEnums.StatusCategory.Neutral:
                default:
                    return "#ffaa44";
            }
        }

        private string GetEffectDisplayName(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "알 수 없는 효과";
            
            if (identifier.Contains("PoisonEffect")) return "맹독";
            if (identifier.Contains("Burn")) return "화상";
            if (identifier.Contains("HolyEnchant")) return "신성한 인챈트";
            if (identifier.Contains("HealingModified")) return "치유 변화";
            if (identifier.Contains("Shield")) return "방어막";
            
            return identifier;
        }

        private string GetEffectDescription(StatusEffect effect)
        {
            StringBuilder desc = new StringBuilder();

            if (effect.AtkMultiplicativeModifier(null) != 0f)
                desc.Append($"공격력 {(effect.AtkMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.AtkAdditiveModifier(null) != 0)
                desc.Append($"공격력 {effect.AtkAdditiveModifier(null):+0;-0} ");
                
            if (effect.DefMultiplicativeModifier(null) != 0f)
                desc.Append($"방어력 {(effect.DefMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.DefAdditiveModifier(null) != 0)
                desc.Append($"방어력 {effect.DefAdditiveModifier(null):+0;-0} ");
                
            if (effect.HpMultiplicativeModifier(null) != 0f)
                desc.Append($"체력 {(effect.HpMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.HpAdditiveModifier(null) != 0)
                desc.Append($"체력 {effect.HpAdditiveModifier(null):+0;-0} ");
                
            if (effect.CritChanceAdditiveModifier(null) != 0f)
                desc.Append($"치명타율 {(effect.CritChanceAdditiveModifier(null) * 100):+0;-0}% ");
            if (effect.CritMultiplierAdditiveModifier(null) != 0f)
                desc.Append($"치명타 배율 {(effect.CritMultiplierAdditiveModifier(null) * 100):+0;-0}% ");
                
            if (effect.CodeAccelerationMultiplicativeModifier(null) != 0f)
                desc.Append($"코드 가속 {(effect.CodeAccelerationMultiplicativeModifier(null) * 100):+0;-0}% ");
                
            if (effect.ReceivingDamageModifier(null) != 1f)
                desc.Append($"받는 피해 {((effect.ReceivingDamageModifier(null) - 1f) * 100):+0;-0}% ");
                
            if (effect.HealingReceivedModifier(null) != 1f)
                desc.Append($"치유량 {((effect.HealingReceivedModifier(null) - 1f) * 100):+0;-0}% ");

            if (effect is IHpChangeEffect hpChangeEffect)
            {
                if (hpChangeEffect.HpFlatChange() != 0)
                    desc.Append($"초당 {hpChangeEffect.HpFlatChange():+0;-0} HP ");
                if (hpChangeEffect.HpPercentageChange() != 0f)
                    desc.Append($"초당 최대HP의 {(hpChangeEffect.HpPercentageChange() * 100):+0;-0}% ");
            }

            return desc.ToString().Trim();
        }

        private string GetSynergyEffectDescription(SynergyEffect effect)
        {
            StringBuilder desc = new StringBuilder();
            
            if (effect.AtkMultiplicativeModifier(null) != 0f)
                desc.Append($"공격력 {(effect.AtkMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.AtkAdditiveModifier(null) != 0)
                desc.Append($"공격력 {effect.AtkAdditiveModifier(null):+0;-0} ");
                
            if (effect.DefMultiplicativeModifier(null) != 0f)
                desc.Append($"방어력 {(effect.DefMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.DefAdditiveModifier(null) != 0)
                desc.Append($"방어력 {effect.DefAdditiveModifier(null):+0;-0} ");
                
            if (effect.HpMultiplicativeModifier(null) != 0f)
                desc.Append($"체력 {(effect.HpMultiplicativeModifier(null) * 100):+0;-0}% ");
            if (effect.HpAdditiveModifier(null) != 0)
                desc.Append($"체력 {effect.HpAdditiveModifier(null):+0;-0} ");
                
            if (effect.CritChanceAdditiveModifier(null) != 0f)
                desc.Append($"치명타율 {(effect.CritChanceAdditiveModifier(null) * 100):+0;-0}% ");
            if (effect.CritMultiplierAdditiveModifier(null) != 0f)
                desc.Append($"치명타 배율 {(effect.CritMultiplierAdditiveModifier(null) * 100):+0;-0}% ");
                
            if (effect.CodeAccelerationMultiplicativeModifier(null) != 0f)
                desc.Append($"코드 가속 {(effect.CodeAccelerationMultiplicativeModifier(null) * 100):+0;-0}% ");

            return desc.ToString().Trim();
        }
        
        private void UpdateSynergyInfo(Entities.Unit unit)
        {
            if (unit == null) return;
            
            var synergies = unit.Synergies;
            
            if (synergies.Count > 0)
            {
                if (synergyTag1 != null) synergyTag1.SetActive(true);
                
                if (synergyText1 != null)
                {
                    var synergyData = GetSynergyData(synergies[0]);
                    synergyText1.text = synergyData?.name ?? $"시너지 {synergies[0]}";
                }
            }
            else
            {
                if (synergyTag1 != null) synergyTag1.SetActive(false);
            }
            
            if (synergies.Count > 1)
            {
                if (synergyTag2 != null) synergyTag2.SetActive(true);
                
                if (synergyText2 != null)
                {
                    var synergyData = GetSynergyData(synergies[1]);
                    synergyText2.text = synergyData?.name ?? $"시너지 {synergies[1]}";
                }
            }
            else
            {
                if (synergyTag2 != null) synergyTag2.SetActive(false);
            }
        }
        
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