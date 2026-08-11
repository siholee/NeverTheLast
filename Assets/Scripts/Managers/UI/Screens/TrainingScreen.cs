using BaseClasses;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 육성 페이즈. 집중 훈련할 주 스탯 하나를 고른다.
    /// </summary>
    public class TrainingScreen : ModalScreen
    {
        protected override string CanvasName => "TrainingCanvas";
        protected override int SortingOrder => 70;
        protected override string Title => "육성 페이즈";
        protected override Vector2 AnchorMin => new(0.10f, 0.20f);
        protected override Vector2 AnchorMax => new(0.90f, 0.80f);

        private TextMeshProUGUI _info;

        private static readonly (BaseEnums.PrimaryStat Stat, string Name, string Effect)[] Stats =
        {
            (BaseEnums.PrimaryStat.STR, "근력 STR", "아이템 중량 한도"),
            (BaseEnums.PrimaryStat.DEX, "민첩 DEX", "공격 속도"),
            (BaseEnums.PrimaryStat.CON, "체력 CON", "최대 체력 · 회복 · 보호막"),
            (BaseEnums.PrimaryStat.INT, "지능 INT", "마나 회복 · 최대 코드 수"),
            (BaseEnums.PrimaryStat.LUK, "행운 LUK", "치명타 확률"),
        };

        protected override void Build()
        {
            TextMeshProUGUI lead = UIBuild.Text("Lead", Body, "집중 훈련할 스탯을 선택하세요.",
                UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(lead.rectTransform, new Vector2(0f, 0.82f), new Vector2(1f, 0.96f));

            _info = UIBuild.Text("Info", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_info.rectTransform, new Vector2(0f, 0.58f), new Vector2(1f, 0.80f));

            for (int i = 0; i < Stats.Length; i++)
            {
                BuildStatButton(i);
            }
        }

        private void BuildStatButton(int index)
        {
            (BaseEnums.PrimaryStat stat, string name, string effect) = Stats[index];

            float width = 1f / Stats.Length;
            Button button = UIBuild.Button($"Train{stat}", Body, "",
                () => GameManager.Instance?.CompleteTrainingPhaseWithFocus(stat));
            UIBuild.Anchor(button.image.rectTransform,
                new Vector2(index * width, 0.10f),
                new Vector2((index + 1) * width, 0.52f), 8f, 0f);

            TextMeshProUGUI title = UIBuild.Text("Name", button.transform, name,
                UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Center);
            UIBuild.Anchor(title.rectTransform, new Vector2(0f, 0.56f), new Vector2(1f, 0.88f), 6f, 0f);

            TextMeshProUGUI desc = UIBuild.Text("Effect", button.transform, effect,
                UITheme.FontMicro, UITheme.TextSecondary, TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(desc.rectTransform, new Vector2(0f, 0.14f), new Vector2(1f, 0.54f), 8f, 0f);
        }

        public override void Show()
        {
            base.Show();
            RefreshInfo();
        }

        private void RefreshInfo()
        {
            if (_info == null) return;

            Unit main = TrainingManager.GetMainUnit();
            string mainName = main != null ? main.UnitName : "메인";
            int trainingLevel = main != null ? main.TrainingLevel : 0;
            int supports = TrainingManager.GetSupportCount();

            string gains = string.Join("   ", new[]
            {
                $"STR +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.STR)}",
                $"DEX +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.DEX)}",
                $"CON +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.CON)}",
                $"INT +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.INT)}",
                $"LUK +{TrainingManager.GetFocusStatGain(BaseEnums.PrimaryStat.LUK)}",
            });

            _info.text =
                $"{mainName} · 트레이닝 Lv.{trainingLevel} · 서포트 {supports}명\n" +
                $"예상 강화량   {gains}\n" +
                "훈련 실패 없음 · 특기 훈련 참여 시 우정 상승 및 스킬 전수 판정";
        }
    }
}
