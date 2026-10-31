namespace Combat
{
    /// <summary>화면 거리로 대상을 고를 때 양 호스트가 공유하는 열 내부의 중앙 정렬 좌표.</summary>
    public static class CombatFieldLayout
    {
        public static float DisplaySlot(int index, int count) => index - (count - 1) * 0.5f;
    }
}
