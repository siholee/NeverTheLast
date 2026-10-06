using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>
    /// UIBuild가 만든 글자에 붙는 크기 기록. <b>설정의 글자 크기를 이미 만든 글자에도 먹이기 위해</b> 있다.
    ///
    /// 화면은 전부 지연 생성이라 한 번 만든 글자는 다시 만들어지지 않는다. 기준 크기를 여기 들고 있다가
    /// 설정이 바뀌면 전부 다시 잰다.
    ///
    /// 크기는 <b>자동 맞춤</b>으로 준다 — 최대는 기준 × 배율, 최소는 기준의 82%(하한 <see cref="UITheme.FontFloor"/>).
    /// 칸에 자리가 있으면 커지고, 좁은 칸에서는 예전 크기 근처까지만 줄어 넘치지 않는다.
    /// 글자 크기를 한 번에 올리면서 수백 개 칸을 하나씩 다시 재지 않아도 되는 이유가 이것이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIScaledText : MonoBehaviour
    {
        /// <summary>1배율일 때의 크기(UITheme의 타이포 상수).</summary>
        public float BaseSize;

        /// <summary>
        /// 자동 맞춤 하한을 따로 올릴 때의 기준 크기(1배율). 0이면 기본 규칙(기준의 82%, 하한 FontFloor)만 쓴다.
        /// 파티 창 · 상점 · 공용 보급과 선택 판만 <see cref="KeepReadable"/>로 켠다 — 다른 화면의 글자는 바꾸지 않는다.
        /// </summary>
        public float MinimumBaseSize;

        private TMP_Text _label;

        static UIScaledText()
        {
            global::Core.SettingsManager.TextScaleChanged += ApplyAll;
        }

        public static void Attach(TMP_Text label, float baseSize)
        {
            if (label == null) return;

            UIScaledText scaled = label.GetComponent<UIScaledText>();
            if (scaled == null) scaled = label.gameObject.AddComponent<UIScaledText>();

            scaled._label = label;
            scaled.BaseSize = baseSize;
            scaled.Apply();
        }

        public void Apply()
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (_label == null || BaseSize <= 0f) return;

            float max = BaseSize * UITheme.TextScale;
            float min = Mathf.Min(BaseSize, Mathf.Max(UITheme.FontFloor, BaseSize * 0.82f));
            // 명시한 화면만 하한을 올린다. 설정의 글자 크기 배율도 함께 탄다(최대를 넘지는 않는다).
            if (MinimumBaseSize > 0f) min = Mathf.Min(max, Mathf.Max(min, MinimumBaseSize * UITheme.TextScale));

            _label.enableAutoSizing = true;
            _label.fontSizeMax = max;
            _label.fontSizeMin = min;
            _label.fontSize = max;
        }

        /// <summary>
        /// <paramref name="root"/> 아래에서 기준 22(<see cref="UITheme.FontReadSmall"/>) 이상으로 만든 글자에만
        /// 가독 하한(<see cref="UITheme.FontReadFloor"/>)을 걸고 다시 잰다. 960×540에서 10.5px 밑으로 줄지 않고 말줄임 · 스크롤로 넘긴다.
        /// 관리 화면이 다시 그린 뒤 부른다. 숨은 자식도 포함한다.
        /// </summary>
        public static void KeepReadable(Transform root)
        {
            if (root == null) return;
            foreach (UIScaledText scaled in root.GetComponentsInChildren<UIScaledText>(true))
            {
                if (scaled.BaseSize < UITheme.FontReadSmall || Mathf.Approximately(scaled.MinimumBaseSize, UITheme.FontReadFloor)) continue;
                scaled.MinimumBaseSize = UITheme.FontReadFloor;
                scaled.Apply();
            }
        }

        /// <summary>
        /// 떠 있든 숨어 있든 모든 글자를 다시 잰다. 숨은 화면의 글자도 찾아야 하므로
        /// FindObjectsByType이 아니라 FindObjectsOfTypeAll을 쓴다.
        /// </summary>
        private static void ApplyAll()
        {
            foreach (UIScaledText scaled in Resources.FindObjectsOfTypeAll<UIScaledText>())
            {
                if (scaled != null && scaled.gameObject.scene.IsValid()) scaled.Apply();
            }
        }
    }
}
