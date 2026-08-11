using System;
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
        private Button _extraBattle;
        private Button _rest;
        private Button _deck;
        private Button _equipment;
        private Button _start;

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
            UIBuild.Pin(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1180f, 132f),
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
            _extraBattle = MakeButton(panel.transform, 1, "추가 전투",
                () => GameManager.Instance?.BeginAdditionalBattleFromPreparation());
            _rest = MakeButton(panel.transform, 2, "휴식",
                () => GameManager.Instance?.RestFromPreparation());
            _deck = MakeButton(panel.transform, 3, "덱 구성",
                () => GameManager.Instance?.OpenDeckSetupFromPreparation());
            _equipment = MakeButton(panel.transform, 4, "장비",
                () => GameManager.Instance?.OpenEquipmentFromPreparation());
            _start = MakeButton(panel.transform, 5, "전투 시작",
                () => GameManager.Instance?.StartRound(), primary: true);

            _root.SetActive(false);
        }

        private static Button MakeButton(Transform parent, int index, string text, Action onClick,
            bool primary = false)
        {
            const float width = 178f;
            const float gap = 10f;
            Button button = UIBuild.Button($"Prep{index}", parent, text, onClick, primary);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 0f), new Vector2(width, 48f),
                new Vector2(18f + index * (width + gap), 16f));
            return button;
        }

        private void Refresh(bool deckOnly, bool actionUsed, string message)
        {
            bool carryBlocked = GameManager.Instance?.HasOverburdenedHeroes == true;
            string carryMessage = GameManager.Instance?.CarryWeightBlockMessage;
            _info.text = carryBlocked
                ? carryMessage
                : !string.IsNullOrWhiteSpace(message)
                ? message
                : deckOnly
                    ? "보스전 준비: 덱 구성과 전투 시작만 가능합니다."
                    : actionUsed
                        ? "준비 행동 완료: 덱을 정리한 뒤 전투를 시작하세요."
                        : "훈련 · 추가 전투 · 휴식 중 하나를 선택하거나 바로 전투를 시작하세요.";

            bool actionAvailable = !carryBlocked && !deckOnly && !actionUsed;
            SetEnabled(_training, actionAvailable);
            SetEnabled(_extraBattle, actionAvailable);
            SetEnabled(_rest, actionAvailable);
            SetEnabled(_deck, !carryBlocked);
            SetEnabled(_equipment, true);
            SetEnabled(_start, !carryBlocked);
        }

        private static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = enabled ? label.color : UITheme.TextMuted;
            }
        }
    }
}
