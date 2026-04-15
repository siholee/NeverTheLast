using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class UIManager : MonoBehaviour
    {
        // 상단
        public TMPro.TextMeshProUGUI gameGoldText;
        public TMPro.TextMeshProUGUI gameLifeText;
        public TMPro.TextMeshProUGUI gameStageText;
        public TMPro.TextMeshProUGUI gameSpeedText;

        public void TestButtonClick()
        {
            // Start Round 함수 여기에 구현하면 됨
            Debug.Log("Test Button Clicked");
        }

        // 턴 행동 선택 패널
        public GameObject actionPanel;
        public UnityEngine.UI.Button normalButton;
        public UnityEngine.UI.Button ultimateButton;

        /// <summary>
        /// 히어로 턴에 행동 선택 패널 표시
        /// </summary>
        public void ShowActionPanel(Unit unit)
        {
            if (actionPanel != null)
            {
                actionPanel.SetActive(true);
                // 궁극기 버튼은 마나가 충분할 때만 활성화
                if (ultimateButton != null)
                {
                    ultimateButton.interactable = unit.ManaCurr >= unit.ManaMax
                        && unit.UltimateCode != null
                        && unit.UltimateCode.HasValidTarget();
                }
            }
            Debug.Log($"[UI] {unit.UnitName} 행동 선택 패널 표시");
        }

        /// <summary>
        /// 행동 선택 패널 숨기기
        /// </summary>
        public void HideActionPanel()
        {
            if (actionPanel != null)
            {
                actionPanel.SetActive(false);
            }
        }

        // 배속
        public void OnGameSpeedButtonClick()
        {
            string currentText = gameSpeedText.text;
            string speedString = currentText.Replace("X", "");
            if (int.TryParse(speedString, out int currentSpeed))
            {
                int newSpeed;
                switch (currentSpeed)
                {
                    case 1:
                        newSpeed = 2;
                        break;
                    case 2:
                        newSpeed = 3;
                        break;
                    case 3:
                        newSpeed = 1;
                        break;
                    default:
                        newSpeed = 1;
                        break;
                }
                
                gameSpeedText.text = newSpeed + "X";
            }
            else
            {
                Debug.LogError("게임 속도 텍스트 파싱 실패: " + currentText);
            }
        }
    }
}