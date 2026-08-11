using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 라운드 보상 선택. 카드 3장을 가로로 놓고 하나를 고르게 한다.
    /// </summary>
    public class RewardScreen : ModalScreen
    {
        private const int CardCount = 3;

        protected override string CanvasName => "RewardCanvas";
        protected override int SortingOrder => 70;
        protected override string Title => "보상 선택";
        protected override Vector2 AnchorMin => new(0.14f, 0.22f);
        protected override Vector2 AnchorMax => new(0.86f, 0.78f);

        private readonly List<Card> _cards = new();
        private List<RewardDef> _rewards = new();

        protected override void Build()
        {
            for (int i = 0; i < CardCount; i++)
            {
                _cards.Add(new Card(Body, i, OnPick));
            }
        }

        public void Show(List<RewardDef> rewards)
        {
            EnsureBuilt();
            _rewards = rewards ?? new List<RewardDef>();

            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(i < _rewards.Count ? _rewards[i] : null);
            }

            Show();
        }

        private void OnPick(int index)
        {
            if (index < 0 || index >= _rewards.Count) return;

            // 보상 대상은 필드 위 첫 아군으로 둔다(기존 동작 유지).
            Unit target = GridManager.Instance?.heroList
                ?.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
            GameManager.Instance?.rewardManager?.ApplyReward(_rewards[index], target);
        }

        private sealed class Card
        {
            private readonly GameObject _root;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _description;

            public Card(Transform parent, int index, System.Action<int> onPick)
            {
                Image panel = UIBuild.Panel($"Reward{index}", parent, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
                _root = panel.gameObject;

                float width = 1f / CardCount;
                UIBuild.Anchor(panel.rectTransform,
                    new Vector2(index * width, 0.06f),
                    new Vector2((index + 1) * width, 0.94f), 12f, 0f);

                _name = UIBuild.Text("Name", panel.transform, "", UITheme.FontHeading,
                    UITheme.Accent, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.86f), 14f, 0f);

                _description = UIBuild.Text("Desc", panel.transform, "", UITheme.FontCaption,
                    UITheme.TextSecondary, TextAlignmentOptions.Top, wrap: true);
                UIBuild.Anchor(_description.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.60f), 16f, 0f);

                Button pick = UIBuild.Button("Pick", panel.transform, "선택",
                    () => onPick(index), primary: true);
                UIBuild.Anchor(pick.image.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.15f), 20f, 0f);
            }

            public void Bind(RewardDef reward)
            {
                _root.SetActive(reward != null);
                if (reward == null) return;

                _name.text = reward.displayName;
                _description.text = reward.description;
            }
        }
    }
}
