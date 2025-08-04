using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Managers.UI
{
    public class SynergyPopup: MonoBehaviour
    {
        public GameObject synergyPopupPanel;
        public TextMeshProUGUI synergyNameText;
        public TextMeshProUGUI synergyDescText;
        public TextMeshProUGUI synergyCountText;
        public List<Image> unitPortraits;
    }
}