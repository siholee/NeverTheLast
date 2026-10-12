using System;
using System.Collections.Generic;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 화면 상단을 한 줄로 묶는 탐색/상태 헤더. 왼쪽부터 순서대로:
    /// [라운드 + 진행 게이지] · [재화/페이즈] · [진행 예고] · [배속] [일시정지] [파티] [메뉴]
    ///
    /// 진행 예고는 TFT의 라운드 트랙을 따른다. 이 게임은 적이 스테이지 시작 시 한 번에
    /// 소환되므로, "다음에 나올 적 개체"가 아니라 "앞으로 올 스테이지의 성격"을 칸으로 보여준다.
    /// </summary>
    public class TopStatusBar
    {
        private const int PreviewSlots = 6;
        private const float SlotSize = 31f;
        private const float SlotGap = 10f;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _roundLabel;
        private readonly TextMeshProUGUI _themeLabel;
        private readonly TextMeshProUGUI _timerLabel;
        private readonly Image _timerRing;
        private readonly Image _crestIcon;
        private readonly TextMeshProUGUI _speedLabel;
        private readonly List<Slot> _slots = new();

        private int _lastStage = -1;

        public RectTransform ResourceRoot { get; private set; }
        private Image _pauseIcon;
        private bool _paused;

        public TopStatusBar(Transform parent, Action onSpeed, Action onPause, Action onCodex, Action onSettings)
        {
            _root = UIBuild.Container("TopStatusBar", parent);
            _root.anchorMin = new Vector2(0,1); _root.anchorMax = Vector2.one;
            _root.pivot = new Vector2(.5f,1);
            _root.sizeDelta = new Vector2(-UITheme.ScreenEdgeInset*2,UITheme.TopBarHeight);
            _root.anchoredPosition = new Vector2(0,-UITheme.ScreenTopInset);
            var background = UIBuild.ChromePanel("HeaderBand",_root);
            UIBuild.Stretch(background.rectTransform);
            background.raycastTarget = false;
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var row = _root.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24,24,8,8); row.spacing = 24;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            RectTransform Block(string name,float width)
            {
                var b=UIBuild.Container(name,_root);
                var layout=b.gameObject.AddComponent<LayoutElement>();
                layout.minWidth=layout.preferredWidth=width; layout.preferredHeight=40;
                return b;
            }
            var roundBlock=Block("RoundBlock",270);
            _timerRing=UIBuild.RadialBar("Timer",roundBlock,UITheme.Accent,UITheme.Track,96,.82f);
            var ringTrack=(RectTransform)_timerRing.transform.parent;
            UIBuild.Pin(ringTrack,new Vector2(0,.5f),new Vector2(28,28),new Vector2(0,0));
            ringTrack.pivot=new Vector2(0,.5f);
            _crestIcon=UIBuild.Icon("CrestGlyph",ringTrack,UIIcon.Route,UITheme.TextPrimary);
            UIBuild.Stretch(_crestIcon.rectTransform,7,7);
            _timerLabel=UIBuild.Text("TimerText",ringTrack,"",14,UITheme.TextPrimary,TextAlignmentOptions.Center);
            UIBuild.Stretch(_timerLabel.rectTransform);
            TooltipTrigger.Attach(ringTrack.gameObject,()=>GameManager.Instance?.IsRoundTurnGauge==true?"남은 전투 턴":"준비 남은 시간",
                ()=>new[]{UITooltip.Line.Note(GameManager.Instance?.IsRoundTurnGauge==true?$"{GameManager.Instance.PhaseRemainingSeconds}턴 남았습니다.":$"{GameManager.Instance?.PhaseRemainingSeconds??0}초 남았습니다.",UITheme.TextSecondary)},UITheme.Accent);
            _roundLabel=UIBuild.Text("RoundLabel",roundBlock,"1-1",22,UITheme.TextPrimary);
            UIBuild.Anchor(_roundLabel.rectTransform,Vector2.zero,new Vector2(.34f,1f));
            _roundLabel.rectTransform.offsetMin=new Vector2(42,0);
            _themeLabel=UIBuild.Text("ThemeLabel",roundBlock,"",18,UITheme.TextSecondary);
            UIBuild.Anchor(_themeLabel.rectTransform,new Vector2(.36f,0f),Vector2.one);
            _themeLabel.rectTransform.offsetMin=new Vector2(8,0);
            _themeLabel.overflowMode=TextOverflowModes.Ellipsis;


            ResourceRoot=Block("ResourceStrip",420);
            var space=Block("HeaderBreathingSpace",0);
            space.GetComponent<LayoutElement>().flexibleWidth=1;
            var preview=Block("StagePreview",290);
            var caption=UIBuild.Label("PreviewCaption",preview,"진행 예고",18,UITheme.TextSecondary);
            caption.text="예고";
            UIBuild.Pin(caption.rectTransform,new Vector2(0,.5f),new Vector2(40,40),Vector2.zero);
            caption.rectTransform.pivot=new Vector2(0,.5f);
            var previewRoot=UIBuild.Container("PreviewSlots",preview);
            UIBuild.Pin(previewRoot,new Vector2(0,.5f),new Vector2(246,31),new Vector2(48,0));
            previewRoot.pivot=new Vector2(0,.5f);
            for(int i=0;i<PreviewSlots;i++)_slots.Add(new Slot(previewRoot,i));

            var controls=Block("Navigation",302);
            Button Control(string name, UIIcon icon, float x, float width, Action action)
            {
                Button button=UIBuild.SymbolButton(name,controls,icon,action);
                button.image.sprite=UIShapes.RoundedRect(6,Color.clear);
                UIBuild.Pin(button.image.rectTransform,new Vector2(0,.5f),new Vector2(width,40),new Vector2(x,0));
                button.image.rectTransform.pivot=new Vector2(0,.5f);
                UIBuild.Pin(button.transform.Find("Icon").GetComponent<RectTransform>(),new Vector2(.5f,.5f),new Vector2(22,22),Vector2.zero);
                return button;
            }
            Button speed=Control("SpeedButton",UIIcon.FastForward,0,76,onSpeed);
            UIBuild.Pin(speed.transform.Find("Icon").GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(22,22),new Vector2(17,0));
            _speedLabel=UIBuild.Text("SpeedLabel",speed.transform,"1×",20,UITheme.TextPrimary,TextAlignmentOptions.Midline);
            UIBuild.Pin(_speedLabel.rectTransform,new Vector2(1,.5f),new Vector2(36,40),new Vector2(-5,0));
            TooltipTrigger.Attach(speed.gameObject,()=>"전투 속도 · "+_speedLabel.text,
                ()=>new[]{UITooltip.Line.Note("클릭하여 1× → 2× → 4× 순으로 전환합니다.\n1×  행동과 효과를 차근차근 확인\n2×  익숙한 전투를 빠르게 확인\n4×  빠른 진행",UITheme.TextSecondary)},UITheme.Accent);
            Button pause=Control("BattlePauseButton",UIIcon.Pause,80,44,onPause);
            _pauseIcon=pause.transform.Find("Icon").GetComponent<Image>();
            TooltipTrigger.Attach(pause.gameObject,()=>_paused?"전투 계속":"일시정지",
                ()=>new[]{UITooltip.Line.Note(_paused?"클릭하여 현재 배속으로 전투를 계속합니다.":"배속 설정을 유지한 채 전투를 멈춥니다.",UITheme.TextSecondary)},UITheme.Accent);
            Image divider=UIBuild.Divider("NavigationRule",controls);
            UIBuild.Pin(divider.rectTransform,new Vector2(0,.5f),new Vector2(1,22),new Vector2(140,0));
            divider.raycastTarget=false;
            Button party=Control("CodexButton",UIIcon.Party,154,64,onCodex);
            Button menu=Control("SettingsButton",UIIcon.Menu,230,64,onSettings);
            TooltipTrigger.Attach(party.gameObject,()=>"파티 · TAB",()=>new[]{UITooltip.Line.Note("아군과 활성 코드, 보관함을 확인합니다.",UITheme.TextSecondary)},UITheme.Accent);
            TooltipTrigger.Attach(menu.gameObject,()=>"메뉴 · ESC",()=>new[]{UITooltip.Line.Note("자료실, 설정 및 저장 메뉴를 엽니다.",UITheme.TextSecondary)},UITheme.Accent);

        }

        public void SetPaused(bool paused)
        {
            _pauseIcon.sprite=UILineIcons.Get(paused?UIIcon.Play:UIIcon.Pause);
            _paused=paused;
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            RoundManager round = game?.RoundManager;
            if (round == null) return;

            _roundLabel.text = $"{round.Round}-{round.StageInRound}";
            _themeLabel.text = string.IsNullOrEmpty(round.CurrentThemeName)
                ? $"STAGE {round.Stage}"
                : round.CurrentThemeName;

            // 원형 게이지 — 준비 단계는 남은 초, 전투 중에는 남은 턴이다.
            _timerRing.fillAmount = game.PhaseProgressRatio;

            int remaining = game.PhaseRemainingSeconds;
            _crestIcon.enabled = remaining <= 0;
            bool turnGauge = game.IsRoundTurnGauge;
            _timerLabel.text = remaining > 0
                ? remaining.ToString()
                : "";

            // 헤더 크롬은 남은 시간과 관계없이 같은 군청색을 유지한다.
            _timerRing.color = UITheme.Accent;

            if (_lastStage != round.Stage)
            {
                _lastStage = round.Stage;
                RefreshPreview(round);
            }
        }

        private void RefreshPreview(RoundManager round)
        {
            List<StageKind> kinds = round.GetUpcomingStageKinds(PreviewSlots);
            for (int i = 0; i < _slots.Count; i++)
            {
                // 첫 칸이 현재 스테이지다.
                _slots[i].SetVisible(i < kinds.Count);
                if (i < kinds.Count) _slots[i].Set(kinds[i], round.Stage+i, i);
            }
        }

        public void SetSpeedLabel(float speed)
        {
            // 정수 배속이면 소수점을 떼서 "2×"처럼 짧게 보여준다.
            _speedLabel.text = Mathf.Approximately(speed, Mathf.Round(speed))
                ? $"{Mathf.RoundToInt(speed)}×"
                : $"{speed:0.#}×";
        }

        /// <summary>진행 예고의 한 칸.</summary>
        private sealed class Slot
        {
            private readonly Image _fill;
            private readonly Image _currentMark;
            private readonly Image _glyph;
            private string _kindName = "전투", _forecast = "", _detail = "";

            public Slot(Transform parent, int index)
            {
                _fill = UIBuild.Panel($"Slot{index}", parent, UITheme.BoardSlate,
                    UIShapes.Corner.All, 5, UITheme.Outline, 1);
                UIBuild.Pin(_fill.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(SlotSize, SlotSize), new Vector2(index * (SlotSize + SlotGap), 0f));
                _fill.rectTransform.pivot=new Vector2(0,.5f);
                _fill.raycastTarget = true;
                TooltipTrigger.Attach(_fill.gameObject, () => _forecast,
                    () => new[] { UITooltip.Line.Note(_detail, UITheme.TextSecondary) }, UITheme.Accent);

                _glyph = UIBuild.Icon("Glyph", _fill.transform, UIIcon.Battle, UITheme.TextPrimary);
                UIBuild.Stretch(_glyph.rectTransform, 6, 6);

                // 현재 스테이지는 색 면 대신 하늘색 노치 하나로만 표시한다.
                _currentMark = UIBuild.Solid("CurrentMark", _fill.transform, UITheme.Accent);
                UIBuild.Pin(_currentMark.rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(SlotSize - 6f, 2f), new Vector2(0f, -4f));
                _currentMark.raycastTarget = false;
                _currentMark.enabled = false;
            }

            public void SetVisible(bool visible) => _fill.gameObject.SetActive(visible);

            public void Set(StageKind kind, int stage, int distance)
            {
                bool isCurrent=distance==0;
                _kindName = kind switch { StageKind.Boss => "보스", StageKind.MidBoss => "중간 보스",
                    StageKind.Elite => "정예", StageKind.Event => "사건", _ => "전투" };
                _forecast=$"{(stage-1)/RoundManager.EncountersPerTheme+1}-{(stage-1)%RoundManager.EncountersPerTheme+1} · {_kindName}";
                string meaning=kind switch { StageKind.Boss=>"구간의 보스와 전투합니다.", StageKind.MidBoss=>"중간 보스와 전투합니다.",
                    StageKind.Elite=>"강화된 적과 전투합니다.", StageKind.Event=>"사건과 선택지가 기다리고 있습니다.", _=>"적 편성과 자동 전투를 진행합니다." };
                _detail=(isCurrent?"현재 스테이지":"앞으로 "+distance+"단계 후")+"\n"+meaning;
                _glyph.sprite = UILineIcons.Get(kind switch { StageKind.Boss => UIIcon.Boss,
                    StageKind.MidBoss => UIIcon.Elite, StageKind.Elite => UIIcon.Elite,
                    StageKind.Event => UIIcon.Event, _ => UIIcon.Battle });
                _glyph.color = isCurrent ? UITheme.TextPrimary : UITheme.TextSecondary;
                _fill.color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.58f);
                _currentMark.enabled = isCurrent;
            }


        }
    }
}
