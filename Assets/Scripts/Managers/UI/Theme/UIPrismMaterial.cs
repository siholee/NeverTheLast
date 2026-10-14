using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Theme
{
    /// <summary>탐색 표면의 실제 장면 흐림. 작은 카드·본문·텍스트에는 적용하지 않는다.</summary>
    public static class UIPrismMaterial
    {
        private static Material _glass;
        public static void Apply(Image image)
        {
            if (_glass == null)
            {
                Shader shader = Resources.Load<Shader>("UI/Shaders/PrismGlass");
                if (shader == null || !shader.isSupported) return;
                _glass = new Material(shader) { name = "PrismGlass.Shared", hideFlags = HideFlags.HideAndDontSave };
                _glass.SetFloat("_Frost", .80f);
                _glass.SetFloat("_Blur", 6f);
            }
            image.material = _glass;
        }
    }
}
