using UnityEngine;
using Entities;

/// <summary>
/// 그리드 슬롯 (전열/후열 × 아군/적군, 각 4슬롯).
/// 슬롯 0 = 해당 진영의 중앙에서 가장 가까운 위치.
/// </summary>
public class Cell : MonoBehaviour
{
    [Header("Grid Identity")]
    public int  slotIndex;    // 0–3 (줄 내 위치; 0 = 중앙에 가까운 쪽)
    public bool isHeroSide;   // true = 아군 진영, false = 적군 진영
    public bool isFrontline;  // true = 전열, false = 후열

    // 레거시 호환 계산 프로퍼티 (BattleManager 정렬, 디버그 로그용)
    public int xPos => isHeroSide ? -(slotIndex + 1) : (slotIndex + 1);
    public int yPos => isFrontline ? 1 : 2;

    [Header("State")]
    public bool       isOccupied = false;
    public GameObject unit;

    [Header("Visuals")]
    public SpriteRenderer portraitRenderer;
    public GameObject     hpBarObj;
    public GameObject     manaBarObj;

    private void OnMouseDown()
    {
        if (!isOccupied || unit == null) return;
        var u = unit.GetComponent<Unit>();
        if (u != null && u.isActive)
            Debug.Log($"[Cell] {u.UnitName} — {(isHeroSide ? "아군" : "적")} {(isFrontline ? "전열" : "후열")} 슬롯 {slotIndex}");
    }
}
