using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 메인 메뉴. 씬에 배치된 예전 버튼들을 쓰지 않고 이 컴포넌트가 직접 조립한다.
    ///
    /// 씬(MainMenu.unity)에는 아직 구형 UI 오브젝트가 남아 있으므로,
    /// 시작할 때 그 캔버스들을 꺼서 새 메뉴만 보이게 한다.
    /// 씬을 정리한 뒤에는 <see cref="HideLegacySceneUI"/>를 지워도 된다.
    ///
    /// 클래스 이름은 MainMenu 씬이 참조하므로 유지한다.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private SettingsUI _settings;
        private Button _continueButton;

        private void Start()
        {
            HideLegacySceneUI();
            // 설정 화면은 이 오브젝트가 직접 들고 있는다(구형 씬 오브젝트에 의존하지 않음).
            _settings = GetComponent<SettingsUI>() ?? gameObject.AddComponent<SettingsUI>();
            BuildMenu();
        }

        /// <summary>씬에 남아 있는 구형 UI 캔버스를 끈다.</summary>
        private void HideLegacySceneUI()
        {
            // 이 컴포넌트가 구형 캔버스 하위에 있으면 함께 꺼져버리므로 먼저 루트로 옮긴다.
            transform.SetParent(null, false);

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                canvas.gameObject.SetActive(false);
            }
        }

        private void BuildMenu()
        {
            Canvas canvas = UIBuild.Canvas("MainMenuCanvas", 10);

            // 배경: 위는 거의 검정, 아래로 갈수록 살짝 밝아지는 무채색.
            Image background = UIBuild.Solid("Background", canvas.transform, new Color(0.043f, 0.047f, 0.051f));
            UIBuild.Stretch(background.rectTransform);

            // 왼쪽 세로 앰버 라인 — 명일방주식 화면 분할.
            Image rail = UIBuild.Solid("Rail", canvas.transform, UITheme.Accent);
            UIBuild.Pin(rail.rectTransform, new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(96f, 0f));
            rail.rectTransform.anchorMax = new Vector2(0f, 1f);
            rail.rectTransform.sizeDelta = new Vector2(4f, 0f);

            TextMeshProUGUI title = UIBuild.Text("Title", canvas.transform, "NEVER THE LAST",
                UITheme.FontDisplay * 1.6f, UITheme.TextPrimary);
            title.characterSpacing = 14f;
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(900f, 70f),
                new Vector2(132f, -140f));

            TextMeshProUGUI subtitle = UIBuild.Label("Subtitle", canvas.transform,
                "AUTO BATTLE  ·  ROGUELITE", UITheme.FontCaption, UITheme.TextMuted);
            UIBuild.Pin(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(700f, 20f),
                new Vector2(136f, -216f));

            float y = -300f;
            MakeMenuButton(canvas.transform, "NewGameBtn", "새로운 여정", ref y, OnNewGame, primary: true);
            _continueButton = MakeMenuButton(canvas.transform, "ContinueBtn", "이어하기", ref y, OnContinue);
            MakeMenuButton(canvas.transform, "InfiniteModeBtn", "무한 모드", ref y, OnInfiniteMode);
            MakeMenuButton(canvas.transform, "SettingsBtn", "설정", ref y, OnSettings);
            MakeMenuButton(canvas.transform, "QuitBtn", "종료", ref y, OnQuit);

            RefreshContinueButton();
        }

        private static Button MakeMenuButton(Transform parent, string name, string text, ref float y,
            System.Action onClick, bool primary = false)
        {
            Button button = UIBuild.Button(name, parent, text, onClick, primary, UITheme.FontHeading);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 52f),
                new Vector2(132f, y));

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(20f, label.rectTransform.offsetMin.y);
            }

            y -= 64f;
            return button;
        }

        private void RefreshContinueButton()
        {
            if (_continueButton == null) return;

            bool hasSave = SaveSystem.HasSave();
            _continueButton.interactable = hasSave;

            TextMeshProUGUI label = _continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = hasSave ? UITheme.TextPrimary : UITheme.TextMuted;
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────

        private static void OnNewGame()
        {
            Debug.Log("[MainMenuUI] 새로운 여정 시작");
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            GameManager.LoadBattleScene();
        }

        private static void OnContinue()
        {
            if (!SaveSystem.HasSave())
            {
                Debug.LogWarning("[MainMenuUI] 저장 데이터 없음");
                return;
            }

            Debug.Log("[MainMenuUI] 런 이어하기");
            GameStartIntent.Current = GameStartIntent.Intent.Continue;
            GameManager.LoadBattleScene();
        }

        private static void OnInfiniteMode()
        {
            Debug.Log("[MainMenuUI] 무한 모드 시작");
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.InfiniteMode;
            GameManager.LoadBattleScene();
        }

        private void OnSettings()
        {
            _settings?.Open();
        }

        private static void OnQuit()
        {
            Debug.Log("[MainMenuUI] 게임 종료");
            Application.Quit();
        }

        /// <summary>구형 호출 경로 호환.</summary>
        public void CloseSettings()
        {
            _settings?.Close();
        }
    }
}
