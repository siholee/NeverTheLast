using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>
    /// 캔버스 안에 안전 영역 전용 콘텐츠 루트를 두고 기기 safe area를 앵커로 반영한다.
    /// 전체 화면 배경까지 잘리지 않도록 자식의 자동 이동은 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UISafeArea : MonoBehaviour
    {
        private RectTransform _content;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        public RectTransform Content
        {
            get
            {
                EnsureContent();
                Apply(force: true);
                return _content;
            }
        }

        private void Awake()
        {
            EnsureContent();
            Apply(force: true);
        }

        private void OnEnable() => Apply(force: true);

        private void Update() => Apply(force: false);

        private void EnsureContent()
        {
            if (_content != null) return;

            Transform existing = transform.Find("SafeArea");
            if (existing != null) _content = existing as RectTransform;
            if (_content != null) return;

            _content = UIBuild.Container("SafeArea", transform);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
        }

        private void Apply(bool force)
        {
            if (_content == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _lastSafeArea && size == _lastScreenSize) return;

            _lastSafeArea = safe;
            _lastScreenSize = size;

            _content.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            _content.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
        }
    }
}
