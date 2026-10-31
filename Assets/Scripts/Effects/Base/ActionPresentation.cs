using Entities;
using Managers;
#if !NTL_SIMULATION
using Combat;
using Managers.Stage;
#endif

namespace Effects.Vfx
{
    /// <summary>스케줄러의 기존 예고 창에만 더하는 Stage 연출. 판정·별도 대기를 소유하지 않는다.</summary>
    public static class ActionPresentation
    {
        public const float FullPreviewSeconds = 1.15f;

        public static float ExtraPreviewSeconds(Unit unit, ActionScheduler.ActionKind kind)
        {
#if NTL_SIMULATION
            return 0f;
#else
            // SeedState는 시드 범위 밖에서는 0으로 복원된다. 비교 경로의 프레임을 보존한다.
            if (VisualModeState.Current != VisualMode.Stage || kind != ActionScheduler.ActionKind.Ultimate ||
                CombatRandom.SeedState != 0u) return 0f;
            UltimateCutIn.Play(unit);
            return FullPreviewSeconds;
#endif
        }
    }
}
