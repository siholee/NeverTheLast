using System;
using System.Collections.Generic;
using Core;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 준비 페이즈 하단 독. 추가 전투와 장비는 없다.
    /// 플레이어는 상점 · 휴식 · 공용 보급 · 덱 구성만 정리한 뒤 출격한다.
    /// </summary>
    public sealed class PreparationScreen
    {
        public const float PanelHeight = 72f;
        public const float PanelBottom = 18f;

        private GameObject _root;
        private GameObject _shadow;
        private CanvasGroup _visibility;
        private Image _stateChip;
        private TextMeshProUGUI _stateLabel;
        private Image _infoBand;
        private TextMeshProUGUI _info;
        private TextMeshProUGUI _resources;

        private Button _rest;
        private Button _shop;
        private Button _inventory;
        private Button _deck;
        private Button _start;
        private TextMeshProUGUI _startTitle;
        private TextMeshProUGUI _startMeta;
        private TextMeshProUGUI _startArrow;

        private bool _restUsed;
        private string _message;
        private float _nextRefreshAt;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Show(bool restUsed, string message = null)
        {
            EnsureBuilt();
            Refresh(restUsed, message);
            _root.SetActive(true);
            _shadow.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            if (_shadow != null) _shadow.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;
            Canvas canvas = UIBuild.Canvas("PreparationCanvas", 40);
            _visibility = canvas.gameObject.AddComponent<CanvasGroup>();
            Image panel = UIBuild.ChromePanel("PreparationPanel", UIBuild.SafeArea(canvas));
            _root = panel.gameObject;
            UIBuild.Anchor(panel.rectTransform, new Vector2(.14f, 0f), new Vector2(.86f, 0f));
            panel.rectTransform.offsetMin = new Vector2(UITheme.ScreenEdgeInset, PanelBottom);
            panel.rectTransform.offsetMax = new Vector2(-UITheme.ScreenEdgeInset, PanelBottom + PanelHeight);
            _shadow = UIBuild.Elevate(panel, 20, 0.14f, 6).gameObject;

            UIArtDirection.Header(panel.transform, 4f);
            _stateChip = UIBuild.Panel("StateChip", panel.transform, UITheme.AccentFaint, cut: 4);
            UIBuild.Anchor(_stateChip.rectTransform, new Vector2(.025f,.71f), new Vector2(.28f,.97f));
            _stateLabel = UIBuild.Text("State", _stateChip.transform, "", 18, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Stretch(_stateLabel.rectTransform, 8, 2);

            _infoBand = UIBuild.Panel("InfoBand", panel.transform, Color.clear, cut: 4);
            UIBuild.Anchor(_infoBand.rectTransform, new Vector2(.30f,.71f), new Vector2(.64f,.97f));
            _infoBand.raycastTarget = false;
            _info = UIBuild.Text("Info", _infoBand.transform, "", 17, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft);
            UIBuild.Anchor(_info.rectTransform, Vector2.zero, new Vector2(0.40f, 1f), 8, 1);
            _resources = UIBuild.Text("Resources", _infoBand.transform, "", 18, UITheme.TextPrimary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_resources.rectTransform, new Vector2(.02f, 0f), Vector2.one, 8, 1);

            _rest = MakeCommand(panel.transform, "△", "휴식", 0.015f, 0.155f,
                () => GameManager.Instance?.OpenRestFromPreparation());
            _shop = MakeCommand(panel.transform, "◉", "상점", 0.165f, 0.305f,
                () => GameManager.Instance?.OpenShopFromPreparation());
            _inventory = MakeCommand(panel.transform, "▱", "공용 보급", 0.315f, 0.475f,
                () => GameManager.Instance?.OpenInventoryFromPreparation());
            _deck = MakeCommand(panel.transform, "◇", "덱 구성", 0.485f, 0.645f,
                () => GameManager.Instance?.OpenDeckSetupFromPreparation());

            _start = UIBuild.Button("Start", panel.transform, "", StartRequested, primary: true);
            UIBuild.Anchor(_start.image.rectTransform, new Vector2(.67f,.11f), new Vector2(.98f,.89f));
            _startTitle = UIBuild.Text("StartTitle", _start.transform, "전투 시작", 24, UITheme.TextOnAccent);
            _startTitle.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(_startTitle.rectTransform, new Vector2(0.07f, 0f), new Vector2(0.8f, 1f));
            _stateChip.gameObject.SetActive(false);
            _infoBand.gameObject.SetActive(false);
            _startMeta = UIBuild.Text("StartMeta", _start.transform, "", 16, UITheme.TextOnAccent);
            _startMeta.gameObject.SetActive(false);
            _startArrow = UIBuild.Text("StartArrow", _start.transform, "→", 34, UITheme.TextOnAccent,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_startArrow.rectTransform, new Vector2(0.81f, 0.12f), new Vector2(0.98f, 0.88f));

            _root.SetActive(false);
            _shadow.SetActive(false);
        }

        private static Button MakeCommand(Transform parent, string glyph, string name,
            float left, float right, Action action)
        {
            Button button = UIBuild.Button($"Prep_{name}", parent, "", action, fontSize: 22);
            UIIcon kind = name switch { "휴식" => UIIcon.Rest, "상점" => UIIcon.Shop,
                "공용 보급" => UIIcon.Supply, _ => UIIcon.Party };
            Image icon = UIBuild.Icon("CommandIcon", button.transform, kind, UITheme.Accent);
            // 기호 상자의 세로 중심을 버튼 중심(.5)에 맞춘다. 이름은 실제 글자 경계로 가운데를 잡는다(Midline).
            UIBuild.Anchor(icon.rectTransform, new Vector2(.05f,.26f), new Vector2(.19f,.74f));
            TextMeshProUGUI heading = UIBuild.Text("Heading", button.transform, name, 21, UITheme.TextPrimary, TextAlignmentOptions.Midline);
            UIBuild.Anchor(heading.rectTransform, new Vector2(.25f,0f), new Vector2(.96f,1f));
            TextMeshProUGUI detail = UIBuild.Text("Description", button.transform, "", 16, UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(detail.rectTransform, new Vector2(.25f,.04f), new Vector2(.96f,.39f));
            detail.gameObject.SetActive(false);
            button.image.sprite=UIShapes.RoundedRect(22,Color.clear);
            TooltipTrigger.Attach(button.gameObject,()=>heading.text,()=>new System.Collections.Generic.List<UITooltip.Line> {
                UITooltip.Line.Note(detail.text,UITheme.TextSecondary),
                UITooltip.Line.Note($"식량 {GameManager.Instance?.inventoryManager?.FoodUnits ?? 0} · 긴 휴식 비용 {GameManager.LongRestFoodCost}",UITheme.TextSecondary) },UITheme.Accent);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(.012f + left, .11f), new Vector2(.012f + right,.89f));
            return button;
        }

        private static void StartRequested()
        {
            Cell.PlacementModeActive = false;
            GameManager.Instance?.StartRound();
        }

        public void Tick()
        {
            if (!IsVisible) return;
            bool foreground = ModalScreen.HasVisibleScreen || GameManager.Instance?.uiManager?.IsOverlayOpen == true;
            _visibility.alpha = foreground ? 0f : 1f;
            _visibility.blocksRaycasts = !foreground;
            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + 0.25f;
            Refresh(_restUsed, _message);
        }

        public bool TryExitDeckMode()
        {
            if (!IsVisible || !Cell.PlacementModeActive) return false;
            GameManager.Instance?.OpenDeckSetupFromPreparation();
            return true;
        }

        private void Refresh(bool restUsed, string message)
        {
            _restUsed = restUsed;
            _message = message;
            GameManager game = GameManager.Instance;
            bool deckMode = Cell.PlacementModeActive;

            string state = restUsed ? "휴식 완료" : "출격 준비";
            Color stateColor = restUsed ? UITheme.TextSecondary : UITheme.TextPrimary;
            _stateLabel.text = state;
            _stateLabel.color = stateColor;
            // 상태는 누를 수 없는 정보다. 테두리 칩은 아래 명령 버튼과 같은 상자로 읽혀 눌러 보게 만들었으므로
            // 면·선을 걷고 패널 제목처럼 굵은 글자만 남긴다.
            _stateChip.sprite = UIShapes.Surface(6, Color.clear, UIShapes.Corner.Diagonal);
            _stateChip.raycastTarget = false;
            _stateLabel.fontStyle = FontStyles.Bold;
            _stateLabel.alignment = TextAlignmentOptions.MidlineLeft;

            string info = !string.IsNullOrWhiteSpace(message) ? message : "PREPARATION";
            if (deckMode)
            {
                info = "<b>덱 구성 모드</b> — 빛나는 칸으로 카드를 끌어 배치를 바꾸세요. 끝나면 [완료] 또는 ESC.";
            }

            _info.text = info;
            _info.color = deckMode ? UITheme.TextPrimary : UITheme.TextSecondary;
            _infoBand.sprite = UIShapes.Surface(6,
                deckMode ? UITheme.AccentFaint : Color.clear,
                UIShapes.Corner.Diagonal, deckMode ? UITheme.MetalBrass : default, deckMode ? 1 : 0);

            _resources.text = $"식량 {game?.inventoryManager?.FoodUnits ?? 0}/{GameManager.LongRestFoodCost}   ·   짧은 휴식 {game?.ShortRestCharges ?? 0}/{GameManager.MaxShortRestCharges}";
            _resources.gameObject.SetActive(false);
            _infoBand.gameObject.SetActive(deckMode);
            _info.gameObject.SetActive(deckMode);
            _info.text = "카드를 빛나는 칸으로 이동 · ESC 완료";
            UIBuild.Anchor(_infoBand.rectTransform,new Vector2(0f,1f),Vector2.one);
            _infoBand.rectTransform.offsetMin=new Vector2(0f,4f);_infoBand.rectTransform.offsetMax=new Vector2(0f,40f);
            UIBuild.Anchor(_info.rectTransform, Vector2.zero, Vector2.one, 8, 1);

            SetButton(_rest, "△", "휴식", game?.CanRest == true,
                restUsed ? "이번 준비에서 사용함" : "짧은 휴식 · 긴 휴식");

            int shopLeft = game?.ShopPurchasesLeft ?? 0;
            SetButton(_shop, "◉", $"상점 · {shopLeft}", game?.CanOpenShop == true,
                shopLeft > 0 ? $"구매 {shopLeft}회 남음" : "구매 횟수 소진");
            SetButton(_inventory, "▱", $"보급 · {game?.inventoryManager?.ItemIdsInHand?.Count ?? 0}", game != null, $"공용 · {game?.inventoryManager?.ItemIdsInHand?.Count ?? 0}개");
            SetButton(_deck, "◇", deckMode ? "완료" : "덱 구성", true,
                deckMode ? "모드 끄기" : "배치 바꾸기");
            // 평소에는 다른 명령과 같이 면 없이, 덱 구성 중에만 공통 선택 표지(옅은 군청 면 + 아래 막대)로 켜져 있음을 보인다.
            // 예전에는 평소에도 회색 면이 깔려 이 칸만 눌려 있는 것처럼 보였다(1920 준비 화면).
            UIArtDirection.SelectRow(_deck.image, deckMode, true, 22);

            RoundManager round = game?.RoundManager;
            string stage = round != null ? $"{round.Round}-{round.StageInRound}" : "";
            _start.interactable = true;
            _startTitle.text = "전투 시작";
            _startMeta.text = round?.IsCurrentBossStage == true ? $"STAGE {stage} · BOSS" : $"STAGE {stage}";
        }

        private static void SetButton(Button button, string glyph, string name, bool enabled, string sub)
        {
            if (button == null) return;
            button.interactable = enabled;
            TextMeshProUGUI label = button.transform.Find("Heading")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI detail = button.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
            if (label != null) { label.text = name; label.color = UITheme.TextPrimary; }
            if (detail != null) detail.text = sub;
        }
    }
}
