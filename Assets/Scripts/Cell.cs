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
            // UIManager를 통해 유닛 정보 패널 업데이트
            GridManager.Instance.SelectUnit(xPos, yPos);
        }
        else
        {
            Debug.Log($"Cell contains inactive or missing unit component");
            GridManager.Instance.SelectUnit(xPos, yPos);
        }
    }
    else
    {
        Debug.Log($"Cell is empty");
        GridManager.Instance.SelectUnit(xPos, yPos);
    }
}
}