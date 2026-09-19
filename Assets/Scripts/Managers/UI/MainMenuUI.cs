using Core;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>캐릭터와 여정 선택을 나란히 놓은 가로형 로비.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        private SettingsUI _settings;
        private void Start()
        {
            transform.SetParent(null, false);
            foreach (Canvas old in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                old.gameObject.SetActive(false);
            _settings = GetComponent<SettingsUI>();
            if (_settings == null) _settings = gameObject.AddComponent<SettingsUI>();
            BuildMenu();
            Screens.CharacterUnlockDialog.ShowPending();
        }

        private void BuildMenu()
        {
            Canvas canvas = UIBuild.Canvas("MainMenuCanvas", 10);
            Image paper = UIBuild.Solid("Ivory", canvas.transform, new Color(0.91f, 0.94f, 0.93f));
            UIBuild.Stretch(paper.rectTransform);
            paper.raycastTarget = false;
            RectTransform root = UIBuild.SafeArea(canvas);
            Image halo = UIBuild.Solid("ArchiveHalo", root, new Color(0.28f, 0.58f, 0.51f, 0.16f));
            halo.sprite = UIShapes.Disc(256, Color.white, 0.985f);
            halo.type = Image.Type.Simple;
            halo.raycastTarget = false;
            halo.preserveAspect = true;
            UIBuild.Anchor(halo.rectTransform, new Vector2(0.01f, 0.03f), new Vector2(0.68f, 0.98f));
            TextMeshProUGUI watermark = UIBuild.Text("MythMark", root, "MYTH\nARCHIVE", 110,
                new Color(0.24f, 0.42f, 0.37f, 0.09f), TextAlignmentOptions.TopLeft);
            watermark.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(watermark.rectTransform, new Vector2(0.03f, 0.32f), new Vector2(0.62f, 0.84f));
            Image character = UIBuild.Solid("LobbyCharacter", root, Color.white);
            character.sprite = SpriteResource.LoadStanding("SEI_STANDING") ?? SpriteResource.LoadStanding("SHI_STANDING");
            character.type = Image.Type.Simple;
            character.preserveAspect = true;
            character.raycastTarget = false;
            character.enabled = character.sprite != null;
            UIBuild.Anchor(character.rectTransform, new Vector2(0.04f, 0.01f), new Vector2(0.64f, 1.02f));
            Copy(root, "Brand", "NEVER THE LAST", 32, UITheme.TextPrimary, 0.035f, 0.89f, 0.44f, 0.97f, true);
            Copy(root, "Edition", "신들의 기록 · 끝나지 않은 여정", 18, UITheme.TextSecondary, 0.037f, 0.85f, 0.44f, 0.90f);
            Image identity = UIBuild.Panel("CharacterIdentity", root, new Color(1, 1, 1, 0.93f), cut: 4);
            UIBuild.Anchor(identity.rectTransform, new Vector2(0.055f, 0.13f), new Vector2(0.31f, 0.25f));
            Copy(identity.transform, "Name", "세이", 34, UITheme.TextPrimary, 0.06f, 0.38f, 0.94f, 0.92f, true);
            Copy(identity.transform, "Role", "별의 궤도를 잇는 서포터", 18, UITheme.TextSecondary, 0.06f, 0.08f, 0.94f, 0.40f);
            RectTransform journey = UIBuild.Container("Journey", root);
            UIBuild.Anchor(journey, new Vector2(0.63f, 0.15f), new Vector2(0.96f, 0.85f));
            Copy(journey, "Chapter", "YOUR NEXT CHAPTER", 16, UITheme.Accent, 0, 0.89f, 1, 1);
            Copy(journey, "Title", "다시, 여정을 향해", 42, UITheme.TextPrimary, 0, 0.77f, 1, 0.91f, true);
            Copy(journey, "Intro", "동료를 만나고, 신화를 이어가세요.", 20, UITheme.TextSecondary, 0, 0.69f, 1, 0.79f);
            RunSaveData save = SaveSystem.HasSave() ? SaveSystem.LoadRun() : null;
            string continuation = save == null ? "저장된 여정이 없습니다" :
                $"구역 {save.currentRound} · {(Mathf.Max(1, save.currentStage) - 1) % 10 + 1} 스테이지   /   목숨 {save.life}";
            JourneyButton(journey, "Continue", "여정 이어가기", continuation, OnContinue, 0.43f, 0.66f, save != null, save != null);
            JourneyButton(journey, "NewRun", "새로운 여정", "메인 캐릭터와 네 명의 동료 선택", OnNewGame, 0.20f, 0.40f, save == null, true);
            int cards = CharacterSelectionManager.CountInfiniteEligibleCards(DataManager.LoadUnitDataList()?.units);
            int required = CharacterSelectionManager.InfiniteRequiredCards;
            JourneyButton(journey, "Endless", "무한 모드", cards >= required ? "육성한 동료들과 한계 너머로" : $"육성 완료 동료 {cards} / {required}",
                OnInfiniteMode, 0, 0.17f, false, cards >= required);
            // 캐릭터 발을 반투명하게 비치면 실수로 잘린 것처럼 보인다. 푸터가 명확히 전경에 오도록 완전 불투명 처리한다.
            Image footer = UIBuild.Solid("Footer", root, Color.white);
            UIBuild.Anchor(footer.rectTransform, Vector2.zero, new Vector2(1, 0.095f));
            Copy(footer.transform, "Version", $"NEVER THE LAST   /   DEMO {Application.version}", 15, UITheme.TextMuted, 0.035f, 0, 0.57f, 1);
            Button settings = UIBuild.Button("Settings", footer.transform, "설정", OnSettings, fontSize: 22);
            UIBuild.Anchor(settings.image.rectTransform, new Vector2(0.68f, 0.15f), new Vector2(0.82f, 0.85f));
            Button quit = UIBuild.Button("Quit", footer.transform, "종료", OnQuit, fontSize: 22);
            UIBuild.Anchor(quit.image.rectTransform, new Vector2(0.84f, 0.15f), new Vector2(0.96f, 0.85f));
            quit.gameObject.SetActive(!Application.isMobilePlatform);
        }

        private static void Copy(Transform parent, string key, string text, float size, Color ink,
            float left, float bottom, float right, float top, bool bold = false)
        {
            var label = UIBuild.Text(key, parent, text, size, ink);
            if (bold) label.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(label.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
        }

        private static void JourneyButton(Transform parent, string key, string title, string description,
            System.Action action, float bottom, float top, bool primary, bool enabled)
        {
            Button button = UIBuild.Button(key, parent, "", action, primary);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(0, bottom), new Vector2(1, top));
            button.interactable = enabled;
            Color ink = primary ? UITheme.TextOnAccent : enabled ? UITheme.TextPrimary : UITheme.TextDisabled;
            Copy(button.transform, "Heading", title, 29, ink, 0.06f, 0.40f, 0.84f, 0.90f, true);
            Copy(button.transform, "Description", description, 18, ink, 0.06f, 0.09f, 0.87f, 0.43f);
            Copy(button.transform, "Arrow", enabled ? "→" : "—", 32, ink, 0.88f, 0.18f, 0.98f, 0.83f);
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
        private static void OnInfiniteMode()
        {
            if (SaveSystem.HasSave())
                Screens.ConfirmDialog.Ask("무한 모드", "진행 중인 여정을 정리하고 무한 모드를 시작할까요?", "시작", BeginInfiniteMode);
            else BeginInfiniteMode();
        }
        private static void BeginInfiniteMode()
        {
            SaveSystem.DeleteSave();
            GameStartIntent.Current = GameStartIntent.Intent.InfiniteMode;
            GameManager.LoadBattleScene();
        }
        private void OnSettings() => _settings?.Open();
        private static void OnQuit() => Application.Quit();
        public void CloseSettings() => _settings?.Close();
    }
}
