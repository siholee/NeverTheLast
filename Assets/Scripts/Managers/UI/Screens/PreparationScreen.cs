using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 준비 페이즈 행동 선택. 전투 화면을 가리지 않도록 화면 하단에만 띄운다.
    /// (모달 베이스를 쓰지 않는 유일한 화면 — 준비 중에도 필드를 보며 유닛을 배치해야 한다.)
    ///
    /// 위에는 현재 상태와 판단 정보를, 아래에는 역할별 커맨드 그룹을 둔다.
    ///   머리 — [준비 행동이 남았는가를 알리는 상태 칩] · 안내문 · 훈련 체력/컨디션/스킬 Pt
    ///   커맨드 독 — [준비 행동: 훈련/휴식] [관리: 상점/스킬] [편성: 덱 구성] [전투 시작]
    ///
    /// 버튼마다 <b>지금 상태를 한 줄 달고 있다</b>(사용함 · 보스전 불가 · 구매 2회 남음 …).
    /// 예전에는 꺼진 버튼과 켜진 회색 버튼이 구별되지 않았고, 왜 꺼졌는지는 윗줄 문장을 읽어야 알았다.
    ///
    /// 덱 구성 모드는 켜져 있는 동안 안내 줄에 오로라 틴트를 주고 버튼을 [완료]로 바꾼다.
    /// 켜졌는지 모르는 채 전투 시작을 눌러 입력이 엇갈렸다는 QA에 대한 답이다.
    /// </summary>
    public class PreparationScreen
    {
        /// <summary>패널 높이. 카메라가 이만큼 아래를 비워 둔다(<see cref="GridManager"/>).</summary>
        public const float PanelHeight = 172f;

        /// <summary>화면 아래 가장자리에서 띄우는 거리.</summary>
        public const float PanelBottom = 24f;


        private GameObject _root;
        private GameObject _shadow;
        private Image _stateChip;
        private TextMeshProUGUI _stateLabel;
        private Image _infoBand;
        private TextMeshProUGUI _info;
        private TextMeshProUGUI _resources;

        private Button _training;
        private Button _rest;
        private Button _shop;
        private Button _skill;
        private Button _deck;
        private Button _start;
        private TextMeshProUGUI _startTitle;
        private TextMeshProUGUI _startMeta;
        private TextMeshProUGUI _startArrow;

        /// <summary>전투 시작을 되물을지 판단하려고 마지막 갱신 상태를 들고 있는다.</summary>
        private bool _deckOnly;
        private bool _actionUsed;
        private string _message;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Show(bool deckOnly, bool actionUsed, string message)
        {
            EnsureBuilt();
            Refresh(deckOnly, actionUsed, message);
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
            Image panel = UIBuild.Panel("PreparationPanel", UIBuild.SafeArea(canvas), UITheme.Surface,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            _root = panel.gameObject;
            UIBuild.Anchor(panel.rectTransform, new Vector2(0.025f, 0), new Vector2(0.975f, 0));
            panel.rectTransform.offsetMin = new Vector2(0, PanelBottom);
            panel.rectTransform.offsetMax = new Vector2(0, PanelBottom + PanelHeight);
            _shadow = UIBuild.Elevate(panel, 20, 0.3f, 6).gameObject;

            // 준비의 선택은 한 줄에, 현재 자원과 안내는 위에 놓는다.
            _stateChip = UIBuild.Panel("StateChip", panel.transform, UITheme.AccentFaint, cut: 4);
            UIBuild.Anchor(_stateChip.rectTransform, new Vector2(0.015f, 0.68f), new Vector2(0.19f, 0.94f));
            _stateLabel = UIBuild.Text("State", _stateChip.transform, "", 19, UITheme.Accent, TextAlignmentOptions.Center);
            UIBuild.Stretch(_stateLabel.rectTransform, 8, 2);
            _infoBand = UIBuild.Panel("InfoBand", panel.transform, Color.clear, cut: 4);
            UIBuild.Anchor(_infoBand.rectTransform, new Vector2(0.20f, 0.66f), new Vector2(0.98f, 0.97f));
            _infoBand.raycastTarget = false;
            _info = UIBuild.Text("Info", _infoBand.transform, "", 18, UITheme.TextPrimary);
            UIBuild.Anchor(_info.rectTransform, new Vector2(0, 0.48f), Vector2.one, 8, 1);
            _resources = UIBuild.Text("Resources", _infoBand.transform, "", 17, UITheme.TextSecondary);
            UIBuild.Anchor(_resources.rectTransform, Vector2.zero, new Vector2(1, 0.48f), 8, 1);

            _training = MakeCommand(panel.transform, "훈련", 0.015f, 0.15f,
                () => GameManager.Instance?.OpenTrainingFromPreparation());
            _rest = MakeCommand(panel.transform, "휴식", 0.16f, 0.295f,
                () => GameManager.Instance?.RestFromPreparation());
            _shop = MakeCommand(panel.transform, "상점", 0.32f, 0.45f,
                () => GameManager.Instance?.OpenShopFromPreparation());
            _skill = MakeCommand(panel.transform, "스킬", 0.46f, 0.59f,
                () => GameManager.Instance?.OpenSkillScreenFromPreparation());
            _deck = MakeCommand(panel.transform, "편성", 0.60f, 0.735f,
                () => GameManager.Instance?.OpenDeckSetupFromPreparation());
            _start = UIBuild.Button("Start", panel.transform, "", StartRequested, primary: true);
            UIBuild.Anchor(_start.image.rectTransform, new Vector2(0.755f, 0.09f), new Vector2(0.985f, 0.60f));
            _startTitle = UIBuild.Text("StartTitle", _start.transform, "전투 시작", 28,
                UITheme.TextOnAccent);
            _startTitle.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(_startTitle.rectTransform, new Vector2(0.07f, 0.38f), new Vector2(0.8f, 0.9f));
            _startMeta = UIBuild.Text("StartMeta", _start.transform, "", 16, UITheme.TextOnAccent);
            UIBuild.Anchor(_startMeta.rectTransform, new Vector2(0.07f, 0.07f), new Vector2(0.8f, 0.40f));
            _startArrow = UIBuild.Text("StartArrow", _start.transform, "→", 34,
                UITheme.TextOnAccent, TextAlignmentOptions.Center);
            UIBuild.Anchor(_startArrow.rectTransform, new Vector2(0.81f, 0.12f), new Vector2(0.98f, 0.88f));
            _root.SetActive(false);
            _shadow.SetActive(false);
        }

        private static Button MakeCommand(Transform parent, string name, float left, float right, Action action)
        {
            Button button = UIBuild.Button($"Prep_{name}", parent, name, action, fontSize: 25);
            UIBuild.Anchor(button.image.rectTransform, new Vector2(left, 0.09f), new Vector2(right, 0.60f));
            return button;
        }

        /// <summary>
        /// 전투 시작. 이번 준비 페이즈에 훈련도 휴식도 하지 않았다면 한 번 되묻는다.
        ///
        /// 준비 행동은 스테이지마다 한 번뿐이라 그냥 넘기면 되돌릴 수 없다.
        /// 행동을 고를 수 없는 상황(보스전 · 이미 행동함)에서는 묻지 않고 바로 시작한다.
        ///
        /// 덱 구성 모드가 켜져 있어도 그대로 시작한다 — 모드는 전투가 시작되며 꺼진다.
        /// </summary>
        private void StartRequested()
        {
            Cell.PlacementModeActive = false;

            if (_deckOnly || _actionUsed)
            {
                GameManager.Instance?.StartRound();
                return;
            }

            ConfirmDialog.Ask(
                "준비 행동 없음",
                "이번 준비 페이즈에서 훈련도 휴식도 하지 않았습니다.\n" +
                "준비 행동은 스테이지마다 한 번뿐이라 전투를 시작하면 이번 기회는 사라집니다.\n\n" +
                "이대로 전투를 시작할까요?",
                "전투 시작",
                () => GameManager.Instance?.StartRound());
        }

        /// <summary>덱 구성 모드만 끄고 안내 줄을 되돌린다. ESC가 부른다.</summary>
        public bool TryExitDeckMode()
        {
            if (!IsVisible || !Cell.PlacementModeActive) return false;
            GameManager.Instance?.OpenDeckSetupFromPreparation();
            return true;
        }

        private void Refresh(bool deckOnly, bool actionUsed, string message)
        {
            _deckOnly = deckOnly;
            _actionUsed = actionUsed;
            _message = message;

            GameManager game = GameManager.Instance;
            bool carryBlocked = game?.HasOverburdenedHeroes == true;
            bool deckMode = Cell.PlacementModeActive;

            // ── 상태 칩 ──
            (string state, Color stateColor) = carryBlocked ? ("중량 초과 — 잠김", UITheme.Danger)
                : deckOnly ? ("보스전 · 훈련 불가", UITheme.Danger)
                : actionUsed ? ("준비 행동 완료", UITheme.TextSecondary)
                : ("준비 행동 1회 남음", UITheme.Accent);
            _stateLabel.text = state;
            _stateLabel.color = stateColor;
            _stateChip.sprite = UIShapes.CutCorner(6, new Color(stateColor.r, stateColor.g, stateColor.b, 0.14f),
                UIShapes.Corner.Diagonal, stateColor, 1);

            // ── 안내 줄 ──
            string info = carryBlocked
                ? game?.CarryWeightBlockMessage
                : !string.IsNullOrWhiteSpace(message)
                    ? message
                    : deckOnly
                        // 상점·스킬은 보스전에도 열려 있다. "덱 구성만"이라고 적어 두었더니 상점도 잠긴 줄 알았다는 QA가 있었다.
                        ? "보스전 준비: 훈련·휴식은 막혀 있습니다. 상점 · 스킬 · 덱 구성은 쓸 수 있습니다."
                        : actionUsed
                            ? "덱을 정리한 뒤 전투를 시작하세요."
                            : "훈련과 휴식 중 하나를 고르거나 바로 전투를 시작하세요.";

            if (deckMode)
            {
                info = "<b>덱 구성 모드</b> — 빛나는 칸이 놓을 수 있는 자리입니다. 카드를 끌어 옮기고(다른 카드 위에 놓으면 교체), " +
                       "짧게 누르면 캐릭터 창이 열립니다. 끝나면 [완료] 또는 ESC.";
            }

            _info.text = info;
            // 모드 안내가 주 행동과 같은 꽉 찬 민트 면이면 시각적 우선순위가 둘이 된다.
            // 옅은 틴트 + 헤어라인으로 상태만 알리고, 가득 찬 민트는 출격에만 남긴다.
            _info.color = deckMode ? UITheme.Accent : UITheme.TextPrimary;
            _infoBand.sprite = UIShapes.CutCorner(6,
                deckMode ? new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.10f)
                    : UITheme.FaintFill,
                UIShapes.Corner.Diagonal, deckMode ? UITheme.Accent : default, deckMode ? 1 : 0);

            // 훈련이냐 휴식이냐를 고르는 자리가 여기다. 판단에 필요한 값을 함께 띄운다.
            // 이 값을 보려고 훈련 화면을 열었다 닫는 왕복이 원래 있었다.
            TrainingState training = TrainingManager.State;
            var resources = new List<string>
            {
                $"훈련 체력 {training.Energy} / {TrainingState.MaxEnergy}",
                $"컨디션 {training.ConditionName}",
                $"스킬 Pt {training.SkillPoints}",
            };
            string tonics = RunManager.Instance?.PartyTonics.DescribeShort();
            if (!string.IsNullOrEmpty(tonics)) resources.Add($"강화제 {tonics}");
            _resources.text = string.Join("   ·   ", resources);
            _resources.color = UITheme.TextSecondary;

            // ── 버튼 ──
            bool actionAvailable = !carryBlocked && !deckOnly && !actionUsed;
            string actionBlocked = carryBlocked ? "중량 초과" : deckOnly ? "보스전 불가" : "사용함";

            SetButton(_training, "훈련", actionAvailable,
                actionAvailable ? $"체력 {training.Energy}" : actionBlocked);
            SetButton(_rest, "휴식", actionAvailable,
                actionAvailable ? $"체력 +{GameManager.RestEnergyRecovery}" : actionBlocked);

            int shopLeft = game?.ShopPurchasesLeft ?? 0;
            SetButton(_shop, "상점", game?.CanOpenShop == true,
                shopLeft > 0 ? $"구매 {shopLeft}회 남음" : "구매 횟수 소진");

            int learnable = TrainingManager.GetSkillOffers().Count(offer => offer.CanLearn);
            SetButton(_skill, "스킬", game?.CanOpenSkillScreen == true,
                learnable > 0 ? $"<color=#{Hex(UITheme.Accent)}>습득 가능 {learnable}</color>" : $"Pt {training.SkillPoints}");

            SetButton(_deck, deckMode ? "완료" : "덱 구성", !carryBlocked,
                deckMode ? "모드 끄기" : "배치 바꾸기");
            // 켜진 모드는 버튼 면을 오로라 테두리로 띄운다. 모드가 켜졌는지가 버튼만 봐도 보여야 한다.
            if (!carryBlocked) _deck.image.sprite = UIShapes.CutCorner(8, deckMode ? UITheme.AccentFaint : UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, deckMode ? UITheme.Accent : UITheme.Outline, deckMode ? 2 : 1);

            RoundManager round = game?.RoundManager;
            string stage = round != null ? $"{round.Round}-{round.StageInRound}" : "";
            _start.interactable = !carryBlocked;
            _startTitle.text = carryBlocked ? "출격 불가" : "전투 시작";
            _startMeta.text = carryBlocked ? "중량 초과 · 장비 정리 필요"
                : round?.IsCurrentBossStage == true ? $"STAGE {stage} · BOSS" : $"STAGE {stage}";

            Color startInk = !carryBlocked ? UITheme.TextOnAccent : UITheme.TextDisabled;
            _startTitle.color = startInk;
            _startArrow.color = startInk;
            _startMeta.color = new Color(startInk.r, startInk.g, startInk.b, !carryBlocked ? 0.70f : 1f);
        }

        /// <summary>
        /// 버튼 이름 아래에 지금 상태를 작게 붙인다. 켜고 끄는 모습은 <see cref="UIButtonStyle"/>이 맡는다.
        /// </summary>
        private static void SetButton(Button button, string name, bool enabled, string sub)
        {
            if (button == null) return;
            button.interactable = enabled;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.richText = true;
            label.text = string.IsNullOrEmpty(sub) ? name : $"{name}\n<size=68%>{sub}</size>";
            label.lineSpacing = -12f;
        }

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
