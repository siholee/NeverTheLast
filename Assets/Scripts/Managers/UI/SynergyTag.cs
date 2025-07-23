using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    public class SynergyTag: MonoBehaviour
    {
        public TMPro.TextMeshProUGUI synergyNameText;
        public TMPro.TextMeshProUGUI synergyCountText;
        public TMPro.TextMeshProUGUI synergyDescriptionText;
        public Image synergyIconImage;

        public void Initialize(string synergyName, Sprite synergyIcon)
        {
            synergyNameText.text = synergyName;
            synergyIconImage.sprite = synergyIcon;
            synergyIconImage.preserveAspect = true; // 아이콘 비율 유지
        } 
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}