using System;
using System.Collections.Generic;
using System.Linq;
using Core;
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
    /// </summary>
    public class PreparationScreen
    {
        private GameObject _root;
        private TextMeshProUGUI _info;
        private Button _training;
        private Button _rest;
        private Button _shop;
        private Button _skill;
        private Button _deck;
        private Button _start;

        /// <summary>전투 시작을 되물을지 판단하려고 마지막 갱신 상태를 들고 있는다.</summary>
        private bool _deckOnly;
        private bool _actionUsed;

        public void Show(bool deckOnly, bool actionUsed, string message)
        {
            EnsureBuilt();
            Refresh(deckOnly, actionUsed, message);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            Canvas canvas = UIBuild.Canvas("PreparationCanvas", 40);
            Image panel = UIBuild.Panel("PreparationPanel", canvas.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 12, UITheme.Outline, 1);
            _root = panel.gameObject;
            // 하단 가운데. 좌하단 파티 카드와 겹치지 않도록 폭을 제한한다.
            // 버튼 6개(훈련 · 휴식 · 상점 · 스킬 · 덱 구성 · 전투 시작)가 좌우 여백 안에 들어가는 폭이다.
            UIBuild.Pin(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1046f, 132f),
                new Vector2(0f, 24f));

            TextMeshProUGUI title = UIBuild.Label("Title", panel.transform, "준비 페이즈",
                UITheme.FontHeading, UITheme.Accent);
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(200f, 24f),
                new Vector2(18f, -12f));

            _info = UIBuild.Text("Info", panel.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary);
            UIBuild.Anchor(_info.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.92f), 0f, 0f);
            _info.rectTransform.offsetMin = new Vector2(224f, _info.rectTransform.offsetMin.y);
            _info.rectTransform.offsetMax = new Vector2(-18f, _info.rectTransform.offsetMax.y);

            _training = MakeButton(panel.transform, 0, "훈련",
                () => GameManager.Instance?.OpenTrainingFromPreparation());
            _rest = MakeButton(panel.transform, 1, "휴식",
                () => GameManager.Instance?.RestFromPreparation());
            // 상점은 준비 행동을 쓰지 않는다. 훈련을 했든 보스전이든 언제나 열린다.
            _shop = MakeButton(panel.transform, 2, "상점",
                () => GameManager.Instance?.OpenShopFromPreparation());
            // 스킬도 준비 행동을 쓰지 않는다. 힌트받은 패시브를 스킬 Pt로 배우는 자리다.
            _skill = MakeButton(panel.transform, 3, "스킬",
                () => GameManager.Instance?.OpenSkillScreenFromPreparation());
            _deck = MakeButton(panel.transform, 4, "덱 구성",
                () => GameManager.Instance?.OpenDeckSetupFromPreparation());
            _start = MakeButton(panel.transform, 5, "전투 시작", StartRequested, primary: true);

            _root.SetActive(false);
        }

        private static Button MakeButton(Transform parent, int index, string text, Action onClick,
            bool primary = false)
        {
            const float width = 160f;
            const float gap = 10f;
            Button button = UIBuild.Button($"Prep{index}", parent, text, onClick, primary);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 0f), new Vector2(width, 48f),
                new Vector2(18f + index * (width + gap), 16f));
            return button;
        }

        /// <summary>
        /// 전투 시작. 이번 준비 페이즈에 훈련도 휴식도 하지 않았다면 한 번 되묻는다.
        ///
        /// 준비 행동은 스테이지마다 한 번뿐이라 그냥 넘기면 되돌릴 수 없다.
        /// 행동을 고를 수 없는 상황(보스전 · 이미 행동함)에서는 묻지 않고 바로 시작한다.
        /// </summary>
        private void StartRequested()
        {
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

        private void Refresh(bool deckOnly, bool actionUsed, string message)
        {
            _deckOnly = deckOnly;
            _actionUsed = actionUsed;

            bool carryBlocked = GameManager.Instance?.HasOverburdenedHeroes == true;
            string carryMessage = GameManager.Instance?.CarryWeightBlockMessage;
            _info.text = carryBlocked
                ? carryMessage
                : !string.IsNullOrWhiteSpace(message)
                ? message
                : deckOnly
                    // 상점·스킬은 보스전에도 열려 있다. "덱 구성만"이라고 적어 두었더니 상점도 잠긴 줄 알았다는 QA가 있었다.
                    ? "보스전 준비: 훈련·휴식은 막혀 있습니다. 상점 · 스킬 · 덱 구성은 쓸 수 있습니다."
                    : actionUsed
                        ? "준비 행동 완료: 덱을 정리한 뒤 전투를 시작하세요."
                        : "훈련과 휴식 중 하나를 선택하거나 바로 전투를 시작하세요.";

            // 훈련이냐 휴식이냐를 고르는 자리가 여기다. 판단에 필요한 두 값을 함께 띄운다.
            // 이 값을 보려고 훈련 화면을 열었다 닫는 왕복이 원래 있었다.
            TrainingState training = TrainingManager.State;
            _info.text += $"\n훈련 체력 {training.Energy} / {TrainingState.MaxEnergy}" +
                          $" · 컨디션 {training.ConditionName} · 스킬 Pt {training.SkillPoints}";

            // 배울 수 있는 힌트가 있으면 그 사실이 버튼 이름보다 먼저 눈에 들어와야 한다.
            int learnable = TrainingManager.GetSkillOffers().Count(offer => offer.CanLearn);
            if (learnable > 0) _info.text += $" · 습득 가능한 스킬 {learnable}개";

            // 걸려 있는 강화제도 준비 페이즈에서만 확인할 수 있다.
            string tonics = RunManager.Instance?.PartyTonics.DescribeShort();
            if (!string.IsNullOrEmpty(tonics)) _info.text += $" · 강화제 {tonics}";

            bool actionAvailable = !carryBlocked && !deckOnly && !actionUsed;
            SetEnabled(_training, actionAvailable);
            SetEnabled(_rest, actionAvailable);
            SetEnabled(_shop, GameManager.Instance?.CanOpenShop == true);
            SetEnabled(_skill, GameManager.Instance?.CanOpenSkillScreen == true);
            SetEnabled(_deck, !carryBlocked);
            SetEnabled(_start, !carryBlocked);
        }

        /// <summary>켤 때 되돌릴 라벨 색. 버튼을 만들 때의 색을 처음 한 번만 기억한다.</summary>
        private readonly Dictionary<TextMeshProUGUI, Color> _labelColors = new();

        private void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;

            // 예전에는 켤 때 `label.color`를 자기 자신으로 다시 넣었다. 그래서 한 번 흐려진
            // 라벨은 영영 흐린 채로 남았고, 훈련을 한 번 하고 나면 다음 스테이지에서도
            // 준비 행동 버튼이 계속 꺼진 것처럼 보였다(실제로는 눌렸다).
            if (!_labelColors.TryGetValue(label, out Color baseColor))
            {
                baseColor = label.color;
                _labelColors[label] = baseColor;
            }

            label.color = enabled ? baseColor : UITheme.TextMuted;
        }
    }
}
