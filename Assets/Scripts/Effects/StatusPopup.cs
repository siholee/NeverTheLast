using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Entities.Status;
using Managers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Effects
{
    /// <summary>성공한 부여만 머리 위에 알린다. 동일 이름은 합치고 한 유닛에 세 줄만 표시한다.</summary>
    [DefaultExecutionOrder(5000)]
    public sealed class StatusPopup : MonoBehaviour
    {
        private sealed class Notice
        {
            public Unit Target; public string Name; public Sprite Icon; public Color Accent;
            public bool Refresh; public float QueuedAt;
        }
        private static StatusPopup _instance;
        private Material _labelMaterial;
        private readonly List<Notice> _pending = new();
        private readonly List<Entry> _live = new();
        private sealed class Entry
        {
            public Notice Notice; public RectTransform Rect; public CanvasGroup Group;
            public TextMeshProUGUI Label; public float Age; public int Slot; public Vector2 Anchor;
            /// <summary>대상이 움직여 이웃 알림과 겹치게 됐을 때만 늘어나는 추가 상승. 줄지 않으므로 흔들리지 않는다.</summary>
            public float Lift, LiftTarget;
            public float Width => Rect.sizeDelta.x;
        }
        private const float Life=1.35f, RowStep=34f, BaseRise=12f, RisePerSecond=0f, BoxHeight=44f;
        /// <summary>글자 높이(22)에 위아래 여유를 더한 실제 점유 높이. 44px 상자의 빈 여백끼리는 겹쳐도 된다.</summary>
        private const float ClearHeight=34f;
        private const float MaxLift=RowStep;
        /// <summary>생성·갱신·충돌 검사가 모두 쓰는 한 가지 좌표 규약. 상단 경계 클램프까지 적용한 실제 자리다.</summary>
        private static Vector2 Place(Vector2 anchor,int slot,float age,float lift,float width)
            => CombatFeedbackSurface.Clamp(anchor+new Vector2(0,BaseRise+slot*RowStep+lift+RisePerSecond*age),new Vector2(width*.5f+2,BoxHeight*.5f));
        private static Vector2 Place(Entry e,float age) => Place(e.Anchor,e.Slot,age,e.Lift,e.Width);
        private static bool Overlaps(Vector2 a,float aWidth,Vector2 b,float bWidth)
            => Mathf.Abs(a.x-b.x)<(aWidth+bWidth)*.5f && Mathf.Abs(a.y-b.y)<ClearHeight;
        public static void Show(Unit target, UnitStatus status, bool refresh)
        {
            if (status == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 검증 fixture는 전투 판정에만 쓰며 출시 화면의 상태 알림으로 노출하지 않는다.
            if(status.Key?.StartsWith("verify_",System.StringComparison.Ordinal)==true) return;
#endif
            Color accent = StatusIcons.FrameKind(status) switch
            {
                StatusFrameKind.Beneficial => new Color(.17f,.51f,.47f),
                StatusFrameKind.Control => new Color(.52f,.35f,.65f),
                StatusFrameKind.Harmful => new Color(.71f,.31f,.40f),
                _ => UITheme.AccentDim
            };
            Enqueue(target,status.StatusName,StatusIcons.For(status),accent,refresh);
        }
        public static void ShowElement(Unit target, BaseEnums.UnitElement element, bool refresh)
            => Enqueue(target, UnitInfoText.ElementName(element.ToString()) + " 원소 부여",
                StatusIcons.Element(element), Color.Lerp(Effects.Vfx.ElementPalette.ColorFor(element.ToString()),UITheme.TextPrimary,.42f), refresh);
        /// <summary>상태를 만들지 않고 실제 경호 개입의 주체만 알린다. 다른 상태 알림과 동일한 충돌 회피를 쓴다.</summary>
        public static void ShowHitWard(Unit target, int remaining)
            => Enqueue(target, $"타수 방호 {remaining}", StatusIcons.Get(StatusGlyph.Defense), UITheme.TextPrimary, false);
        public static void ShowGuard(Unit protector)
            => Enqueue(protector,"경호",UILineIcons.Get(UIIcon.Shield),UITheme.AccentDim,false);
        public static void ShowToughnessBreak(Unit target)
            => Enqueue(target, "강인도 격파", StatusIcons.Get(StatusGlyph.Defense), UITheme.AccentDim, false);
        private static void Enqueue(Unit target, string name, Sprite icon, Color accent, bool refresh)
        {
            if(target==null || !target.isActive || !target.IsOnField || string.IsNullOrWhiteSpace(name) ||
                GameManager.Instance == null || GameManager.Instance.gameState!=BaseEnums.GameState.RoundInProgress) return;
            if (_instance == null) _instance=CombatFeedbackSurface.Root.gameObject.AddComponent<StatusPopup>();
            var same=_instance._live.FirstOrDefault(e=>e.Notice.Target==target && e.Notice.Name==name);
            if(same!=null) { same.Notice.Refresh |= refresh; FitLabel(same); return; }
            var queued=_instance._pending.FirstOrDefault(e=>e.Target==target && e.Name==name);
            if(queued!=null) {queued.Refresh |= refresh;return;}
            if(_instance._pending.Count>=48) _instance._pending.RemoveAt(0);
            _instance._pending.Add(new Notice{Target=target,Name=name,Icon=icon,Accent=accent,Refresh=refresh,QueuedAt=Vfx.VfxTiming.ReadableTime});
        }
        private static string Label(Notice n) => n.Name + (n.Refresh ? " · 갱신" : "");
        private void LateUpdate()
        {
            if(GameManager.Instance==null || GameManager.Instance.gameState!=BaseEnums.GameState.RoundInProgress)
            { Clear(); return; }
            if(Time.timeScale<=0) {foreach(var pending in _pending)pending.QueuedAt+=Vfx.VfxTiming.ReadableDeltaTime; return;}
            for(int i=_live.Count-1;i>=0;i--)
            {
                Entry e=_live[i]; e.Age+=Vfx.VfxTiming.ReadableDeltaTime;
                if(e.Age>=Life || e.Notice.Target==null || !e.Notice.Target.isActive)
                {Destroy(e.Rect.gameObject);_live.RemoveAt(i);continue;}
            }
            // 생성 순서대로 자리를 잡는다. 대상 카드가 돌진·재배치로 움직여 앞선 알림과 겹치게 되면
            // 뒤 알림만 한 줄 높이까지 부드럽게 더 올린다. 생성 시 검사는 정지한 대상만 보장하기 때문이다.
            for(int i=0;i<_live.Count;i++)
            {
                Entry e=_live[i];
                e.Anchor=CombatFeedbackSurface.Position(e.Notice.Target,true);
                Vector2 desired=Place(e.Anchor,e.Slot,e.Age,e.LiftTarget,e.Width);
                for(int j=0;j<i;j++)
                {
                    Entry other=_live[j];
                    Vector2 placed=other.Rect.anchoredPosition;
                    if(!Overlaps(placed,other.Width,desired,e.Width) || desired.y<placed.y) continue;
                    e.LiftTarget=Mathf.Min(MaxLift,e.LiftTarget+placed.y+ClearHeight-desired.y);
                    desired=Place(e.Anchor,e.Slot,e.Age,e.LiftTarget,e.Width);
                }
                e.Lift=Mathf.MoveTowards(e.Lift,e.LiftTarget,RowStep*6*Vfx.VfxTiming.ReadableDeltaTime);
                e.Rect.anchoredPosition=Place(e,e.Age);
                // 뒤늦은 추가타 숫자가 들어오면 알림 시간을 보존하고 빈 줄에서 다시 읽히게 한다.
                if (!DamagePopup.HasRoom(e.Rect.anchoredPosition,new Vector2(e.Width*.5f,ClearHeight*.5f)) &&
                    Vfx.VfxTiming.ReadableTime-e.Notice.QueuedAt<8f) e.Age-=Vfx.VfxTiming.ReadableDeltaTime;
                e.Group.alpha=CombatFeedbackSurface.NoticeHasRoom(e.Rect.anchoredPosition, new Vector2(e.Width * .5f, ClearHeight * .5f)) ? Mathf.Min(Mathf.Clamp01(e.Age/.10f),Mathf.Clamp01((Life-e.Age)/.30f)) : 0;
                e.Rect.localScale=Vector3.one * (1 + .05f*(1-Mathf.Clamp01(e.Age/.12f)));
            }
            for(int i=0;i<_pending.Count;)
            {
                Notice n=_pending[i];
                if(n.Target==null || !n.Target.isActive || Vfx.VfxTiming.ReadableTime-n.QueuedAt>8) {_pending.RemoveAt(i);continue;}
                var slots=_live.Where(e=>e.Notice.Target==n.Target).Select(e=>e.Slot).ToList();
                if(slots.Count>=3 || _live.Count>=24){i++;continue;}
                // 대각 진형에서는 이웃 카드의 알림 줄이 같은 높이에 와 글자가 겹쳤다(960 실화면).
                // 한 유닛 세 줄 제한은 그대로 두고, 이미 떠 있는 알림과 겹치는 자리만 위로 건너뛴다.
                // 상단 경계·이웃 얼굴·정보판 때문에 빈 자리가 없으면 옆으로 보내지 않고 큐에서 차례를 기다린다.
                Vector2 anchor=CombatFeedbackSurface.Position(n.Target,true);
                float width=EstimateWidth(n);
                int slot=0;
                while(slot<MaxSlot && (slots.Contains(slot) || CollidesWithLive(anchor,slot,width))) slot++;
                if(slot>=MaxSlot){i++;continue;}
                Spawn(n,slot,anchor);_pending.RemoveAt(i);
            }
        }
        /// <summary>대상 머리 위 세 줄 범위만 쓴다. 위 행의 다른 캐릭터까지 올라가지 않는다.</summary>
        private const int MaxSlot=3;
        // 자동 맞춤된 기존 글자의 크기로 새 알림 폭을 추측하지 않는다. 최대 폭을 미리 확보한다.
        private float EstimateWidth(Notice n) => 236;
        /// <summary>
        /// 새 알림이 떠 있는 동안의 실제 궤적을 기존 알림의 남은 궤적과 비교한다. 둘 다 같은 속도로 오르지만
        /// 위쪽이 상단 경계에 막히면 아래 알림이 따라붙으므로 시작·중간·끝 세 시점을 클램프한 자리로 본다.
        /// 같은 유닛의 알림도 상단에서 접힐 수 있어 대상을 가리지 않는다.
        /// </summary>
        private bool CollidesWithLive(Vector2 anchor,int slot,float width)
        {
            for (int k = 0; k < 3; k++)
                if (!CombatFeedbackSurface.NoticeHasRoom(Place(anchor, slot, Life * k * .5f, 0, width), new Vector2(width * .5f + 2, ClearHeight * .5f))) return true;
            foreach(Entry e in _live)
            {
                if(e.Rect==null) continue;
                float remaining=Mathf.Max(0,Life-e.Age);
                for(int k=0;k<3;k++)
                {
                    float t=remaining*k*.5f;
                    if(Overlaps(Place(e,e.Age+t),e.Width,Place(anchor,slot,t,0,width),width)) return true;
                }
            }
            return false;
        }
        private void Spawn(Notice notice,int slot,Vector2 anchor)
        {
            var rect=UIBuild.Container("StatusNotice",transform);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(236,BoxHeight);
            var icon=UIBuild.Solid("StatusGlyph",rect,notice.Accent);icon.sprite=notice.Icon;icon.raycastTarget=false;
            UIBuild.Pin(icon.rectTransform,new Vector2(0,.5f),new Vector2(24,24),new Vector2(10,0));
            icon.rectTransform.pivot=new Vector2(0,.5f);
            var text=UIBuild.Text("StatusName",rect,Label(notice),22,UITheme.TextPrimary);
            text.fontStyle=FontStyles.Bold;
            // 면·테두리 없이 글자에만 얇은 흰 여유를 주어 밝은 배경에서도 구분한다.
            var material=_labelMaterial;
            if(material==null) _labelMaterial=material=new Material(text.fontSharedMaterial);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,.10f);
            material.SetColor(ShaderUtilities.ID_OutlineColor,new Color(1,1,1,.95f));
            text.fontSharedMaterial=material;
            text.overflowMode=TextOverflowModes.Ellipsis;
            UIBuild.Stretch(text.rectTransform,44,0);text.rectTransform.offsetMax=new Vector2(-9,0);
            var group=rect.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.alpha=0;
            var entry=new Entry{Notice=notice,Rect=rect,Group=group,Label=text,Slot=slot,Anchor=anchor};
            FitLabel(entry);
            // 같은 프레임의 다음 알림이 (0,0)이 아닌 실제 자리와 비교하도록 생성 즉시 배치한다.
            rect.anchoredPosition=Place(entry,0);
            _live.Add(entry);
        }
        private static void FitLabel(Entry entry)
        {
            entry.Label.text=Label(entry.Notice);
            entry.Rect.sizeDelta=new Vector2(Mathf.Clamp(entry.Label.GetPreferredValues(entry.Label.text).x+62,128,236),BoxHeight);
        }
        private void Clear()
        {foreach(Entry e in _live)if(e.Rect!=null)Destroy(e.Rect.gameObject);_live.Clear();_pending.Clear();}
        private void OnDestroy(){Clear();if(_labelMaterial!=null)Destroy(_labelMaterial);if(_instance==this)_instance=null;}
    }
}
