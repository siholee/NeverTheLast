using System.Collections;
using System.Collections.Generic;
using Entities;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BaseClasses.BaseEnums;

namespace Managers.UI
{
    /// <summary>
    /// Self-constructing battle UI.  Attach to the BattleManager GameObject.
    /// Builds the full battle canvas at runtime (Awake) — no Inspector wiring needed.
    /// Visual style mirrors RotationBattle: dark panels, HP bars, SP dots, battle log.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        // ── Palette ───────────────────────────────────────────────────────────────
        static readonly Color C_BG         = new(0.05f, 0.05f, 0.12f);
        static readonly Color C_Panel      = new(0.10f, 0.12f, 0.24f, 0.95f);
        static readonly Color C_PanelEnemy = new(0.22f, 0.10f, 0.10f, 0.95f);
        static readonly Color C_Border     = new(0.25f, 0.25f, 0.50f, 0.80f);
        static readonly Color C_HPFull     = new(0.20f, 0.78f, 0.22f);
        static readonly Color C_HPLow      = new(0.88f, 0.20f, 0.20f);
        static readonly Color C_HPBg       = new(0.08f, 0.08f, 0.10f);
        static readonly Color C_BtnBasic   = new(0.10f, 0.22f, 0.10f);
        static readonly Color C_BtnClass   = new(0.20f, 0.10f, 0.30f); // 클래스 스킬 — 보라
        static readonly Color C_BtnNormal  = new(0.10f, 0.14f, 0.30f);
        static readonly Color C_BtnUlt     = new(0.28f, 0.10f, 0.10f);
        static readonly Color C_BtnOff     = new(0.08f, 0.08f, 0.14f, 0.55f);
        static readonly Color C_Text       = Color.white;
        static readonly Color C_TextSub    = new(0.65f, 0.70f, 0.90f);
        static readonly Color C_SPFull     = new(0.40f, 0.80f, 1.00f);
        static readonly Color C_SPEmpty    = new(0.20f, 0.22f, 0.32f);
        static readonly Color C_Gold       = new(1.00f, 0.85f, 0.30f);
        static readonly Color C_BarSep     = new(0.25f, 0.30f, 0.55f, 0.60f);

        // ── Inspector field (wired by SceneBuilder) ───────────────────────────────
        [SerializeField] public TMP_FontAsset koreanFont;

        // ── Built references ──────────────────────────────────────────────────────
        private Canvas _canvas;

        private readonly List<UnitPanel> _heroPanels  = new();
        private readonly List<UnitPanel> _enemyPanels = new();

        private readonly List<Image> _spDots    = new();   // 5 dots
        private readonly List<Image> _actionPips = new();  // up to 3 action pips

        private TextMeshProUGUI _logText;
        private readonly Queue<string> _logLines = new();
        private const int MaxLogLines = 5;

        private GameObject       _actionPanel;   // wraps skill buttons; shown on hero turn
        private Button           _basicBtn;
        private Button           _classBtn;      // 클래스 스킬 버튼 (4th slot)
        private Button           _normalBtn;
        private Button           _ultimateBtn;
        private TextMeshProUGUI  _basicBtnLbl;   // label refs for UpdateSkillLabels()
        private TextMeshProUGUI  _classBtnLbl;
        private TextMeshProUGUI  _normalBtnLbl;
        private TextMeshProUGUI  _ultBtnLbl;
        private TextMeshProUGUI  _spText;
        private TextMeshProUGUI  _actionsLeftText;

        private GameObject              _rewardPanel;
        private TextMeshProUGUI[]       _rewardTexts   = new TextMeshProUGUI[3];
        private Button[]                _rewardButtons = new Button[3];

        private TextMeshProUGUI _reactionLabel;
        private Coroutine       _reactionHide;

        // ── 캐릭터 선택 ───────────────────────────────────────────────────────────
        private GameObject _charSelectCanvas;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake() => BuildAll();

        private void Start()
        {
            // Wire UIManager references so its existing methods (ShowActionPanel, etc.)
            // operate on our runtime-built controls — must run in Start() so GameManager
            // and UIManager are already initialized.
            var ui = GameManager.Instance?.uiManager;
            if (ui != null) BindUIManager(ui);

            // Subscribe SP display
            var sp = GameManager.Instance?.spManager;
            if (sp != null) sp.OnSPChanged += RefreshSP;

            // Initial refresh
            RefreshSP(sp?.Current ?? 0);
        }

        private void OnDestroy()
        {
            var sp = GameManager.Instance?.spManager;
            if (sp != null) sp.OnSPChanged -= RefreshSP;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Set UIManager's button/panel fields to the runtime-built controls.
        /// Called from Start() after GameManager is ready.
        /// </summary>
        public void BindUIManager(UIManager ui)
        {
            ui.actionPanel    = _actionPanel;
            ui.basicButton    = _basicBtn;
            ui.classButton    = _classBtn;
            ui.normalButton   = _normalBtn;
            ui.ultimateButton = _ultimateBtn;
            ui.spText         = _spText;
            ui.actionsLeftText = _actionsLeftText;
            ui.rewardPanel    = _rewardPanel;
            ui.rewardTexts    = _rewardTexts;
            ui.rewardButtons  = _rewardButtons;
            ui.spPips         = _spDots.ToArray();
        }

        /// <summary>Bind unit panels to the current hero / enemy lists and refresh display.</summary>
        public void RefreshUnits()
        {
            var heroes  = GridManager.Instance?.heroList  ?? new List<Unit>();
            var enemies = GridManager.Instance?.enemyList ?? new List<Unit>();

            for (int i = 0; i < _heroPanels.Count; i++)
            {
                bool has = i < heroes.Count && heroes[i].isActive;
                _heroPanels[i].Root.gameObject.SetActive(has);
                if (has) _heroPanels[i].Bind(heroes[i]);
            }
            for (int i = 0; i < _enemyPanels.Count; i++)
            {
                bool has = i < enemies.Count && enemies[i].isActive;
                _enemyPanels[i].Root.gameObject.SetActive(has);
                if (has) _enemyPanels[i].Bind(enemies[i]);
            }
        }

        // ── 캐릭터 선택 패널 ─────────────────────────────────────────────────────

        /// <summary>
        /// 영웅 선택 화면 표시. UIManager.ShowCharacterSelection()에서 호출.
        /// 사용 가능한 플레이어 유닛(id &lt; 100)을 카드로 나열하고 "전투 시작" 버튼을 제공.
        /// </summary>
        public void ShowCharacterSelectionPanel()
        {
            // 이미 빌드됐으면 재사용
            if (_charSelectCanvas != null)
            {
                _charSelectCanvas.SetActive(true);
                return;
            }

            // ── 전용 Canvas (sortingOrder=20 — 최상위) ────────────────────────────
            _charSelectCanvas = new GameObject("CharSelectCanvas");
            _charSelectCanvas.transform.SetParent(transform);
            var cv = _charSelectCanvas.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 20;
            var sc = _charSelectCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            sc.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight  = 0.5f;
            _charSelectCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var rt = _charSelectCanvas.GetComponent<RectTransform>();

            // 배경 오버레이
            CsImg(rt, "BG", new Color(0.03f, 0.04f, 0.10f, 0.97f), 0, 0, 1, 1);

            // 타이틀
            var title = CsLbl(rt, "Title", "영웅 선택", 36,
                TextAlignmentOptions.Center, new Color(0.80f, 0.90f, 1.0f),
                0.1f, 0.87f, 0.9f, 0.97f);
            title.fontStyle = FontStyles.Bold;

            CsLbl(rt, "Sub", "전투에 참여할 영웅을 선택하세요", 16,
                TextAlignmentOptions.Center, new Color(0.55f, 0.65f, 0.85f),
                0.15f, 0.81f, 0.85f, 0.88f);

            // ── 유닛 카드 (플레이어 유닛만 — id < 100) ──────────────────────────
            var units = GameManager.Instance?.unitDataList?.units;
            var playerUnits = units != null
                ? units.FindAll(u => u.id < 100)
                : new List<UnitData>();

            int count  = Mathf.Max(1, playerUnits.Count);
            float totalW = count * 0.18f + (count - 1) * 0.02f;
            float startX = 0.5f - totalW / 2f;

            for (int i = 0; i < playerUnits.Count; i++)
            {
                var ud  = playerUnits[i];
                float x0 = startX + i * 0.20f;
                float x1 = x0 + 0.18f;

                // 카드 배경
                var card = CsImg(rt, $"Card{i}",
                    new Color(0.10f, 0.12f, 0.24f, 0.95f), x0, 0.28f, x1, 0.78f);

                // 원소 색 상단 띠 (카드 상단 12%)
                var elemColor = ElemColor(System.Enum.TryParse<ElementType>(
                    ud.element, true, out var el) ? el : ElementType.Physical);
                CsImg(card.rectTransform, "ElemBar", elemColor, 0, 0.88f, 1, 1f);
                CsLbl(card.rectTransform, "ElemLbl", ud.element ?? "?", 10,
                    TextAlignmentOptions.Center, Color.white, 0, 0.88f, 1, 1f);

                // 포트레이트 영역 (카드 중·상단: y 0.43~0.87)
                var portraitBg = CsImg(card.rectTransform, "PortraitBg",
                    new Color(0.12f, 0.14f, 0.28f), 0, 0.43f, 1, 0.87f);
                if (!string.IsNullOrEmpty(ud.portrait))
                {
                    var spr = Resources.Load<Sprite>($"Sprite/Portraits/{ud.portrait}");
                    if (spr != null)
                    {
                        portraitBg.sprite         = spr;
                        portraitBg.type           = UnityEngine.UI.Image.Type.Simple;
                        portraitBg.preserveAspect = true;
                        portraitBg.color          = Color.white;
                    }
                }

                // 이름 반투명 배경 + 이름 (포트레이트 하단 오버레이)
                CsImg(card.rectTransform, "NameBg",
                    new Color(0.04f, 0.05f, 0.12f, 0.85f), 0, 0.42f, 1, 0.48f);
                var nameLbl = CsLbl(card.rectTransform, "Name", ud.name ?? "???", 15,
                    TextAlignmentOptions.Center, Color.white, 0.04f, 0.42f, 0.96f, 0.48f);
                nameLbl.fontStyle = FontStyles.Bold;

                // 구분선
                CsImg(card.rectTransform, "Div",
                    new Color(0.30f, 0.38f, 0.65f, 0.70f), 0.04f, 0.415f, 0.96f, 0.418f);

                // 스탯 표시 — STR / DEX / CON / INT / LUK + 파생 스탯
                int hp    = ud.con * 300 + 500;
                int speed = ud.dex * 10  + 60;
                var statsLbl = CsLbl(card.rectTransform, "Stats",
                    $"STR {ud.str}  DEX {ud.dex}  CON {ud.con}\n" +
                    $"INT {ud.intel}  LUK {ud.luk}\n" +
                    $"HP {hp}  SPD {speed}",
                    11, TextAlignmentOptions.Center, new Color(0.75f, 0.85f, 1.0f),
                    0.05f, 0.02f, 0.95f, 0.41f);
                statsLbl.enableWordWrapping = true;
                statsLbl.lineSpacing        = 2f;
            }

            // ── 전투 시작 버튼 ──────────────────────────────────────────────────
            var startBg = CsImg(rt, "StartBtnBG",
                new Color(0.10f, 0.28f, 0.10f), 0.35f, 0.08f, 0.65f, 0.20f);
            var startBtn = startBg.gameObject.AddComponent<UnityEngine.UI.Button>();
            startBtn.targetGraphic = startBg;
            var btnColors = startBtn.colors;
            btnColors.normalColor      = new Color(0.10f, 0.28f, 0.10f);
            btnColors.highlightedColor = new Color(0.14f, 0.40f, 0.14f);
            btnColors.pressedColor     = new Color(0.07f, 0.20f, 0.07f);
            startBtn.colors = btnColors;

            var startLbl = CsLbl(startBg.rectTransform, "Lbl", "전투 시작", 24,
                TextAlignmentOptions.Center, Color.white, 0, 0, 1, 1);
            startLbl.fontStyle = FontStyles.Bold;

            startBtn.onClick.AddListener(() =>
            {
                CharacterSelectionManager.Instance.UseDefaultLineup();
                CharacterSelectionManager.Instance.ConfirmSelection();
                _charSelectCanvas.SetActive(false);
            });
        }

        /// <summary>캐릭터 선택 패널 숨기기 (전투 시작 버튼 외부에서 강제 숨길 때).</summary>
        public void HideCharacterSelectionPanel()
        {
            if (_charSelectCanvas != null)
                _charSelectCanvas.SetActive(false);
        }

        // ── 캐릭터 선택 전용 팩토리 헬퍼 ─────────────────────────────────────────
        // (기존 Img/Lbl은 BattleUI 캔버스 기준이므로 별도 정의)

        private UnityEngine.UI.Image CsImg(RectTransform parent, string name, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = r.offsetMax = Vector2.zero;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            return img;
        }

        private TextMeshProUGUI CsLbl(RectTransform parent, string name, string text,
            int size, TextAlignmentOptions align, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = r.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.color         = color;
            tmp.alignment     = align;
            tmp.raycastTarget = false;
            tmp.overflowMode  = TextOverflowModes.Ellipsis;
            if (koreanFont != null) tmp.font = koreanFont;
            return tmp;
        }

        /// <summary>Refresh all panel HP displays (call after any damage event).</summary>
        public void RefreshAllPanels()
        {
            foreach (var p in _heroPanels)  p.Refresh();
            foreach (var p in _enemyPanels) p.Refresh();
        }

        /// <summary>Append one line to the Pokémon-style battle log.</summary>
        public void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            if (_logText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var l in _logLines) { if (sb.Length > 0) sb.Append('\n'); sb.Append(l); }
            _logText.text = sb.ToString();
        }

        /// <summary>
        /// 현재 행동 유닛의 스킬 이름을 버튼 라벨에 반영.
        /// 궁극기가 없으면 버튼을 숨김. UIManager.ShowActionPanel()에서 호출.
        /// </summary>
        public void UpdateSkillLabels(Unit unit)
        {
            if (unit == null) return;

            // Basic 버튼 (항상 표시)
            if (_basicBtnLbl != null)
                _basicBtnLbl.text = unit.BasicCode != null
                    ? $"{unit.BasicCode.CodeName}\n[Basic]"
                    : "기본 공격\n[Basic]";

            // 클래스 스킬 버튼 — 코드가 없으면 숨김
            bool hasClass = unit.ClassCode != null;
            if (_classBtn != null)
                _classBtn.gameObject.SetActive(hasClass);
            if (hasClass && _classBtnLbl != null)
                _classBtnLbl.text = $"{unit.ClassCode.CodeName}\n[Class]";

            // Normal(Skill) 버튼
            if (_normalBtnLbl != null)
                _normalBtnLbl.text = unit.NormalCode != null
                    ? $"{unit.NormalCode.CodeName}\n[SP-{unit.NormalCode.SpCost}]"
                    : "스킬\n[SP-?]";

            // 궁극기 버튼 — 코드가 없으면 버튼 자체를 숨김
            bool hasUlt = unit.UltimateCode != null;
            if (_ultimateBtn != null)
                _ultimateBtn.gameObject.SetActive(hasUlt);
            if (hasUlt && _ultBtnLbl != null)
                _ultBtnLbl.text = $"{unit.UltimateCode.CodeName}\n[Ultimate]";
        }

        /// <summary>Flash an elemental reaction label in the centre of the screen.</summary>
        public void ShowReaction(string label, Color color)
        {
            if (_reactionHide != null) StopCoroutine(_reactionHide);
            _reactionLabel.text  = label;
            _reactionLabel.color = color;
            _reactionLabel.gameObject.SetActive(true);
            _reactionHide = StartCoroutine(HideAfter(_reactionLabel, 1.4f));
        }

        private IEnumerator HideAfter(TextMeshProUGUI t, float seconds)
        { yield return new WaitForSeconds(seconds); t.gameObject.SetActive(false); }

        // ── SP display (subscribed in Start) ─────────────────────────────────────

        private void RefreshSP(int current)
        {
            int max = SPManager.Max;
            if (_spText != null) _spText.text = $"SP  {current}/{max}";
            for (int i = 0; i < _spDots.Count; i++)
                _spDots[i].color = i < current ? C_SPFull : C_SPEmpty;
        }

        // ── Canvas construction ───────────────────────────────────────────────────

        private void BuildAll()
        {
            // ── Canvas ────────────────────────────────────────────────────────────
            var canvasGo = new GameObject("BattleUICanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;   // behind TopBarCanvas (order 10) but covers the 3-D world
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Full dark background — hides world-space portraits / sprites
            Img(root, "BG", C_BG, 0, 0, 1, 1);

            // ── Character area (y: 0.22 … 0.93) ──────────────────────────────────
            // 2 rows × 4 columns per side; player left, enemy right.
            //   Front row: y 0.22 … 0.57
            //   Back  row: y 0.58 … 0.93
            // Row divider label at y 0.57 … 0.58.
            float frontBot = 0.22f, frontTop = 0.57f;
            float backBot  = 0.58f, backTop  = 0.93f;
            const float colW   = 0.107f;
            const float colGap = 0.012f;

            // Row divider label
            var rowBg = Img(root, "RowDivider",
                new Color(0.05f, 0.05f, 0.12f, 0.85f), 0f, 0.567f, 1f, 0.578f);
            Lbl(rowBg.rectTransform, "RowLbl",
                "[ FRONTLINE  +20% DEF ]                           [ BACKLINE ]",
                9, TextAlignmentOptions.Center, new Color(0.55f, 0.70f, 0.90f),
                0f, 0f, 1f, 1f);

            for (int line = 0; line < 2; line++)
            {
                float yBot = line == 0 ? frontBot : backBot;
                float yTop = line == 0 ? frontTop : backTop;

                for (int slot = 0; slot < 4; slot++)
                {
                    float offset = slot * (colW + colGap);

                    // Hero panel (left side: x 0.01 … 0.49)
                    float pxMin = 0.01f + offset;
                    var hp = new UnitPanel(root, false, line * 4 + slot,
                                          pxMin, pxMin + colW, yBot, yTop, koreanFont);
                    _heroPanels.Add(hp);
                    hp.Root.gameObject.SetActive(false);

                    // Enemy panel (right side: x 0.52 … 0.99)
                    float exMin = 0.52f + offset;
                    var ep = new UnitPanel(root, true, line * 4 + slot,
                                          exMin, exMin + colW, yBot, yTop, koreanFont);
                    _enemyPanels.Add(ep);
                    ep.Root.gameObject.SetActive(false);
                }
            }

            // ── Action bar: 행동 pips + SP dots  (just above skill buttons) ────────
            // Pixel-anchored: starts 198 px from bottom, height 50 px.
            var actionBar = Img(root, "ActionBar",
                new Color(0.07f, 0.07f, 0.18f, 0.92f), 0, 0, 0, 0);
            actionBar.rectTransform.anchorMin        = new Vector2(0f, 0f);
            actionBar.rectTransform.anchorMax        = new Vector2(1f, 0f);
            actionBar.rectTransform.anchoredPosition = new Vector2(0f, 198f);
            actionBar.rectTransform.sizeDelta        = new Vector2(0f, 50f);

            // Top separator
            var sep0 = Img(actionBar.rectTransform, "Sep", C_BarSep, 0, 1, 1, 1);
            sep0.rectTransform.sizeDelta = new Vector2(0, 2);

            // Section 1 (0–25%): 행동 pips ──────────────────────────────────────
            var actLbl = Lbl(actionBar.rectTransform, "ActLbl", "행동", 13,
                TextAlignmentOptions.Left, C_Gold, 0.012f, 0.08f, 0.09f, 0.92f);
            actLbl.fontStyle = FontStyles.Bold;

            for (int i = 0; i < 3; i++)
            {
                float x0 = 0.100f + i * 0.042f;
                var pip = Img(actionBar.rectTransform, $"ActPip{i}", C_SPFull,
                             x0, 0.14f, x0 + 0.035f, 0.86f);
                pip.gameObject.SetActive(false);
                _actionPips.Add(pip);
            }

            // Divider
            Img(actionBar.rectTransform, "Div1", new Color(0.28f, 0.32f, 0.55f, 0.55f),
                0.248f, 0.08f, 0.250f, 0.92f);

            // Section 2 (25–100%): SP dots + text ───────────────────────────────
            var spLbl = Lbl(actionBar.rectTransform, "SPLbl", "SP", 13,
                TextAlignmentOptions.Left, C_TextSub, 0.260f, 0.08f, 0.320f, 0.92f);
            spLbl.fontStyle = FontStyles.Bold;

            for (int i = 0; i < SPManager.Max; i++)
            {
                float x0 = 0.330f + i * 0.050f;
                var dot = Img(actionBar.rectTransform, $"SPDot{i}", C_SPEmpty,
                              x0, 0.14f, x0 + 0.042f, 0.86f);
                _spDots.Add(dot);
            }

            _spText = Lbl(actionBar.rectTransform, "SPText", "SP  0/5", 11,
                TextAlignmentOptions.Left, C_SPFull, 0.592f, 0.10f, 0.72f, 0.90f);

            _actionsLeftText = Lbl(actionBar.rectTransform, "ActionsLeft", "행동 1", 11,
                TextAlignmentOptions.Right, new Color(0.4f, 0.85f, 1f), 0.85f, 0.10f, 0.98f, 0.90f);

            // ── Skill buttons (4×, bottom 0–196 px) ──────────────────────────────
            // Basic | Class | Normal | Ultimate — 유닛에 코드가 없는 버튼은 숨김
            // Wrapped in _actionPanel so UIManager.ShowActionPanel/HideActionPanel work.
            _actionPanel = new GameObject("ActionPanel");
            _actionPanel.transform.SetParent(root, false);
            var apRt = _actionPanel.AddComponent<RectTransform>();
            apRt.anchorMin        = new Vector2(0f, 0f);
            apRt.anchorMax        = new Vector2(1f, 0f);
            apRt.anchoredPosition = new Vector2(0f, 2f);
            apRt.sizeDelta        = new Vector2(0f, 196f);
            var apRoot = apRt;

            // 4-slot equal layout: each ~24.75% wide, 2px gap
            _basicBtn    = BuildSkillBtn(apRoot, "BasicBtn",
                "기본 공격\n[Basic]",   C_BtnBasic,  0.002f, 0.249f);
            _classBtn    = BuildSkillBtn(apRoot, "ClassBtn",
                "클래스\n[Class]",     C_BtnClass,  0.251f, 0.498f);
            _normalBtn   = BuildSkillBtn(apRoot, "NormalBtn",
                "스킬\n[SP-?]",         C_BtnNormal, 0.501f, 0.748f);
            _ultimateBtn = BuildSkillBtn(apRoot, "UltimateBtn",
                "궁극기\n[Ultimate]",   C_BtnUlt,    0.750f, 0.998f);

            // Cache label references so UpdateSkillLabels() can change them at runtime
            _basicBtnLbl  = _basicBtn   .GetComponentInChildren<TextMeshProUGUI>();
            _classBtnLbl  = _classBtn   .GetComponentInChildren<TextMeshProUGUI>();
            _normalBtnLbl = _normalBtn  .GetComponentInChildren<TextMeshProUGUI>();
            _ultBtnLbl    = _ultimateBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Wire onClick directly (UIManager.Start may run before BindUIManager)
            _basicBtn   .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectBasic());
            _classBtn   .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectClass());
            _normalBtn  .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectNormal());
            _ultimateBtn.onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectUltimate());

            _actionPanel.SetActive(false);   // hidden until it's the hero's turn

            // ── Battle log (Pokémon-style text box) ───────────────────────────────
            BuildBattleLog(root);

            // ── Reaction label (centre overlay) ───────────────────────────────────
            var rBg = Img(root, "ReactionBG", new Color(0, 0, 0, 0), 0.2f, 0.44f, 0.8f, 0.58f);
            _reactionLabel = Lbl(rBg.rectTransform, "ReactionLbl", "", 28,
                TextAlignmentOptions.Center, Color.white, 0, 0, 1, 1);
            _reactionLabel.fontStyle = FontStyles.Bold;
            _reactionLabel.gameObject.SetActive(false);

            // ── Reward panel (built last so it renders on top) ────────────────────
            BuildRewardPanel(root);
        }

        // ── Skill button factory ──────────────────────────────────────────────────

        private Button BuildSkillBtn(RectTransform parent, string goName,
            string label, Color bgColor, float xMin, float xMax)
        {
            var bg = Img(parent, goName, bgColor, xMin, 0, xMax, 1);

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor      = bgColor;
            cb.highlightedColor = new Color(
                Mathf.Min(1f, bgColor.r + 0.12f),
                Mathf.Min(1f, bgColor.g + 0.12f),
                Mathf.Min(1f, bgColor.b + 0.12f));
            cb.pressedColor     = bgColor * 0.65f;
            cb.disabledColor    = C_BtnOff;
            btn.colors = cb;

            var lbl = Lbl(bg.rectTransform, "Lbl", label, 18,
                TextAlignmentOptions.Center, C_Text, 0.04f, 0.04f, 0.96f, 0.96f);
            lbl.fontStyle     = FontStyles.Bold;
            lbl.raycastTarget = false;

            return btn;
        }

        // ── Battle log ────────────────────────────────────────────────────────────

        private void BuildBattleLog(RectTransform root)
        {
            // Sits between action bar top (248 px) and skill buttons bottom (198 px)
            // → height = 248 − 198 = 50 px, but we want more room so extend it upward.
            // Anchored: starts at 248 px, height 110 px.
            var logBg = Img(root, "BattleLog",
                new Color(0.04f, 0.05f, 0.10f, 0.90f), 0, 0, 1, 0);
            logBg.rectTransform.anchorMin        = new Vector2(0f, 0f);
            logBg.rectTransform.anchorMax        = new Vector2(1f, 0f);
            logBg.rectTransform.anchoredPosition = new Vector2(0f, 250f);
            logBg.rectTransform.sizeDelta        = new Vector2(0f, 110f);

            // Top border
            var top = Img(logBg.rectTransform, "TopBorder",
                new Color(0.25f, 0.30f, 0.55f, 0.70f), 0, 1f, 1, 1f);
            top.rectTransform.sizeDelta = new Vector2(0, 2);

            // Left accent strip
            var leftBar = Img(logBg.rectTransform, "LeftBar",
                new Color(0.20f, 0.25f, 0.50f, 0.70f), 0, 0, 0, 1);
            leftBar.rectTransform.sizeDelta = new Vector2(5, 0);

            _logText = Lbl(logBg.rectTransform, "LogText", "", 13,
                TextAlignmentOptions.BottomLeft, C_Text,
                0.006f, 0.04f, 0.998f, 0.96f);
            _logText.richText           = true;
            _logText.enableWordWrapping = false;
            _logText.overflowMode       = TextOverflowModes.Overflow;
            _logText.lineSpacing        = -4f;
        }

        // ── Reward panel ──────────────────────────────────────────────────────────

        private void BuildRewardPanel(RectTransform root)
        {
            var panel = Img(root, "RewardPanel_BUI",
                new Color(0.02f, 0.02f, 0.08f, 0.96f), 0, 0, 1, 1);
            _rewardPanel = panel.gameObject;

            var hdr = Lbl(panel.rectTransform, "Hdr",
                "VICTORY! — 보상을 선택하세요", 24,
                TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.25f),
                0.05f, 0.83f, 0.95f, 0.97f);
            hdr.fontStyle = FontStyles.Bold;

            for (int i = 0; i < 3; i++)
            {
                float xMin = i / 3f + 0.025f;
                float xMax = (i + 1) / 3f - 0.025f;

                var card = Img(panel.rectTransform, $"Card{i}", C_Panel,
                    xMin, 0.20f, xMax, 0.80f);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card;
                var cb = btn.colors;
                cb.normalColor      = C_Panel;
                cb.highlightedColor = new Color(0.25f, 0.30f, 0.60f);
                cb.pressedColor     = new Color(0.18f, 0.22f, 0.46f);
                btn.colors = cb;

                // Rarity strip at top of card
                var rarityBg = Img(card.rectTransform, "Rarity",
                    new Color(0.20f, 0.50f, 0.20f), 0, 0.88f, 1, 1f);
                var rarityLbl = Lbl(rarityBg.rectTransform, "RarityLbl", "노말", 10,
                    TextAlignmentOptions.Center, Color.white, 0, 0, 1, 1);
                rarityLbl.fontStyle = FontStyles.Bold;

                // Reward name/description text → UIManager.rewardTexts[i]
                var nameL = Lbl(card.rectTransform, $"RewardName{i}", "", 13,
                    TextAlignmentOptions.Center, C_Text, 0.05f, 0.10f, 0.95f, 0.86f);
                nameL.fontStyle          = FontStyles.Bold;
                nameL.enableWordWrapping = true;

                _rewardTexts[i]   = nameL;
                _rewardButtons[i] = btn;
            }

            _rewardPanel.SetActive(false);
        }

        // ── Image / Label factory helpers ─────────────────────────────────────────

        private Image Img(RectTransform parent, string name, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private TextMeshProUGUI Lbl(RectTransform parent, string name, string text,
            int size, TextAlignmentOptions align, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.color         = color;
            tmp.alignment     = align;
            tmp.raycastTarget = false;
            tmp.overflowMode  = TextOverflowModes.Ellipsis;
            if (koreanFont != null) tmp.font = koreanFont;
            return tmp;
        }

        // ── Action pip refresh (called by UIManager.ShowActionPanel) ──────────────

        public void RefreshActionPips(int actionsLeft)
        {
            for (int i = 0; i < _actionPips.Count; i++)
            {
                bool inBudget = i < BattleManager.DefaultMainActions;
                _actionPips[i].gameObject.SetActive(inBudget);
                if (inBudget)
                    _actionPips[i].color = i < actionsLeft
                        ? new Color(1.00f, 0.85f, 0.30f, 0.95f)
                        : new Color(0.30f, 0.30f, 0.35f, 0.60f);
            }
        }

        // ── Inner class: UnitPanel ────────────────────────────────────────────────

        private class UnitPanel
        {
            public  RectTransform   Root;
            private Unit            _unit;
            private Image           _hpFill;
            private RectTransform   _hpFillRt;
            private TextMeshProUGUI _nameLabel;
            private TextMeshProUGUI _hpText;
            private Image           _aura;
            private readonly TMP_FontAsset _font;

            public UnitPanel(RectTransform canvas, bool enemy, int idx,
                float xMin, float xMax, float yMin, float yMax, TMP_FontAsset font)
            {
                _font = font;

                Color bgColor = enemy ? C_PanelEnemy : C_Panel;
                var bg = MkImg(canvas, (enemy ? "E" : "P") + idx,
                    bgColor, xMin, yMin, xMax, yMax);
                Root = bg.rectTransform;

                // Border
                var border = MkImg(Root, "Border", C_Border, 0, 0, 1, 1);
                border.type = Image.Type.Sliced;

                // Aura strip (top 10%)
                _aura = MkImg(Root, "Aura", Color.clear, 0, 0.90f, 1, 1f);
                _aura.gameObject.SetActive(false);

                // Unit name
                _nameLabel = MkLbl(Root, "Name", "???", 13,
                    TextAlignmentOptions.Left, C_Text, 0.05f, 0.54f, 0.95f, 0.92f);
                _nameLabel.fontStyle = FontStyles.Bold;

                // HP bar background
                var hpBg  = MkImg(Root, "HPBg", C_HPBg, 0.05f, 0.08f, 0.95f, 0.52f);
                var fill  = MkImg(hpBg.rectTransform, "Fill", C_HPFull, 0, 0, 1, 1);
                _hpFill   = fill;
                _hpFillRt = fill.rectTransform;

                // HP text (right-aligned, inside HP bg area)
                _hpText = MkLbl(Root, "HPText", "", 10,
                    TextAlignmentOptions.Right, C_TextSub, 0.05f, 0.08f, 0.95f, 0.52f);
            }

            public void Bind(Unit u) { _unit = u; Refresh(); }

            public void Refresh()
            {
                if (_unit == null) return;
                _nameLabel.text = _unit.UnitName;

                float r = _unit.HpMax > 0
                    ? Mathf.Clamp01((float)_unit.HpCurr / _unit.HpMax)
                    : 0f;

                _hpFillRt.anchorMin = Vector2.zero;
                _hpFillRt.anchorMax = new Vector2(r, 1f);
                _hpFillRt.offsetMin = _hpFillRt.offsetMax = Vector2.zero;
                _hpFill.color = r > 0.35f ? C_HPFull : C_HPLow;
                _hpText.text  = $"{_unit.HpCurr}/{_unit.HpMax}";

                bool hasAura = _unit.CurrentAura != ElementType.Physical;
                _aura.gameObject.SetActive(hasAura);
                if (hasAura) _aura.color = ElemColor(_unit.CurrentAura);

                Root.gameObject.SetActive(_unit.isActive);
            }

            // Static helpers inside inner class (no font access needed for Image)
            static Image MkImg(RectTransform parent, string name, Color color,
                float xMin, float yMin, float xMax, float yMax)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(xMin, yMin);
                rt.anchorMax = new Vector2(xMax, yMax);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = go.AddComponent<Image>();
                img.color = color;
                return img;
            }

            TextMeshProUGUI MkLbl(RectTransform parent, string name, string text,
                int size, TextAlignmentOptions align, Color color,
                float xMin, float yMin, float xMax, float yMax)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(xMin, yMin);
                rt.anchorMax = new Vector2(xMax, yMax);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text          = text;
                tmp.fontSize      = size;
                tmp.color         = color;
                tmp.alignment     = align;
                tmp.raycastTarget = false;
                tmp.overflowMode  = TextOverflowModes.Ellipsis;
                if (_font != null) tmp.font = _font;
                return tmp;
            }
        }

        // ── Element colour lookup ─────────────────────────────────────────────────

        static Color ElemColor(ElementType e) => e switch
        {
            ElementType.Pyro    => new Color(1.00f, 0.35f, 0.10f),
            ElementType.Hydro   => new Color(0.10f, 0.55f, 1.00f),
            ElementType.Anemo   => new Color(0.40f, 0.90f, 0.70f),
            ElementType.Electro => new Color(0.70f, 0.30f, 1.00f),
            ElementType.Dendro  => new Color(0.30f, 0.80f, 0.20f),
            ElementType.Cryo    => new Color(0.70f, 0.90f, 1.00f),
            ElementType.Geo     => new Color(1.00f, 0.80f, 0.20f),
            _                   => Color.white,
        };
    }
}
