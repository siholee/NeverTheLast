using System.Collections.Generic;
using Entities;
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
    /// 아래로 갈수록 카드를 살짝 작게·연하게 그려 원근을 준다(스타레일과 동일한 처리).
    /// </summary>
    public class ActionQueuePanel
    {
        private const int MaxSlots = 8;
        private const float SlotHeight = 52f;
        private const float SlotGap = 4f;
        private const float PanelWidth = 132f;

        private readonly RectTransform _root;
        private readonly List<Slot> _slots = new();

        public ActionQueuePanel(Transform parent)
        {
            _root = UIBuild.Container("ActionQueue", parent);
            UIBuild.Pin(_root, new Vector2(0f, 1f),
                new Vector2(PanelWidth, MaxSlots * (SlotHeight + SlotGap)),
                new Vector2(18f, -18f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", _root, "ORDER",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(80f, 12f),
                new Vector2(4f, 14f));

            for (int i = 0; i < MaxSlots; i++)
            {
                _slots.Add(new Slot(_root, i));
            }
        }

        public void Tick()
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
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
            private readonly TextMeshProUGUI _etaLabel;
            private readonly CanvasGroup _group;

            private string _portraitPath;

            public Slot(Transform parent, int index)
            {
                // 위쪽일수록 넓게 그려 "곧 행동한다"는 것을 크기로도 알린다.
                float width = Mathf.Lerp(PanelWidth, PanelWidth * 0.72f, index / (float)MaxSlots);

                _frame = UIBuild.Panel($"Slot{index}", parent, UITheme.HudBar,
                    UIShapes.Corner.Diagonal, 6);
                _rect = _frame.rectTransform;
                UIBuild.Pin(_rect, new Vector2(0f, 1f), new Vector2(width, SlotHeight),
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
                    new Vector2(SlotHeight - 12f, SlotHeight - 12f), new Vector2(10f, 0f));

                _etaLabel = UIBuild.Text("Eta", _frame.transform, "", UITheme.FontMicro,
                    UITheme.TextSecondary, TextAlignmentOptions.MidlineRight);
                UIBuild.Stretch(_etaLabel.rectTransform, 8f, 0f);

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
                    ? "ULT"
                    : reservation.ActionsAhead <= 0
                        ? "NOW"
                        : $"+{reservation.ActionsAhead}";
                _etaLabel.color = reservation.IsUltimate ? UITheme.Accent : UITheme.TextMuted;

                // 아래로 갈수록 연하게. 맨 위 두 칸은 또렷하게 유지한다.
                _group.alpha = Mathf.Lerp(1f, 0.45f, Mathf.Max(0, index - 1) / (float)MaxSlots);

                LoadPortrait(unit);
            }

            private void LoadPortrait(Unit unit)
            {
                if (_portraitPath == unit.PortraitPath) return;
                _portraitPath = unit.PortraitPath;

                Sprite sprite = string.IsNullOrEmpty(unit.PortraitPath)
                    ? null
                    : Resources.Load<Sprite>(unit.PortraitPath);
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
