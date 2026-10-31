namespace Combat
{
    /// <summary>전투 중 처치 골드는 금력처럼 같은 전투의 판정에도 영향을 준다.</summary>
    public static class CombatKillEconomy
    {
        public const int GoldPerKillPerStage = 25;
        public static int GoldForStage(int stage) => GoldPerKillPerStage * System.Math.Max(1, stage);
    }
}
