using System.Collections.Generic;
using BaseClasses;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SkillHintState = Core.SkillHintState;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 스킬 습득 화면. 훈련에서 받아 둔 <b>힌트</b>를 스킬 Pt로 실제 패시브로 바꾼다.
    ///
    /// 우마무스메와 같은 구조다 — 훈련은 힌트만 흘리고, 무엇을 배울지는 준비 페이즈에서
    /// 플레이어가 고른다. 힌트가 없는 코드는 이 목록에 아예 오르지 않는다.
    ///
    /// 금(강화) 코드는 대응하는 은(일반) 코드를 이미 배운 뒤에만 살 수 있다.
    /// 선행 코드가 없으면 카드에 <c>선행 필요: ○○</c>가 붙고 눌러도 값이 나가지 않는다.
    ///
    /// 목록이 길어질 수 있어 <b>쪽 넘김</b>으로 끊는다. 스크롤을 쓰지 않는 이유는 이 프로젝트의
    /// 화면들이 전부 손으로 배치되기 때문이다 — 쪽은 자리가 항상 같아 눈이 덜 흔들린다.
    /// </summary>
    public class SkillScreen : ModalScreen
    {
        private const int Columns = 4;
        private const int Rows = 2;
        private const int PageSize = Columns * Rows;

        protected override string CanvasName => "SkillCanvas";
        protected override int SortingOrder => 73;
        protected override string Title => "스킬";
        protected override string Caption => "SKILL";
        protected override Vector2 AnchorMin => new(0.10f, 0.16f);
        protected override Vector2 AnchorMax => new(0.90f, 0.84f);
        protected override bool CloseOnBackdrop => true;

        private readonly List<Card> _cards = new();
        private List<TrainingManager.SkillOffer> _offers = new();
        private TextMeshProUGUI _wallet;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _pageLabel;
        private Button _prev;
        private Button _next;
        private int _page;

        // 힌트가 하나도 없을 때 가운데를 채우는 판. 카드 여덟 자리가 통째로 비면
        // 화면이 고장 난 것처럼 보인다 — 무엇을 해야 하는지까지 여기서 말한다.
        private RectTransform _emptyRoot;
        private TextMeshProUGUI _emptyBody;
        private Button _emptyAction;

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(_offers.Count / (float)PageSize));

        protected override void Build()
        {
            _wallet = UIBuild.Text("Wallet", Body, "", UITheme.FontBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_wallet.rectTransform, new Vector2(0f, 0.90f), Vector2.one);

            for (int i = 0; i < PageSize; i++)
            {
                _cards.Add(new Card(Body, i, OnPick));
            }

            _message = UIBuild.Text("Message", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_message.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.16f));

            _prev = UIBuild.Button("Prev", Body, "◀", () => TurnPage(-1));
            UIBuild.Anchor(_prev.GetComponent<RectTransform>(),
                new Vector2(0.24f, 0.01f), new Vector2(0.32f, 0.09f));

            _pageLabel = UIBuild.Text("Page", Body, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_pageLabel.rectTransform, new Vector2(0.33f, 0.01f), new Vector2(0.43f, 0.09f));

            _next = UIBuild.Button("Next", Body, "▶", () => TurnPage(1));
            UIBuild.Anchor(_next.GetComponent<RectTransform>(),
                new Vector2(0.44f, 0.01f), new Vector2(0.52f, 0.09f));

            Button close = UIBuild.Button("Close", Body, "닫기", Hide, primary: true);
            UIBuild.Anchor(close.GetComponent<RectTransform>(),
                new Vector2(0.58f, 0.01f), new Vector2(0.76f, 0.09f));

            BuildEmptyState();
        }

        /// <summary>
        /// 빈 상태. 고리 + 물음표 · 한 줄 제목 · 설명 · 행동 유도 버튼으로 세운다.
        /// 일러스트 자산이 없으므로 도형(<see cref="UIShapes.Disc"/>)으로 그린다.
        /// </summary>
        private void BuildEmptyState()
        {
            _emptyRoot = UIBuild.Container("EmptyState", Body);
            UIBuild.Anchor(_emptyRoot, new Vector2(0f, 0.16f), new Vector2(1f, 0.88f));

            Image ring = UIBuild.Solid("Ring", _emptyRoot, Color.white);
            ring.sprite = UIShapes.Disc(132, UITheme.AccentFaint, 0.86f);
            ring.raycastTarget = false;
            UIBuild.Pin(ring.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(132f, 132f), Vector2.zero);

            TextMeshProUGUI glyph = UIBuild.Text("Glyph", _emptyRoot, "?", UITheme.FontDisplay,
                UITheme.AccentDim, TextAlignmentOptions.Center);
            UIBuild.Pin(glyph.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(132f, 132f), Vector2.zero);

            TextMeshProUGUI headline = UIBuild.Text("Headline", _emptyRoot, "받아 둔 스킬 힌트가 없습니다",
                UITheme.FontTitle, UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(headline.rectTransform, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.5f));

            _emptyBody = UIBuild.Text("Body", _emptyRoot, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Top, wrap: true);
            UIBuild.Anchor(_emptyBody.rectTransform, new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.37f));

            _emptyAction = UIBuild.Button("GoTrain", _emptyRoot, "훈련하러 가기", GoTraining, primary: true);
            UIBuild.Anchor(_emptyAction.GetComponent<RectTransform>(),
                new Vector2(0.37f, 0.02f), new Vector2(0.63f, 0.14f));

            _emptyRoot.gameObject.SetActive(false);
        }

        /// <summary>빈 상태의 행동 유도. 스킬 화면을 닫고 곧바로 훈련 화면으로 보낸다.</summary>
        private void GoTraining()
        {
            Hide();
            GameManager.Instance?.OpenTrainingFromPreparation();
        }

        public override void Show()
        {
            EnsureBuilt();
            _message.text = "";
            _page = 0;
            Refresh();
            base.Show();
        }

        private void TurnPage(int delta)
        {
            _page = Mathf.Clamp(_page + delta, 0, PageCount - 1);
            Refresh();
        }

        private void Refresh()
        {
            _offers = TrainingManager.GetSkillOffers();
            _page = Mathf.Clamp(_page, 0, PageCount - 1);

            int points = TrainingManager.State.SkillPoints;
            bool empty = _offers.Count == 0;

            SkillHintState hintState = TrainingManager.Hints;
            string pity = hintState.PityReady
                ? "다음 훈련은 힌트 보장"
                : $"힌트 보장까지 훈련 {hintState.TrainingsUntilPity}회";
            _wallet.text = empty
                ? $"스킬 Pt {points}   ·   {pity}"
                : $"스킬 Pt {points}   ·   힌트 {_offers.Count}개   ·   힌트 레벨이 오를수록 값이 싸집니다";

            for (int i = 0; i < _cards.Count; i++)
            {
                int index = _page * PageSize + i;
                _cards[i].Bind(index < _offers.Count ? _offers[index] : default,
                    index < _offers.Count);
            }

            // 빈 판과 쪽 넘김은 서로 배타적이다. 힌트가 없으면 쪽 번호도 의미가 없다.
            RefreshEmptyState(empty);
            _pageLabel.gameObject.SetActive(!empty);
            _prev.gameObject.SetActive(!empty);
            _next.gameObject.SetActive(!empty);

            _pageLabel.text = $"{_page + 1} / {PageCount}";
            _prev.interactable = _page > 0;
            _next.interactable = _page < PageCount - 1;
        }

        /// <summary>
        /// 빈 상태의 안내문과 행동 유도. 훈련을 열 수 없는 상황이면 버튼을 끄고
        /// <b>왜 못 여는지</b>까지 적는다 — 눌리지 않는 버튼만 남기면 고장으로 읽힌다.
        /// </summary>
        private void RefreshEmptyState(bool empty)
        {
            _emptyRoot.gameObject.SetActive(empty);
            if (!empty) return;
            SkillHintState hintState = TrainingManager.Hints;
            string pity = hintState.PityReady
                ? "다음 훈련은 힌트 보장"
                : $"힌트 보장까지 훈련 {hintState.TrainingsUntilPity}회";

            bool canTrain = GameManager.Instance?.CanOpenTraining == true;
            _emptyBody.text =
                "훈련에 앉은 서포트가 확률로 힌트를 흘립니다. 특기 훈련에 앉은 서포트일수록 잘 흘리고, " +
                "같은 힌트를 다시 받으면 값이 싸집니다.\n" +
                $"힌트 없이 끝난 훈련이 {SkillHintState.PityTrainings}번 쌓이면 다음 훈련은 반드시 힌트를 줍니다 — {pity}." +
                (canTrain ? "" : "\n\n이번 준비 페이즈의 행동은 이미 썼습니다. 다음 스테이지에서 훈련하세요.");

            _emptyAction.interactable = canTrain;
            TextMeshProUGUI label = _emptyAction.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = canTrain ? UITheme.TextOnAccent : UITheme.TextMuted;
        }

        private void OnPick(int slot)
        {
            int index = _page * PageSize + slot;
            if (index < 0 || index >= _offers.Count) return;

            TrainingManager.SkillOffer offer = _offers[index];
            if (!offer.CanLearn)
            {
                _message.text = $"{offer.Name} — {offer.BlockedReason}";
                Refresh();
                return;
            }

            if (!TrainingManager.TryLearnSkill(offer.CodeId, out string reason))
            {
                _message.text = $"{offer.Name} — {reason}";
                Refresh();
                return;
            }

            GameManager.Instance?.NotifySkillLearned();
            _message.text = $"{offer.Name} 습득 — 스킬 Pt {offer.Cost}를 썼습니다.";
            Refresh();
        }

        /// <summary>등급 색. 은은 은색, 금은 앰버, 고유는 보라를 그대로 쓴다.</summary>
        private static Color GradeColor(BaseEnums.CodeGrade grade) => grade switch
        {
            BaseEnums.CodeGrade.Enhanced => UITheme.CodeEnhanced,
            BaseEnums.CodeGrade.Unique => UITheme.CodeUnique,
            _ => UITheme.CodeNormal,
        };

        private sealed class Card
        {
            private readonly GameObject _root;
            private readonly Image _gradeStrip;
            private readonly TextMeshProUGUI _grade;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _hint;
            private readonly TextMeshProUGUI _cost;
            private readonly TextMeshProUGUI _state;

            public Card(Transform parent, int index, System.Action<int> onPick)
            {
                Image panel = UIBuild.Panel($"Skill{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
                _root = panel.gameObject;

                int column = index % Columns;
                int row = index / Columns;
                float width = 1f / Columns;
                const float top = 0.88f;
                const float bottom = 0.18f;
                float height = (top - bottom) / Rows;
                float cardTop = top - row * height;
                UIBuild.Anchor(panel.rectTransform,
                    new Vector2(column * width, cardTop - height),
                    new Vector2((column + 1) * width, cardTop), 8f, 6f);

                UIBuild.OnClick(_root, () => onPick(index));

                _gradeStrip = UIBuild.Solid("Grade", panel.transform, UITheme.CodeNormal);
                UIBuild.Anchor(_gradeStrip.rectTransform, new Vector2(0f, 0.955f), new Vector2(1f, 1f));

                _grade = UIBuild.Label("GradeLabel", panel.transform, "", UITheme.FontMicro, UITheme.TextMuted,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(_grade.rectTransform, new Vector2(0f, 0.80f), new Vector2(1f, 0.94f), 8f, 0f);

                _name = UIBuild.Text("Name", panel.transform, "", UITheme.FontHeading, UITheme.TextPrimary,
                    TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 0.79f), 10f, 0f);

                _hint = UIBuild.Text("Hint", panel.transform, "", UITheme.FontCaption, UITheme.TextSecondary,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(_hint.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.52f), 8f, 0f);

                _cost = UIBuild.Text("Cost", panel.transform, "", UITheme.FontTitle, UITheme.Accent,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(_cost.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.36f), 8f, 0f);

                _state = UIBuild.Text("State", panel.transform, "", UITheme.FontMicro, UITheme.TextMuted,
                    TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_state.rectTransform, new Vector2(0f, 0.03f), new Vector2(1f, 0.15f), 6f, 0f);
            }

            public void Bind(TrainingManager.SkillOffer offer, bool visible)
            {
                _root.SetActive(visible);
                if (!visible) return;

                Color grade = GradeColor(offer.Grade);
                _gradeStrip.color = grade;
                _grade.text = offer.Grade == BaseEnums.CodeGrade.Enhanced ? "강화 · 금" : "일반 · 은";
                _grade.color = grade;
                _name.text = offer.Name;
                _hint.text = $"힌트 Lv.{offer.HintLevel} / {SkillHintState.MaxLevel}";

                _cost.text = $"{offer.Cost} Pt";
                _cost.color = offer.CanLearn ? UITheme.Accent : UITheme.TextMuted;
                _state.text = offer.CanLearn ? "습득" : offer.BlockedReason;
                _state.color = offer.CanLearn ? UITheme.Positive : UITheme.TextMuted;
            }
        }
    }
}
