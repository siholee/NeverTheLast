using UnityEngine;
using System.Collections.Generic;
namespace Managers
{
    public class BottomPanelManager : MonoBehaviour
    {
        public static BottomPanelManager Instance { get; private set; }
        public List<GameObject> shopPanels;
        public int cost;

        public void ClickShopPanel(int index)
        {
            Debug.Log($"Shop panel {index} was clicked!");
        }

        public void ClickRefreshButton()
        {
            // Console log for testing
            Debug.Log("Refresh button clicked!");
        }

        public void ClickUpButton()
        {
            // Console log for testing
            Debug.Log("Up button clicked!");
            cost += 1;
        }

        public void ClickDownButton()
        {
            // Console log for testing
            Debug.Log("Down button clicked!");
            cost -= 1;
        }

    }
}