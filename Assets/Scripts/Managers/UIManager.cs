using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
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
        
        // 시너지 팝업
        public SynergyPopup synergyPopupObj;
        
        // 유닛 정보 탭
        public InfoTab infoTab;
        
        private Camera mainCamera;
        
        private void Start()
        {
            // 시작할 때 팝업 비활성화
            if (synergyPopupObj != null)
            {
                synergyPopupObj.synergyPopupPanel.SetActive(false);
            }
            
            // 시작할 때 InfoTab 비활성화
            if (infoTab != null)
            {
                infoTab.gameObject.SetActive(false);
            }

            // 메인 카메라 참조 가져오기
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            // 마우스 왼쪽 버튼 클릭 감지
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
        }

        private void HandleMouseClick()
        {
            if (mainCamera == null) return;
            
            Vector3 mousePosition = Input.mousePosition;
            // Z값을 카메라와의 거리로 설정하여 정확한 월드 좌표 변환
            mousePosition.z = -mainCamera.transform.position.z;
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
            
            // Raycast로 클릭된 오브젝트 찾기
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPosition);
            
            if (hitCollider != null)
            {
                Cell clickedCell = hitCollider.GetComponent<Cell>();
                if (clickedCell != null)
                {
                    // Cell을 감지했을 때 실제 Cell 위치에 구 오브젝트 표시
                    Vector3 cellPosition = clickedCell.transform.position;
                    HandleCellClick(clickedCell);
                }
            }
            else
            {
                Debug.Log("Empty space clicked");
                HideInfoTab();
            }
        }
        
        public void ShowSynergyPopup(Vector3 position, SynergyInfo synergyInfo)
        {
            if (synergyPopupObj == null) return;
            synergyPopupObj.synergyPopupPanel.SetActive(true);

            // 팝업 위치 설정 (우상단 -> 좌상단)
            RectTransform popupRect = synergyPopupObj.synergyPopupPanel.GetComponent<RectTransform>();
            // Vector2 screenPosition = Camera.main.WorldToScreenPoint(position);
            popupRect.position = position;

            // 시너지 정보 설정
            synergyPopupObj.synergyNameText.text = synergyInfo.Name;
            synergyPopupObj.synergyDescText.text = synergyInfo.Description;
            synergyPopupObj.synergyCountText.text = $"{synergyInfo.Count}/{synergyInfo.MaxCount}";

            // 유닛 포트레이트 설정
            for (int i = 0; i < synergyPopupObj.unitPortraits.Count; i++)
            {
                if (i < synergyInfo.Units.Count)
                {
                    synergyPopupObj.unitPortraits[i].sprite = Resources.Load<Sprite>(synergyInfo.Units[i].PortraitPath);
                    synergyPopupObj.unitPortraits[i].gameObject.SetActive(true);
                }
                else
                {
                    synergyPopupObj.unitPortraits[i].gameObject.SetActive(false);
                }
            }
        }

        public void HideSynergyPopup()
        {
            if (synergyPopupObj != null)
            {
                synergyPopupObj.synergyPopupPanel.SetActive(false);
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
            synergyList.Sort((a, b) => b.Count.CompareTo(a.Count));
            
            for (int i = 0; i < synergyTagContainer.childCount; i++)
            {
                var synergyTag = synergyTagContainer.GetChild(i).GetComponent<SynergyTag>();
                if (i < synergyList.Count && synergyList[i].Count > 0)
                {
                    SynergyInfo synergyInfo = synergyList[i];
                    synergyTag.Initialize(synergyInfo, Resources.Load<Sprite>(synergyInfo.Units[0].PortraitPath));
                    synergyTag.synergyCountText.text = $"{synergyInfo.Count} | {synergyInfo.MaxCount}";
                    synergyTag.SetActive(true);
                }
                else
                {
                    synergyTag.SetActive(false);
                }
            }
        }

        // InfoTab 관련 메서드들
        public void ShowInfoTab(Unit unit)
        {
            if (infoTab == null || unit == null) return;
            
            // InfoTab 활성화
            infoTab.gameObject.SetActive(true);
            
            // 유닛 정보 설정
            infoTab.unitNameText.text = unit.UnitName;
            infoTab.unitLevelText.text = $"Lv.{unit.Level}";
            infoTab.maxHpText.text = $"Max HP: {unit.HpMax}";
            infoTab.currentHpText.text = $"Current HP: {unit.HpCurr}";
            infoTab.atkText.text = $"ATK: {unit.AtkCurr}";
            infoTab.defText.text = $"DEF: {unit.DefCurr}";
            infoTab.critPosText.text = $"Crit Rate: {(unit.CritChanceCurr * 100):F1}%";
            infoTab.critDmgText.text = $"Crit DMG: {(unit.CritMultiplierCurr * 100):F1}%";
            infoTab.coolDownText.text = $"Normal CD: {unit.normalCooldown:F1}s";
            infoTab.penetrationText.text = "Penetration: N/A"; // 관통력은 아직 구현되지 않음
            
            // 업그레이드 정보 설정 (임시값)
            infoTab.upgradePosText.text = "Upgrade Rate: 50%";
            infoTab.upgradeFailText.text = "Fail Rate: 50%";
        }
        
        public void HideInfoTab()
        {
            if (infoTab != null)
            {
                infoTab.gameObject.SetActive(false);
            }
        }

        // Cell 클릭 처리를 위한 중앙화된 메서드
        public void HandleCellClick(Cell clickedCell)
        {
            Debug.Log($"Cell clicked: ({clickedCell.xPos}, {clickedCell.yPos})");

            // Cell이 클릭되면 유닛 정보를 표시
            if (clickedCell.isOccupied && clickedCell.unit != null)
            {
                Unit cellUnit = clickedCell.unit.GetComponent<Unit>();
                if (cellUnit != null && cellUnit.isActive)
                {
                    Debug.Log($"Cell contains active unit: {cellUnit.UnitName}");
                    // InfoTab 활성화 및 유닛 정보 표시
                    ShowInfoTab(cellUnit);
                }
                else
                {
                    Debug.Log($"Cell contains inactive or missing unit component");
                    // 빈 셀 클릭 시 InfoTab 숨기기
                    HideInfoTab();
                }
            }
        }

        // 기존 메서드들...
        public void TestButtonClick()
        {
            Debug.Log("Test Button Clicked");
        }

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

                Time.timeScale = newSpeed;
                gameSpeedText.text = newSpeed + "X";
            }
            else
            {
                Debug.LogError("게임 속도 텍스트 파싱 실패: " + currentText);
            }
        }

        public void OnGameStartButtonClick()
        {
            GameManager.Instance.gameState = GameState.RoundInProgress;
        }
    }
}
