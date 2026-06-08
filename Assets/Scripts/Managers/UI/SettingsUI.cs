using Core;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 설정 패널 UI 컨트롤러.
    /// SettingsPanel은 비활성 상태로 시작하므로 Start()가 호출되지 않음.
    /// 모든 초기화는 OnEnable()에서 처리 — 패널이 활성화될 때 자식을 탐색.
    /// transform.Find()는 비활성 자식도 찾을 수 있음 (GameObject.Find()와 달리).
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private Slider _musicSlider;
        private Slider _sfxSlider;

        private void OnEnable()
        {
            // 패널이 활성화될 때마다 자식을 탐색 + 값 초기화.
            // Start()가 비활성 상태에서 호출되지 않으므로 여기서 참조도 확보.
            _musicSlider = transform.Find("MusicSlider")?.GetComponent<Slider>();
            _sfxSlider   = transform.Find("SFXSlider")?.GetComponent<Slider>();

            var backBtnGo = transform.Find("SettingsBackBtn");
            if (backBtnGo != null)
            {
                var backBtn = backBtnGo.GetComponent<Button>();
                if (backBtn != null)
                {
                    backBtn.onClick.RemoveAllListeners();
                    backBtn.onClick.AddListener(OnBack);
                }
            }

            // 현재 SettingsManager 값으로 슬라이더 초기화
            if (SettingsManager.Instance == null) return;

            if (_musicSlider != null)
            {
                _musicSlider.value = SettingsManager.Instance.MusicVolume;
                _musicSlider.onValueChanged.RemoveAllListeners();
                _musicSlider.onValueChanged.AddListener(v => SettingsManager.Instance.MusicVolume = v);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.value = SettingsManager.Instance.SfxVolume;
                _sfxSlider.onValueChanged.RemoveAllListeners();
                _sfxSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SfxVolume = v);
            }
        }

        private void OnBack()
        {
            SettingsManager.Instance?.SaveSettings();

            // 부모 캔버스의 MainMenuUI에 패널 닫기 위임
            var menuUI = GetComponentInParent<MainMenuUI>(true);
            if (menuUI != null)
                menuUI.CloseSettings();
            else
                gameObject.SetActive(false);
        }
    }
}
