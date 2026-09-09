#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 테스트용 디버그 모드의 상태 보관소.
    ///
    /// 전투가 완전 자동이라 <b>한 판을 끝까지 돌려 보는 것 말고는 확인할 방법이 없다.</b>
    /// 공허 8테마처럼 10스테이지를 지나야 보이는 것, Lv.90에 열리는 코드, 엘리트 단독 전투 같은
    /// 것들을 매번 정상 플레이로 도달하려면 시간이 너무 든다. 그래서 상태를 직접 밀어 넣는다.
    ///
    /// <b>에디터와 개발 빌드에서만 컴파일된다.</b> 출시 빌드에는 이 파일 자체가 들어가지 않으므로
    /// 호출부도 같은 <c>#if</c>로 감싸야 한다.
    /// </summary>
    public static class DebugMode
    {
        private static bool _sessionActive;
        public static bool SessionActive
        {
            get
            {
#if UNITY_EDITOR
                // Play 중 스크립트 재컴파일로 static 필드가 초기화돼도 저장 보호를 유지한다.
                return _sessionActive || UnityEditor.SessionState.GetBool("NTL.DebugSessionActive", false);
#else
                return _sessionActive;
#endif
            }
            private set
            {
                _sessionActive = value;
#if UNITY_EDITOR
                UnityEditor.SessionState.SetBool("NTL.DebugSessionActive", value);
#endif
            }
        }
        public static bool SuiteRunning;
        private static string _snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeSession()
        {
            SessionActive = false;
            SuiteRunning = false;
            PanelOpen = false;
            _snapshot = null;
            ResetAll();
            SaveSystem.ResetDebugStorage();
        }

        // 한 번 디버그로 바뀐 런은 토글을 꺼도 세션 종료까지 메모리에만 저장한다.
        public static void BeginSession()
        {
            if (SessionActive) return;
            SessionActive = true;
            var run = Managers.RunManager.Instance;
            if (run != null && Managers.GameManager.Instance?.RoundManager != null)
                _snapshot = JsonUtility.ToJson(run.CaptureDebugSnapshot());
            Debug.Log("[디버그] 테스트 세션 시작 — 런/해금/보스 기록은 디스크에 저장하지 않습니다.");
        }

        public static bool RestoreSnapshot()
        {
            if (string.IsNullOrEmpty(_snapshot) || Managers.RunManager.Instance == null) return false;
            ResetAll();
            Managers.GameManager.Instance.DebugResetBattle();
            Managers.RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(_snapshot));
            return true;
        }

        /// <summary>패널이 열려 있는가. 열려 있지 않아도 아래 토글은 계속 작동한다.</summary>
        public static bool PanelOpen;

        /// <summary>
        /// 아군이 피해를 받지 않는다. 적의 행동과 편성을 <b>죽지 않고</b> 끝까지 보기 위한 것이다.
        /// 지속피해·고정피해를 포함해 <see cref="Entities.Unit.TakeDamage"/> 입구에서 전부 막는다.
        /// </summary>
        public static bool AllyInvincible;

        /// <summary>
        /// 적이 피해를 받지 않는다. 아군 편성이 버티는지, 적 패시브가 도는지를 보려면
        /// 전투가 끝나지 않아야 한다.
        /// </summary>
        public static bool EnemyInvincible;

        /// <summary>
        /// 0이 아니면 라운드가 고르는 대신 이 테마로 고정한다.
        ///
        /// 테마는 평소 <c>(Round - 1) % 활성테마수</c>로 정해지므로, 열한 번째 테마를 보려면
        /// 101스테이지까지 가야 한다. 그때는 적 레벨도 101이라 <b>테마만 따로 볼 수가 없다.</b>
        /// </summary>
        public static int ForcedThemeId;

        /// <summary>디버그로 바꾼 것이 하나라도 있는가. HUD에 표시해 실수로 켜 둔 채 재지 않게 한다.</summary>
        public static bool AnyOverrideActive
            => AllyInvincible || EnemyInvincible || ForcedThemeId > 0;

        /// <summary>전투 배속. HUD의 배속 버튼과 같은 <c>Time.timeScale</c>을 쓴다.</summary>
        public static void SetTimeScale(float scale)
        {
            BeginSession();
            Time.timeScale = Mathf.Clamp(scale, 0.1f, 20f);
            Debug.Log($"[디버그] 배속 {Time.timeScale:0.##}배");
        }

        /// <summary>디버그로 켠 것을 모두 되돌린다.</summary>
        public static void ResetAll()
        {
            AllyInvincible = false;
            EnemyInvincible = false;
            ForcedThemeId = 0;
            Time.timeScale = 1f;
            Debug.Log("[디버그] 모든 강제 설정을 되돌렸다.");
        }
    }
}
#endif
