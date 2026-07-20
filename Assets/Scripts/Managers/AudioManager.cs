using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// BGM/효과음 재생 담당. 씬 전환에도 유지되며 필요할 때 자동 생성된다.
    ///
    /// 오디오 클립은 Resources에서 이름으로 로드한다:
    ///  - BGM: `Audio/BGM/{name}`
    ///  - SFX: `Audio/SFX/{name}`
    ///
    /// 아직 오디오 에셋이 없어도 게임이 멈추지 않도록, 클립을 찾지 못하면 조용히 무시한다
    /// (같은 이름은 한 번만 경고 로그를 남긴다). 나중에 파일을 넣기만 하면 그대로 동작한다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const string BgmPathPrefix = "Audio/BGM/";
        private const string SfxPathPrefix = "Audio/SFX/";
        private const float BgmFadeDuration = 0.6f;

        public static AudioManager Instance { get; private set; }

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private string _currentBgmName;
        private Coroutine _bgmFadeRoutine;

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly HashSet<string> _missingLogged = new();

        /// <summary>인스턴스를 보장한다. 씬에 없으면 만들어 둔다.</summary>
        public static AudioManager EnsureExists()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;

            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>설정값(SettingsManager)의 볼륨을 각 소스에 반영한다.</summary>
        public void ApplyVolumes()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (_bgmSource != null) _bgmSource.volume = settings != null ? settings.MusicVolume : 1f;
            if (_sfxSource != null) _sfxSource.volume = settings != null ? settings.SfxVolume : 1f;
        }

        // ===== BGM =====

        /// <summary>
        /// BGM 재생. 같은 곡이 이미 재생 중이면 아무것도 하지 않는다(대사마다 재시작 방지).
        /// 이름이 비어 있으면 무시한다.
        /// </summary>
        public void PlayBgm(string bgmName, bool fade = true)
        {
            if (string.IsNullOrWhiteSpace(bgmName)) return;
            if (_currentBgmName == bgmName && _bgmSource != null && _bgmSource.isPlaying) return;

            AudioClip clip = LoadClip(BgmPathPrefix + bgmName, bgmName);
            if (clip == null)
            {
                // 클립이 없어도 "현재 곡" 상태는 갱신해 두어 매 대사마다 로드를 시도하지 않게 한다.
                _currentBgmName = bgmName;
                return;
            }

            _currentBgmName = bgmName;
            if (!fade)
            {
                _bgmSource.clip = clip;
                _bgmSource.Play();
                ApplyVolumes();
                return;
            }

            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(CrossFadeBgm(clip));
        }

        /// <summary>BGM 정지.</summary>
        public void StopBgm(bool fade = true)
        {
            _currentBgmName = null;
            if (_bgmSource == null || !_bgmSource.isPlaying) return;

            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            if (!fade)
            {
                _bgmSource.Stop();
                return;
            }

            _bgmFadeRoutine = StartCoroutine(FadeOutBgm());
        }

        private IEnumerator CrossFadeBgm(AudioClip next)
        {
            float targetVolume = SettingsManager.Instance != null ? SettingsManager.Instance.MusicVolume : 1f;

            if (_bgmSource.isPlaying)
            {
                yield return FadeVolume(_bgmSource.volume, 0f);
            }

            _bgmSource.clip = next;
            _bgmSource.Play();
            yield return FadeVolume(0f, targetVolume);
            _bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgm()
        {
            yield return FadeVolume(_bgmSource.volume, 0f);
            _bgmSource.Stop();
            ApplyVolumes();
            _bgmFadeRoutine = null;
        }

        private IEnumerator FadeVolume(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < BgmFadeDuration)
            {
                // 사건 연출은 게임 속도 배율의 영향을 받지 않아야 한다.
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / BgmFadeDuration));
                yield return null;
            }
            _bgmSource.volume = to;
        }

        // ===== SFX =====

        /// <summary>효과음 1회 재생. 이름이 비어 있거나 클립이 없으면 무시한다.</summary>
        public void PlaySfx(string sfxName, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(sfxName) || _sfxSource == null) return;

            AudioClip clip = LoadClip(SfxPathPrefix + sfxName, sfxName);
            if (clip == null) return;

            float baseVolume = SettingsManager.Instance != null ? SettingsManager.Instance.SfxVolume : 1f;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(baseVolume * volumeScale));
        }

        // ===== 내부 =====

        private AudioClip LoadClip(string resourcePath, string displayName)
        {
            if (_clipCache.TryGetValue(resourcePath, out AudioClip cached)) return cached;

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            _clipCache[resourcePath] = clip; // null도 캐시해 반복 로드를 막는다.

            if (clip == null && _missingLogged.Add(resourcePath))
            {
                Debug.Log($"[오디오] 클립이 없어 재생을 건너뜁니다: {resourcePath} (이름: {displayName})");
            }

            return clip;
        }
    }
}
