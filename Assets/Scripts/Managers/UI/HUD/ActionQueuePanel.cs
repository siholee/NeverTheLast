using System.Collections.Generic;
using BaseClasses;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 좌상단 행동서열. 붕괴: 스타레일의 세로 행동 순서 스택을 따른다.
    ///
    /// 맨 위가 지금(또는 바로 다음) 행동할 유닛이고, 아래로 갈수록 나중이다.
    /// 예약된 궁극기는 속도를 무시하고 맨 앞에 앰버로 표시된다.
    /// 아래로 갈수록 카드를 연하게 그려 순서의 깊이를 구분한다.
    /// </summary>
    public class ActionQueuePanel
    {
        private const int MaxSlots = 6;
        private const float SlotHeight = 72f;
        private const float SlotGap = 6f;
        private const float PanelWidth = 228f;

        private readonly RectTransform _root;
        private readonly List<Slot> _slots = new();

        public ActionQueuePanel(Transform parent)
        {
            _root = UIBuild.Container("ActionQueue", parent);
            UIBuild.Pin(_root, new Vector2(0f, 1f),
                new Vector2(PanelWidth, MaxSlots * (SlotHeight + SlotGap)),
                new Vector2(24f, -160f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", _root, "ACTION  /  행동 순서",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(190f, 16f),
                new Vector2(4f, 14f));

            for (int i = 0; i < MaxSlots; i++)
            {
                _slots.Add(new Slot(_root, i));
            }
            _root.gameObject.SetActive(false);
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            bool inBattle = game != null && game.gameState == BaseEnums.GameState.RoundInProgress;
            if (_root.gameObject.activeSelf != inBattle) _root.gameObject.SetActive(inBattle);
            if (!inBattle) return;

            ActionScheduler scheduler = game.ActionScheduler;
            if (scheduler == null)
            {
                foreach (Slot slot in _slots) slot.Clear();
                return;
            }

            List<ActionScheduler.Reservation> order = scheduler.ForecastOrder(MaxSlots);
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < order.Count) _slots[i].Set(order[i], i);
                else _slots[i].Clear();
            }
        }

        private sealed class Slot
        {
            private readonly RectTransform _rect;
            private readonly Image _frame;
            private readonly Image _portrait;
            private readonly Image _sideBar;
            private readonly TextMeshProUGUI _nameLabel;
            private readonly TextMeshProUGUI _etaLabel;
            private readonly CanvasGroup _group;

            private string _portraitPath;

            public Slot(Transform parent, int index)
            {
                _frame = UIBuild.Panel($"Slot{index}", parent, UITheme.HudBar,
                    UIShapes.Corner.Diagonal, 6);
                _rect = _frame.rectTransform;
                UIBuild.Pin(_rect, new Vector2(0f, 1f), new Vector2(PanelWidth, SlotHeight),
                    new Vector2(0f, -index * (SlotHeight + SlotGap)));

                // 진영/행동 종류를 알리는 왼쪽 세로 띠.
                _sideBar = UIBuild.Solid("Side", _frame.transform, UITheme.Accent);
                UIBuild.Anchor(_sideBar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f));
                _sideBar.rectTransform.pivot = new Vector2(0f, 0.5f);
                _sideBar.rectTransform.sizeDelta = new Vector2(4f, 0f);
                _sideBar.raycastTarget = false;

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(_frame.transform, false);
                _portrait = portraitGo.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Pin(_portrait.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(64f, 64f), new Vector2(8f, 0f));

                _nameLabel = UIBuild.Text("Name", _frame.transform, "", UITheme.FontCaption,
                    UITheme.TextPrimary, TextAlignmentOptions.Left);
                UIBuild.Anchor(_nameLabel.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 0.94f));
                _nameLabel.rectTransform.offsetMin = new Vector2(82f, _nameLabel.rectTransform.offsetMin.y);
                _nameLabel.rectTransform.offsetMax = new Vector2(-10f, _nameLabel.rectTransform.offsetMax.y);
                _nameLabel.overflowMode = TextOverflowModes.Ellipsis;

                _etaLabel = UIBuild.Text("Eta", _frame.transform, "", UITheme.FontMicro,
                    UITheme.TextSecondary, TextAlignmentOptions.Left);
                UIBuild.Anchor(_etaLabel.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.48f));
                _etaLabel.rectTransform.offsetMin = new Vector2(82f, _etaLabel.rectTransform.offsetMin.y);
                _etaLabel.rectTransform.offsetMax = new Vector2(-10f, _etaLabel.rectTransform.offsetMax.y);

                _group = UIBuild.Group(_frame.gameObject);
                _group.blocksRaycasts = false;
                _frame.gameObject.SetActive(false);
            }

            public void Set(ActionScheduler.Reservation reservation, int index)
            {
                Unit unit = reservation.Unit;
                if (unit == null)
                {
                    Clear();
                    return;
                }

                _frame.gameObject.SetActive(true);

                // 궁극기 예약은 앰버, 아군은 흰빛, 적군은 붉은 띠로 구분한다.
                _sideBar.color = reservation.IsUltimate
                    ? UITheme.Accent
                    : unit.IsEnemy
                        ? UITheme.Enemy
                        : UITheme.TextSecondary;

                // 초가 아니라 '몇 번째 행동인지'를 보여 준다.
                _etaLabel.text = reservation.IsUltimate
                    ? "궁극기"
                    : reservation.ActionsAhead <= 0
                        ? "지금 행동"
                        : $"{reservation.ActionsAhead}번째 뒤";
                _etaLabel.color = reservation.IsUltimate ? UITheme.Accent : UITheme.TextMuted;
                _nameLabel.text = unit.UnitName;

                // 아래로 갈수록 연하게. 맨 위 두 칸은 또렷하게 유지한다.
                _group.alpha = Mathf.Lerp(1f, 0.45f, Mathf.Max(0, index - 1) / (float)MaxSlots);

                LoadPortrait(unit);
            }

            private void LoadPortrait(Unit unit)
            {
                if (_portraitPath == unit.PortraitPath) return;
                _portraitPath = unit.PortraitPath;

                Sprite sprite = SpriteResource.LoadPortrait(unit.PortraitPath);
                _portrait.sprite = sprite;
                _portrait.enabled = sprite != null;
            }

            public void Clear()
            {
                _frame.gameObject.SetActive(false);
                _portraitPath = null;
            }
        }
    }
}
