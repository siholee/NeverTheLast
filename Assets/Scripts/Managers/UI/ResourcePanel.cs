using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    public class ResourcePanel: MonoBehaviour
    {
        public TMPro.TMP_Text token1Text;
        public TMPro.TMP_Text token2Text;
        public TMPro.TMP_Text token3Text;
        public TMPro.TMP_Text token4Text;
        public TMPro.TMP_Text token5Text;
        public TMPro.TMP_Text token6Text;
        public TMPro.TMP_Text token7Text;
        public TMPro.TMP_Text token8Text;
        public TMPro.TMP_Text rerollText;

        public void UpdatePanel(Dictionary<int, int> tokens, int rerollTickets)
        {
            for (int i = 1; i <= 8; i++)
            {
                // Use reflection to get the corresponding text field
                var textField = (TMPro.TMP_Text)this.GetType().GetField($"token{i}Text").GetValue(this);
                if (tokens.TryGetValue(i, out var token))
                {
                    textField.text = token.ToString();
                }
                else
                {
                    textField.text = "0";
                }
            }
        }
    }
}