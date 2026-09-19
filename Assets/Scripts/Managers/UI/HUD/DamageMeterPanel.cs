using System.Collections.Generic;
using BaseClasses;
using Combat;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 우측 딜 그래프. 전투 중에만 보이고, 누가 얼마나 넣고 있는지를 막대로 보여 준다.
    ///
    /// 전투가 자동이라 플레이어가 판단할 수 있는 것은 편성뿐이다. 누가 일을 하고 누가 놀고 있는지가
    /// 보여야 다음 판의 배치 · 훈련을 고를 수 있다. 막대는 이번 판 1등을 기준 길이로 잰다.
    /// 값은 <see cref="DamageMeter"/>가 세고, 결과 화면도 같은 값을 읽는다.
    /// </summary>
    public class DamageMeterPanel
    {
        private const int MaxRows = 6;
        private const float RowHeight = 38f;
        private const float RowGap = 4f;
        private const float PanelWidth = 250f;

        /// <summary>갱신 주기(초, 실시간). 8배속에서도 순위가 깜빡이지 않을 만큼만 자주 본다.</summary>
        private const float RefreshInterval = 0.2f;

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _total;
        private readonly List<Row> _rows = new();
        private float _nextRefreshAt;

        public DamageMeterPanel(Transform parent)
        {
            _root = UIBuild.Container("DamageMeter", parent);
            UIBuild.Pin(_root, new Vector2(1f, 1f),
                new Vector2(PanelWidth, MaxRows * (RowHeight + RowGap) + 20f),
                new Vector2(-24f, -160f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", _root, "DAMAGE  /  딜량",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(150f, 16f), new Vector2(4f, 14f));

            _total = UIBuild.Text("Total", _root, "", UITheme.FontMicro, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_total.rectTransform, new Vector2(1f, 1f), new Vector2(110f, 16f), new Vector2(-4f, 14f));

            for (int i = 0; i < MaxRows; i++) _rows.Add(new Row(_root, i));
            _root.gameObject.SetActive(false);
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            bool inBattle = game != null && game.gameState == BaseEnums.GameState.RoundInProgress;
            if (_root.gameObject.activeSelf != inBattle) _root.gameObject.SetActive(inBattle);
            if (!inBattle || Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + RefreshInterval;

            List<DamageMeter.Entry> entries = DamageMeter.Sorted();
            long max = DamageMeter.Max();
            long total = DamageMeter.Total();
            _total.text = total > 0 ? $"합계 {total:N0}" : "";

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < entries.Count) _rows[i].Set(entries[i], max, total);
                else _rows[i].Clear();
            }
        }

        /// <summary>딜량 한 줄. 초상화 · 이름 · 수치, 그 아래 막대.</summary>
        internal sealed class Row
        {
            private readonly Image _frame;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _value;
            private readonly Image _fill;
            private string _portraitPath;

            public Row(Transform parent, int index)
            {
                _frame = UIBuild.Glass($"Row{index}", parent, 8);
                _frame.raycastTarget = false;
                UIBuild.Pin(_frame.rectTransform, new Vector2(0f, 1f), new Vector2(PanelWidth, RowHeight),
                    new Vector2(0f, -index * (RowHeight + RowGap)));

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(_frame.transform, false);
                _portrait = portraitGo.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Pin(_portrait.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(RowHeight - 6f, RowHeight - 6f), new Vector2(4f, 0f));

                _name = UIBuild.Text("Name", _frame.transform, "", UITheme.FontMicro, UITheme.TextPrimary);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.42f), new Vector2(0.62f, 1f));
                _name.rectTransform.offsetMin = new Vector2(RowHeight + 4f, 0f);
                _name.overflowMode = TextOverflowModes.Ellipsis;

                _value = UIBuild.Text("Value", _frame.transform, "", UITheme.FontMicro, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_value.rectTransform, new Vector2(0.55f, 0.42f), new Vector2(1f, 1f), 8f, 0f);

                Image track = UIBuild.Solid("Track", _frame.transform, UITheme.Track);
                track.raycastTarget = false;
                UIBuild.Anchor(track.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.34f));
                track.rectTransform.offsetMin = new Vector2(RowHeight + 4f, 0f);
                track.rectTransform.offsetMax = new Vector2(-8f, 0f);

                _fill = UIBuild.Solid("Fill", track.transform, UITheme.Accent);
                _fill.raycastTarget = false;
                _fill.rectTransform.anchorMin = Vector2.zero;
                _fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                _fill.rectTransform.offsetMin = Vector2.zero;
                _fill.rectTransform.offsetMax = Vector2.zero;
            }

            public void Set(DamageMeter.Entry entry, long max, long total)
            {
                _frame.gameObject.SetActive(true);

                if (_portraitPath != entry.Portrait)
                {
                    _portraitPath = entry.Portrait;
                    Sprite sprite = SpriteResource.LoadPortrait(entry.Portrait);
                    _portrait.sprite = sprite;
                    _portrait.enabled = sprite != null;
                }

                bool down = entry.Unit == null || !entry.Unit.isActive;
                _portrait.color = down ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
                _name.text = entry.Name;
                _name.color = down ? UITheme.TextMuted : UITheme.TextPrimary;

                int share = total > 0 ? Mathf.RoundToInt(100f * entry.Damage / total) : 0;
                _value.text = entry.Damage > 0 ? $"{entry.Damage:N0}  <size=80%>{share}%</size>" : "0";
                _value.richText = true;

                float ratio = max > 0 ? Mathf.Clamp01(entry.Damage / (float)max) : 0f;
                _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                _fill.enabled = ratio > 0f;
            }

            public void Clear() => _frame.gameObject.SetActive(false);
        }
    }
}
