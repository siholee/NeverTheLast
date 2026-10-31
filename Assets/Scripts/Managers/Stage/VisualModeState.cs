using System;
using UnityEngine;

namespace Managers.Stage
{
    public enum VisualMode { Card, Stage }

    /// <summary>개발 패널과 검증 요청이 공유하는 표시 모드. 저장하지 않는다.</summary>
    public static class VisualModeState
    {
        public static VisualMode Current { get; private set; } = VisualMode.Card;
        public static event Action<VisualMode> Changed;

        public static void Set(VisualMode mode)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            mode = VisualMode.Card;
#endif
            if (Current == mode) return;
            Current = mode;
            Changed?.Invoke(mode);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // 도메인 리로드를 꺼도 다음 Play는 Card에서 시작한다.
            Current = VisualMode.Card;
            Changed = null;
        }
    }
}
