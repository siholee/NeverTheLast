using Core;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI
{
    /// <summary>
    /// 메인 메뉴. 씬에 배치된 예전 버튼들을 쓰지 않고 이 컴포넌트가 직접 조립한다.
    ///
    /// 화면 구성(디자인 캔버스 MainMenu 아트보드와 같다):
    ///   · 왼쪽 세로 앰버 레일 — 화면을 여백과 내용으로 가른다
    ///   · 큰 제목 + 라틴 마이크로 라벨
    ///   · 5줄 메뉴 — <b>가리킨 줄 하나만</b> 앰버로 채워지고 나머지는 헤어라인만 남는다
    ///   · 오른쪽 스탠딩 일러스트 + 사선 빗금 기둥
    ///   · 우상단 저장 칩, 좌하단 버전 표기
    ///
    /// 앰버를 목록 전체에 흩뿌리지 않는 것이 핵심이다. 예전 메뉴는 버튼마다 색이
    /// 달라(파랑·초록·보라·빨강) 어디를 눌러야 하는지가 색으로 읽히지 않았다.
    ///
    /// 씬(MainMenu.unity)에는 아직 구형 UI 오브젝트가 남아 있으므로,
    /// 시작할 때 그 캔버스들을 꺼서 새 메뉴만 보이게 한다.
    /// 씬을 정리한 뒤에는 <see cref="HideLegacySceneUI"/>를 지워도 된다.
    ///
    /// 클래스 이름은 MainMenu 씬이 참조하므로 유지한다.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        /// <summary>메뉴 한 줄의 크기와 간격.</summary>
        private const float RowWidth = 470f;
        private const float RowHeight = 58f;
        private const float RowGap = 9f;

        /// <summary>왼쪽 여백. 레일(96)에서 한 칸 더 들어온 자리.</summary>
        private const float MarginLeft = 150f;

        /// <summary>
        /// 빗금과 메뉴 줄의 바탕색. <b>흰색 + 낮은 알파를 쓰면 안 된다.</b>
        /// 선형 색공간이라 거의 검정인 배경 위에 얹은 알파 2%짜리 흰색이
        /// sRGB로 되돌아오며 서너 배로 튄다(예전에 메뉴 줄이 회색 막대처럼 보이던 원인이다).
        /// 배경보다 한 단계만 밝은 <b>불투명</b> 회색을 쓴다.
        /// </summary>
        private static readonly Color StripeInk = new(0.086f, 0.090f, 0.098f, 1f);
        private static readonly Color RowIdle = new(0.075f, 0.080f, 0.086f, 1f);

        private SettingsUI _settings;
        private MenuRow[] _rows;
        private TextMeshProUGUI _hint;
        private int _hovered = -1;

        /// <summary>메뉴 한 줄이 들고 있는 것들. 강조 상태를 통째로 갈아 끼우려고 묶어 둔다.</summary>
        private sealed class MenuRow
        {
            public Image Frame;
            public Image Mark;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Latin;
            public string Hint;
            public bool Enabled = true;
        }

        private void Start()
        {
            HideLegacySceneUI();
            // 설정 화면은 이 오브젝트가 직접 들고 있는다(구형 씬 오브젝트에 의존하지 않음).
            // ??는 UnityEngine.Object의 수명 검사를 건너뛰므로 == 오버로드를 타야 한다.
            SettingsUI existing = GetComponent<SettingsUI>();
            _settings = existing != null ? existing : gameObject.AddComponent<SettingsUI>();
            BuildMenu();
            Screens.CharacterUnlockDialog.ShowPending();
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
            Transform root = canvas.transform;

            BuildBackdrop(root);
            BuildTitle(root);
            BuildRows(root);
            BuildFooter(root);
            BuildSaveChip(root);
        }

        // ── 배경 ─────────────────────────────────────────────────────

        private static void BuildBackdrop(Transform root)
        {
            Image background = UIBuild.Solid("Background", root, new Color(0.039f, 0.043f, 0.047f));
            UIBuild.Stretch(background.rectTransform);

            // 오른쪽 기둥의 사선 빗금. 스탠딩 뒤에 깔려 인물을 화면에서 떼어 놓는다.
            Image stripes = UIBuild.Solid("Stripes", root, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(14, StripeInk, Color.clear);
            stripes.type = Image.Type.Tiled;
            stripes.color = Color.white;
            stripes.raycastTarget = false;
            UIBuild.Pin(stripes.rectTransform, new Vector2(1f, 1f), new Vector2(520f, 0f), Vector2.zero);
            stripes.rectTransform.anchorMin = new Vector2(1f, 0f);
            stripes.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripes.rectTransform.pivot = new Vector2(1f, 0.5f);
            stripes.rectTransform.sizeDelta = new Vector2(520f, 0f);
            stripes.rectTransform.anchoredPosition = Vector2.zero;

            Sprite standing = LoadStanding();
            if (standing != null)
            {
                var go = new GameObject("Standing", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                var image = go.GetComponent<Image>();
                image.sprite = standing;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0.92f);

                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(1080f, 1080f);
                rect.anchoredPosition = new Vector2(-150f, -40f);
            }

            // 아래쪽 암전. 인물의 발치를 화면에 녹여 잘린 티가 나지 않게 한다.
            var fadeGo = new GameObject("BottomFade", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(root, false);
            var fade = fadeGo.GetComponent<Image>();
            fade.sprite = UIShapes.VerticalGradient(64,
                new Color(0.031f, 0.035f, 0.039f, 0f), new Color(0.031f, 0.035f, 0.039f, 1f));
            // 1xN 스프라이트라 테두리가 없다. Sliced로 두면 Unity가 경고를 뱉는다.
            fade.type = Image.Type.Simple;
            fade.raycastTarget = false;
            fade.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            fade.rectTransform.anchorMax = new Vector2(1f, 0f);
            fade.rectTransform.pivot = new Vector2(0.5f, 0f);
            fade.rectTransform.sizeDelta = new Vector2(0f, 420f);
            fade.rectTransform.anchoredPosition = Vector2.zero;

            Image rail = UIBuild.Solid("Rail", root, UITheme.Accent);
            rail.raycastTarget = false;
            rail.rectTransform.anchorMin = new Vector2(0f, 0f);
            rail.rectTransform.anchorMax = new Vector2(0f, 1f);
            rail.rectTransform.pivot = new Vector2(0f, 0.5f);
            rail.rectTransform.sizeDelta = new Vector2(4f, 0f);
            rail.rectTransform.anchoredPosition = new Vector2(96f, 0f);
        }

        /// <summary>표지에 세울 스탠딩. 없으면 그냥 생략한다(배경만 남는다).</summary>
        private static Sprite LoadStanding()
        {
            Sprite sei = SpriteResource.LoadStanding("SEI_STANDING");
            return sei != null ? sei : SpriteResource.LoadStanding("SHI_STANDING");
        }

        // ── 제목 ─────────────────────────────────────────────────────

        private static void BuildTitle(Transform root)
        {
            Image tick = UIBuild.Solid("TitleTick", root, UITheme.Accent);
            tick.raycastTarget = false;
            UIBuild.Pin(tick.rectTransform, new Vector2(0f, 1f), new Vector2(3f, 12f),
                new Vector2(MarginLeft, -300f));

            TextMeshProUGUI kicker = UIBuild.Label("Kicker", root,
                "SOLID / AUTO BATTLE ROGUELITE", UITheme.FontMicro, UITheme.TextMuted);
            kicker.characterSpacing = 28f;
            UIBuild.Pin(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(700f, 14f),
                new Vector2(MarginLeft + 12f, -299f));

            TextMeshProUGUI title = UIBuild.Text("Title", root, "NEVER\nTHE LAST",
                86f, UITheme.TextPrimary, TextAlignmentOptions.TopLeft);
            title.characterSpacing = 10f;
            title.lineSpacing = -22f;
            title.fontStyle = FontStyles.Normal;
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(900f, 190f),
                new Vector2(MarginLeft, -330f));

            TextMeshProUGUI subtitle = UIBuild.Text("Subtitle", root, "네 버   더   라 스 트",
                16f, new Color(0.337f, 0.353f, 0.369f));
            subtitle.characterSpacing = 30f;
            UIBuild.Pin(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(700f, 20f),
                new Vector2(MarginLeft + 4f, -524f));
        }

        // ── 메뉴 ─────────────────────────────────────────────────────

        private void BuildRows(Transform root)
        {
            (string label, string latin, string hint, System.Action act)[] defs =
            {
                ("새로운 여정", "NEW RUN",
                    "메인 캐릭터와 서포터를 고르고 1스테이지부터 시작합니다.", OnNewGame),
                ("이어하기", "CONTINUE",
                    "저장된 런을 이어서 진행합니다.", OnContinue),
                ("무한 모드", "ENDLESS",
                    "육성을 마친 캐릭터로 끝없이 이어지는 스테이지에 도전합니다.", OnInfiniteMode),
                ("설정", "SETTINGS",
                    "화면 · 음량 · 조작을 조정합니다.", OnSettings),
                ("종료", "QUIT",
                    "게임을 끝냅니다.", OnQuit),
            };

            _rows = new MenuRow[defs.Length];
            float top = -610f;

            for (int i = 0; i < defs.Length; i++)
            {
                int index = i;
                (string label, string latin, string hint, System.Action act) def = defs[i];

                Image frame = UIBuild.Panel($"Row{i}", root, RowIdle, UIShapes.Corner.Diagonal, 11);
                UIBuild.Pin(frame.rectTransform, new Vector2(0f, 1f), new Vector2(RowWidth, RowHeight),
                    new Vector2(MarginLeft, top));

                Image mark = UIBuild.Solid("Mark", frame.transform, UITheme.Outline);
                mark.raycastTarget = false;
                UIBuild.Pin(mark.rectTransform, new Vector2(0f, 1f), new Vector2(3f, RowHeight),
                    Vector2.zero);

                TextMeshProUGUI label = UIBuild.Text("Label", frame.transform, def.label,
                    19f, UITheme.TextSecondary);
                label.characterSpacing = 6f;
                UIBuild.Stretch(label.rectTransform, 0f, 0f);
                label.rectTransform.offsetMin = new Vector2(26f, 0f);

                TextMeshProUGUI latin = UIBuild.Label("Latin", frame.transform, def.latin,
                    UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                latin.characterSpacing = 22f;
                UIBuild.Stretch(latin.rectTransform, 0f, 0f);
                latin.rectTransform.offsetMax = new Vector2(-22f, 0f);

                _rows[i] = new MenuRow
                {
                    Frame = frame,
                    Mark = mark,
                    Label = label,
                    Latin = latin,
                    Hint = def.hint,
                };

                System.Action action = def.act;
                UIBuild.OnClick(frame.gameObject, () =>
                {
                    if (_rows[index].Enabled) action();
                });
                AddHover(frame.gameObject, index);

                top -= RowHeight + RowGap;
            }

            // 설명줄. 가리킨 줄이 무엇을 하는지 한 문장으로 받아 준다.
            Image rule = UIBuild.Divider("HintRule", root);
            rule.raycastTarget = false;
            UIBuild.Pin(rule.rectTransform, new Vector2(0f, 1f), new Vector2(RowWidth, 1f),
                new Vector2(MarginLeft, -958f));

            _hint = UIBuild.Text("Hint", root, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Pin(_hint.rectTransform, new Vector2(0f, 1f), new Vector2(RowWidth, 44f),
                new Vector2(MarginLeft, -976f));

            RefreshContinueRow();
            Highlight(0);
        }

        /// <summary>줄 하나에 포인터 진입/이탈을 붙인다. 강조는 "가리킨 곳"만 따라간다.</summary>
        private void AddHover(GameObject target, int index)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Highlight(index));
            trigger.triggers.Add(enter);
        }

        /// <summary>가리킨 줄만 앰버로 채우고 나머지는 헤어라인으로 되돌린다.</summary>
        private void Highlight(int index)
        {
            if (_rows == null) return;
            _hovered = index;

            for (int i = 0; i < _rows.Length; i++)
            {
                MenuRow row = _rows[i];
                bool on = i == index && row.Enabled;

                row.Frame.sprite = UIShapes.CutCorner(11,
                    on ? UITheme.Accent : RowIdle,
                    UIShapes.Corner.Diagonal,
                    on ? new Color(0f, 0f, 0f, 0f) : UITheme.Divider,
                    on ? 0 : 1);

                row.Mark.color = on ? new Color(0.071f, 0.071f, 0.071f, 0.45f)
                    : row.Enabled ? UITheme.Outline : new Color(1f, 1f, 1f, 0.07f);

                row.Label.color = on ? UITheme.TextOnAccent
                    : row.Enabled ? UITheme.TextSecondary : UITheme.TextMuted;

                row.Latin.color = on ? new Color(0.071f, 0.071f, 0.071f, 0.55f)
                    : row.Enabled ? UITheme.TextMuted : new Color(1f, 1f, 1f, 0.12f);
            }

            if (_hint != null && index >= 0 && index < _rows.Length)
            {
                _hint.text = _rows[index].Enabled
                    ? _rows[index].Hint
                    : "저장된 런이 없습니다. 새로운 여정으로 시작하세요.";
            }
        }

        // ── 아래쪽 표기 ──────────────────────────────────────────────

        private static void BuildFooter(Transform root)
        {
            TextMeshProUGUI version = UIBuild.Label("Version", root,
                $"VER {Application.version}      BUILD 2026.08      ANDROID · IL2CPP",
                UITheme.FontMicro, new Color(0.263f, 0.275f, 0.290f));
            version.characterSpacing = 16f;
            UIBuild.Pin(version.rectTransform, new Vector2(0f, 0f), new Vector2(760f, 16f),
                new Vector2(MarginLeft, 40f));
        }

        /// <summary>우상단 저장 칩. 이어하기가 무엇을 이어 주는지 미리 보여 준다.</summary>
        private static void BuildSaveChip(Transform root)
        {
            if (!SaveSystem.HasSave()) return;

            RunSaveData save = SaveSystem.LoadRun();
            if (save == null) return;

            Image chip = UIBuild.Panel("SaveChip", root, new Color(0.039f, 0.043f, 0.051f, 0.86f),
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Pin(chip.rectTransform, new Vector2(1f, 1f), new Vector2(320f, 40f),
                new Vector2(-56f, -52f));

            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(chip.transform, false);
            var dot = dotGo.GetComponent<Image>();
            dot.sprite = UIShapes.Disc(16, UITheme.Accent);
            dot.raycastTarget = false;
            UIBuild.Pin(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 6f),
                new Vector2(18f, 0f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", chip.transform, "SAVE",
                UITheme.FontMicro, UITheme.TextMuted);
            caption.characterSpacing = 20f;
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 14f),
                new Vector2(32f, 0f));

            TextMeshProUGUI text = UIBuild.Text("Text", chip.transform,
                $"스테이지 {save.currentStage}-{save.currentRound}",
                UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            UIBuild.Stretch(text.rectTransform, 0f, 0f);
            text.rectTransform.offsetMax = new Vector2(-18f, 0f);
        }

        private void RefreshContinueRow()
        {
            if (_rows == null || _rows.Length < 2) return;

            _rows[1].Enabled = SaveSystem.HasSave();
            Highlight(_hovered);
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
