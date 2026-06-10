using Core;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 메인 메뉴 UI 컨트롤러.
    /// Inspector 와이어링 없이 GameObject.Find()로 버튼을 발견 (RotationBattle 동일 패턴).
    /// SceneBuilder가 생성한 오브젝트 이름: NewGameBtn, ContinueBtn, SettingsBtn, QuitBtn, SettingsPanel.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private Button     _continueBtn;
        private GameObject _settingsPanel;

        private void Awake()
        {
            // GetComponentInChildren(includeInactive: true)으로 비활성 SettingsPanel도 탐색.
            // GameObject.Find()는 비활성 오브젝트를 찾지 못하므로 이 방법이 유일하게 안전함.
            var settingsUI = GetComponentInChildren<SettingsUI>(true);
            _settingsPanel = settingsUI != null ? settingsUI.gameObject : null;
        }

        private void Start()
        {
            // ── 버튼 연결 (활성 오브젝트이므로 GameObject.Find 사용 가능) ─────
            var newGameBtn  = FindBtn("NewGameBtn");
            var infiniteBtn = FindBtn("InfiniteModeBtn");
            var continueBtn = FindBtn("ContinueBtn");
            var settingsBtn = FindBtn("SettingsBtn");
            var quitBtn     = FindBtn("QuitBtn");

            if (newGameBtn  != null) newGameBtn.onClick.AddListener(OnNewGame);
            if (infiniteBtn != null) infiniteBtn.onClick.AddListener(OnInfiniteMode);
            if (settingsBtn != null) settingsBtn.onClick.AddListener(OnSettings);
            if (quitBtn     != null) quitBtn.onClick.AddListener(OnQuit);

            _continueBtn = continueBtn;
            if (_continueBtn != null)
            {
                _continueBtn.onClick.AddListener(OnContinue);
                RefreshContinueBtn();
            }
        }

        // ── 이어하기 버튼 상태 갱신 ─────────────────────────────────────────────
        private void RefreshContinueBtn()
        {
            if (_continueBtn == null) return;
            bool hasSave = SaveSystem.HasSave();
            _continueBtn.interactable = hasSave;

            // 저장 없으면 텍스트 색 흐리게
            var lbl = _continueBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl != null)
                lbl.color = hasSave ? Color.white : new Color(0.45f, 0.45f, 0.45f);
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────────

        private void OnNewGame()
        {
            Debug.Log("[MainMenuUI] 새로운 여정 시작");
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            Managers.GameManager.LoadBattleScene();
        }

        private void OnContinue()
        {
            if (!SaveSystem.HasSave())
            {
                Debug.LogWarning("[MainMenuUI] 저장 데이터 없음");
                return;
            }
            Debug.Log("[MainMenuUI] 런 이어하기");
            GameStartIntent.Current = GameStartIntent.Intent.Continue;
            Managers.GameManager.LoadBattleScene();
        }

        private void OnInfiniteMode()
        {
            Debug.Log("[MainMenuUI] 무한 모드 시작");
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.InfiniteMode;
            Managers.GameManager.LoadBattleScene();
        }

        private void OnSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(true);
        }

        private void OnQuit()
        {
            Debug.Log("[MainMenuUI] 게임 종료");
            Application.Quit();
        }

        /// <summary>SettingsUI에서 뒤로 버튼 클릭 시 호출</summary>
        public void CloseSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────────
        private static Button FindBtn(string goName)
        {
            var go = GameObject.Find(goName);
            return go != null ? go.GetComponent<Button>() : null;
        }
    }
}
