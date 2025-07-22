using System;
using System.Collections.Generic;
using BaseClasses;
using Managers.UI;
using UnityEngine;
using UnityEngine.UI;
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
        
        // 좌측 사이드바
        public Transform synergyTagContainer;

        public void TestButtonClick()
        {
            // Start Round 함수 여기에 구현하면 됨
            Debug.Log("Test Button Clicked");
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

        public void SetSynergyText(Dictionary<int, SynergyInfo> synergyCounts)
        {
            List<SynergyInfo> synergyList = new List<SynergyInfo>();
            foreach (var synergy in synergyCounts.Values)
            {
                if (synergy.Count > 0)
                {
                    synergyList.Add(synergy);
                }
            }
            synergyList.Sort((a, b) => b.Count.CompareTo(a.Count)); // Count 기준으로 내림차순 정렬
            for (int i = 0; i < synergyTagContainer.childCount; i++)
            {
                var synergyTag = synergyTagContainer.GetChild(i).GetComponent<SynergyTag>();
                if (i < synergyList.Count && synergyList[i].Count > 0 )
                {
                    SynergyInfo synergyInfo = synergyList[i];
                    synergyTag.Initialize(synergyInfo.Name, Resources.Load<Sprite>(synergyInfo.Units[0].PortraitPath));
                    synergyTag.synergyCountText.text = $"{synergyInfo.Count} | {synergyInfo.MaxCount}";
                    synergyTag.SetActive(true);
                }
                else
                {
                    synergyTag.SetActive(false);
                }
            }
        }
    }
}