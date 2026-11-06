using System;
using UnityEngine;

namespace Managers.Stage
{
    public enum VisualMode { Card, Stage }

    /// <summary>게임·개발 패널·검증 요청이 공유하는 표시 모드. 저장하지 않는다.</summary>
    public static class VisualModeState
    {
        public static VisualMode Current { get; private set; } = VisualMode.Stage;
        public static event Action<VisualMode> Changed;

        public static void Set(VisualMode mode)
        {
            if (Current == mode) return;
            Current = mode;
            Changed?.Invoke(mode);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // 도메인 리로드를 꺼도 다음 Play는 SD 무대에서 시작한다.
            Current = VisualMode.Stage;
            Changed = null;
        }
    }
}
