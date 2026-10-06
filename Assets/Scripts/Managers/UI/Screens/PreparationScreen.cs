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
        public const float PanelHeight = 176f;
        public const float PanelBottom = 18f;

        private GameObject _root;
        private GameObject _shadow;
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
            Image panel = UIBuild.ChromePanel("PreparationPanel", UIBuild.SafeArea(canvas));
            _root = panel.gameObject;
            UIBuild.Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
            panel.rectTransform.offsetMin = new Vector2(UITheme.ScreenEdgeInset, PanelBottom);
            panel.rectTransform.offsetMax = new Vector2(-UITheme.ScreenEdgeInset, PanelBottom + PanelHeight);
            _shadow = UIBuild.Elevate(panel, 20, 0.42f, 6).gameObject;

            UIArtDirection.Header(panel.transform, 4f);
            _stateChip = UIBuild.Panel("StateChip", panel.transform, UITheme.AccentFaint, cut: 4);
            UIBuild.Anchor(_stateChip.rectTransform, new Vector2(0.018f, 0.68f), new Vector2(0.17f, 0.92f));
            _stateLabel = UIBuild.Text("State", _stateChip.transform, "", 18, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Stretch(_stateLabel.rectTransform, 8, 2);

            _infoBand = UIBuild.Panel("InfoBand", panel.transform, Color.clear, cut: 4);
            UIBuild.Anchor(_infoBand.rectTransform, new Vector2(0.18f, 0.66f), new Vector2(0.98f, 0.94f));
            _infoBand.raycastTarget = false;
            _info = UIBuild.Text("Info", _infoBand.transform, "", 17, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft);
            UIBuild.Anchor(_info.rectTransform, Vector2.zero, new Vector2(0.34f, 1f), 8, 1);
            _resources = UIBuild.Text("Resources", _infoBand.transform, "", 18, UITheme.TextPrimary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_resources.rectTransform, new Vector2(0.32f, 0f), Vector2.one, 8, 1);

            _rest = MakeCommand(panel.transform, "△", "휴식", 0.015f, 0.155f,
                () => GameManager.Instance?.OpenRestFromPreparation());
            _shop = MakeCommand(panel.transform, "◉", "상점", 0.165f, 0.305f,
                () => GameManager.Instance?.OpenShopFromPreparation());
            _inventory = MakeCommand(panel.transform, "▱", "공용 보급", 0.315f, 0.475f,
                () => GameManager.Instance?.OpenInventoryFromPreparation());
            _deck = MakeCommand(panel.transform, "◇", "덱 구성", 0.485f, 0.645f,
                () => GameManager.Instance?.OpenDeckSetupFromPreparation());

            _start = UIBuild.Button("Start", panel.transform, "", StartRequested, primary: true);
            UIBuild.Anchor(_start.image.rectTransform, new Vector2(0.665f, 0.09f), new Vector2(0.985f, 0.60f));
            _startTitle = UIBuild.Text("StartTitle", _start.transform, "전투 시작", 28, UITheme.TextOnAccent);
            _startTitle.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(_startTitle.rectTransform, new Vector2(0.07f, 0.38f), new Vector2(0.8f, 0.9f));
            _startMeta = UIBuild.Text("StartMeta", _start.transform, "", 16, UITheme.TextOnAccent);
            UIBuild.Anchor(_startMeta.rectTransform, new Vector2(0.07f, 0.07f), new Vector2(0.8f, 0.40f));
            _startArrow = UIBuild.Text("StartArrow", _start.transform, "→", 34, UITheme.TextOnAccent,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_startArrow.rectTransform, new Vector2(0.81f, 0.12f), new Vector2(0.98f, 0.88f));

            _root.SetActive(false);
            _shadow.SetActive(false);
        }

        private static Button MakeCommand(Transform parent, string glyph, string name,
            float left, float right, Action action)
        {
            Button button = UIBuild.Button($"Prep_{name}", parent, $"{glyph}  {name}", action, fontSize: 22);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(left, 0.09f), new Vector2(right, 0.60f));
            return button;
        }

        private static void StartRequested()
        {
            Cell.PlacementModeActive = false;
            GameManager.Instance?.StartRound();
        }

        public void Tick()
        {
            if (!IsVisible || Time.unscaledTime < _nextRefreshAt) return;
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
            _stateChip.sprite = UIShapes.CutCorner(6,
                new Color(stateColor.r, stateColor.g, stateColor.b, 0.14f),
                UIShapes.Corner.Diagonal, stateColor, 1);

            string info = !string.IsNullOrWhiteSpace(message) ? message : "PREPARATION";
            if (deckMode)
            {
                info = "<b>덱 구성 모드</b> — 빛나는 칸으로 카드를 끌어 배치를 바꾸세요. 끝나면 [완료] 또는 ESC.";
            }

            _info.text = info;
            _info.color = deckMode ? UITheme.TextPrimary : UITheme.TextSecondary;
            _infoBand.sprite = UIShapes.CutCorner(6,
                deckMode ? UITheme.AccentFaint : Color.clear,
                UIShapes.Corner.Diagonal, deckMode ? UITheme.MetalBrass : default, deckMode ? 1 : 0);

            Unit lead = CharacterSelectionManager.FindLeadUnit();
            var resources = new List<string>();
            if (lead != null) resources.Add($"{lead.UnitName} Lv.{lead.Level}");
            resources.Add($"골드 {game?.inventoryManager?.Gold ?? 0}");
            resources.Add($"식량 {game?.inventoryManager?.FoodUnits ?? 0}/{GameManager.LongRestFoodCost}");
            resources.Add($"짧은 휴식 {game?.ShortRestCharges ?? 0}/{GameManager.MaxShortRestCharges}");
            _resources.text = string.Join("   ·   ", resources);

            SetButton(_rest, "△", "휴식", game?.CanRest == true,
                restUsed ? "이번 준비에서 사용함" : "짧은 휴식 · 긴 휴식");

            int shopLeft = game?.ShopPurchasesLeft ?? 0;
            SetButton(_shop, "◉", "상점", game?.CanOpenShop == true,
                shopLeft > 0 ? $"구매 {shopLeft}회 남음" : "구매 횟수 소진");
            SetButton(_inventory, "▱", "공용 보급", game != null, $"공용 · {game?.inventoryManager?.ItemIdsInHand?.Count ?? 0}개");
            SetButton(_deck, "◇", deckMode ? "완료" : "덱 구성", true,
                deckMode ? "모드 끄기" : "배치 바꾸기");
            _deck.image.sprite = UIShapes.RoundedRect(10,
                deckMode ? UITheme.AccentDim : UITheme.Surface,
                deckMode ? UITheme.Accent : UITheme.Outline, deckMode ? 2 : 1);

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
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.richText = true;
            label.text = string.IsNullOrEmpty(sub)
                ? $"{glyph}  {name}"
                : $"{glyph}  {name}\n<size=68%>{sub}</size>";
            label.lineSpacing = -12f;
        }
    }
}
