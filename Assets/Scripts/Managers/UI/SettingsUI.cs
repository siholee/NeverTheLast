using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 설정 패널. 씬에 배치된 슬라이더에 의존하지 않고 이 컴포넌트가 직접 조립한다.
    /// (씬 오브젝트는 <see cref="MainMenuUI"/>가 시작 시 숨긴다.)
    ///
    /// 클래스 이름은 MainMenu 씬이 참조하므로 유지한다.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private GameObject _panel;
        private Slider _musicSlider;
        private Slider _sfxSlider;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Open()
        {
            EnsureBuilt();
            SyncFromSettings();
            _panel.SetActive(true);
        }

        public void Close()
        {
            SettingsManager.Instance?.SaveSettings();
            if (_panel != null) _panel.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_panel != null) return;

            Canvas canvas = UIBuild.Canvas("SettingsCanvas", 90);
            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_panel.GetComponent<RectTransform>());

            Image backdrop = UIBuild.Backdrop("Backdrop", _panel.transform);
            UIBuild.OnClick(backdrop.gameObject, Close);

            Image card = UIBuild.Panel("Card", _panel.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 14, UITheme.Outline, 1);
            UIBuild.Pin(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560f, 340f),
                Vector2.zero);

            TextMeshProUGUI title = UIBuild.Label("Title", card.transform, "설정",
                UITheme.FontTitle, UITheme.TextPrimary);
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(240f, 30f),
                new Vector2(28f, -24f));

            _musicSlider = BuildSlider(card.transform, "음악", -110f,
                value =>
                {
                    if (SettingsManager.Instance != null) SettingsManager.Instance.MusicVolume = value;
                });

            _sfxSlider = BuildSlider(card.transform, "효과음", -180f,
                value =>
                {
                    if (SettingsManager.Instance != null) SettingsManager.Instance.SfxVolume = value;
                });

            Button back = UIBuild.Button("Back", card.transform, "뒤로", Close, primary: true);
            UIBuild.Pin(back.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 46f),
                new Vector2(0f, 28f));

            _panel.SetActive(false);
        }

        /// <summary>라벨 + 슬라이더 + 수치 한 줄을 만든다.</summary>
        private static Slider BuildSlider(Transform parent, string label, float y,
            UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = UIBuild.Container($"{label}Row", parent);
            UIBuild.Pin(row, new Vector2(0f, 1f), new Vector2(0f, 44f), new Vector2(28f, y));
            row.anchorMax = new Vector2(1f, 1f);
            row.sizeDelta = new Vector2(-56f, 44f);

            TextMeshProUGUI caption = UIBuild.Text("Label", row, label, UITheme.FontBody,
                UITheme.TextSecondary);
            UIBuild.Anchor(caption.rectTransform, new Vector2(0f, 0f), new Vector2(0.26f, 1f));

            // 슬라이더는 Unity 기본 컴포넌트를 쓰되 배경/핸들만 테마 도형으로 교체한다.
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var slider = sliderGo.GetComponent<Slider>();
            UIBuild.Anchor(sliderGo.GetComponent<RectTransform>(),
                new Vector2(0.28f, 0.30f), new Vector2(0.84f, 0.70f));

            Image track = UIBuild.Solid("Track", sliderGo.transform, UITheme.SurfaceSunken);
            UIBuild.Stretch(track.rectTransform);

            var fillArea = UIBuild.Container("FillArea", sliderGo.transform);
            UIBuild.Stretch(fillArea);
            Image fill = UIBuild.Solid("Fill", fillArea, UITheme.Accent);
            UIBuild.Stretch(fill.rectTransform);

            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = track;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            TextMeshProUGUI valueLabel = UIBuild.Text("Value", row, "100",
                UITheme.FontCaption, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(valueLabel.rectTransform, new Vector2(0.86f, 0f), new Vector2(1f, 1f));

            slider.onValueChanged.AddListener(onChanged);
            slider.onValueChanged.AddListener(v => valueLabel.text = Mathf.RoundToInt(v * 100f).ToString());
            return slider;
        }

        private void SyncFromSettings()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings == null) return;

            if (_musicSlider != null) _musicSlider.value = settings.MusicVolume;
            if (_sfxSlider != null) _sfxSlider.value = settings.SfxVolume;
        }
    }
}
