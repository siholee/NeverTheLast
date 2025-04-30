using UnityEngine;
namespace Managers
{
    public class ShopPanelManager : MonoBehaviour
    {
        public static ShopPanelManager Instance { get; private set; }
        public int shopIndex; 

        public void OnClick()
        {
            // 자신의 shopIndex를 사용하여 BottomPanelManager의 ClickShopPanel 호출
            if (BottomPanelManager.Instance != null)
            {
                Debug.Log($"ShopPanel {shopIndex} clicked, forwarding to BottomPanelManager");
                BottomPanelManager.Instance.ClickShopPanel(shopIndex);
            }
            else
            {
                Debug.LogError("BottomPanelManager instance not found!");
            }
        }

        public void OnPanelClicked()
        {
            OnClick();
        }
    }
}