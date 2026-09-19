using System;
using Core;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 인게임 메뉴(ESC 또는 우상단 ≡ — 둘은 같은 입력이다).
    ///   계속하기 · 설정 · 자료실 · 저장 · 불러오기 · 저장 후 나가기 · 나가기 · 바탕화면으로 나가기
    ///
    /// TAB 캐릭터 창과는 별개의 창이다. 캐릭터 창이 떠 있어도 ESC는 그것을 닫지 않고
    /// 그 <b>위에</b> 메뉴를 띄운다(<see cref="UITheme.LayerMenu"/>). 메뉴를 닫으면 캐릭터 창으로 돌아온다.
    ///
    ///   저장하고 메인 메뉴로   — 저장하고 메인 메뉴로
    ///   저장 없이 메인 메뉴로  — 저장하지 않고 메인 메뉴로
    ///   게임 종료             — 저장하지 않고 게임 종료
    ///
    /// 나가기 셋은 예전에 "저장 후 나가기 · 나가기 · 바탕화면으로 나가기"였다. 이름만으로는
    /// 무엇이 저장되고 어디로 가는지 갈리지 않는다는 QA가 있어, 이름에 결과를 적고
    /// 버튼 오른쪽에 한 줄 설명을 붙였다. "나가기" 묶음은 머리글로 따로 떼었다.
    ///
    /// 열려 있는 동안 시간을 멈춘다. 전투는 자동이라 메뉴를 보는 사이에 파티가 쓰러지면 안 된다.
    /// 저장은 <see cref="RunManager.CanSaveNow"/>가 허락하는 순간에만 된다 — 전투 중에 저장하면
    /// 쓰러진 아군이 저장본에서 빠지므로, 그때는 전투 직전 자동 저장을 그대로 둔다.
    /// </summary>
    public class PauseMenuScreen : ModalScreen
    {
        protected override string CanvasName => "PauseMenuCanvas";

        /// <summary>TAB 캐릭터 창보다 위, 자료실·설정·확인 창보다 아래.</summary>
        protected override int SortingOrder => UITheme.LayerMenu;

        protected override string Title => "메뉴";
        protected override string Caption => "MENU";
        protected override Vector2 AnchorMin => new(0.32f, 0.08f);
        protected override Vector2 AnchorMax => new(0.68f, 0.92f);
        protected override bool CloseOnBackdrop => true;

        private const float RowHeight = 50f;
        private const float RowGap = 8f;

        private readonly Func<SettingsUI> _settings;
        private readonly WikiScreen _wiki;
        private Button _save;
        private Button _load;
        private Button _saveAndExit;
        private TextMeshProUGUI _status;
        private float _resumeTimeScale = 1f;

        public PauseMenuScreen(Func<SettingsUI> settings, WikiScreen wiki)
        {
            _settings = settings;
            _wiki = wiki;
        }

        protected override void Build()
        {
            float y = 0f;
            AddRow("Resume", "계속하기", "ESC", Hide, ref y, primary: true);
            AddRow("Settings", "설정", "소리 · 글자 크기", OpenSettings, ref y);
            AddRow("Wiki", "자료실", "캐릭터 · 코드 · 장비 · 적", OpenWiki, ref y);
            _save = AddRow("Save", "저장", "지금 상태를 저장", Save, ref y);
            _load = AddRow("Load", "불러오기", "마지막 저장으로 되돌리기", AskLoad, ref y);

            y += 10f;
            TextMeshProUGUI heading = UIBuild.Label("ExitHeading", Body, "나가기", UITheme.FontCaption, UITheme.Accent);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 24f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += 30f;

            _saveAndExit = AddRow("SaveAndExit", "저장하고 메인 메뉴로", "이어하기로 여기서 계속", AskSaveAndExit, ref y);
            AddRow("Exit", "저장 없이 메인 메뉴로", "마지막 저장 이후는 사라짐", AskExit, ref y);
            AddRow("Desktop", "게임 종료", "저장하지 않고 끔", AskDesktop, ref y);

            _status = UIBuild.Text("Status", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _status.rectTransform.sizeDelta = new Vector2(0f, 44f);
            _status.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        }

        /// <summary>메뉴 한 줄. 왼쪽에 이름, 오른쪽에 누르면 무엇이 되는지 한 줄.</summary>
        private Button AddRow(string name, string label, string description, Action onClick, ref float y,
            bool primary = false)
        {
            Button button = UIBuild.Button(name, Body, label, onClick, primary);
            RectTransform rect = button.image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -y);
            y += RowHeight + RowGap;

            TextMeshProUGUI title = button.GetComponentInChildren<TextMeshProUGUI>();
            title.alignment = TextAlignmentOptions.MidlineLeft;
            UIBuild.Stretch(title.rectTransform, 18f, 4f);

            if (!string.IsNullOrEmpty(description))
            {
                TextMeshProUGUI hint = UIBuild.Text("Description", button.transform, description, UITheme.FontCaption,
                    primary ? new Color(UITheme.TextOnAccent.r, UITheme.TextOnAccent.g, UITheme.TextOnAccent.b, 0.72f) : UITheme.TextMuted,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Stretch(hint.rectTransform, 18f, 4f);
            }

            return button;
        }

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _resumeTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            Refresh(null);
        }

        public override void Hide()
        {
            if (!IsVisible) return;
            _settings()?.Close();
            _wiki?.Hide();
            base.Hide();
            Time.timeScale = _resumeTimeScale;
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        /// <summary>지금 할 수 있는 것만 켜고, 못 하는 이유를 아래에 적는다.</summary>
        private void Refresh(string message)
        {
            bool canSave = RunManager.Instance != null && RunManager.Instance.CanSaveNow;
            SetEnabled(_save, canSave);
            SetEnabled(_saveAndExit, canSave);
            SetEnabled(_load, SaveSystem.HasSave());

            if (message != null) _status.text = message;
            else if (!canSave) _status.text = "전투 중이거나 정산 중에는 저장할 수 없습니다.\n나가면 이 전투 직전 자동 저장에서 이어집니다.";
            else _status.text = "";
        }

        /// <summary>꺼진 모습은 UIButtonStyle이 그린다.</summary>
        private static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
        }

        // ── 동작 ─────────────────────────────────────────────────────

        /// <summary>메뉴는 열어 둔 채 위에 띄운다. 닫으면 메뉴로 돌아오고, 시간은 계속 멈춰 있다.</summary>
        private void OpenWiki()
        {
            _wiki?.Show();
        }

        private void OpenSettings()
        {
            _settings()?.Open();
        }

        private void Save()
        {
            if (!TrySave())
            {
                Refresh(null);
                return;
            }
            Refresh("저장했습니다.");
        }

        private static bool TrySave()
        {
            RunManager run = RunManager.Instance;
            if (run == null || !run.CanSaveNow) return false;
            run.SaveCurrentRun();
            return true;
        }

        private void AskLoad()
        {
            ConfirmDialog.Ask("불러오기",
                "마지막으로 저장한 시점으로 돌아갑니다.\n그 뒤의 진행은 사라집니다.",
                "불러오기", () =>
                {
                    Time.timeScale = 1f;
                    GameStartIntent.Current = GameStartIntent.Intent.Continue;
                    GameManager.LoadBattleScene();
                });
        }

        /// <summary>저장하고 메인 메뉴로. 저장할 수 없는 때(전투 중)에는 버튼이 꺼져 있다.</summary>
        private void AskSaveAndExit()
        {
            ConfirmDialog.Ask("저장하고 메인 메뉴로",
                "지금 상태를 저장하고 메인 메뉴로 나갑니다.\n메인 메뉴의 이어하기로 여기서부터 계속할 수 있습니다.",
                "나가기", () =>
                {
                    if (!TrySave())
                    {
                        Refresh(null);
                        return;
                    }

                    Time.timeScale = 1f;
                    GameManager.LoadMainMenuScene();
                });
        }

        private void AskExit()
        {
            ConfirmDialog.Ask("저장 없이 메인 메뉴로",
                "저장하지 않고 메인 메뉴로 나갑니다.\n마지막 저장 이후의 진행은 사라지며, 메인 메뉴의 이어하기로 그 시점부터 다시 할 수 있습니다.",
                "나가기", () =>
                {
                    Time.timeScale = 1f;
                    GameManager.LoadMainMenuScene();
                });
        }

        /// <summary>
        /// 저장하지 않고 게임을 끈다. 저장이 필요하면 [저장] 또는 [저장 후 나가기]를 먼저 쓴다 —
        /// 여기서 몰래 저장하면 "그냥 끄기"를 고른 사람의 저장본을 덮어쓰게 된다.
        /// </summary>
        private void AskDesktop()
        {
            ConfirmDialog.Ask("게임 종료",
                "저장하지 않고 게임을 종료합니다.\n마지막 저장 이후의 진행은 사라집니다.",
                "종료", () =>
                {
                    Time.timeScale = 1f;
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
        }
    }
}
