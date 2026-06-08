namespace Core
{
    /// <summary>
    /// 메인메뉴 → Game 씬 전환 시 의도를 전달하는 정적 클래스.
    /// MonoBehaviour가 아니므로 씬 전환 후에도 값이 유지됨.
    /// </summary>
    public static class GameStartIntent
    {
        public enum Intent
        {
            /// <summary>에디터에서 Game 씬을 직접 시작 (기본값)</summary>
            DirectStart,
            /// <summary>메인메뉴에서 "새로운 여정" 선택</summary>
            NewGame,
            /// <summary>메인메뉴에서 "이어하기" 선택</summary>
            Continue,
        }

        public static Intent Current { get; set; } = Intent.DirectStart;
    }
}
