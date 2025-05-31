using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Top Panel References")]
    [SerializeField] private Button menuBtn;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI lifeText; 
    
    private void OpenMenu()
    {
        Debug.Log("메뉴 버튼이 클릭되었습니다");
    }
    
    public void UpdateGold(int amount)
    {
        if (goldText != null)
            goldText.text = $"Gold: {amount}";
    }
    
    public void UpdateLife(int amount)
    {
        if (lifeText != null)
            lifeText.text = $"Life: {amount}";
    }
}