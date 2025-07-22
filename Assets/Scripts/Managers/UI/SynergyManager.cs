using System;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class SynergyManager : MonoBehaviour
    {
        // 기본
        public string synergyName;
        public string synergyDescription;
        public int synergyLevel;
        public int synergyUpgradeCost;

        // UI
        public TMPro.TextMeshProUGUI synergyNameText;
        public TMPro.TextMeshProUGUI synergyDescriptionText;
        public TMPro.TextMeshProUGUI synergyLevelText;
        public TMPro.TextMeshProUGUI synergyUpgradeCostText;
        public GameObject synergyDisplay;

        // 1. RefreshUI - UI에서 시너지 정보를 읽어서 변수에 저장
        public void RefreshUI()
        {
            if (synergyNameText != null)
                synergyName = synergyNameText.text;
            
            if (synergyDescriptionText != null)
                synergyDescription = synergyDescriptionText.text;
            
            if (synergyLevelText != null && int.TryParse(synergyLevelText.text, out int level))
                synergyLevel = level;
            
            if (synergyUpgradeCostText != null && int.TryParse(synergyUpgradeCostText.text, out int cost))
                synergyUpgradeCost = cost;
        }

        // 2. TestButtonClick - 시너지 레벨 증가 및 UI 새로고침
        public void TestButtonClick()
        {
            synergyLevel++;
            RefreshUI();
            Debug.Log($"{synergyName} 시너지 레벨 증가: {synergyLevel}");
        }

        // 3. HoverControl - 마우스 호버 시 시너지 디스플레이 제어
        public void HoverControl()
        {
            if (synergyDisplay != null)
            {
                synergyDisplay.SetActive(true);
                RefreshUI(); // 호버 시 최신 정보로 UI 업데이트
            }
        }

        public void OnMouseExit()
        {
            if (synergyDisplay != null)
            {
                synergyDisplay.SetActive(false);
            }
        }

        // Start에서 초기 UI 설정
        void Start()
        {
            if (synergyDisplay != null)
                synergyDisplay.SetActive(false); // 초기에는 비활성화
            
            RefreshUI(); // 초기 UI 설정
        }
    }
}