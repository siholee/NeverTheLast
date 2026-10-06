using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Managers
{
    /// <summary>하네스가 재생·병합·누락을 같은 형식으로 수집하는 효과음 기록.</summary>
    [Serializable]
    public sealed class SfxPlaybackEvent
    {
        public float realtime;
        public string cue;
        public string clip;
        public string mixerGroup;
        public bool played;
        public string reason;
        public float volume;
        public float pitch;
        public float timeScale;
    }

    /// <summary>
    /// BGM과 논리 SFX 큐를 재생한다. SFX는 풀링된 AudioSource를 쓰며 YAML의 동시 재생 수·최소 간격과
    /// 실제 시간 기준 배속 병합을 적용한다. AudioSource는 timeScale과 독립이므로 히트스톱 중에도 끝까지 난다.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const string BgmPathPrefix = "Audio/BGM/";
        private const string SfxPathPrefix = "Audio/Sfx/";
        private const string CueDataPath = "Data/95_sfx_cues";
        private const float BgmFadeDuration = 0.6f;

        private sealed class SfxVoice
        {
            public AudioSource Source;
            public string Cue;
            public string MixerGroup;
            public string VoiceGroup;
            public float CueVolume;
            public float EndsAt;
        }

        public static AudioManager Instance { get; private set; }
        public static event Action<SfxPlaybackEvent> SfxEventRecorded;

        private AudioSource _bgmSource;
        private string _currentBgmName;
        private Coroutine _bgmFadeRoutine;
        private Coroutine _duckRoutine;
        private float _bgmDuckMultiplier = 1f;

        private readonly Dictionary<string, AudioClip> _clipCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingLogged = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SfxCueData> _cues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _lastPlayedAt = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lastClipByCue = new(StringComparer.OrdinalIgnoreCase);
        private string _lastAvoidRepeatClip;
        private string _lastLokapalaVoiceClip;
        private readonly System.Random _lokapalaAudioRandom = new();
        private readonly Dictionary<string, float> _mixerGroupGain = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SFX"] = 1f,
            ["UI"] = 0.92f,
            ["VOICE"] = 1f,
        };
        private readonly List<SfxVoice> _voices = new();
        private SfxCueDataList _cueData;

        public static AudioManager EnsureExists()
        {
            if (Instance != null) return Instance;
            return new GameObject("AudioManager").AddComponent<AudioManager>();
        }

        /// <summary>인스턴스 유무와 관계없이 논리 큐를 재생하는 호출부용 진입점.</summary>
        public static void PlayCue(string cueName, float volumeScale = 1f)
            => EnsureExists().PlaySfx(cueName, volumeScale);

        /// <summary>유닛 ID·코드 슬롯을 사람이 읽는 큐 이름으로 바꿔 재생한다.</summary>
        public static void PlayAction(Entities.Unit unit, SfxActionSlot slot, SfxCuePhase phase,
            string codeName = null, float volumeScale = 1f)
        {
            string cue = SfxCueRoutes.Resolve(unit, slot, phase, codeName);
            if (!string.IsNullOrWhiteSpace(cue)) PlayCue(cue, volumeScale);
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
            LoadCueData();

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.ignoreListenerPause = true;

            int poolSize = Mathf.Clamp(_cueData?.poolSize ?? 18, 4, 48);
            for (int i = 0; i < poolSize; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.ignoreListenerPause = true;
                _voices.Add(new SfxVoice { Source = source });
            }

            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>설정 볼륨을 BGM과 현재 재생 중인 코드 믹서 그룹에 다시 곱한다.</summary>
        public void ApplyVolumes()
        {
            if (_bgmSource != null) _bgmSource.volume = MusicVolume * _bgmDuckMultiplier;
            foreach (SfxVoice voice in _voices)
            {
                if (voice.Source == null) continue;
                voice.Source.volume = Mathf.Clamp01(voice.CueVolume * SfxVolume * GroupGain(voice.MixerGroup));
            }
        }

        /// <summary>믹서 에셋을 들이기 전에도 SFX/UI/Voice 버스를 독립적으로 조정한다.</summary>
        public void SetMixerGroupVolume(string mixerGroup, float volume)
        {
            string key = NormalizeGroup(mixerGroup);
            _mixerGroupGain[key] = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        // ===== BGM =====

        public void PlayBgm(string bgmName, bool fade = true)
        {
            if (string.IsNullOrWhiteSpace(bgmName)) return;
            if (_currentBgmName == bgmName && _bgmSource != null && _bgmSource.isPlaying) return;

            AudioClip clip = LoadClip(BgmPathPrefix + bgmName);
            if (clip == null)
            {
                _currentBgmName = bgmName;
                WarnOnce("bgm:" + bgmName, $"[오디오] BGM 클립이 없어 건너뜁니다: {bgmName}");
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
            if (_bgmSource.isPlaying) yield return FadeVolume(_bgmSource.volume, 0f);
            _bgmSource.clip = next;
            _bgmSource.Play();
            yield return FadeVolume(0f, MusicVolume * _bgmDuckMultiplier);
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
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / BgmFadeDuration));
                yield return null;
            }
            _bgmSource.volume = to;
        }

        // ===== SFX =====

        /// <summary>논리 큐를 한 번 재생한다. 누락·병합도 하네스 이벤트로 남긴다.</summary>
        public void PlaySfx(string cueName, float volumeScale = 1f)
        {
            if (string.IsNullOrWhiteSpace(cueName)) return;
            string normalized = cueName.Trim();
            if (!_cues.TryGetValue(normalized, out SfxCueData cue))
            {
                Skip(normalized, null, null, "unknown_queue");
                WarnOnce("cue:" + normalized, $"[오디오] 등록되지 않은 SFX 큐를 건너뜁니다: {normalized}");
                return;
            }

            float now = Time.realtimeSinceStartup;
            float minInterval = Mathf.Max(0f, cue.minInterval);
            if (cue.speedMerge)
            {
                // 전장 내부 기준은 사용자 표시의 절반(8× → timeScale 4)이라 HUD의 단일 원천을 읽는다.
                float speed = Mathf.Clamp(UI.HUD.BattleHud.CurrentSpeedMultiplier, 1f, 8f);
                float floor1 = Mathf.Max(0f, _cueData?.speedMergeFloor ?? 0.04f);
                float floor8 = Mathf.Max(floor1, _cueData?.speedMergeAt8x ?? 0.12f);
                minInterval = Mathf.Max(minInterval, Mathf.Lerp(floor1, floor8, (speed - 1f) / 7f));
            }
            if (_lastPlayedAt.TryGetValue(normalized, out float last) && now - last < minInterval)
            {
                Skip(normalized, null, cue.mixerGroup, "interval_merge");
                return;
            }

            // 지정 보이스 묶음만 한 사람이 끝난 뒤 다음 사람을 허용한다.
            if (!string.IsNullOrWhiteSpace(cue.voiceGroup) || NormalizeGroup(cue.mixerGroup) == "VOICE")
                foreach (SfxVoice voice in _voices)
                    if ((voice.VoiceGroup == cue.voiceGroup && !string.IsNullOrWhiteSpace(cue.voiceGroup) ||
                         voice.VoiceGroup == "vedic_temporary" && NormalizeGroup(cue.mixerGroup) == "VOICE") && VoiceActive(voice, now))
                    {
                        Skip(normalized, null, cue.mixerGroup, "voice_group_busy");
                        return;
                    }

            int maxVoices = Mathf.Max(1, cue.maxVoices);
            int active = 0;
            foreach (SfxVoice voice in _voices)
                if (voice.Cue == normalized && VoiceActive(voice, now)) active++;
            if (active >= maxVoices)
            {
                Skip(normalized, null, cue.mixerGroup, "concurrent_limit");
                return;
            }

            AudioClip clip = PickClip(normalized, cue, out string clipName);
            if (clip == null)
            {
                string reason = cue.clips == null || cue.clips.Count == 0 ? "no_candidates" : "clip_missing";
                Skip(normalized, clipName, cue.mixerGroup, reason);
                WarnOnce("missing:" + normalized,
                    $"[오디오] SFX 큐에 재생 가능한 클립이 없어 건너뜁니다: {normalized}");
                return;
            }

            SfxVoice target = null;
            foreach (SfxVoice voice in _voices)
            {
                if (VoiceActive(voice, now)) continue;
                target = voice;
                break;
            }
            if (target == null)
            {
                Skip(normalized, clipName, cue.mixerGroup, "pool_full");
                return;
            }

            float volume = Mathf.Clamp01(AudioRange(normalized,
                Mathf.Min(cue.volumeMin, cue.volumeMax), Mathf.Max(cue.volumeMin, cue.volumeMax)) * volumeScale);
            float pitch = Mathf.Clamp(AudioRange(normalized,
                Mathf.Min(cue.pitchMin, cue.pitchMax), Mathf.Max(cue.pitchMin, cue.pitchMax)), 0.25f, 3f);
            string mixerGroup = NormalizeGroup(cue.mixerGroup);
            float audibleVolume = volume * SfxVolume * GroupGain(mixerGroup);
            if (audibleVolume <= 0.0001f)
            {
                Skip(normalized, clipName, mixerGroup, "muted", volume, pitch);
                return;
            }

            target.Cue = normalized;
            target.MixerGroup = mixerGroup;
            target.VoiceGroup = cue.voiceGroup;
            target.CueVolume = volume;
            target.EndsAt = now + clip.length / Mathf.Abs(pitch) + 0.02f;
            target.Source.Stop();
            target.Source.clip = clip;
            target.Source.pitch = pitch;
            target.Source.volume = Mathf.Clamp01(audibleVolume);
            target.Source.Play();
            _lastPlayedAt[normalized] = now;
            _lastClipByCue[normalized] = clipName;
            if (cue.avoidImmediateRepeat) _lastAvoidRepeatClip = clipName;
            if (IsLokapalaPresentation(normalized) && mixerGroup == "VOICE") _lastLokapalaVoiceClip = clipName;

            Record(normalized, clipName, mixerGroup, true, "played", volume, pitch);
            if (cue.duckBgm < 0.999f && cue.duckSeconds > 0f) BeginBgmDuck(cue.duckBgm, cue.duckSeconds);
        }

        // 새 연출 큐를 추가해도 전투의 치명타·상태 명중 난수열을 소비하지 않는다. 기존 큐의 동작은 유지한다.
        private static bool IsLokapalaPresentation(string cue)
            => cue.StartsWith("indra.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("vayu.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("yama.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("varuna.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("agni.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("surya.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("kubera.", StringComparison.OrdinalIgnoreCase) ||
               cue.StartsWith("chandra.moon_cycle.", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cue, "chandra.changya.resolve", StringComparison.OrdinalIgnoreCase);

        private float AudioRange(string cue, float min, float max)
            => IsLokapalaPresentation(cue)
                ? Mathf.Lerp(min, max, (float)_lokapalaAudioRandom.NextDouble())
                : UnityEngine.Random.Range(min, max);

        private AudioClip PickClip(string cueName, SfxCueData cue, out string selected)
        {
            selected = null;
            if (cue.clips == null || cue.clips.Count == 0) return null;
            int start = IsLokapalaPresentation(cueName)
                ? _lokapalaAudioRandom.Next(cue.clips.Count) : UnityEngine.Random.Range(0, cue.clips.Count);
            for (int i = 0; i < cue.clips.Count; i++)
            {
                string candidate = cue.clips[(start + i) % cue.clips.Count];
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                selected = candidate.Trim();
                if (cue.avoidImmediateRepeat && cue.clips.Count > 1 &&
                    ((string.IsNullOrWhiteSpace(cue.voiceGroup) && _lastClipByCue.TryGetValue(cueName, out string previous) &&
                      string.Equals(selected, previous, StringComparison.OrdinalIgnoreCase)) ||
                     string.Equals(selected, _lastAvoidRepeatClip, StringComparison.OrdinalIgnoreCase))) continue;
                if (IsLokapalaPresentation(cueName) && NormalizeGroup(cue.mixerGroup) == "VOICE" &&
                    string.Equals(selected, _lastLokapalaVoiceClip, StringComparison.OrdinalIgnoreCase)) continue;
                AudioClip clip = LoadClip(SfxPathPrefix + selected);
                if (clip != null) return clip;
            }
            return null;
        }

        private static bool VoiceActive(SfxVoice voice, float now)
            => voice.Source != null && (voice.Source.isPlaying || now < voice.EndsAt);

        private void BeginBgmDuck(float factor, float seconds)
        {
            if (_duckRoutine != null) StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckBgm(Mathf.Clamp(factor, 0.25f, 1f), seconds));
        }

        private IEnumerator DuckBgm(float factor, float seconds)
        {
            float start = _bgmDuckMultiplier;
            for (float age = 0f; age < 0.08f; age += Time.unscaledDeltaTime)
            {
                _bgmDuckMultiplier = Mathf.Lerp(start, factor, Mathf.Clamp01(age / 0.08f));
                ApplyBgmVolume();
                yield return null;
            }
            _bgmDuckMultiplier = factor;
            ApplyBgmVolume();
            float until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < until) yield return null;
            start = _bgmDuckMultiplier;
            for (float age = 0f; age < 0.22f; age += Time.unscaledDeltaTime)
            {
                _bgmDuckMultiplier = Mathf.Lerp(start, 1f, Mathf.Clamp01(age / 0.22f));
                ApplyBgmVolume();
                yield return null;
            }
            _bgmDuckMultiplier = 1f;
            ApplyBgmVolume();
            _duckRoutine = null;
        }

        private void ApplyBgmVolume()
        {
            if (_bgmSource != null) _bgmSource.volume = MusicVolume * _bgmDuckMultiplier;
        }

        // ===== 데이터와 기록 =====

        private void LoadCueData()
        {
            TextAsset asset = Resources.Load<TextAsset>(CueDataPath);
            if (asset == null)
            {
                _cueData = new SfxCueDataList();
                WarnOnce("cue-data", $"[오디오] {CueDataPath}.yaml이 없어 빈 SFX 카탈로그를 사용합니다.");
                return;
            }

            try
            {
                _cueData = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                    .Deserialize<SfxCueDataList>(asset.text) ?? new SfxCueDataList();
                foreach (SfxCueData cue in _cueData.cues ?? new List<SfxCueData>())
                {
                    if (cue == null || string.IsNullOrWhiteSpace(cue.name)) continue;
                    _cues[cue.name.Trim()] = cue;
                }
            }
            catch (Exception exception)
            {
                _cueData = new SfxCueDataList();
                Debug.LogError($"[오디오] SFX 큐 데이터를 읽지 못했습니다: {exception.Message}");
            }
        }

        private AudioClip LoadClip(string resourcePath)
        {
            if (_clipCache.TryGetValue(resourcePath, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            _clipCache[resourcePath] = clip;
            return clip;
        }

        private void Skip(string cue, string clip, string mixerGroup, string reason,
            float volume = 0f, float pitch = 1f)
            => Record(cue, clip, NormalizeGroup(mixerGroup), false, reason, volume, pitch);

        private static void Record(string cue, string clip, string mixerGroup, bool played, string reason,
            float volume, float pitch)
        {
            SfxEventRecorded?.Invoke(new SfxPlaybackEvent
            {
                realtime = Time.realtimeSinceStartup,
                cue = cue ?? "",
                clip = clip ?? "",
                mixerGroup = mixerGroup ?? "SFX",
                played = played,
                reason = reason ?? "",
                volume = volume,
                pitch = pitch,
                timeScale = Time.timeScale,
            });
        }

        private void WarnOnce(string key, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_missingLogged.Add(key)) Debug.LogWarning(message);
#endif
        }

        private float GroupGain(string mixerGroup)
            => _mixerGroupGain.TryGetValue(NormalizeGroup(mixerGroup), out float gain) ? gain : 1f;

        private static string NormalizeGroup(string mixerGroup)
            => string.IsNullOrWhiteSpace(mixerGroup) ? "SFX" : mixerGroup.Trim().ToUpperInvariant();

        private static float MusicVolume => SettingsManager.Instance != null ? SettingsManager.Instance.MusicVolume : 1f;
        private static float SfxVolume => SettingsManager.Instance != null ? SettingsManager.Instance.SfxVolume : 1f;
    }
}
