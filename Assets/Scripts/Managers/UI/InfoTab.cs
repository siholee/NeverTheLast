using UnityEngine;
using System.Text;
using System.Collections.Generic;
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
        
        [Header("Identity Information")]
        public GameObject identityTag1;
        public GameObject identityTag2;
        public TMPro.TextMeshProUGUI elementText;
        public TMPro.TextMeshProUGUI classText;
        
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
            UpdateIdentityInfo(currentDisplayedUnit);
            
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

        private void UpdateIdentityInfo(Entities.Unit unit)
        {
            if (unit == null) return;

            if (!string.IsNullOrWhiteSpace(unit.Element))
            {
                if (identityTag1 != null) identityTag1.SetActive(true);
                if (elementText != null)
                {
                    elementText.text = $"원소: {unit.GetCombatElementDisplay()} | 중량 {unit.CarryWeightCurrent}/{unit.CarryWeightMax} | 코드 {unit.LearnedCodeCount}/{unit.MaxCodeCount}";
                }
            }
            else
            {
                if (identityTag1 != null) identityTag1.SetActive(false);
            }
            
            if (!string.IsNullOrWhiteSpace(unit.MainStat) || !string.IsNullOrWhiteSpace(unit.SubStat))
            {
                if (identityTag2 != null) identityTag2.SetActive(true);
                if (classText != null) classText.text = $"스탯: {unit.MainStat} / {unit.SubStat}";
            }
            else
            {
                if (identityTag2 != null) identityTag2.SetActive(false);
            }
        }
    }
}
