using System.Collections.Generic;
using BaseClasses;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 훈련 결과 화면. 결정을 누른 뒤 <b>무슨 일이 일어났는지</b>를 한 장에 보여 준다.
    ///
    ///   위   — 성공 / 실패. 화면에서 제일 먼저 읽혀야 하는 한 가지다.
    ///   가운데 — 오른 스탯, 스킬 Pt, 훈련 레벨, 체력, 컨디션 변화
    ///   아래 — 이 훈련에 참여한 서포트별 기여와 우정 변화, 흘러나온 스킬 힌트
    ///
    /// 예전에는 준비 페이즈 안내줄에 한 문장으로 흘려보냈다. 실패했는지조차
    /// 눈에 띄지 않아, 체력을 20 쓰고 아무것도 못 얻은 턴을 그냥 지나치게 됐다.
    /// </summary>
    public class TrainingResultScreen : ModalScreen
    {
        protected override string CanvasName => "TrainingResultCanvas";
        protected override int SortingOrder => 80;
        protected override string Title => "훈련 결과";
        protected override string Caption => "TRAINING RESULT";
        protected override Vector2 AnchorMin => new(0.26f, 0.16f);
        protected override Vector2 AnchorMax => new(0.74f, 0.88f);

        /// <summary>서포트 줄을 몇 개까지 그려 둘지. 편성 상한과 같다.</summary>
        private const int MaxSupportRows = 5;

        private Image _verdictPanel;
        private TextMeshProUGUI _verdictLabel;
        private TextMeshProUGUI _verdictSub;

        private Image _gainCard;
        private TextMeshProUGUI _gainValue;
        private TextMeshProUGUI _gainStat;
        private TextMeshProUGUI _gainSecondary;
        private readonly TextMeshProUGUI[] _rowKeys = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _rowValues = new TextMeshProUGUI[4];

        private TextMeshProUGUI _supportCaption;
        private RectTransform _supportArea;
        private readonly SupportResultRow[] _supportRows = new SupportResultRow[MaxSupportRows];
        private TextMeshProUGUI _transferLine;

        protected override void Build()
        {
            BuildVerdict();
            BuildGain();
            BuildSupports();
            BuildConfirm();
        }

        // ── 성공 / 실패 ──────────────────────────────────────────────

        private void BuildVerdict()
        {
            _verdictPanel = UIBuild.Panel("Verdict", Body, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 12, UITheme.Outline, 1);
            UIBuild.Anchor(_verdictPanel.rectTransform, new Vector2(0f, 0.80f), new Vector2(1f, 1f));

            _verdictLabel = UIBuild.Text("Label", _verdictPanel.transform, "", 46f,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            _verdictLabel.characterSpacing = 10f;
            UIBuild.Anchor(_verdictLabel.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.94f));

            _verdictSub = UIBuild.Text("Sub", _verdictPanel.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_verdictSub.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.36f));
        }

        // ── 상승 · 소모 ──────────────────────────────────────────────

        /// <summary>
        /// 왼쪽은 오른 스탯 카드(스탯 이름 · 큰 +수치 · 부가 상승), 오른쪽은 변화 네 줄이다.
        ///
        /// 예전에는 "+5  (INT +1 · LUK +1)"를 54pt 한 줄로 32% 폭 칸에 넣었다. 글자 자동 맞춤은
        /// 기준의 82%까지만 줄어들어 문장이 칸을 뚫고 오른쪽 표 위로 쏟아졌다. 큰 숫자는 주 상승만
        /// 들고, 부가 상승은 그 아래 작은 줄로 나눈다.
        /// </summary>
        private void BuildGain()
        {
            RectTransform block = UIBuild.Container("Gain", Body);
            UIBuild.Anchor(block, new Vector2(0f, 0.52f), new Vector2(1f, 0.77f));

            _gainCard = UIBuild.Panel("GainCard", block, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10);
            UIBuild.Anchor(_gainCard.rectTransform, new Vector2(0f, 0f), new Vector2(0.36f, 1f));

            _gainStat = UIBuild.Label("Stat", _gainCard.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_gainStat.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.96f), 8f, 0f);

            _gainValue = UIBuild.Text("Value", _gainCard.transform, "", 48f, UITheme.Accent,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_gainValue.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.74f), 8f, 0f);
            _gainValue.fontSizeMin = UITheme.FontHeading;

            _gainSecondary = UIBuild.Text("Secondary", _gainCard.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_gainSecondary.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.30f), 8f, 0f);
            _gainSecondary.overflowMode = TextOverflowModes.Ellipsis;

            for (int i = 0; i < _rowKeys.Length; i++)
            {
                float top = 1f - i * 0.25f;

                _rowKeys[i] = UIBuild.Text($"Key{i}", block, "", UITheme.FontCaption, UITheme.TextMuted);
                UIBuild.Anchor(_rowKeys[i].rectTransform,
                    new Vector2(0.41f, top - 0.24f), new Vector2(0.62f, top));

                _rowValues[i] = UIBuild.Text($"Value{i}", block, "", UITheme.FontBody,
                    UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_rowValues[i].rectTransform,
                    new Vector2(0.62f, top - 0.24f), new Vector2(1f, top), 8f, 0f);
                // 체력 변화처럼 "-14 (남은 86)"보다 긴 값도 작은 창에서 한 줄로 보인다.
                _rowValues[i].fontSizeMin = UITheme.FontFloor;
                _rowValues[i].overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // ── 서포트 ───────────────────────────────────────────────────

        private void BuildSupports()
        {
            _supportCaption = UIBuild.Label("SupportCaption", Body, "참여 서포트",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Anchor(_supportCaption.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 0.51f), 4f, 0f);

            Image rule = UIBuild.Divider("SupportRule", Body);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 0.455f), new Vector2(1f, 0.455f));
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);

            _supportArea = UIBuild.Container("Supports", Body);
            UIBuild.Anchor(_supportArea, new Vector2(0f, 0.16f), new Vector2(1f, 0.45f));

            for (int i = 0; i < _supportRows.Length; i++)
            {
                _supportRows[i] = new SupportResultRow(_supportArea, i, _supportRows.Length);
            }

            _transferLine = UIBuild.Text("Transfer", Body, "", UITheme.FontCaption, UITheme.Accent);
            UIBuild.Anchor(_transferLine.rectTransform, new Vector2(0f, 0.11f), new Vector2(1f, 0.155f), 4f, 0f);
        }

        private void BuildConfirm()
        {
            Image confirm = UIBuild.Panel("Confirm", Body, UITheme.Accent, UIShapes.Corner.Diagonal, 10);
            UIBuild.Anchor(confirm.rectTransform, new Vector2(0.30f, 0f), new Vector2(0.70f, 0.09f));
            UIBuild.OnClick(confirm.gameObject, Confirm);

            TextMeshProUGUI label = UIBuild.Text("Label", confirm.transform, "확인", UITheme.FontHeading,
                UITheme.TextOnAccent, TextAlignmentOptions.Center);
            UIBuild.Stretch(label.rectTransform);
        }

        private void Confirm()
        {
            Hide();
            GameManager.Instance?.CompleteTrainingResult();
        }

        // ── 표시 ─────────────────────────────────────────────────────

        public void Show(TrainingManager.TrainingResult result)
        {
            EnsureBuilt();
            base.Show();

            bool failed = result.Failed;

            _verdictPanel.sprite = UIShapes.CutCorner(12,
                failed ? new Color(0.992f, 0.941f, 0.945f, 1f) : new Color(0.925f, 0.976f, 0.949f, 1f),
                UIShapes.Corner.Diagonal,
                failed ? UITheme.Danger : UITheme.Positive, 2);

            _verdictLabel.text = failed ? "실 패" : "성 공";
            _verdictLabel.color = failed ? UITheme.Danger : UITheme.Positive;

            _verdictSub.text = failed
                ? $"{result.FocusName} 훈련에 실패했습니다 · 성공률 {100 - result.FailureRate}%"
                : $"{result.FocusName} 훈련 · 성공률 {100 - result.FailureRate}%";

            Color statColor = failed ? UITheme.TextMuted : UITheme.Stat(result.Focus);
            _gainValue.text = failed ? "+0" : $"+{result.StatGain}";
            _gainValue.color = statColor;
            _gainStat.text = $"{result.Focus} 상승";
            _gainStat.color = statColor;
            _gainSecondary.text = !failed && result.SecondaryText.Length > 0 ? result.SecondaryText : "";
            _gainCard.sprite = UIShapes.CutCorner(10,
                failed ? UITheme.SurfaceSunken : new Color(statColor.r, statColor.g, statColor.b, 0.10f),
                UIShapes.Corner.Diagonal, failed ? UITheme.Outline : statColor, 1);

            SetRow(0, "스킬 Pt", failed ? "—" : $"+{result.SkillPointsGained}",
                failed ? UITheme.TextMuted : UITheme.Positive);
            SetRow(1, "훈련 레벨", failed ? "그대로" : $"Lv {result.NewFocusTrainingLevel}",
                failed ? UITheme.TextMuted : UITheme.TextPrimary);
            // 지능 훈련처럼 체력을 오히려 채워 주는 훈련이 있다. 소모량이 음수면 회복이다.
            SetRow(2, "체력",
                result.EnergySpent > 0
                    ? $"-{result.EnergySpent}   (남은 {result.EnergyAfter})"
                    : $"+{-result.EnergySpent} 회복   (남은 {result.EnergyAfter})",
                result.EnergySpent > 0 ? new Color(0.878f, 0.647f, 0.290f) : UITheme.Positive);
            // 훈련은 컨디션을 바꾸지 않는다. 대신 이번 훈련이 쓴 트레이닝 노트가 있으면 그것을 적는다.
            SetRow(3, "컨디션",
                result.NoteBonusUsed > 0
                    ? $"{result.ConditionAfter} · 노트 +{result.NoteBonusUsed}% 사용"
                    : result.ConditionAfter,
                result.NoteBonusUsed > 0 ? UITheme.Positive : UITheme.TextPrimary);

            RefreshSupports(result);
        }

        private void SetRow(int index, string key, string value, Color color)
        {
            _rowKeys[index].text = key;
            _rowValues[index].text = value;
            _rowValues[index].color = color;
        }

        private void RefreshSupports(TrainingManager.TrainingResult result)
        {
            List<TrainingManager.SupportOutcome> supports =
                result.Supports ?? new List<TrainingManager.SupportOutcome>();

            for (int i = 0; i < _supportRows.Length; i++)
            {
                _supportRows[i].Bind(i < supports.Count ? supports[i] : (TrainingManager.SupportOutcome?)null);
            }

            _supportCaption.text = supports.Count > 0
                ? $"참여 서포트 {supports.Count}명"
                : "이번 훈련에 참여한 서포트가 없습니다";

            // 힌트는 매번 나오지 않는다. 나왔을 때만 한 줄을 내준다.
            var names = new List<string>();
            foreach (TrainingManager.SupportOutcome support in supports)
            {
                if (!string.IsNullOrEmpty(support.HintedName))
                {
                    names.Add($"{support.HintedName} Lv.{support.HintedLevel}");
                }
            }

            _transferLine.text = names.Count > 0
                ? $"스킬 힌트 — {string.Join(", ", names)} (준비 페이즈에서 스킬 Pt로 습득)"
                : "";
        }

        /// <summary>서포트 한 명의 결과 줄. 초상화 · 이름 · 기여 · 우정 변화.</summary>
        private sealed class SupportResultRow
        {
            private const float RowHeight = 36f;
            private const float RowGap = 5f;

            private readonly Image _frame;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _bonus;
            private readonly TextMeshProUGUI _bond;

            public SupportResultRow(Transform parent, int index, int count)
            {
                // 칸 높이를 고정해 위에서부터 쌓는다. 영역을 5등분하면 창 크기에 따라 줄이
                // 납작해져 초상화가 점만 해졌다.
                _frame = UIBuild.Panel($"Support{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 6);
                RectTransform rect = _frame.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, RowHeight);
                rect.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap));

                var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(_frame.transform, false);
                _portrait = portraitObject.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Pin(_portrait.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(RowHeight - 6f, RowHeight - 6f), new Vector2(8f, 0f));

                _name = UIBuild.Text("Name", _frame.transform, "", UITheme.FontBody, UITheme.TextPrimary);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0f), new Vector2(0.52f, 1f));
                _name.rectTransform.offsetMin = new Vector2(RowHeight + 12f, 0f);
                _name.overflowMode = TextOverflowModes.Ellipsis;

                _bonus = UIBuild.Text("Bonus", _frame.transform, "", UITheme.FontBody, UITheme.Positive,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_bonus.rectTransform, new Vector2(0.52f, 0f), new Vector2(0.72f, 1f));

                _bond = UIBuild.Text("Bond", _frame.transform, "", UITheme.FontCaption, UITheme.TextSecondary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_bond.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 1f), 12f, 0f);
            }

            public void Bind(TrainingManager.SupportOutcome? outcome)
            {
                _frame.gameObject.SetActive(outcome.HasValue);
                if (!outcome.HasValue) return;

                TrainingManager.SupportOutcome value = outcome.Value;

                Sprite portrait = SpriteResource.LoadPortrait(value.PortraitPath);
                _portrait.sprite = portrait;
                _portrait.enabled = portrait != null;

                _name.text = value.Friendship ? $"{value.Name}   우정 훈련" : value.Name;
                _name.color = value.Friendship ? UITheme.Accent : UITheme.TextPrimary;

                _bonus.text = value.StatBonus > 0 ? $"+{value.StatBonus}" : "—";
                _bonus.color = value.StatBonus > 0 ? UITheme.Positive : UITheme.TextMuted;

                _bond.text = value.BondAfter > value.BondBefore
                    ? $"우정 {value.BondBefore} → {value.BondAfter}"
                    : $"우정 {value.BondAfter}";
            }
        }
    }
}
