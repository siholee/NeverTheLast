using UnityEngine;

namespace Core
{
    /// <summary>
    /// 정적 세이브 시스템. PlayerPrefs에 JSON으로 런 데이터를 저장/불러오기.
    /// </summary>
    public static class SaveSystem
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 디버그 세션의 저장소. <b>쓰기만 격리되고 읽기는 copy-on-write다</b> —
        // 아직 쓴 적 없는 키는 실제 PlayerPrefs가 그대로 비쳐 보인다.
        // F1 디버그로 진행 중인 런을 들여다볼 때는 그게 맞다(내 계정 상태를 봐야 하니까).
        // 반대로 "완주하면 해금되는가"처럼 <b>빈 계정에서 출발해야 뜻이 있는</b> 검증에서는
        // 실제 해금이 비쳐 들어와 거짓 통과가 된다. 그런 자리는 <see cref="SeedEmptyDebugAccount"/>로
        // 계정 키를 먼저 덮어 두어야 한다.
        private static readonly System.Collections.Generic.Dictionary<string, string> DebugStrings = new();
        private static readonly System.Collections.Generic.Dictionary<string, int> DebugInts = new();
        internal static void ResetDebugStorage() { DebugStrings.Clear(); DebugInts.Clear(); }

        /// <summary>
        /// 디버그 저장소를 <b>아무것도 없는 계정</b>으로 시드한다. 해금·완주 기록·저장된 런이
        /// 전부 빈 상태에서 시작하므로, 이 세션이 만들어 낸 것만 남는다.
        /// 실제 PlayerPrefs는 건드리지 않는다.
        /// </summary>
        public static void SeedEmptyDebugAccount()
        {
            if (!DebugMode.SessionActive)
            {
                Debug.LogWarning("[SaveSystem] 디버그 세션이 아닌데 빈 계정 시드를 요청했다 — 무시한다.");
                return;
            }
            DebugStrings[KeyTrainedCharacters] = JsonUtility.ToJson(new TrainedCharacterCollection());
            DebugStrings[KeyJson] = "";
            DebugInts[KeyExists] = 0;
            Debug.Log("[SaveSystem] 디버그 계정을 비웠다 — 해금·완주 기록 없음에서 출발한다.");
        }
#endif
        private static int ReadInt(string key, int fallback = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMode.SessionActive && DebugInts.TryGetValue(key, out int value)) return value;
#endif
            return PlayerPrefs.GetInt(key, fallback);
        }
        private static string ReadString(string key, string fallback = "")
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMode.SessionActive && DebugStrings.TryGetValue(key, out string value)) return value;
#endif
            return PlayerPrefs.GetString(key, fallback);
        }
        private static void WriteInt(string key, int value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMode.SessionActive) { DebugInts[key] = value; return; }
#endif
            PlayerPrefs.SetInt(key, value);
        }
        private static void WriteString(string key, string value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMode.SessionActive) { DebugStrings[key] = value; return; }
#endif
            PlayerPrefs.SetString(key, value);
        }
        private static void Flush()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMode.SessionActive) return;
#endif
            PlayerPrefs.Save();
        }
        private const string KeyJson   = "NTL_RunSave";
        private const string KeyExists = "NTL_HasSave";
        // 2026-09 전면 개편으로 유닛 ID의 뜻이 바뀌었다. 옛 해금 기록이 새 로스터에 비치지 않게 키를 새로 쓴다.
        private const string KeyTrainedCharacters = "NTL_UnlockedCharacters_v9";
        private const string KeyBossDefeatedPrefix = "NTL_BossDefeated_v9_";

        /// <summary>저장된 런이 있는지 확인</summary>
        public static bool HasSave()
        {
            return ReadInt(KeyExists, 0) == 1;
        }

        /// <summary>현재 런 상태를 JSON으로 PlayerPrefs에 저장</summary>
        public static void SaveRun(RunSaveData data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            WriteString(KeyJson, json);
            WriteInt(KeyExists, 1);
            Flush();
            Debug.Log($"[SaveSystem] 런 저장 완료 (Stage {data.currentStage}, Round {data.currentRound})");
        }

        /// <summary>저장된 런 데이터 불러오기. 없으면 null 반환.</summary>
        public static RunSaveData LoadRun()
        {
            if (!HasSave())
            {
                Debug.LogWarning("[SaveSystem] 저장된 런 없음");
                return null;
            }
            string json = ReadString(KeyJson, "{}");
            RunSaveData data = JsonUtility.FromJson<RunSaveData>(json);
            if (data == null || data.version < RunSaveData.CurrentVersion)
            {
                // 구버전 저장본(레거시 스탯 필드 포함)은 필드 매핑이 달라 안전하게 복원할 수 없다.
                Debug.LogWarning($"[SaveSystem] 저장 포맷 버전 불일치 (저장 {data?.version ?? 0} < 현재 {RunSaveData.CurrentVersion}) — 세이브 폐기");
                DeleteSave();
                return null;
            }
            Debug.Log($"[SaveSystem] 런 불러오기 완료 (Stage {data.currentStage}, Round {data.currentRound})");
            return data;
        }

        /// <summary>저장 데이터 삭제 (게임오버 또는 런 완료 시)</summary>
        public static void DeleteSave()
        {
            WriteString(KeyJson, "");
            WriteInt(KeyExists, 0);
            Flush();
            Debug.Log("[SaveSystem] 세이브 삭제");
        }

        /// <summary>계정 단위 캐릭터 해금 기록. 없으면 빈 기록이다.</summary>
        public static TrainedCharacterCollection LoadTrainedCharacters()
        {
            string json = ReadString(KeyTrainedCharacters, "");
            TrainedCharacterCollection data = string.IsNullOrEmpty(json)
                ? new TrainedCharacterCollection()
                : JsonUtility.FromJson<TrainedCharacterCollection>(json);
            return Normalize(data);
        }

        public static void AcknowledgeCharacterUnlock(int unitId)
        {
            var data = LoadTrainedCharacters();
            if (data.pendingCharacterUnlockIds.Remove(unitId)) SaveTrainedCharacterCollection(data);
        }

        public static bool IsStarterUnlocked(int unitId) => LoadTrainedCharacters().unlockedStarterUnitIds.Contains(unitId);

        /// <summary><c>10_units.yaml</c>에 실제로 있는 유닛 ID인가.</summary>
        private static bool UnitExists(int unitId)
        {
            var units = Managers.GameManager.Instance?.unitDataList?.units;
            if (units == null) return true;   // 데이터가 아직 없으면 판단을 미루고 통과시킨다
            return units.Exists(unit => unit != null && unit.id == unitId);
        }

        /// <summary>
        /// 스타팅 후보로 연다. <paramref name="announce"/>면 안내 대기열에도 올려
        /// 메인 메뉴가 <see cref="Managers.UI.Screens.CharacterUnlockDialog"/>로 알리게 한다.
        /// </summary>
        public static void AddStarterUnlock(int unitId, bool announce = false)
        {
            if (unitId <= 0) return;
            if (Managers.GameManager.Instance != null && !UnitExists(unitId))
            {
                Debug.LogWarning($"[SaveSystem] 해금 요청 무시 — 유닛 {unitId}는 데이터에 없다");
                return;
            }

            TrainedCharacterCollection data = LoadTrainedCharacters();
            if (data.unlockedStarterUnitIds.Contains(unitId)) return;

            data.unlockedStarterUnitIds.Add(unitId);
            if (announce && !data.pendingCharacterUnlockIds.Contains(unitId))
                data.pendingCharacterUnlockIds.Add(unitId);
            SaveTrainedCharacterCollection(data);
            Debug.Log($"[SaveSystem] 스타팅 후보 해금: {unitId}");
        }

        /// <summary>런을 넘어 유지되는 보스 최초 격파 기록.</summary>
        public static bool HasDefeatedBoss(int bossId)
        {
            return bossId > 0 && ReadInt(KeyBossDefeatedPrefix + bossId, 0) == 1;
        }

        /// <summary>최초 기록이면 true. 이미 격파한 보스라면 false.</summary>
        public static bool MarkBossDefeated(int bossId)
        {
            if (bossId <= 0 || HasDefeatedBoss(bossId)) return false;
            WriteInt(KeyBossDefeatedPrefix + bossId, 1);
            Flush();
            Debug.Log($"[SaveSystem] 보스 최초 격파 기록: {bossId}");
            return true;
        }

        private static TrainedCharacterCollection Normalize(TrainedCharacterCollection data)
        {
            data ??= new TrainedCharacterCollection();
            data.unlockedStarterUnitIds ??= new System.Collections.Generic.List<int>();
            data.pendingCharacterUnlockIds ??= new System.Collections.Generic.List<int>();
            data.unlockedStarterUnitIds.Sort();
            return data;
        }
        private static void SaveTrainedCharacterCollection(TrainedCharacterCollection data)
        {
            data = Normalize(data);
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            WriteString(KeyTrainedCharacters, json);
            Flush();
        }
    }
}
