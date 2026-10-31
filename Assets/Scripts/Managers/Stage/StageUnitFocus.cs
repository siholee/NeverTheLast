using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.Stage
{
    /// <summary>필드와 두 HUD 패널이 공유하는 개발용 Stage 호버. 판정·선택·저장과 무관하다.</summary>
    public sealed class StageUnitFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static object _source;
        private static Unit _focused;
        private Unit _unit;
        private Image _hit, _outline;
        private CanvasGroup _group;
        private bool _originalRaycast, _originalBlocks;

        public static Unit Focused => VisualModeState.Current == VisualMode.Stage && _focused != null && _focused.isActive ? _focused : null;
        public static bool IsFocused(Unit unit) => unit != null && Focused == unit;
        public Unit BoundUnit => _unit;
        public bool Highlighted => _outline != null && _outline.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _source = null; _focused = null; }
        public static void Clear() => Reset();

        public static void Set(object source, Unit unit, bool on)
        {
            if (on && VisualModeState.Current == VisualMode.Stage && unit != null)
            { _source = source; _focused = unit; }
            else if (ReferenceEquals(_source, source)) Reset();
        }

        public static StageUnitFocus Attach(Image hit, CanvasGroup group = null)
        {
            var link = hit.gameObject.AddComponent<StageUnitFocus>();
            link._hit = hit;
            link._group = group;
            link._originalRaycast = hit.raycastTarget;
            link._originalBlocks = group != null && group.blocksRaycasts;
            link._outline = UIBuild.Panel("StageFocus", hit.transform, Color.clear,
                UIShapes.Corner.None, 8, Color.white, 1);
            UIBuild.Stretch(link._outline.rectTransform);
            link._outline.raycastTarget = false;
            link._outline.enabled = false;
            return link;
        }

        public void Bind(Unit unit)
        {
            if (_unit != unit) Set(this, _unit, false);
            _unit = unit;
        }

        public void OnPointerEnter(PointerEventData data) => Set(this, _unit, true);
        public void OnPointerExit(PointerEventData data) => Set(this, _unit, false);
        private void OnDisable() => Set(this, _unit, false);

        private void LateUpdate()
        {
            bool stage = VisualModeState.Current == VisualMode.Stage;
            _hit.raycastTarget = stage || _originalRaycast;
            if (_group != null) _group.blocksRaycasts = stage || _originalBlocks;
            _outline.enabled = stage && IsFocused(_unit);
        }
    }
}
