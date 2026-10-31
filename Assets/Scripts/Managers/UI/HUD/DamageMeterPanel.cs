using System.Collections.Generic;
using BaseClasses;
using Combat;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Managers.Stage;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 우측 피해 집계 레일. 한 장의 라이트 글라스 안에서 이름과 수치를 탭형으로 정렬하고,
    /// 행 사이는 헤어라인만 둔다. 새 피해가 들어온 행만 잠깐 밝아진다.
    /// </summary>
    public sealed class DamageMeterPanel
    {
        private const int MaxRows = 6;
        private const float RowHeight = 52f;
        private const float PanelWidth = 224f;
        private const float HeaderHeight = 54f;
        private const float FooterHeight = 54f;
        private const float RefreshInterval = 0.2f;

        private readonly Image _panel;
        private readonly TextMeshProUGUI _total;
        private readonly List<Row> _rows = new();
        private float _nextRefreshAt;
        private bool _stageLayout;
        private readonly List<System.Action> _restoreStage = new();

        public DamageMeterPanel(Transform parent)
        {
            _panel = UIBuild.ChromePanel("DamageMeter", parent);
            UIBuild.Pin(_panel.rectTransform, new Vector2(1f, 1f),
                new Vector2(PanelWidth, HeaderHeight + MaxRows * RowHeight + FooterHeight),
                new Vector2(-UITheme.ScreenEdgeInset, -UITheme.BelowTopBar));
            _panel.raycastTarget = false;

            TextMeshProUGUI caption = UIBuild.Label("Caption", _panel.transform, "피해 집계",
                UITheme.FontBody, UITheme.TextSecondary);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f),
                new Vector2(PanelWidth - 36f, 28f), new Vector2(18f, -12f));

            _total = UIBuild.Text("Total", _panel.transform, "", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_total.rectTransform, new Vector2(1f, 0f),
                new Vector2(PanelWidth - 92f, 46f), new Vector2(-18f, 10f));
            TextMeshProUGUI totalLabel = UIBuild.Text("TotalLabel", _panel.transform, "합계",
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UIBuild.Pin(totalLabel.rectTransform, new Vector2(0f, 0f),
                new Vector2(64f, 46f), new Vector2(18f, 10f));
            Image totalDivider = UIBuild.Divider("TotalDivider", _panel.transform);
            UIBuild.Pin(totalDivider.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(PanelWidth - 36f, 1f), new Vector2(0f, FooterHeight));

            for (int i = 0; i < MaxRows; i++) _rows.Add(new Row(_panel.transform, i));
            _panel.gameObject.SetActive(false);
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            bool inBattle = game != null && game.gameState == BaseEnums.GameState.RoundInProgress;
            if (_panel.gameObject.activeSelf != inBattle) _panel.gameObject.SetActive(inBattle);
            if (!inBattle) return;
            ApplyStageLayout(DamageMeter.Sorted().Count);
            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + RefreshInterval;

            List<DamageMeter.Entry> entries = DamageMeter.Sorted();
            long total = DamageMeter.Total();
            _total.text = total > 0 ? total.ToString("N0") : "0";

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < entries.Count) _rows[i].Set(entries[i], total);
                else _rows[i].Clear();
            }
        }

        private void ApplyStageLayout(int count)
        {
            bool stage=VisualModeState.Current==VisualMode.Stage;
            if(stage&&!_stageLayout)
            {
                foreach(var rect in _panel.GetComponentsInChildren<RectTransform>(true))
                {
                    Vector2 min=rect.anchorMin,max=rect.anchorMax,pivot=rect.pivot,size=rect.sizeDelta,pos=rect.anchoredPosition;Vector3 originalScale=rect.localScale;
                    _restoreStage.Add(()=>{if(rect==null)return;rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=pos;rect.localScale=originalScale;});
                }
                foreach(var text in _panel.GetComponentsInChildren<TMP_Text>(true))
                {float font=text.fontSize;Color color=text.color;bool auto=text.enableAutoSizing;_restoreStage.Add(()=>{if(text==null)return;text.fontSize=font;text.color=color;text.enableAutoSizing=auto;});}
                Sprite sprite=_panel.sprite;Color tint=_panel.color;
                _restoreStage.Add(()=>{_panel.sprite=sprite;_panel.color=tint;});
            }
            if(!stage&&_stageLayout){foreach(var restore in _restoreStage)restore();_restoreStage.Clear();}
            _stageLayout=stage;if(!stage)return;
            float scale=StageBattleHud.UiScale(_panel.rectTransform);
            int rows=Mathf.Min(MaxRows,count);
            UIBuild.Pin(_panel.rectTransform,new Vector2(1,1),new Vector2(230,82+rows*40),new Vector2(-24*scale,-100*scale));
            _panel.rectTransform.localScale=Vector3.one*scale;
            _panel.sprite=UIShapes.RoundedRect(14,Color.white);_panel.type=Image.Type.Sliced;_panel.color=StageBattleHud.Glass;
            var caption=_panel.transform.Find("Caption").GetComponent<TMP_Text>();caption.fontSize=15;
            UIBuild.Pin(caption.rectTransform,new Vector2(0,1),new Vector2(198,15),new Vector2(16,-14));
            _total.fontSize=20;_total.color=StageBattleHud.Ink;
            UIBuild.Pin(_total.rectTransform,new Vector2(1,0),new Vector2(130,24),new Vector2(-16,12));
            var totalLabel=_panel.transform.Find("TotalLabel").GetComponent<TMP_Text>();totalLabel.fontSize=14;
            UIBuild.Pin(totalLabel.rectTransform,new Vector2(0,0),new Vector2(64,24),new Vector2(16,12));
            var divider=_panel.transform.Find("TotalDivider").GetComponent<RectTransform>();
            UIBuild.Pin(divider,new Vector2(.5f,0),new Vector2(198,1),new Vector2(0,45));
            for(int i=0;i<_rows.Count;i++)_rows[i].ApplyStageLayout(i);
            foreach(var text in _panel.GetComponentsInChildren<TMP_Text>(true))text.enableAutoSizing=false;
        }

        private sealed class Row
        {
            private readonly StageUnitFocus _stageFocus;
            private readonly RectTransform _root;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _value;
            private string _portraitPath;
            private long _lastDamage;
            private float _flashUntil;
            public void ApplyStageLayout(int index)
            {
                UIBuild.Pin(_root,new Vector2(0,1),new Vector2(198,40),new Vector2(16,-37-index*40));
                UIBuild.Pin(_portrait.rectTransform,new Vector2(0,.5f),new Vector2(30,30),Vector2.zero);
                _portrait.rectTransform.pivot=new Vector2(0,.5f);
                _name.fontSize=14;_value.fontSize=16;
            }

            public Row(Transform parent, int index)
            {
                _root = UIBuild.Container($"Row{index}", parent);
                UIBuild.Anchor(_root, new Vector2(0.07f, 0f), new Vector2(0.93f, 0f));
                _root.pivot = new Vector2(0.5f, 0f);
                _root.sizeDelta = new Vector2(0f, RowHeight);
                _root.anchoredPosition = new Vector2(0f, FooterHeight + (MaxRows - 1 - index) * RowHeight);

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(_root, false);
                _portrait = portraitGo.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Pin(_portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(34f, 40f), Vector2.zero);

                _name = UIBuild.Text("Name", _root, "", UITheme.FontBody, UITheme.TextPrimary);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f));
                _name.rectTransform.offsetMin = new Vector2(40f, 0f);
                _name.overflowMode = TextOverflowModes.Ellipsis;

                _value = UIBuild.Text("Value", _root, "", UITheme.FontBody - 1f, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(_value.rectTransform, new Vector2(0.56f, 0f), Vector2.one);

                Image divider = UIBuild.Divider("Divider", _root);
                UIBuild.Anchor(divider.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));
                divider.rectTransform.sizeDelta = new Vector2(0f, 1f);
                divider.raycastTarget = false;
                Image hit = _root.gameObject.AddComponent<Image>();
                hit.color = Color.clear; hit.raycastTarget = false;
                _stageFocus = StageUnitFocus.Attach(hit);
                _root.gameObject.SetActive(false);
            }

            public void Set(DamageMeter.Entry entry, long total)
            {
                _stageFocus.Bind(entry.Unit);
                _root.gameObject.SetActive(true);
                if (_portraitPath != entry.Portrait)
                {
                    _portraitPath = entry.Portrait;
                    Sprite sprite = UIPortraitCrop.Face(SpriteResource.LoadPortrait(entry.Portrait));
                    _portrait.sprite = sprite;
                    _portrait.enabled = sprite != null;
                }

                if (entry.Damage > _lastDamage) _flashUntil = Time.unscaledTime + 0.35f;
                _lastDamage = entry.Damage;
                bool flash = Time.unscaledTime < _flashUntil;
                bool down = entry.Unit == null || !entry.Unit.isActive;
                Color color = down ? UITheme.TextMuted : flash ? UITheme.Highlight : UITheme.TextPrimary;
                _portrait.color = down ? new Color(0.42f, 0.42f, 0.42f, 0.72f) : Color.white;
                _name.text = entry.Name;
                _name.color = color;
                _value.color = color;
                int share = total > 0 ? Mathf.RoundToInt(100f * entry.Damage / total) : 0;
                _value.text = VisualModeState.Current == VisualMode.Stage ? entry.Damage.ToString("N0") : entry.Damage > 0 ? $"{entry.Damage:N0}\n<size=72%>{share}%</size>" : "0";
                _value.richText = true;
                _value.lineSpacing = -12f;
            }

            public void Clear()
            {
                _stageFocus.Bind(null);
                _root.gameObject.SetActive(false);
                _portraitPath = null;
                _lastDamage = 0;
            }
        }
    }
}
