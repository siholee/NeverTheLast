using UnityEngine;
using Entities;
using Managers;

public class Cell : MonoBehaviour
{
    public int xPos;
    public int yPos;
    public float reservedTime;
    public bool isOccupied = false;
    public GameObject unit;
    public SpriteRenderer portraitRenderer;
    public GameObject hpBarObj;
    public GameObject manaBarObj;
    public GameObject infoTab;

    private void Update()
    {
        reservedTime = Mathf.Max(0, reservedTime - Time.deltaTime);
    }
    
    // InfoTab UI 업데이트 담당 메서드
    public void UpdateInfoTab()
    {
        if (infoTab == null) return;
        
        // 유닛이 있고 활성 상태인 경우 InfoTab 표시
        if (isOccupied && unit != null)
        {
            Unit cellUnit = unit.GetComponent<Unit>();
            if (cellUnit != null && cellUnit.isActive)
            {
                infoTab.SetActive(true);
                // 추가적인 InfoTab 업데이트 로직이 필요하다면 여기에 추가
            }
            else
            {
                infoTab.SetActive(false);
            }
        }
        else
        {
            infoTab.SetActive(false);
        }
    }
    
    // InfoTab 숨기기
    public void HideInfoTab()
    {
        if (infoTab != null)
        {
            infoTab.SetActive(false);
        }
    }
}
