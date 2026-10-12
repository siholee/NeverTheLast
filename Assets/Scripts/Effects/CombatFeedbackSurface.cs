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
            foreach(var card in Object.FindObjectsByType<UnitCardView>(FindObjectsSortMode.None))
            {
                if(card==view)continue;
                Rect hud=card.HudScreenRect(camera);
                if(hud.height<=0||hud.yMin<portrait.yMax||hud.yMin-portrait.yMax>=132f)continue;
                if(hud.xMax>portrait.xMin-60f&&hud.xMin<portrait.xMax+60f)return true;
            }
            return false;
        }

        private static int _obstacleFrame = -1;
        private static readonly List<Rect> HudObstacles = new();
        /// <summary>정보판과 원화의 얼굴을 피할 공간이 없으면 알림을 큐에 남긴다.</summary>
        public static bool NoticeHasRoom(Vector2 position, Vector2 halfSize)
        {
            if (_obstacleFrame != Time.frameCount)
            {
                _obstacleFrame = Time.frameCount; HudObstacles.Clear();
                foreach (var card in Object.FindObjectsByType<UnitCardView>(FindObjectsSortMode.None))
                {
                    AddObstacle(card.HudScreenRect(Camera.main));
                    Rect face = card.PortraitFocusScreenRect(Camera.main);
                    face.yMin = Mathf.Lerp(face.yMin, face.yMax, .55f);
                    AddObstacle(face);
                }
            }
            Rect box = new(position - halfSize, halfSize * 2);
            foreach (Rect obstacle in HudObstacles) if (box.Overlaps(obstacle)) return false;
            return DamagePopup.HasRoom(position, halfSize);
        }
        private static void AddObstacle(Rect screen)
        {
            if (screen.width <= 0 || screen.height <= 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.min, null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen.max, null, out Vector2 max);
            HudObstacles.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
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
