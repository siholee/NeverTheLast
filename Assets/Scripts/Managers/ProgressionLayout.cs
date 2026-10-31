using UnityEngine;

namespace Managers
{
    /// <summary>테마의 기본8칸을 보존하고 매4번째 테마 뒤에 휴식9·고정보스10을 덧붙인다.</summary>
    public static class ProgressionLayout
    {
        public const int ThemesPerCheckpoint = 4;
        public const int BlockLength = RoundManager.EncountersPerTheme * ThemesPerCheckpoint + 2;
        public static int ThemeOf(int stage)
        {
            int index = Mathf.Max(1, stage) - 1;
            return index / BlockLength * ThemesPerCheckpoint + Mathf.Min(index % BlockLength / RoundManager.EncountersPerTheme + 1, ThemesPerCheckpoint);
        }
        public static int SlotOf(int stage)
        {
            int index = (Mathf.Max(1, stage) - 1) % BlockLength;
            return index < BlockLength - 2 ? index % RoundManager.EncountersPerTheme + 1 : index - (BlockLength - 2) + 9;
        }
        public static int SlotsInTheme(int theme) => Mathf.Max(1, theme) % ThemesPerCheckpoint == 0 ? 10 : 8;
        public static int StageOf(int theme, int slot)
        {
            theme = Mathf.Max(1, theme);
            return (theme - 1) * RoundManager.EncountersPerTheme + (theme - 1) / ThemesPerCheckpoint * 2 + Mathf.Clamp(slot, 1, SlotsInTheme(theme));
        }
        public static bool IsRest(int stage) => SlotOf(stage) == 9;
        public static bool IsFixedBoss(int stage) => SlotOf(stage) == 10;
        public static string Label(int stage) => $"{ThemeOf(stage)}-{SlotOf(stage)}";
        public static int FromLegacyEightSlot(int stage) => StageOf((Mathf.Max(1, stage) - 1) / 8 + 1, (Mathf.Max(1, stage) - 1) % 8 + 1);
    }
}
