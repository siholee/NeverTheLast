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
    /// 인게임 메뉴(우상단 ≡ 또는 ESC). 계속하기 · 설정 · 저장 · 불러오기 · 저장 후 나가기 · 메인 메뉴로.
    ///
    /// 열려 있는 동안 시간을 멈춘다. 전투는 자동이라 메뉴를 보는 사이에 파티가 쓰러지면 안 된다.
    /// 저장은 <see cref="RunManager.CanSaveNow"/>가 허락하는 순간에만 된다 — 전투 중에 저장하면
    /// 쓰러진 아군이 저장본에서 빠지므로, 그때는 전투 직전 자동 저장을 그대로 둔다.
    /// </summary>
    public class PauseMenuScreen : ModalScreen
    {
        protected override string CanvasName => "PauseMenuCanvas";

        /// <summary>다른 모달(70대)보다 위, 설정·확인 창(90)보다 아래.</summary>
        protected override int SortingOrder => 85;

        protected override string Title => "메뉴";
        protected override string Caption => "MENU";
        protected override Vector2 AnchorMin => new(0.36f, 0.16f);
        protected override Vector2 AnchorMax => new(0.64f, 0.84f);
        protected override bool CloseOnBackdrop => true;

        private const float RowHeight = 48f;
        private const float RowGap = 10f;

        private readonly Func<SettingsUI> _settings;
        private Button _save;
        private Button _load;
        private TextMeshProUGUI _status;
        private float _resumeTimeScale = 1f;

        public PauseMenuScreen(Func<SettingsUI> settings)
        {
            _settings = settings;
        }

        protected override void Build()
        {
            int row = 0;
            AddRow("Resume", "계속하기", Hide, ref row, primary: true);
            AddRow("Settings", "설정", OpenSettings, ref row);
            _save = AddRow("Save", "저장", Save, ref row);
            _load = AddRow("Load", "불러오기", AskLoad, ref row);
            AddRow("SaveAndQuit", "저장 후 나가기", AskSaveAndQuit, ref row);
            AddRow("MainMenu", "메인 메뉴로 나가기", AskMainMenu, ref row);

            _status = UIBuild.Text("Status", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _status.rectTransform.sizeDelta = new Vector2(0f, 44f);
            _status.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        }

        private Button AddRow(string name, string label, Action onClick, ref int row, bool primary = false)
        {
            Button button = UIBuild.Button(name, Body, label, onClick, primary);
            RectTransform rect = button.image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -row * (RowHeight + RowGap));
            row++;
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
            SetEnabled(_load, SaveSystem.HasSave());

            if (message != null) _status.text = message;
            else if (!canSave) _status.text = "전투 중이거나 정산 중에는 저장할 수 없습니다.\n나가면 이 전투 직전 자동 저장에서 이어집니다.";
            else _status.text = "";
        }

        private static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.alpha = enabled ? 1f : 0.4f;
        }

        // ── 동작 ─────────────────────────────────────────────────────

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

        private void AskSaveAndQuit()
        {
            bool canSave = RunManager.Instance != null && RunManager.Instance.CanSaveNow;
            ConfirmDialog.Ask("저장 후 나가기",
                canSave
                    ? "지금 상태를 저장하고 게임을 종료합니다."
                    : "전투 중이라 지금 상태는 저장되지 않습니다.\n이 전투 직전 자동 저장을 남기고 게임을 종료합니다.",
                "나가기", () =>
                {
                    TrySave();
                    Time.timeScale = 1f;
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
        }

        private void AskMainMenu()
        {
            ConfirmDialog.Ask("메인 메뉴로",
                "저장하지 않고 메인 메뉴로 나갑니다.\n마지막 저장 이후의 진행은 사라지며, 메인 메뉴의 이어하기로 그 시점부터 다시 할 수 있습니다.",
                "나가기", () =>
                {
                    Time.timeScale = 1f;
                    GameManager.LoadMainMenuScene();
                });
        }
    }
}
