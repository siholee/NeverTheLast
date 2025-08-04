using BaseClasses;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Managers.UI
{
    public class SynergyTag: MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TMPro.TextMeshProUGUI synergyNameText;
        public TMPro.TextMeshProUGUI synergyCountText;
        public TMPro.TextMeshProUGUI synergyDescriptionText;
        public Image synergyIconImage;
        
        private SynergyInfo _synergyInfo;

        public void Initialize(SynergyInfo info, Sprite synergyIcon)
        {
            synergyNameText.text = info.Name;
            synergyIconImage.sprite = synergyIcon;
            synergyIconImage.preserveAspect = true; // 아이콘 비율 유지
            _synergyInfo = info;
        } 
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("SynergyTag OnPointerEnter called");
            if (_synergyInfo == null || GameManager.Instance.uiManager == null) return;
            Vector3 worldPosition = transform.position + new Vector3(transform.GetComponent<RectTransform>().rect.width, 0, 0);
            GameManager.Instance.uiManager.ShowSynergyPopup(worldPosition, _synergyInfo);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (GameManager.Instance.uiManager != null)
            {
                GameManager.Instance.uiManager.HideSynergyPopup();
            }
        }
    }
}