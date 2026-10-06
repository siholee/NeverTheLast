using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Theme
{
    /// <summary>
    /// 전장 배경 평균 휘도에 따라 라이트/스모크 글라스의 불투명도만 보정한다.
    /// 색상 역할과 형태는 그대로 두고 배경 무늬가 글자를 침범하는 양만 줄인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackdropAdaptiveSurface : MonoBehaviour
    {
        public enum SurfaceKind
        {
            Chrome,
            Smoke,
        }

        private Image _image;
        private SurfaceKind _kind;
        private int _radius;
        private float _baseOpacity;
        private float _lastLuminance = -1f;

        public static void Attach(Image image, SurfaceKind kind, int radius, float baseOpacity = 0.55f)
        {
            if (image == null) return;
            var adaptive = image.GetComponent<BackdropAdaptiveSurface>()
                           ?? image.gameObject.AddComponent<BackdropAdaptiveSurface>();
            adaptive._image = image;
            adaptive._kind = kind;
            adaptive._radius = Mathf.Max(2, radius);
            adaptive._baseOpacity = Mathf.Clamp01(baseOpacity);
            adaptive.Refresh(force: true);
        }

        private void OnEnable() => Refresh(force: true);

        private void Update() => Refresh(force: false);

        private void Refresh(bool force)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            float luminance = UITheme.BackdropLuminance;
            if (!force && Mathf.Abs(luminance - _lastLuminance) < 0.01f) return;
            _lastLuminance = luminance;

            if (_kind == SurfaceKind.Chrome)
            {
                Color fill = UITheme.HudBar;
                fill.a = UITheme.AdaptiveHudAlpha;
                _image.sprite = UIShapes.CutCorner(8, fill, UIShapes.Corner.Diagonal, UITheme.Outline, 1);
                return;
            }

            float alpha = Mathf.Clamp01(Mathf.Lerp(0.54f, 0.82f, _baseOpacity) +
                                        UITheme.AdaptiveSmokeAlphaBonus);
            Color smoke = new(UITheme.PanelCharacter.r, UITheme.PanelCharacter.g,
                UITheme.PanelCharacter.b, alpha);
            _image.sprite = UIShapes.RoundedRect(_radius, smoke,
                new Color(UITheme.MetalSilver.r, UITheme.MetalSilver.g, UITheme.MetalSilver.b, 0.58f), 1);
        }
    }
}
