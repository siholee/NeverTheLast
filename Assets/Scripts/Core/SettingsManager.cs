using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 싱글톤 설정 매니저. 화면(표시 방식 · 해상도 · 수직 동기화 · 프레임 제한),
    /// 인터페이스(글자 크기 · HUD 크기), 소리(음악 · 효과음)를 PlayerPrefs에 저장/불러오기.
    /// DontDestroyOnLoad — 씬 전환 후에도 유지.
    ///
    /// 게임 시작 때 스스로 만들어진다(<see cref="Bootstrap"/>). 예전에는 Game 씬의 GameManager가
    /// 만들어 주었기 때문에 메인 메뉴에서 연 설정은 저장할 곳이 없었고, 화면 설정도 적용할 시점이 없었다.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string KeyMusic = "NTL_MusicVol";
        private const string KeySfx   = "NTL_SfxVol";
        private const string KeyTextScale = "NTL_TextScale";
        private const string KeyHudScale = "NTL_HudScale";
        private const string KeyDisplayMode = "NTL_DisplayMode";
        private const string KeyWidth = "NTL_ResWidth";
        private const string KeyHeight = "NTL_ResHeight";
        private const string KeyVSync = "NTL_VSync";
        private const string KeyFrameLimit = "NTL_FrameLimit";

        // ── 인터페이스 ───────────────────────────────────────────────

        /// <summary>
        /// 글자 크기 단계. 2560×1440에서도 글자가 작다는 QA에 따라 설정에서 고를 수 있게 했다.
        /// 캔버스 전체를 키우지 않고 글자만 키운다 — 패널 폭이 기준 해상도에 맞춰 계산된 화면이 많아
        /// 캔버스를 키우면 칸이 화면 밖으로 밀린다. 글자는 자기 칸 안에서 자동 맞춤으로 커진다.
        /// </summary>
        public static readonly float[] TextScaleSteps = { 1f, 1.15f, 1.3f };
        public static readonly string[] TextScaleNames = { "보통", "크게", "아주 크게" };

        /// <summary>
        /// HUD 크기 단계. 전투 중 늘 떠 있는 것(상단 바 · 행동 순서 · 준비 바 · 툴팁)만 키운다.
        /// 모달 화면은 기준 해상도로 칸을 나누므로 여기에 들지 않는다 — 글자 크기로 키운다.
        /// 1.15를 넘기면 1080p에서 상단 자원 띠와 우상단 바가 겹친다.
        /// </summary>
        public static readonly float[] HudScaleSteps = { 0.9f, 1f, 1.15f };
        public static readonly string[] HudScaleNames = { "작게", "보통", "크게" };

        /// <summary>글자 크기가 바뀌었다. 이미 만든 글자들이 이것을 듣고 다시 잰다.</summary>
        public static event System.Action TextScaleChanged;

        /// <summary>HUD 크기가 바뀌었다. HUD 캔버스와 전장 카메라가 다시 맞춘다.</summary>
        public static event System.Action HudScaleChanged;

        // ── 화면 ─────────────────────────────────────────────────────

        public enum DisplayMode
        {
            /// <summary>테두리 없는 전체 화면. 알트탭이 빠르고 모니터 해상도를 바꾸지 않는다.</summary>
            Borderless = 0,

            /// <summary>전용 전체 화면. 윈도우에서만 진짜 독점이고 다른 OS에서는 테두리 없는 창으로 떨어진다.</summary>
            Exclusive = 1,

            /// <summary>창 모드. 창 크기를 바꿀 수 있다.</summary>
            Windowed = 2,
        }

        public static readonly string[] DisplayModeNames = { "테두리 없는 전체 화면", "전체 화면", "창 모드" };

        /// <summary>프레임 제한. 0은 제한 없음. 수직 동기화가 켜져 있으면 모니터 주사율이 대신 정한다.</summary>
        public static readonly int[] FrameLimitSteps = { 30, 60, 120, 144, 240, 0 };

        public static string FrameLimitName(int limit) => limit <= 0 ? "제한 없음" : $"{limit} FPS";

        private float _musicVolume = 1f;
        private float _sfxVolume   = 1f;
        private int _textScaleIndex;
        private int _hudScaleIndex = 1;

        public DisplayMode Mode { get; private set; } = DisplayMode.Borderless;
        public int ResolutionWidth { get; private set; }
        public int ResolutionHeight { get; private set; }
        public bool VSync { get; private set; } = true;
        public int FrameLimit { get; private set; } = 60;

        public int TextScaleIndex
        {
            get => _textScaleIndex;
            set
            {
                int next = Mathf.Clamp(value, 0, TextScaleSteps.Length - 1);
                if (next == _textScaleIndex) return;
                _textScaleIndex = next;
                TextScaleChanged?.Invoke();
            }
        }

        public float TextScale => TextScaleSteps[Mathf.Clamp(_textScaleIndex, 0, TextScaleSteps.Length - 1)];

        public int HudScaleIndex
        {
            get => _hudScaleIndex;
            set
            {
                int next = Mathf.Clamp(value, 0, HudScaleSteps.Length - 1);
                if (next == _hudScaleIndex) return;
                _hudScaleIndex = next;
                HudScaleChanged?.Invoke();
            }
        }

        public float HudScale => HudScaleSteps[Mathf.Clamp(_hudScaleIndex, 0, HudScaleSteps.Length - 1)];

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

        /// <summary>첫 씬이 뜨자마자 설정을 불러와 화면 모드까지 적용한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("SettingsManager").AddComponent<SettingsManager>();
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
            PlayerPrefs.SetInt(KeyTextScale, _textScaleIndex);
            PlayerPrefs.SetInt(KeyHudScale, _hudScaleIndex);
            PlayerPrefs.SetInt(KeyDisplayMode, (int)Mode);
            PlayerPrefs.SetInt(KeyWidth, ResolutionWidth);
            PlayerPrefs.SetInt(KeyHeight, ResolutionHeight);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyFrameLimit, FrameLimit);
            PlayerPrefs.Save();
        }

        public void LoadSettings()
        {
            _musicVolume = PlayerPrefs.GetFloat(KeyMusic, 1f);
            _sfxVolume   = PlayerPrefs.GetFloat(KeySfx,   1f);
            _textScaleIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyTextScale, 0), 0, TextScaleSteps.Length - 1);
            _hudScaleIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyHudScale, 1), 0, HudScaleSteps.Length - 1);
            ApplyMusicVolume();
            ApplySfxVolume();

            // 화면 설정은 저장된 적이 있을 때만 적용한다. 처음 켠 사람은 유니티가 잡은 기본값
            // (모니터 해상도의 테두리 없는 전체 화면)을 그대로 쓴다.
            DisplayMode current = Screen.fullScreenMode switch
            {
                FullScreenMode.Windowed => DisplayMode.Windowed,
                FullScreenMode.ExclusiveFullScreen => DisplayMode.Exclusive,
                _ => DisplayMode.Borderless,
            };
            Mode = (DisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(KeyDisplayMode, (int)current), 0, 2);
            ResolutionWidth = PlayerPrefs.GetInt(KeyWidth, Screen.width);
            ResolutionHeight = PlayerPrefs.GetInt(KeyHeight, Screen.height);
            VSync = PlayerPrefs.GetInt(KeyVSync, 1) == 1;
            FrameLimit = PlayerPrefs.GetInt(KeyFrameLimit, 60);

            ApplyFrameRate();
            if (PlayerPrefs.HasKey(KeyDisplayMode) && !Application.isEditor) ApplyDisplay();
        }

        // ── 화면 적용 ────────────────────────────────────────────────

        /// <summary>
        /// 고를 수 있는 해상도. 모니터가 알려 준 목록에서 주사율만 다른 중복을 지우고 1280×720 이상만 남긴다.
        /// 창 모드에서 쓰는 흔한 크기가 목록에 없으면(창 모드 전용 모니터 등) 채워 넣는다.
        /// </summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var sizes = new HashSet<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width >= 1280 && resolution.height >= 720)
                    sizes.Add(new Vector2Int(resolution.width, resolution.height));
            }

            Resolution native = Screen.currentResolution;
            foreach (Vector2Int common in new[]
                     {
                         new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
                         new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
                     })
            {
                if (common.x <= Mathf.Max(native.width, 1280) && common.y <= Mathf.Max(native.height, 720))
                    sizes.Add(common);
            }

            return sizes.OrderBy(size => size.x * size.y).ThenBy(size => size.x).ToList();
        }

        /// <summary>
        /// 표시 방식 · 해상도를 한 번에 바꾼다. <paramref name="save"/>가 false면 화면에만 시험 적용한다.
        /// 설정 화면은 [유지] 확인 전까지 PlayerPrefs를 건드리지 않아, 잘못된 해상도에서 종료돼도
        /// 다음 실행은 마지막으로 확정한 화면 설정으로 돌아온다.
        /// </summary>
        public void SetDisplay(DisplayMode mode, int width, int height, bool save = true)
        {
            Mode = mode;
            ResolutionWidth = Mathf.Max(640, width);
            ResolutionHeight = Mathf.Max(360, height);
            ApplyDisplay();
            if (save) SaveSettings();
        }

        public void SetVSync(bool on)
        {
            VSync = on;
            ApplyFrameRate();
        }

        public void SetFrameLimit(int limit)
        {
            FrameLimit = Mathf.Max(0, limit);
            ApplyFrameRate();
        }

        private void ApplyDisplay()
        {
            FullScreenMode mode = Mode switch
            {
                DisplayMode.Windowed => FullScreenMode.Windowed,
                DisplayMode.Exclusive when Application.platform == RuntimePlatform.WindowsPlayer =>
                    FullScreenMode.ExclusiveFullScreen,
                _ => FullScreenMode.FullScreenWindow,
            };

            Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
        }

        private void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            // 수직 동기화가 켜져 있으면 targetFrameRate는 무시된다. 끈 경우에만 제한이 뜻을 가진다.
            Application.targetFrameRate = VSync ? -1 : (FrameLimit <= 0 ? -1 : FrameLimit);
        }

        /// <summary>
        /// 음량은 AudioManager가 음원마다 나눠 건다(배경음 · 효과음).
        /// 예전에는 음악 음량을 AudioListener 전체에 걸어, 음악을 줄이면 효과음까지 같이 작아졌고
        /// 배경음은 두 번 곱해졌다. 효과음 슬라이더는 아무 데도 닿지 않았다.
        /// </summary>
        private void ApplyMusicVolume()
        {
            AudioListener.volume = 1f;
            Managers.AudioManager.Instance?.ApplyVolumes();
        }

        private void ApplySfxVolume()
        {
            Managers.AudioManager.Instance?.ApplyVolumes();
        }
    }
}
