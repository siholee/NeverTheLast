using Core;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>캐릭터 스탠딩과 비대칭 작전 타일을 분리한 밝은 로비.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        private SettingsUI _settings;
        private Screens.WikiScreen _wiki;
        private CanvasGroup _lobbyContent;
        private void Start()
        {
            transform.SetParent(null, false);
            _settings = GetComponent<SettingsUI>();
            if (_settings == null) _settings = gameObject.AddComponent<SettingsUI>();
            Canvas runtime = BuildMenu();

            // 씬의 구형 캔버스와 같은 이름을 재사용하면 UIBuild가 비활성 캔버스를 돌려줘
            // 새 밝은 메뉴가 전부 보이지 않았다. 컴포넌트가 붙은 루트는 살리고 렌더만 끈다.
            foreach (Canvas old in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (old == runtime) continue;
                old.enabled = false;
                GraphicRaycaster raycaster = old.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }
            Screens.CharacterUnlockDialog.ShowPending();
        }

        // 로비는 창 안의 버튼 목록이 아니라 원화 + 독립된 작전 타일이다.
        // 명일방주의 비대칭 탐색 구조를 참고하되 실제 여정/자료실/설정 기능만 배치한다.
        private Canvas BuildMenu()
        {
            Canvas canvas = UIBuild.Canvas("MainMenuBrightCanvas", 10);
            canvas.gameObject.SetActive(true);
            Image sky = UIBuild.Solid("SkyCitadel", canvas.transform, Color.white);
            sky.sprite = UIChromeArt.MainMenuBackground;
            sky.type = Image.Type.Simple;
            sky.raycastTarget = false;
            UIBuild.Stretch(sky.rectTransform);
            RectTransform root = UIBuild.SafeArea(canvas);
            var content = UIBuild.Container("LobbyContent", root);
            UIBuild.Stretch(content);
            _lobbyContent = UIBuild.Group(content.gameObject);
            root = content;

            Sprite sei = SpriteResource.LoadStanding("SEI_STANDING") ?? SpriteResource.LoadStanding("SHI_STANDING");
            Image character = UIBuild.Solid("LobbyCharacter", root, Color.white);
            character.sprite = sei;
            // Solid의 기본 Sliced는 preserveAspect를 무시한다. 원화는 반드시 Simple이다.
            character.type = Image.Type.Simple;
            character.preserveAspect = true;
            character.raycastTarget = false;
            character.enabled = sei != null;
            UIBuild.Anchor(character.rectTransform, new Vector2(.12f, -.08f), new Vector2(.60f, .99f));

            Image brandLight = UIBuild.Solid("BrandLight", root, Color.white);
            brandLight.sprite = UIShapes.Glow(128, new Color(1f, 1f, 1f, .85f));
            brandLight.type = Image.Type.Simple;
            brandLight.raycastTarget = false;
            UIBuild.Anchor(brandLight.rectTransform, new Vector2(-.06f, .79f), new Vector2(.31f, 1.04f));
            Copy(root, "Brand", "NEVER THE LAST", 28f, UITheme.Accent, .045f, .895f, .35f, .965f, true);
            Copy(root, "Edition", "신들의 기록 · 끝나지 않은 여정", 16f, UITheme.TextSecondary, .046f, .867f, .35f, .905f);
            Image brandRule = UIBuild.Divider("BrandRule", root);
            brandRule.raycastTarget = false;
            UIBuild.Anchor(brandRule.rectTransform, new Vector2(.046f, .85f), new Vector2(.155f, .85f));
            brandRule.rectTransform.sizeDelta = new Vector2(0f, 2f);

            Image identityLight = UIBuild.Solid("IdentityLight", root, Color.white);
            identityLight.sprite = UIShapes.Glow(128, new Color(1f, 1f, 1f, .84f));
            identityLight.type = Image.Type.Simple;
            identityLight.raycastTarget = false;
            UIBuild.Anchor(identityLight.rectTransform, new Vector2(-.04f, .09f), new Vector2(.29f, .43f));
            Copy(root, "CharacterIndex", "함께하는 동료", 17f, UITheme.Accent, .05f, .305f, .25f, .345f, true);
            Copy(root, "Name", "세이", 38f, UITheme.TextPrimary, .05f, .235f, .26f, .305f, true);
            Copy(root, "Role", "대지를 고르는 수호자", 18f, UITheme.TextSecondary, .052f, .20f, .28f, .242f);

            RunSaveData save = SaveSystem.HasSave() ? SaveSystem.LoadRun() : null;
            string continuation = save == null ? "저장된 여정이 없습니다" :
                $"스테이지 {save.currentRound} · {(Mathf.Max(1, save.currentStage) - 1) % RoundManager.EncountersPerTheme + 1}라운드  /  목숨 {save.life}";
            RectTransform terminal = UIBuild.Container("LobbyTerminal", root);
            UIBuild.Anchor(terminal, new Vector2(.57f, .235f), new Vector2(.955f, .74f));
            Copy(terminal, "TerminalLabel", "여정", 18f, UITheme.Accent, 0f, 1.035f, .5f, 1.105f, true);
            Copy(terminal, "TerminalMode", "무한 모드", 16f, UITheme.TextSecondary, .77f, 1.035f, 1f, 1.105f);
            terminal.Find("TerminalMode").GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
            // 저장 유무에 따라 가장 큰 타일에는 지금 실행 가능한 주 행동을 둔다.
            LobbyTile(terminal, save != null ? "Continue" : "NewRun",
                save != null ? "여정 이어가기" : "새로운 여정",
                save != null ? continuation : "해금한 동료 최대 다섯과 함께", UIIcon.Route,
                save != null ? OnContinue : OnNewGame, 0f, .59f, 1f, 1f, true, true);
            LobbyTile(terminal, save != null ? "NewRun" : "Continue",
                save != null ? "새로운 여정" : "여정 이어가기",
                save != null ? "새로운 편성으로 출발" : continuation, UIIcon.Party,
                save != null ? OnNewGame : OnContinue, 0f, .285f, .57f, .56f, false, save != null);
            LobbyTile(terminal, "Wiki", "자료실", "전투 · 동료 · 코드", UIIcon.Guide,
                () => (_wiki ??= new Screens.WikiScreen()).ShowArticle("home"),
                .59f, .285f, 1f, .56f, false, true);
            LobbyTile(terminal, "Settings", "설정", "화면과 사운드", UIIcon.Menu,
                OnSettings, 0f, .08f, .57f, .255f, false, true, true);
            Button quit = LobbyTile(terminal, "Quit", "게임 종료", "", UIIcon.Close,
                OnQuit, .59f, .08f, 1f, .255f, false, true, true);
            quit.gameObject.SetActive(!Application.isMobilePlatform);
            Copy(root, "Version", $"NEVER THE LAST   /   DEMO {Application.version}", 14f,
                UITheme.TextSecondary, .046f, .035f, .45f, .075f);
            return canvas;
        }

        private static void Copy(Transform parent, string key, string text, float size, Color ink,
            float left, float bottom, float right, float top, bool bold = false)
        {
            var label = UIBuild.Text(key, parent, text, size, ink);
            if (bold) label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            UIBuild.Anchor(label.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
        }

        private static Button LobbyTile(Transform parent, string key, string title, string description,
            UIIcon icon, System.Action action, float left, float bottom, float right, float top,
            bool primary, bool enabled, bool compact = false)
        {
            Button button = UIBuild.Button(key, parent, "", action, primary);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
            button.image.sprite = UIShapes.RoundedRect(3, primary ? UITheme.Accent : new Color(.97f, .985f, 1f, .96f),
                primary ? Color.clear : UITheme.MetalSilver, primary ? 0 : 1);
            button.interactable = enabled;
            UIBuild.Elevate(button.image, 8, .12f, 5);
            Color ink = primary ? UITheme.TextOnAccent : enabled ? UITheme.TextPrimary : UITheme.TextDisabled;
            // 큰 기호는 주 행동의 오른쪽 여백만 쓰고 본문과 겹치지 않는다.
            Image motif = UIBuild.Icon("TileMotif", button.transform, icon,
                primary ? new Color(ink.r, ink.g, ink.b, .18f)
                    : new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, enabled ? .52f : .20f));
            motif.sprite = UILineIcons.Get(icon, 192);
            UIBuild.Anchor(motif.rectTransform, new Vector2(.73f, .20f), new Vector2(.95f, .80f));
            Copy(button.transform, "Heading", title, primary ? 32f : compact ? 22f : 25f, ink,
                .055f, compact ? .30f : .41f, .85f, compact ? .83f : .82f, true);
            if (!string.IsNullOrEmpty(description))
                Copy(button.transform, "Description", description, primary ? 18f : 16f, ink,
                    .055f, compact ? .13f : .16f, .91f, compact ? .35f : .41f);
            if (primary)
            {
                Image arrow = UIBuild.Icon("Arrow", button.transform, UIIcon.Arrow, ink);
                UIBuild.Anchor(arrow.rectTransform, new Vector2(.925f, .73f), new Vector2(.965f, .91f));
                Image line = UIBuild.Solid("TileAccent", button.transform, new Color(.64f, .84f, .96f, .9f));
                line.raycastTarget = false;
                UIBuild.Anchor(line.rectTransform, Vector2.zero, new Vector2(1f, 0f));
                line.rectTransform.sizeDelta = new Vector2(0f, 3f);
            }
            return button;
        }

        private static void OnNewGame()
        {
            if (SaveSystem.HasSave())
                Screens.ConfirmDialog.Ask("새로운 여정", "진행 중인 여정을 정리하고 새로 시작할까요?", "새로 시작", BeginNewGame);
            else BeginNewGame();
        }
        private static void BeginNewGame()
        {
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            GameManager.LoadBattleScene();
        }
        private static void OnContinue()
        {
            if (!SaveSystem.HasSave()) return;
            GameStartIntent.Current = GameStartIntent.Intent.Continue;
            GameManager.LoadBattleScene();
        }
        private void Update()
        {
            if (_lobbyContent != null)
            {
                // 설정 모달 뒤에는 배경만 남겨 잘린 메뉴 문구와 원화 얼굴이 읽기를 방해하지 않게 한다.
                bool settingsOpen = _settings != null && _settings.IsOpen;
                _lobbyContent.alpha = Mathf.Lerp(_lobbyContent.alpha, settingsOpen ? 0f : 1f,
                    1f-Mathf.Exp(-Time.unscaledDeltaTime*24f));
                _lobbyContent.blocksRaycasts = _lobbyContent.interactable = !settingsOpen;
            }
#if ENABLE_INPUT_SYSTEM
            bool escape = UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
            bool escape = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (!escape || _wiki?.IsVisible != true) return;
            if (GuideTermTooltip.TryDismiss()) return;
            Screens.ModalScreen.TryEscapeTop();
        }
        private void OnSettings() => _settings?.Open();
        private static void OnQuit() => Application.Quit();
        public void CloseSettings() => _settings?.Close();
    }
}
