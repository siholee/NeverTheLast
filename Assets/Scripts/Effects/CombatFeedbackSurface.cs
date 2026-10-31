using System.Collections.Generic;
using Entities.View;
using Effects.Vfx;
using Entities;
using Managers.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Effects
{
    /// <summary>전투 숫자와 상태 알림의 비입력 화면 표면. 원화 크기와 카메라 배율에 관계없이 읽힌다.</summary>
    internal static class CombatFeedbackSurface
    {
        private static int _cardFrame = -1;
        private static UnitCardView[] _cards = System.Array.Empty<UnitCardView>();
        private static UnitCardView[] Cards()
        {
            if (_cardFrame != Time.frameCount)
            {
                _cardFrame = Time.frameCount;
                _cards = Object.FindObjectsByType<UnitCardView>(FindObjectsSortMode.None);
            }
            return _cards;
        }
        private static RectTransform _root;
        public static RectTransform Root
        {
            get
            {
                if (_root != null) return _root;
                Canvas canvas = UIBuild.Canvas("CombatFeedbackCanvas", 30);
                canvas.GetComponent<GraphicRaycaster>().enabled = false;
                _root = (RectTransform)canvas.transform;
                CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false;
                return _root;
            }
        }
        public static Vector2 Position(Unit unit, bool above)
        {
            RectTransform root = Root;
            Camera camera = Camera.main;
            if (camera == null || unit == null) return Vector2.zero;
            var stageView = CardMotion.ViewOf(unit);
            if (stageView?.StageProxy != null)
            {
                Vector3 anchor = camera.WorldToScreenPoint(stageView.CenterWorld);
                anchor.y = above ? camera.WorldToScreenPoint(stageView.StageProxy.VisibleHeadWorldBounds.max).y + 26f : anchor.y + 60f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, anchor, null, out Vector2 stageLocal);
                return stageLocal;
            }
            Vector3 world = VfxSpace.WorldPoint(unit);
            world.y += Cell.CardSize * (above ? .53f : .14f);
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (above)
            {
                var view=CardMotion.ViewOf(unit);
                Rect portrait=view!=null ? view.PortraitFocusScreenRect(camera) : default;
                if(portrait.height>0)screen.y=portrait.yMax+8f;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            return local;
        }
        /// <summary>앞선 행의 정보판이 머리 위에 있으면 숫자를 해당 표적 원화 안쪽 두 행에 둔다.</summary>
        internal static bool DamageHasHudAbove(Unit target)
        {
            var view=CardMotion.ViewOf(target);Camera camera=Camera.main;
            if(view==null||camera==null)return false;
            Rect portrait=view.PortraitFocusScreenRect(camera);
            foreach(var card in Cards())
            {
                if (card == null) continue;
                if(card==view)continue;
                Rect hud=card.HudScreenRect(camera);
                if(hud.height<=0||hud.yMin<portrait.yMax||hud.yMin-portrait.yMax>=132f)continue;
                if(hud.xMax>portrait.xMin-60f&&hud.xMin<portrait.xMax+60f)return true;
            }
            return false;
        }

        private static int _obstacleFrame = -1;
        private static readonly List<Rect> HudObstacles = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly List<string> HudObstacleSources = new();
        private static string _obstacleSource;
#endif
        private static RectTransform _actionQueue;
        private static RectTransform _featureBanner;
        private static RectTransform _stageDamageMeter;
        private static readonly List<RectTransform> QueueRects = new();
        private static readonly Vector3[] Corners = new Vector3[4];
        /// <summary>정보판과 원화의 얼굴을 피할 공간이 없으면 알림을 큐에 남긴다.</summary>
        public static bool NoticeHasRoom(Vector2 position, Vector2 halfSize,DamagePopup ignore=null)
        {
            if (_obstacleFrame != Time.frameCount)
            {
                _obstacleFrame = Time.frameCount; HudObstacles.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                HudObstacleSources.Clear();
#endif
                foreach (var card in Cards())
                {
                    if (card == null) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var renderer = card.PortraitRenderer;
                    _obstacleSource = $"unit={card.BoundUnit?.ID}; card={card.name}; renderer={renderer?.name}/{renderer?.GetInstanceID()}; sprite={renderer?.sprite?.name}";
#endif
                    AddObstacle(card.HudScreenRect(Camera.main));
                    AddObstacle(card.StageHudOrbScreenRect(Camera.main));
                    AddObstacle(card.ResourceBadgeScreenRect(Camera.main));
                    Rect face = card.PortraitFocusScreenRect(Camera.main);
                    if (card.StageProxy == null) face.yMin = Mathf.Lerp(face.yMin, face.yMax, .55f);
                    AddObstacle(face);
                    // 가면처럼 얼굴이 낮게 놓인 원화는 상단 비율 추정만으로 턱·광대를 피할 수 없다.
                    AddObstacle(AuthoredFaceScreenRect(card.BoundUnit));
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _obstacleSource = "UI";
#endif
                // 확장된 행동 공지는 좁은 레일 밖으로 나온다. 알림이 그 글자 위에 겹치지 않게
                // 현재 표시 면만 피하며 행동서열의 유지 시간과 배치는 건드리지 않는다.
                if (_actionQueue == null) _actionQueue = GameObject.Find("ActionQueue")?.GetComponent<RectTransform>();
                if (_actionQueue != null && _actionQueue.gameObject.activeInHierarchy)
                {
                    AddUiObstacle(_actionQueue);
                    _actionQueue.GetComponentsInChildren(false, QueueRects);
                    foreach (var rect in QueueRects)
                        if (rect.name == "InsertedAction" &&
                            (rect.GetComponent<CanvasGroup>() == null || rect.GetComponent<CanvasGroup>().alpha > .02f))
                            AddUiObstacle(rect);
                }
                if(_featureBanner==null)_featureBanner=GameObject.Find("FeatureEnemyBanner")?.GetComponent<RectTransform>();
                if(_featureBanner!=null&&_featureBanner.gameObject.activeInHierarchy)AddUiObstacle(_featureBanner);
                if(Managers.Stage.StageBattleHud.Visible)
                {
                    if(_stageDamageMeter==null)_stageDamageMeter=GameObject.Find("DamageMeter")?.GetComponent<RectTransform>();
                    if(_stageDamageMeter!=null&&_stageDamageMeter.gameObject.activeInHierarchy)AddUiObstacle(_stageDamageMeter);
                }
            }
            Rect box = new(position - halfSize, halfSize * 2);
            foreach (Rect obstacle in HudObstacles) if (box.Overlaps(obstacle)) return false;
            return DamagePopup.HasRoom(position, halfSize,ignore);
        }
        private static int _recipientFrame=-1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 실패한 검사 표본만 진단한다. 회피 판정·배치에는 관여하지 않는다.
        internal static string DescribeNoticeCollision(Rect box, DamagePopup ignore)
        {
            var details = new List<string>();
            for (int i = 0; i < HudObstacles.Count; i++)
                if (box.Overlaps(HudObstacles[i])) details.Add($"cachedObstacle[{i}]={HudObstacles[i]}; source={HudObstacleSources[i]}");
            void ScreenObstacle(string name, Rect screen)
            {
                if (screen.width <= 0 || screen.height <= 0) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.min, null, out Vector2 a);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.max, null, out Vector2 b);
                Rect local = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                if (box.Overlaps(local)) details.Add($"{name}: local={local}; screen={screen}");
            }
            foreach (var card in Cards())
            {
                if (card == null) continue;
                var renderer = card.PortraitRenderer;
                string key = $"unit={card.BoundUnit?.ID}; card={card.name}; renderer={renderer?.name}/{renderer?.GetInstanceID()}; sprite={renderer?.sprite?.name}";
                for (int i = 0; i < HudObstacles.Count; i++)
                    if (box.Overlaps(HudObstacles[i]) && HudObstacleSources[i].StartsWith(key))
                        details.Add($"current {key}: focusScreen={card.PortraitFocusScreenRect(Camera.main)}; authoredScreen={AuthoredFaceScreenRect(card.BoundUnit)}");
                ScreenObstacle(key + "/hud", card.HudScreenRect(Camera.main));
                ScreenObstacle(key + "/orb", card.StageHudOrbScreenRect(Camera.main));
                ScreenObstacle(key + "/resource", card.ResourceBadgeScreenRect(Camera.main));
                Rect face = card.PortraitFocusScreenRect(Camera.main);
                if (card.StageProxy == null) face.yMin = Mathf.Lerp(face.yMin, face.yMax, .55f);
                ScreenObstacle(key + "/focusFace", face);
                ScreenObstacle(key + "/authoredFace", AuthoredFaceScreenRect(card.BoundUnit));
            }
            void UiObstacle(RectTransform rect)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy) return;
                rect.GetWorldCorners(Corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);
                ScreenObstacle("ui=" + rect.name, Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
            UiObstacle(_actionQueue); UiObstacle(_featureBanner); UiObstacle(_stageDamageMeter);
            foreach (var rect in QueueRects)
                if (rect != null && rect.name == "InsertedAction" &&
                    (rect.GetComponent<CanvasGroup>() == null || rect.GetComponent<CanvasGroup>().alpha > .02f)) UiObstacle(rect);
            foreach (var visual in Object.FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None))
            {
                var renderer = visual.Renderer;
                if (!visual.IsSwarm || renderer == null || !renderer.enabled || renderer.color.a <= .1f) continue;
                Bounds bounds = renderer.bounds;
                Vector2 a = Camera.main.WorldToScreenPoint(bounds.min), b = Camera.main.WorldToScreenPoint(bounds.max);
                ScreenObstacle($"swarm owner={visual.Owner.ID}; renderer={renderer.name}/{renderer.GetInstanceID()}; sprite={renderer.sprite?.name}; pose={visual.Pose}",
                    Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
            details.Add(DamagePopup.DescribeRoomCollision(box, ignore));
            return string.Join("\n", details);
        }
#endif
        private static Rect AuthoredFaceScreenRect(Unit target)
        {
            Camera camera=Camera.main;
            if(target==null||camera==null)return default;
            Vector4 face=VfxSpace.PortraitFaceWorldRect(target);
            if(face.z<=face.x||face.w<=face.y)return default;
            Vector3 a=camera.WorldToScreenPoint(new Vector3(face.x,face.y,0f));
            Vector3 b=camera.WorldToScreenPoint(new Vector3(face.z,face.w,0f));
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x)-8f,Mathf.Min(a.y,b.y)-8f,
                Mathf.Max(a.x,b.x)+8f,Mathf.Max(a.y,b.y)+8f);
        }
        internal static bool OverlapsAuthoredFace(Unit target,Rect box)
        {
            Rect screen=AuthoredFaceScreenRect(target);
            if(screen.width<=0f)return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root,screen.min,null,out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root,screen.max,null,out Vector2 max);
            return box.Overlaps(Rect.MinMaxRect(min.x,min.y,max.x,max.y));
        }
        private static readonly List<(Unit unit,Rect body)> RecipientBodies=new();
        /// <summary>수신자 몸통보다 이웃 몸통에 가까운 빈 공간에는 숫자를 보내지 않는다.</summary>
        internal static bool BelongsTo(Unit target,Vector2 position)
        {
            if(_recipientFrame!=Time.frameCount)
            {
                _recipientFrame=Time.frameCount;RecipientBodies.Clear();
                foreach(var card in Cards())
                {
                    Unit unit=card?.BoundUnit;
                    if(unit==null||!unit.isActive||!unit.IsOnField)continue;
                    Rect portrait=card.PortraitFocusScreenRect(Camera.main),hud=card.HudScreenRect(Camera.main);
                    Vector2 minScreen=new(portrait.xMin,hud.yMax);
                    Vector2 maxScreen=new(portrait.xMax,Mathf.Max(hud.yMax,Mathf.Lerp(portrait.yMin,portrait.yMax,.55f)));
                    if (card.StageProxy != null)
                    {
                        Bounds lower = card.StageProxy.LowerWorldBounds;
                        minScreen = Camera.main.WorldToScreenPoint(lower.min);
                        maxScreen = Camera.main.WorldToScreenPoint(lower.max);
                    }
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Root,minScreen,null,out Vector2 min);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Root,maxScreen,null,out Vector2 max);
                    RecipientBodies.Add((unit,Rect.MinMaxRect(min.x,min.y,max.x,max.y)));
                }
            }
            float own=float.PositiveInfinity,nearest=float.PositiveInfinity;
            foreach(var recipient in RecipientBodies)
            {
                if(recipient.unit.IsEnemy!=target.IsEnemy)continue;
                Rect body=recipient.body;
                float dx=Mathf.Max(body.xMin-position.x,0f,position.x-body.xMax);
                float dy=Mathf.Max(body.yMin-position.y,0f,position.y-body.yMax);
                float distance=dx*dx+dy*dy+(body.center-position).sqrMagnitude*.001f;
                if(recipient.unit==target)own=distance;
                nearest=Mathf.Min(nearest,distance);
            }
            return float.IsPositiveInfinity(own)||own<=nearest+.01f;
        }
        private static void AddObstacle(Rect screen)
        {
            if (screen.width <= 0 || screen.height <= 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.min, null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.max, null, out Vector2 max);
            HudObstacles.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HudObstacleSources.Add(_obstacleSource + "; screen=" + screen);
#endif
        }
        private static void AddUiObstacle(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);
            AddObstacle(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        public static Vector2 Clamp(Vector2 position, Vector2 halfSize)
        {
            Rect r = Root.rect;
            // 행동 순서·피해 기록·하단 명령 리본을 피하는 전장 영역.
            position.x = Mathf.Clamp(position.x, r.xMin + 150 + halfSize.x, r.xMax - 236 - halfSize.x);
            position.y = Mathf.Clamp(position.y, r.yMin + 154 + halfSize.y, r.yMax - 108 - halfSize.y);
            return position;
        }
    }
}
