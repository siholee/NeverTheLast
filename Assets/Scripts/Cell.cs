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
    
private void OnMouseDown()
{
    Debug.Log($"Cell clicked: ({xPos}, {yPos})");
    
    // Cell이 클릭되면 GridManager를 통해 유닛 정보를 표시
    if (isOccupied && unit != null)
    {
        Unit cellUnit = unit.GetComponent<Unit>();
        if (cellUnit != null && cellUnit.isActive)
        {
            Debug.Log($"Cell contains active unit: {cellUnit.UnitName}");
            // UIManager를 통해 InfoTab 활성화 및 유닛 정보 표시
            if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            {
                GameManager.Instance.uiManager.ShowInfoTab(cellUnit);
            }
        }
        else
        {
            Debug.Log($"Cell contains inactive or missing unit component");
            // 빈 셀 클릭 시 InfoTab 숨기기
            if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            {
                GameManager.Instance.uiManager.HideInfoTab();
            }
        }
    }
    else
    {
        Debug.Log($"Cell is empty");
        // 빈 셀 클릭 시 InfoTab 숨기기
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.HideInfoTab();
        }
    }
}
}