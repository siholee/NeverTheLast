using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
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
    /// 좌측 행동서열. 발동 공지를 별도 배너로 겹치지 않고 같은 레일의 확장 슬롯으로 끼워 넣는다.
    /// 예약·행동치 변동·사망으로 순서가 바뀌면 슬롯의 정체성을 유지한 채 새 자리까지 보간한다.
    /// </summary>
    public sealed class ActionQueuePanel
    {
        private const int MaxSlots = 6;
        private const int MaxAnnouncements = 3;
        private const float PanelWidth = 118f;
        private const float HeaderHeight = 44f;
        private const float RowStep = 74f;
        private const float SlotSize = 62f;
        private const float ExpandedWidth = 440f;
        private const float ExpandedHeight = 68f;

        private readonly Image _panel;
        private readonly Image _rail;
        private readonly List<Slot> _slots = new();
        private readonly List<InsertedSlot> _announcements = new();
        private readonly Dictionary<Unit, AdjustmentCue> _adjustments = new();
        private bool _wasInBattle;
        private float _panelHeight;

        public ActionQueuePanel(Transform parent)
        {
            _panel = UIBuild.ChromePanel("ActionQueue", parent);
            _panelHeight = HeaderHeight + MaxSlots * RowStep + 18f;
            UIBuild.Pin(_panel.rectTransform, new Vector2(0f, 1f),
                new Vector2(PanelWidth, _panelHeight),
                new Vector2(UITheme.ScreenEdgeInset, -UITheme.BelowTopBar));
            _panel.raycastTarget = false;
            Color panelColor = _panel.color;
            panelColor.a = 0.82f;
            _panel.color = panelColor;

            TextMeshProUGUI caption = UIBuild.Label("Caption", _panel.transform, "행동 순서",
                16, UITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f),
                new Vector2(100f, 28f), new Vector2(14f, -9f));

            TextMeshProUGUI hint = UIBuild.Label("Hint", _panel.transform, "",
                UITheme.FontCaption, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(hint.rectTransform, new Vector2(1f, 1f),
                new Vector2(158f, 30f), new Vector2(-18f, -18f));

            _rail = UIBuild.Solid("Rail", _panel.transform,
                new Color(UITheme.MetalSilver.r, UITheme.MetalSilver.g, UITheme.MetalSilver.b, 0.32f));
            UIBuild.Pin(_rail.rectTransform, new Vector2(0f, 1f),
                new Vector2(2f, MaxSlots * RowStep - 12f),
                new Vector2(48f, -HeaderHeight - 4f));
            _rail.raycastTarget = false;

            for (int i = 0; i < MaxSlots; i++) _slots.Add(new Slot(_panel.transform));
            ActionAnnouncer.Announced += OnAnnounced;
            ActionScheduler.AnyActionStarted += OnActionStarted;
            ActionScheduler.AnyActionFinished += OnActionFinished;
            ActionScheduler.AnyActionOrderAdjusted += OnActionOrderAdjusted;
            _panel.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            ActionAnnouncer.Announced -= OnAnnounced;
            ActionScheduler.AnyActionStarted -= OnActionStarted;
            ActionScheduler.AnyActionFinished -= OnActionFinished;
            ActionScheduler.AnyActionOrderAdjusted -= OnActionOrderAdjusted;
            ClearAnnouncements();
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            bool inBattle = game != null && game.gameState == BaseEnums.GameState.RoundInProgress;
            if (_panel.gameObject.activeSelf != inBattle) _panel.gameObject.SetActive(inBattle);
            if (!inBattle)
            {
                if (_wasInBattle) ClearAnnouncements();
                _wasInBattle = false;
                return;
            }

            _wasInBattle = true;
            TickAnnouncements();
            PruneAdjustmentCues();

            foreach (Slot slot in _slots) slot.BeginLayout();
            ActionScheduler scheduler = game.ActionScheduler;
            int orderCount = 0;
            if (scheduler != null)
            {
                IReadOnlyList<ActionScheduler.Reservation> order = scheduler.ForecastOrderCached(MaxSlots);
                orderCount = order.Count;
                for (int i = 0; i < order.Count; i++)
                {
                    ActionScheduler.Reservation reservation = order[i];
                    Slot slot = _slots.FirstOrDefault(item => !item.Claimed && item.Key == reservation.StableKey)
                                ?? _slots.FirstOrDefault(item => !item.Claimed && !item.Active)
                                ?? _slots.First(item => !item.Claimed);
                    AdjustmentCue cue = reservation.Unit != null &&
                                        _adjustments.TryGetValue(reservation.Unit, out AdjustmentCue value)
                        ? value
                        : default;
                    slot.Bind(reservation, i + _announcements.Count,
                        i == 0 && _announcements.Count == 0, cue);
                }
            }

            foreach (Slot slot in _slots)
            {
                if (!slot.Claimed) slot.Release();
                slot.Tick();
            }

            // 실제로 보이는 슬롯 수만큼만 유리판과 레일을 열어, 적은 서열에서 큰 빈 판이 남지 않게 한다.
            int visibleRows = Mathf.Max(1, orderCount + _announcements.Count);
            float targetHeight = HeaderHeight + visibleRows * RowStep + 18f;
            _panelHeight = Damp(_panelHeight, targetHeight, 13f);
            _panel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _panelHeight);
            _rail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(30f, _panelHeight - HeaderHeight - 30f));
        }

        private void OnAnnounced(ActionAnnouncement announcement)
        {
            if (announcement.Unit == null) return;
            _announcements.Insert(0, new InsertedSlot(_panel.transform, announcement));
            while (_announcements.Count > MaxAnnouncements)
            {
                // 도중에 패시브 공지가 겹쳐도 실행 중인 행동의 슬롯을 밀어내지 않는다.
                int oldest = _announcements.FindLastIndex(row => !row.HoldingAction);
                if (oldest < 0) break;
                _announcements[oldest].Destroy();
                _announcements.RemoveAt(oldest);
            }
        }

        private void OnActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            int id = GameManager.Instance.ActionScheduler.CurrentActionId;
            _announcements.FirstOrDefault(row => row.WaitingForAction && row.Unit == unit)?.BindAction(id);
        }

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            int id = GameManager.Instance.ActionScheduler.CurrentActionId;
            foreach (InsertedSlot row in _announcements)
                if (row.Unit == unit && row.ActionId == id) row.FinishAction();
        }

        private void OnActionOrderAdjusted(Unit unit, bool advanced)
        {
            if (unit == null) return;
            _adjustments[unit] = new AdjustmentCue(advanced, Time.unscaledTime + 0.92f);
        }

        private void PruneAdjustmentCues()
        {
            float now = Time.unscaledTime;
            foreach (Unit unit in _adjustments.Where(pair => pair.Key == null || pair.Value.Until <= now)
                         .Select(pair => pair.Key).ToList())
                _adjustments.Remove(unit);
        }

        private void TickAnnouncements()
        {
            for (int i = _announcements.Count - 1; i >= 0; i--)
            {
                InsertedSlot row = _announcements[i];
                if (row.Tick(i)) continue;
                row.Destroy();
                _announcements.RemoveAt(i);
            }
        }

        private void ClearAnnouncements()
        {
            foreach (InsertedSlot row in _announcements) row.Destroy();
            _announcements.Clear();
            _adjustments.Clear();
            foreach (Slot slot in _slots) slot.Reset();
        }

        private static float RowY(int index) => -HeaderHeight - 8f - index * RowStep;

        private static float Damp(float current, float target, float sharpness)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));

        private static void FitOneLine(TextMeshProUGUI text, float minimum, float maximum)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minimum;
            text.fontSizeMax = maximum;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private readonly struct AdjustmentCue
        {
            public readonly bool Advanced;
            public readonly float Until;
            public bool Visible => Until > Time.unscaledTime;

            public AdjustmentCue(bool advanced, float until)
            {
                Advanced = advanced;
                Until = until;
            }
        }

        /// <summary>실행 중 사건은 초상 슬롯이 가로로 열리며 내용을 보여 주고, 닫힐 때 아래 서열이 올라온다.</summary>
        private sealed class InsertedSlot
        {
            private const float OpenSeconds = 0.18f;
            private const float HoldSeconds = 2.8f;
            private const float CloseSeconds = 0.24f;

            private readonly Image _frame;
            private readonly CanvasGroup _group;
            private readonly CanvasGroup _details;
            public Unit Unit { get; }
            public int ActionId { get; private set; } = -1;
            private readonly bool _followsAction;
            private bool _finished;
            public bool HoldingAction => _followsAction && !_finished;
            public bool WaitingForAction => HoldingAction && ActionId < 0;
            public void BindAction(int id) => ActionId = id;
            public void FinishAction()
            {
                _finished = true;
                _age = Mathf.Max(_age, OpenSeconds + HoldSeconds);
            }

            private float _age;
            private float _y;

            public InsertedSlot(Transform parent, ActionAnnouncement announcement)
            {
                Unit = announcement.Unit;
                _followsAction = announcement.FollowsAction;
                Color team = UITheme.Accent;
                Color fill = UITheme.SurfaceRaised;
                fill.a = 0.98f;
                _frame = UIBuild.Panel("InsertedAction", parent, fill,
                    UIShapes.Corner.Diagonal, 11, team, 1);
                RectTransform rect = _frame.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(_followsAction ? ExpandedWidth : SlotSize, ExpandedHeight);
                rect.anchoredPosition = new Vector2(18f, RowY(0));
                _frame.raycastTarget = false;

                Image ring = UIBuild.Panel("PortraitRing", _frame.transform,
                    new Color(team.r, team.g, team.b, 0.18f), UIShapes.Corner.Diagonal, 9, team, 1);
                UIBuild.Pin(ring.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(56f, 56f), new Vector2(6f, 0f));

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(ring.transform, false);
                Image portrait = portraitGo.GetComponent<Image>();
                portrait.sprite = SpriteResource.LoadPortrait(announcement.Unit.PortraitPath);
                portrait.enabled = portrait.sprite != null;
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                UIBuild.Stretch(portrait.rectTransform, 4f, 4f);

                var detailsGo = new GameObject("InsertedDetails", typeof(RectTransform), typeof(CanvasGroup));
                detailsGo.transform.SetParent(_frame.transform, false);
                UIBuild.Stretch(detailsGo.GetComponent<RectTransform>());
                _details = detailsGo.GetComponent<CanvasGroup>();
                _details.blocksRaycasts = false;
                _details.interactable = false;
                _details.alpha = _followsAction ? 1f : 0f;

                TextMeshProUGUI unit = UIBuild.Text("Unit", detailsGo.transform, announcement.UnitName,
                    UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UIBuild.Pin(unit.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(132f, 34f), new Vector2(72f, 14f));
                FitOneLine(unit, 18f, 22f);

                TextMeshProUGUI kind = UIBuild.Text("Kind", detailsGo.transform, announcement.KindLabel,
                    UITheme.FontCaption, team, TextAlignmentOptions.MidlineLeft);
                UIBuild.Pin(kind.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(132f, 24f), new Vector2(72f, -15f));
                FitOneLine(kind, 16f, 18f);

                TextMeshProUGUI action = UIBuild.Text("Action", detailsGo.transform, announcement.ActionName,
                    UITheme.FontTitle, UITheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UIBuild.Pin(action.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(198f, 52f), new Vector2(218f, -2f));
                action.fontSize = 22f;
                action.enableAutoSizing = false;
                action.textWrappingMode = TextWrappingModes.Normal;


                _group = UIBuild.Group(_frame.gameObject);
                _group.blocksRaycasts = false;
                _group.interactable = false;
                // 예약행동의 짧은 예고는 생성 프레임부터 읽힌다. 패시브 공지의 진입 모션은 유지한다.
                _group.alpha = _followsAction ? 1f : 0f;
                _age = _followsAction ? OpenSeconds : 0f;
                _y = RowY(0) + (_followsAction ? 0f : 18f);
            }

            public bool Tick(int index)
            {
                if(Time.timeScale>0) _age += Time.unscaledDeltaTime * BattleHud.CurrentSpeedMultiplier;
                // 실행 시간으로 수명을 정한다. 선제 개입이 끝나도 부모 행동은 자기 종료를 기다린다.
                if (WaitingForAction && _age > OpenSeconds &&
                    GameManager.Instance?.ActionScheduler?.PreviewUnit != Unit) FinishAction();
                if (HoldingAction) _age = Mathf.Min(_age, OpenSeconds + HoldSeconds * .5f);
                float age = _age;
                float total = OpenSeconds + HoldSeconds + CloseSeconds;
                if (age >= total) return false;

                _y = Damp(_y, RowY(index), 15f);
                float open = age < OpenSeconds
                    ? Mathf.SmoothStep(0f, 1f, age / OpenSeconds)
                    : age < OpenSeconds + HoldSeconds
                        ? 1f
                        : 1f - Mathf.SmoothStep(0f, 1f, (age - OpenSeconds - HoldSeconds) / CloseSeconds);
                // 완료 뒤에는 내용과 면을 함께 지운다. 글자만 먼저 사라진 큰 빈 판을 남기지 않는다.
                bool closing = age >= OpenSeconds + HoldSeconds;
                _frame.rectTransform.sizeDelta = new Vector2(closing ? ExpandedWidth : Mathf.Lerp(SlotSize, ExpandedWidth, open), ExpandedHeight);
                _frame.rectTransform.anchoredPosition = new Vector2(18f, _y);
                _group.alpha = Mathf.Clamp01(open * 1.25f);
                // 폭이 충분히 열린 뒤에만 세부 글자를 보여 중간 프레임에서도 서로 겹치지 않는다.
                _details.alpha = closing ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 0.99f, open));
                return true;
            }

            public void Destroy()
            {
                if (_frame != null) Object.Destroy(_frame.gameObject);
            }

        }

        private sealed class Slot
        {
            private readonly Image _frame;
            private readonly Image _ring;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _queueMark;
            private readonly TextMeshProUGUI _adjustmentMark;
            private readonly CanvasGroup _group;
            private string _portraitPath;
            private float _targetY;
            private float _y;

            public string Key { get; private set; }
            public bool Active => _frame.gameObject.activeSelf;
            public bool Claimed { get; private set; }

            public Slot(Transform parent)
            {
                _frame = UIBuild.Panel("OrderSlot", parent, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 9, UITheme.MetalSilver, 1);
                RectTransform rect = _frame.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                rect.anchoredPosition = new Vector2(18f, RowY(MaxSlots));

                _ring = UIBuild.Panel("TeamRing", _frame.transform, Color.clear,
                    UIShapes.Corner.Diagonal, 8, UITheme.MetalSilver, 2);
                UIBuild.Stretch(_ring.rectTransform, 4f, 4f);

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(_ring.transform, false);
                _portrait = portraitGo.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Stretch(_portrait.rectTransform, 4f, 4f);

                _queueMark = UIBuild.Text("QueueMark", _frame.transform, string.Empty,
                    UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Pin(_queueMark.rectTransform, new Vector2(1f, 1f),
                    new Vector2(24f, 24f), new Vector2(5f, 5f));

                _adjustmentMark = UIBuild.Text("AdjustmentMark", _frame.transform, string.Empty,
                    UITheme.FontTitle, UITheme.Accent, TextAlignmentOptions.Center);
                UIBuild.Pin(_adjustmentMark.rectTransform, new Vector2(1f, 0.5f),
                    new Vector2(30f, 34f), new Vector2(14f, 0f));

                _group = UIBuild.Group(_frame.gameObject);
                _group.blocksRaycasts = false;
                _frame.gameObject.SetActive(false);
            }

            public void BeginLayout() => Claimed = false;

            public void Bind(ActionScheduler.Reservation reservation, int row, bool current, AdjustmentCue cue)
            {
                Unit unit = reservation.Unit;
                if (unit == null)
                {
                    Release();
                    return;
                }

                bool newlyBound = Key != reservation.StableKey || !Active;
                Key = reservation.StableKey;
                Claimed = true;
                _targetY = RowY(row);
                if (newlyBound)
                {
                    _y = _targetY - 20f;
                    _group.alpha = 0f;
                    _frame.gameObject.SetActive(true);
                }

                Color team = UITheme.Accent;
                Color border = current || reservation.IsQueued ? team : UITheme.MetalSilver;
                _frame.sprite = UIShapes.Surface(9, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal,
                    border, current || reservation.IsQueued ? 1 : 0);
                _ring.sprite = UIShapes.Surface(8, Color.clear, UIShapes.Corner.Diagonal, team, 1);
                _frame.rectTransform.localScale = Vector3.one * (current ? 1.06f : 1f);
                _queueMark.text = reservation.IsQueued ? QueueMark(reservation.Kind) : string.Empty;
                _queueMark.color = team;
                _adjustmentMark.text = cue.Visible ? cue.Advanced ? "↑" : "↓" : string.Empty;
                _adjustmentMark.color = UITheme.Accent;
                LoadPortrait(unit);
            }

            public void Release()
            {
                Claimed = false;
                Key = null;
            }

            public void Tick()
            {
                if (!Active) return;
                if (!Claimed)
                {
                    _group.alpha = Damp(_group.alpha, 0f, 18f);
                    if (_group.alpha <= 0.03f) Reset();
                    return;
                }

                _y = Damp(_y, _targetY, 16f);
                _group.alpha = Damp(_group.alpha, 1f, 18f);
                _frame.rectTransform.anchoredPosition = new Vector2(18f, _y);
            }

            public void Reset()
            {
                _frame.gameObject.SetActive(false);
                Key = null;
                Claimed = false;
                _portraitPath = null;
            }

            private void LoadPortrait(Unit unit)
            {
                if (_portraitPath == unit.PortraitPath) return;
                _portraitPath = unit.PortraitPath;
                Sprite sprite = UIPortraitCrop.Face(SpriteResource.LoadPortrait(unit.PortraitPath));
                _portrait.sprite = sprite;
                _portrait.enabled = sprite != null;
            }

            private static string QueueMark(ActionScheduler.ActionKind kind) => kind switch
            {
                ActionScheduler.ActionKind.Ultimate => "궁",
                ActionScheduler.ActionKind.PriorityAdditional => "+",
                ActionScheduler.ActionKind.Additional => "+",
                ActionScheduler.ActionKind.Coordinated => "협",
                ActionScheduler.ActionKind.Special => "특",
                _ => string.Empty,
            };
        }
    }
}
