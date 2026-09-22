using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 육성 페이즈. 우마무스메의 훈련 화면 구성을 따른다.
    ///
    ///   상단 — 스테이지 · 체력 게이지 · 컨디션 · 스킬 Pt
    ///   좌   — 고른 훈련의 상세(레벨 · 상승 내역 · 체력 소모 · 성공률)
    ///   중앙 — 메인 캐릭터
    ///   우   — <b>이 훈련에 앉은 서포트만</b>. 다른 자리에 앉은 서포트는 아예 나오지 않는다.
    ///   하단 — 5스탯 현황 + 훈련 5종 + 결정
    ///
    /// <b>훈련 하나는 대응하는 스탯 하나만 올린다.</b> 계산은 전부
    /// <see cref="TrainingManager"/>에 있고 이 화면은 그 값을 읽어 그린다.
    /// </summary>
    public class TrainingScreen : ModalScreen
    {
        protected override string CanvasName => "TrainingCanvas";
        protected override int SortingOrder => 70;
        protected override string Title => "육성 페이즈";
        protected override Vector2 AnchorMin => new(0.04f, 0.06f);
        protected override Vector2 AnchorMax => new(0.96f, 0.94f);

        private static readonly BaseEnums.PrimaryStat[] StatOrder =
        {
            BaseEnums.PrimaryStat.STR, BaseEnums.PrimaryStat.DEX, BaseEnums.PrimaryStat.CON,
            BaseEnums.PrimaryStat.INT, BaseEnums.PrimaryStat.LUK,
        };

        private const int MaxSupportRows = 5;

        private BaseEnums.PrimaryStat _selected = BaseEnums.PrimaryStat.STR;
        private bool _selectionInitialized;

        // 상단
        private TextMeshProUGUI _stageLabel;
        private TextMeshProUGUI _energyLabel;
        private Image _energyFill;
        private TextMeshProUGUI _conditionLabel;
        private TextMeshProUGUI _skillPointLabel;

        // 좌측 상세
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailEffect;
        private TextMeshProUGUI _detailLevel;
        private readonly Image[] _levelPips = new Image[TrainingState.MaxTrainingLevel];
        private TextMeshProUGUI _gainValue;
        private TextMeshProUGUI _gainStat;
        private readonly TextMeshProUGUI[] _breakdownKeys = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _breakdownValues = new TextMeshProUGUI[4];
        private TextMeshProUGUI _costValue;
        private TextMeshProUGUI _skillValue;
        private TextMeshProUGUI _failValue;

        // 중앙
        private Image _mainPortrait;
        private TextMeshProUGUI _mainName;
        private TextMeshProUGUI _mainMeta;

        // 우측
        private readonly SupportRow[] _supportRows = new SupportRow[MaxSupportRows];
        private TextMeshProUGUI _supportCaption;

        // 하단
        private readonly StatCard[] _statCards = new StatCard[5];
        private readonly TrainingButton[] _trainingButtons = new TrainingButton[5];
        private TextMeshProUGUI _decideHint;

        protected override void Build()
        {
            // 명시적인 뒤로 가기. 머리 오른쪽 위 — 모달 제목과 같은 줄이다.
            Button back = UIBuild.Button("Back", Body.parent, "돌아가기  <size=70%>ESC</size>",
                () => GameManager.Instance?.CancelTrainingFromPreparation(), false, UITheme.FontBody);
            back.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            UIBuild.Pin(back.image.rectTransform, new Vector2(1f, 1f), new Vector2(170f, 42f),
                new Vector2(-UITheme.PanelPad, -18f));

            BuildTopStrip();
            BuildDetailPanel();
            BuildMainColumn();
            BuildSupportColumn();
            BuildStatStrip();
            BuildTrainingButtons();
        }

        public override void Show()
        {
            base.Show();

            // 이번 턴의 서포트 배치를 확정한다. 이미 굴렸으면 그대로 쓴다 —
            // 화면을 여닫는 것만으로 자리가 다시 굴려지면 배치가 선택이 아니게 된다.
            TrainingManager.EnsureSupportPlacement();

            if (!_selectionInitialized)
            {
                _selected = ResolveDefaultFocus();
                _selectionInitialized = true;
            }

            Refresh();
        }

        /// <summary>ESC는 [돌아가기]와 같다. 준비 행동을 쓰지 않고 준비 페이즈로 돌아간다.</summary>
        protected override bool OnEscape()
        {
            GameManager.Instance?.CancelTrainingFromPreparation();
            return true;
        }

        /// <summary>처음 열릴 때는 메인의 주스탯을 골라 둔다.</summary>
        private static BaseEnums.PrimaryStat ResolveDefaultFocus()
        {
            Unit main = TrainingManager.GetMainUnit();
            if (main != null && Enum.TryParse(main.MainStat, true, out BaseEnums.PrimaryStat parsed))
            {
                return parsed;
            }

            return BaseEnums.PrimaryStat.STR;
        }

        // ── 상단 ─────────────────────────────────────────────────────

        private void BuildTopStrip()
        {
            RectTransform strip = UIBuild.Container("TopStrip", Body);
            UIBuild.Anchor(strip, new Vector2(0f, 0.87f), new Vector2(1f, 1f));

            _stageLabel = UIBuild.Text("Stage", strip, "", UITheme.FontHeading, UITheme.TextPrimary);
            UIBuild.Anchor(_stageLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.22f, 1f));

            // 체력 게이지
            TextMeshProUGUI energyCaption = UIBuild.Label("EnergyCaption", strip, "체력",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Anchor(energyCaption.rectTransform, new Vector2(0.24f, 0.55f), new Vector2(0.40f, 1f));

            Image track = UIBuild.Bar("Energy", strip, UITheme.Hp, UITheme.Track);
            _energyFill = track;
            var energyTrack = (RectTransform)track.rectTransform.parent;
            UIBuild.Anchor(energyTrack, new Vector2(0.24f, 0.16f), new Vector2(0.55f, 0.5f));

            _energyLabel = UIBuild.Text("EnergyValue", strip, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_energyLabel.rectTransform, new Vector2(0.40f, 0.55f), new Vector2(0.55f, 1f));

            _conditionLabel = UIBuild.Text("Condition", strip, "", UITheme.FontHeading, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_conditionLabel.rectTransform, new Vector2(0.58f, 0f), new Vector2(0.78f, 1f));

            _skillPointLabel = UIBuild.Text("SkillPoint", strip, "", UITheme.FontHeading, UITheme.Accent,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_skillPointLabel.rectTransform, new Vector2(0.80f, 0f), new Vector2(1f, 1f));
        }

        // ── 좌측 상세 ────────────────────────────────────────────────

        private void BuildDetailPanel()
        {
            Image panel = UIBuild.Panel("Detail", Body, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10);
            UIBuild.Anchor(panel.rectTransform, new Vector2(0f, 0.30f), new Vector2(0.28f, 0.84f));

            Transform root = panel.transform;

            _detailName = UIBuild.Text("Name", root, "", UITheme.FontTitle, UITheme.TextPrimary);
            UIBuild.Anchor(_detailName.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), 18f, 0f);

            _detailEffect = UIBuild.Text("Effect", root, "", UITheme.FontCaption, UITheme.TextMuted);
            UIBuild.Anchor(_detailEffect.rectTransform, new Vector2(0f, 0.81f), new Vector2(1f, 0.88f), 18f, 0f);

            _detailLevel = UIBuild.Text("Level", root, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_detailLevel.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.79f), 18f, 0f);

            for (int i = 0; i < _levelPips.Length; i++)
            {
                Image pip = UIBuild.Solid($"Pip{i}", root, UITheme.Divider);
                float width = 1f / _levelPips.Length;
                UIBuild.Anchor(pip.rectTransform,
                    new Vector2(i * width, 0.68f), new Vector2((i + 1) * width, 0.71f), 20f, 0f);
                _levelPips[i] = pip;
            }

            _gainValue = UIBuild.Text("GainValue", root, "", UITheme.FontDisplay, UITheme.Accent);
            UIBuild.Anchor(_gainValue.rectTransform, new Vector2(0f, 0.52f), new Vector2(0.55f, 0.65f), 18f, 0f);

            _gainStat = UIBuild.Text("GainStat", root, "", UITheme.FontBody, UITheme.TextSecondary);
            UIBuild.Anchor(_gainStat.rectTransform, new Vector2(0.55f, 0.52f), new Vector2(1f, 0.62f), 0f, 0f);

            for (int i = 0; i < _breakdownKeys.Length; i++)
            {
                float top = 0.48f - i * 0.062f;
                _breakdownKeys[i] = UIBuild.Text($"Key{i}", root, "", UITheme.FontCaption, UITheme.TextMuted);
                UIBuild.Anchor(_breakdownKeys[i].rectTransform,
                    new Vector2(0f, top - 0.05f), new Vector2(0.62f, top), 18f, 0f);

                _breakdownValues[i] = UIBuild.Text($"Value{i}", root, "", UITheme.FontCaption,
                    UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_breakdownValues[i].rectTransform,
                    new Vector2(0.62f, top - 0.05f), new Vector2(1f, top), 0f, 0f);
                _breakdownValues[i].rectTransform.offsetMax =
                    new Vector2(-18f, _breakdownValues[i].rectTransform.offsetMax.y);
            }

            _costValue = BuildDetailTile(root, 0f, "체력", out _);
            _skillValue = BuildDetailTile(root, 0.34f, "스킬 Pt", out _);
            _failValue = BuildDetailTile(root, 0.68f, "성공률", out _);
        }

        private static TextMeshProUGUI BuildDetailTile(Transform root, float x, string caption,
            out TextMeshProUGUI captionLabel)
        {
            Image tile = UIBuild.Panel($"Tile{caption}", root, UITheme.Surface, UIShapes.Corner.None, 4);
            UIBuild.Anchor(tile.rectTransform, new Vector2(x, 0.04f), new Vector2(x + 0.32f, 0.20f), 6f, 0f);
            tile.rectTransform.offsetMin = new Vector2(tile.rectTransform.offsetMin.x + 12f, tile.rectTransform.offsetMin.y);

            captionLabel = UIBuild.Label("Caption", tile.transform, caption, UITheme.FontMicro, UITheme.TextMuted,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(captionLabel.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 0.92f));

            TextMeshProUGUI value = UIBuild.Text("Value", tile.transform, "", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(value.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.52f));
            return value;
        }

        // ── 중앙 ─────────────────────────────────────────────────────

        private void BuildMainColumn()
        {
            RectTransform column = UIBuild.Container("Main", Body);
            UIBuild.Anchor(column, new Vector2(0.30f, 0.30f), new Vector2(0.63f, 0.84f));

            Image card = UIBuild.Panel("Card", column, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal, 24,
                UITheme.Outline, 1);
            UIBuild.Anchor(card.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 1f));

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(card.transform, false);
            _mainPortrait = portraitObject.GetComponent<Image>();
            _mainPortrait.preserveAspect = true;
            _mainPortrait.raycastTarget = false;
            UIBuild.Stretch(_mainPortrait.rectTransform, 10f, 10f);

            _mainName = UIBuild.Text("Name", column, "", UITheme.FontTitle, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_mainName.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.17f));

            _mainMeta = UIBuild.Text("Meta", column, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_mainMeta.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.08f));
        }

        // ── 우측 서포트 ──────────────────────────────────────────────

        private void BuildSupportColumn()
        {
            RectTransform column = UIBuild.Container("Supports", Body);
            UIBuild.Anchor(column, new Vector2(0.65f, 0.30f), new Vector2(1f, 0.84f));

            _supportCaption = UIBuild.Label("Caption", column, "서포트 편성", UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Anchor(_supportCaption.rectTransform, new Vector2(0f, 0.94f), new Vector2(1f, 1f));

            for (int i = 0; i < _supportRows.Length; i++)
            {
                _supportRows[i] = new SupportRow(column, i, _supportRows.Length);
            }
        }

        // ── 하단 ─────────────────────────────────────────────────────

        private void BuildStatStrip()
        {
            RectTransform strip = UIBuild.Container("Stats", Body);
            UIBuild.Anchor(strip, new Vector2(0f, 0.16f), new Vector2(1f, 0.27f));

            for (int i = 0; i < StatOrder.Length; i++)
            {
                _statCards[i] = new StatCard(strip, StatOrder[i], i, StatOrder.Length);
            }
        }

        private void BuildTrainingButtons()
        {
            RectTransform strip = UIBuild.Container("Trainings", Body);
            UIBuild.Anchor(strip, new Vector2(0f, 0f), new Vector2(0.78f, 0.14f));

            for (int i = 0; i < TrainingManager.Options.Length; i++)
            {
                BaseEnums.PrimaryStat stat = TrainingManager.Options[i].Stat;
                _trainingButtons[i] = new TrainingButton(strip, stat, i, TrainingManager.Options.Length,
                    () => Select(stat));
            }

            Image decide = UIBuild.Panel("Decide", Body, UITheme.Accent, UIShapes.Corner.Diagonal, 10);
            UIBuild.Anchor(decide.rectTransform, new Vector2(0.80f, 0f), new Vector2(1f, 0.14f));
            UIBuild.OnClick(decide.gameObject, Decide);

            TextMeshProUGUI label = UIBuild.Text("Label", decide.transform, "결정", UITheme.FontTitle,
                UITheme.TextOnAccent, TextAlignmentOptions.Center);
            UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.92f));

            _decideHint = UIBuild.Text("Hint", decide.transform, "", UITheme.FontMicro,
                new Color(UITheme.TextOnAccent.r, UITheme.TextOnAccent.g, UITheme.TextOnAccent.b, 0.72f),
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_decideHint.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.42f));
        }

        // ── 동작 ─────────────────────────────────────────────────────

        private void Select(BaseEnums.PrimaryStat stat)
        {
            _selected = stat;
            _selectionInitialized = true;
            Refresh();
        }

        private void Decide()
        {
            GameManager.Instance?.CompleteTrainingPhaseWithFocus(_selected);
        }

        private void Refresh()
        {
            TrainingState state = TrainingManager.State;
            TrainingManager.TrainingOption option = TrainingManager.GetOption(_selected);
            Unit main = TrainingManager.GetMainUnit();

            RefreshTopStrip(state);
            RefreshDetail(state, option);
            RefreshMain(main);
            RefreshSupports();
            RefreshStatCards(main);
            RefreshButtons(state);
        }

        private void RefreshTopStrip(TrainingState state)
        {
            RoundManager round = GameManager.Instance?.RoundManager;
            _stageLabel.text = round != null ? $"{round.Round}-{round.StageInRound} 육성" : "육성";

            float ratio = state.Energy / (float)TrainingState.MaxEnergy;
            _energyFill.fillAmount = ratio;
            _energyFill.color = state.Energy >= 60 ? UITheme.Hp
                : state.Energy >= 30 ? new Color(0.878f, 0.647f, 0.290f)
                : UITheme.Danger;
            _energyLabel.text = $"{state.Energy} / {TrainingState.MaxEnergy}";

            _conditionLabel.text = $"컨디션 {state.ConditionName} · ×{state.ConditionMultiplier:0.00}" +
                                   (state.NotePercent > 0 ? $" · 노트 +{state.NotePercent}%" : "");
            // 힌트 천장이 어디쯤인지 보여 준다. 힌트가 운이라 "언제 오느냐"를 모르면 Pt만 쌓이는 느낌이 든다.
            global::Core.SkillHintState hints = TrainingManager.Hints;
            _skillPointLabel.text = hints.PityReady
                ? $"스킬 Pt {state.SkillPoints} · 이번 훈련 힌트 보장"
                : $"스킬 Pt {state.SkillPoints} · 힌트 보장까지 {hints.TrainingsUntilPity}회";
        }

        private void RefreshDetail(TrainingState state, TrainingManager.TrainingOption option)
        {
            int level = state.GetLevel(_selected);
            int baseGain = option.BaseGain(level);
            int support = TrainingManager.GetSupportBonus(_selected);
            int gain = TrainingManager.GetProjectedGain(_selected);
            int failure = TrainingManager.GetFailureRate(_selected);
            float specialty = TrainingManager.GetSpecialtyMultiplier(_selected);

            _detailName.text = $"{option.Name} 훈련";
            _detailName.color = UITheme.Stat(_selected);
            _detailEffect.text = option.Effect;
            _detailLevel.text = $"훈련 레벨  Lv {level} / {TrainingState.MaxTrainingLevel}";

            for (int i = 0; i < _levelPips.Length; i++)
            {
                _levelPips[i].color = i < level ? UITheme.Stat(_selected) : UITheme.Divider;
            }

            string secondary = TrainingManager.FormatSecondary(TrainingManager.GetProjectedSecondaryGains(_selected));
            _gainValue.text = $"+{gain}";
            _gainValue.color = UITheme.Stat(_selected);
            // 근력·체력·행운은 부 스탯도 함께 오른다. 그 몫이 스탯 이름 옆에 붙어야 훈련끼리 비교가 된다.
            _gainStat.text = secondary.Length > 0 ? $"{_selected}  · {secondary}" : _selected.ToString();

            SetBreakdown(0, $"기본 (Lv {level})", $"+{baseGain}", UITheme.TextPrimary);
            SetBreakdown(1, "서포트 보너스", support > 0 ? $"+{support}" : "—",
                support > 0 ? UITheme.Positive : UITheme.TextMuted);
            // 트레이닝 노트가 있으면 컨디션 줄에 함께 적는다 — 이번 훈련에 곱해지는 배율은 둘이 한 벌이다.
            if (state.NotePercent > 0)
            {
                SetBreakdown(2, "컨디션 · 노트", $"×{state.ConditionMultiplier:0.00} · +{state.NotePercent}%",
                    UITheme.Positive);
            }
            else
            {
                SetBreakdown(2, "컨디션", $"×{state.ConditionMultiplier:0.00}", UITheme.TextPrimary);
            }
            // 주/부 스탯 배율은 여기서 곱하지 않는다. 최종 스탯을 낼 때 UnitStats가 곱한다.
            SetBreakdown(3, "최종 스탯 배율", $"×{specialty:0.00}",
                specialty > 1f ? UITheme.Accent : UITheme.TextMuted);

            int energyCost = TrainingManager.GetEnergyCost(_selected);
            _costValue.text = energyCost < 0 ? $"+{-energyCost}" : $"-{energyCost}";
            _costValue.color = energyCost < 0 ? UITheme.Positive : new Color(0.878f, 0.647f, 0.290f);

            _skillValue.text = $"+{option.SkillPoints}";
            _skillValue.color = UITheme.Positive;

            // 실패율보다 성공률을 크게 보여 준다. 누르기 전에 확인해야 하는 숫자다.
            int success = 100 - failure;
            _failValue.text = $"{success} %";
            _failValue.color = failure == 0 ? UITheme.Positive
                : failure < 20 ? new Color(0.878f, 0.647f, 0.290f)
                : UITheme.Danger;

            _decideHint.text = secondary.Length > 0
                ? $"{option.Name} +{gain} · {secondary} · 성공 {success}%"
                : $"{option.Name} +{gain} · 성공 {success}%";
        }

        private void SetBreakdown(int index, string key, string value, Color color)
        {
            _breakdownKeys[index].text = key;
            _breakdownValues[index].text = value;
            _breakdownValues[index].color = color;
        }

        private void RefreshMain(Unit main)
        {
            if (main == null)
            {
                _mainName.text = "메인 없음";
                _mainMeta.text = "";
                _mainPortrait.enabled = false;
                return;
            }

            Sprite portrait = SpriteResource.LoadPortrait(main.PortraitPath);
            _mainPortrait.sprite = portrait;
            _mainPortrait.enabled = portrait != null;

            _mainName.text = main.UnitName;
            _mainMeta.text = $"주스탯 {main.MainStat} · 트레이닝 Lv {main.TrainingLevel}";
        }

        private void RefreshSupports()
        {
            // 우마무스메처럼 이 훈련에 앉은 서포트만 세운다. 다른 자리에 앉았거나 쉬는 서포트를
            // 회색으로 남겨 두면 목록이 늘 꽉 차 보여서, 정작 이번 선택의 값어치가 묻힌다.
            List<Unit> seated = TrainingManager.GetSupportsOn(_selected);
            int total = TrainingManager.GetSupportCount();

            for (int i = 0; i < _supportRows.Length; i++)
            {
                _supportRows[i].Bind(i < seated.Count ? seated[i] : null, _selected);
            }

            _supportCaption.text = seated.Count > 0
                ? $"이 훈련의 서포트 {seated.Count} / {total}"
                : $"이 훈련에 온 서포트가 없습니다 · 전체 {total}";
            _supportCaption.color = seated.Count > 0 ? UITheme.TextMuted : UITheme.TextMuted;
        }

        private void RefreshStatCards(Unit main)
        {
            int gain = TrainingManager.GetProjectedGain(_selected);
            foreach (StatCard card in _statCards)
            {
                card.Refresh(main, _selected, gain);
            }
        }

        private void RefreshButtons(TrainingState state)
        {
            foreach (TrainingButton button in _trainingButtons)
            {
                button.Refresh(state, _selected);
            }
        }

        // ── 조각 ─────────────────────────────────────────────────────

        /// <summary>서포트 한 줄. 초상화 · 이름 · 특기 · 우정 게이지 · 이번 훈련 참여 여부.</summary>
        private sealed class SupportRow
        {
            private readonly Image _frame;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _specialty;
            private readonly TextMeshProUGUI _state;
            private readonly Image _bondFill;

            public SupportRow(Transform parent, int index, int count)
            {
                float height = 0.90f / count;
                float top = 0.90f - index * height;

                _frame = UIBuild.Panel($"Support{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 6);
                UIBuild.Anchor(_frame.rectTransform,
                    new Vector2(0f, top - height + 0.012f), new Vector2(1f, top));

                var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(_frame.transform, false);
                _portrait = portraitObject.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Anchor(_portrait.rectTransform, new Vector2(0f, 0.1f), new Vector2(0.22f, 0.9f), 8f, 0f);

                _name = UIBuild.Text("Name", _frame.transform, "", UITheme.FontHeading, UITheme.TextPrimary);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0.24f, 0.55f), new Vector2(0.72f, 0.95f));

                _specialty = UIBuild.Text("Specialty", _frame.transform, "", UITheme.FontMicro,
                    UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_specialty.rectTransform, new Vector2(0.72f, 0.55f), new Vector2(1f, 0.95f), 10f, 0f);

                Image bondTrack = UIBuild.Bar("Bond", _frame.transform, UITheme.Mana, UITheme.Surface);
                _bondFill = bondTrack;
                UIBuild.Anchor((RectTransform)bondTrack.rectTransform.parent,
                    new Vector2(0.24f, 0.36f), new Vector2(1f, 0.5f), 10f, 0f);

                _state = UIBuild.Text("State", _frame.transform, "", UITheme.FontMicro, UITheme.TextMuted);
                UIBuild.Anchor(_state.rectTransform, new Vector2(0.24f, 0.06f), new Vector2(1f, 0.34f));
            }

            /// <summary>이 서포트가 지금 고른 훈련에 앉아 있는지 돌려준다.</summary>
            public bool Bind(Unit support, BaseEnums.PrimaryStat focus)
            {
                _frame.gameObject.SetActive(support != null);
                if (support == null) return false;

                Sprite portrait = SpriteResource.LoadPortrait(support.PortraitPath);
                _portrait.sprite = portrait;
                _portrait.enabled = portrait != null;

                int bond = TrainingManager.GetSupportBond(support);
                bool specialty = TrainingManager.IsSupportSpecialty(support, focus);
                bool friendship = specialty && bond >= TrainingManager.FriendshipBondThreshold;

                _portrait.color = Color.white;

                _name.text = support.UnitName;
                _name.color = UITheme.TextPrimary;

                _specialty.text = $"특기 {TrainingManager.GetSupportSpecialty(support)}";
                _specialty.color = specialty ? UITheme.Accent : UITheme.TextMuted;

                _bondFill.fillAmount = bond / (float)SupportBondState.MaxBond;
                _bondFill.color = bond >= TrainingManager.FriendshipBondThreshold ? UITheme.Accent : UITheme.Mana;

                _state.text = friendship ? $"우정 훈련 · 우정 {bond}"
                    : specialty ? $"특기 일치 · 우정 {bond}"
                    : $"우정 {bond}";
                _state.color = friendship ? UITheme.Accent
                    : specialty ? UITheme.Positive
                    : UITheme.TextSecondary;

                return true;
            }
        }

        /// <summary>하단 5스탯 칸. 고른 훈련의 스탯에만 +N이 붙는다.</summary>
        private sealed class StatCard
        {
            private readonly BaseEnums.PrimaryStat _stat;
            private readonly Image _frame;
            private readonly TextMeshProUGUI _key;
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _delta;

            public StatCard(Transform parent, BaseEnums.PrimaryStat stat, int index, int count)
            {
                _stat = stat;

                float width = 1f / count;
                _frame = UIBuild.Panel($"Stat{stat}", parent, UITheme.SurfaceSunken, UIShapes.Corner.None, 4);
                UIBuild.Anchor(_frame.rectTransform,
                    new Vector2(index * width, 0f), new Vector2((index + 1) * width, 1f), 5f, 0f);

                _key = UIBuild.Label("Key", _frame.transform, stat.ToString(), UITheme.FontMicro, UITheme.TextMuted);
                UIBuild.Anchor(_key.rectTransform, new Vector2(0f, 0.55f), new Vector2(0.5f, 0.95f), 12f, 0f);

                _value = UIBuild.Text("Value", _frame.transform, "", UITheme.FontTitle, UITheme.TextPrimary);
                UIBuild.Anchor(_value.rectTransform, new Vector2(0f, 0.08f), new Vector2(0.62f, 0.55f), 12f, 0f);

                _delta = UIBuild.Text("Delta", _frame.transform, "", UITheme.FontHeading, UITheme.Accent,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_delta.rectTransform, new Vector2(0.62f, 0.08f), new Vector2(1f, 0.55f), 12f, 0f);
            }

            public void Refresh(Unit main, BaseEnums.PrimaryStat focus, int gain)
            {
                bool active = _stat == focus;

                _key.color = active ? UITheme.Stat(_stat) : UITheme.TextMuted;
                _value.text = main != null ? main.GetBasePrimaryStat(_stat).ToString() : "—";
                _delta.text = active ? $"+{gain}" : "";
                _delta.color = UITheme.Stat(_stat);
            }
        }

        /// <summary>훈련 버튼 하나. 이름 · 레벨 · 체력 소모 · 예상 상승치.</summary>
        private sealed class TrainingButton
        {
            /// <summary>버튼 위에 얹는 서포트 자리 하나의 크기(px).</summary>
            private const float SeatSize = 26f;
            private const float SeatGap = 3f;

            private readonly BaseEnums.PrimaryStat _stat;
            private readonly Image _frame;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _meta;
            private readonly TextMeshProUGUI _gain;

            /// <summary>이 훈련에 앉은 서포트를 보여 주는 작은 초상화들.</summary>
            private readonly Image[] _seatFrames = new Image[MaxSupportRows];
            private readonly Image[] _seatPortraits = new Image[MaxSupportRows];

            public TrainingButton(Transform parent, BaseEnums.PrimaryStat stat, int index, int count,
                Action onClick)
            {
                _stat = stat;

                float width = 1f / count;
                _frame = UIBuild.Panel($"Train{stat}", parent, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal, 8);
                UIBuild.Anchor(_frame.rectTransform,
                    new Vector2(index * width, 0f), new Vector2((index + 1) * width, 1f), 6f, 0f);
                UIBuild.OnClick(_frame.gameObject, onClick);

                _name = UIBuild.Text("Name", _frame.transform, TrainingManager.GetOption(stat).Name,
                    UITheme.FontHeading, UITheme.TextPrimary);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.62f, 0.92f), 14f, 0f);

                _meta = UIBuild.Text("Meta", _frame.transform, "", UITheme.FontMicro, UITheme.TextMuted);
                UIBuild.Anchor(_meta.rectTransform, new Vector2(0f, 0.1f), new Vector2(0.62f, 0.48f), 14f, 0f);

                _gain = UIBuild.Text("Gain", _frame.transform, "", UITheme.FontTitle, UITheme.TextSecondary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_gain.rectTransform, new Vector2(0.62f, 0.1f), new Vector2(1f, 0.92f), 14f, 0f);

                // 서포트 자리. 버튼 윗변에 걸쳐 놓아 "누가 여기 앉았는지"가 버튼을 고르기 전에 보인다.
                for (int seat = 0; seat < _seatFrames.Length; seat++)
                {
                    Image frame = UIBuild.Panel($"Seat{seat}", _frame.transform, UITheme.SurfaceSunken,
                        UIShapes.Corner.Diagonal, 4, UITheme.Outline, 1);
                    UIBuild.Pin(frame.rectTransform, new Vector2(0f, 1f),
                        new Vector2(SeatSize, SeatSize),
                        new Vector2(10f + seat * (SeatSize + SeatGap), SeatSize * 0.45f));

                    var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                    portraitObject.transform.SetParent(frame.transform, false);
                    var portrait = portraitObject.GetComponent<Image>();
                    portrait.preserveAspect = true;
                    portrait.raycastTarget = false;
                    UIBuild.Stretch(portrait.rectTransform, 2f, 2f);

                    _seatFrames[seat] = frame;
                    _seatPortraits[seat] = portrait;
                    frame.gameObject.SetActive(false);
                }
            }

            public void Refresh(TrainingState state, BaseEnums.PrimaryStat focus)
            {
                bool active = _stat == focus;
                TrainingManager.TrainingOption option = TrainingManager.GetOption(_stat);

                _frame.sprite = UIShapes.CutCorner(8,
                    active ? UITheme.AccentFaint : UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal,
                    active ? UITheme.Accent : UITheme.Outline,
                    active ? 2 : 1);

                _name.color = active ? UITheme.TextPrimary : UITheme.TextSecondary;
                int energyCost = TrainingManager.GetEnergyCost(_stat);
                _meta.text = $"Lv {state.GetLevel(_stat)} · 체력 " +
                             (energyCost < 0 ? $"+{-energyCost}" : $"-{energyCost}");

                _gain.text = $"+{TrainingManager.GetProjectedGain(_stat)}";
                _gain.color = active ? UITheme.Stat(_stat) : UITheme.TextMuted;

                RefreshSeats();
            }

            /// <summary>이번 턴에 이 훈련에 앉은 서포트들을 버튼 위에 늘어놓는다.</summary>
            private void RefreshSeats()
            {
                List<Unit> seated = TrainingManager.GetSupportsOn(_stat);

                for (int seat = 0; seat < _seatFrames.Length; seat++)
                {
                    Unit support = seat < seated.Count ? seated[seat] : null;
                    _seatFrames[seat].gameObject.SetActive(support != null);
                    if (support == null) continue;

                    Sprite portrait = SpriteResource.LoadPortrait(support.PortraitPath);
                    _seatPortraits[seat].sprite = portrait;
                    _seatPortraits[seat].enabled = portrait != null;

                    // 특기 자리에 앉은 서포트는 테두리를 앰버로 — 우정 훈련까지 이어지는 자리다.
                    bool specialty = TrainingManager.IsSupportSpecialty(support, _stat);
                    _seatFrames[seat].sprite = UIShapes.CutCorner(4, UITheme.SurfaceSunken,
                        UIShapes.Corner.Diagonal,
                        specialty ? UITheme.Accent : UITheme.Outline,
                        specialty ? 2 : 1);
                }
            }
        }
    }
}
