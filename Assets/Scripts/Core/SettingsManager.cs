using UnityEngine;

namespace Core
{
    /// <summary>
    /// 싱글톤 설정 매니저. 음악 및 SFX 볼륨을 PlayerPrefs에 저장/불러오기.
    /// DontDestroyOnLoad — 씬 전환 후에도 유지.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string KeyMusic = "NTL_MusicVol";
        private const string KeySfx   = "NTL_SfxVol";

        private float _musicVolume = 1f;
        private float _sfxVolume   = 1f;

        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                ApplyMusicVolume();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                ApplySfxVolume();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KeyMusic, _musicVolume);
            PlayerPrefs.SetFloat(KeySfx,   _sfxVolume);
            PlayerPrefs.Save();
        }

        public void LoadSettings()
        {
            _musicVolume = PlayerPrefs.GetFloat(KeyMusic, 1f);
            _sfxVolume   = PlayerPrefs.GetFloat(KeySfx,   1f);
            ApplyMusicVolume();
            ApplySfxVolume();
        }

        private void ApplyMusicVolume()
        {
            // TODO: 실제 AudioMixer 연결 시 mixer.SetFloat("MusicVol", Mathf.Log10(_musicVolume) * 20f)
            AudioListener.volume = _musicVolume;
        }

        private void ApplySfxVolume()
        {
            // TODO: SfxManager와 연결
        }
    }
}
