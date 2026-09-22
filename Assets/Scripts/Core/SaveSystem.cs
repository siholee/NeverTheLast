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
        private const string KeyTrainedCharacters = "NTL_TrainedCharacters";
        private const string KeyBossDefeatedPrefix = "NTL_BossDefeated_";

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

        public static TrainedCharacterCollection LoadTrainedCharacters()
        {
            string json = ReadString(KeyTrainedCharacters, "");
            if (string.IsNullOrEmpty(json))
            {
                return new TrainedCharacterCollection();
            }

            TrainedCharacterCollection data = JsonUtility.FromJson<TrainedCharacterCollection>(json);
            bool hadLavoisier = data?.unlockedStarterUnitIds?.Contains(Entities.LavoisierChemistry.UnitId) == true;
            data = NormalizeTrainedCharacterCollection(data);
            // 이전 버전의 완주 기록에도 최초 클리어 해금을 소급 적용한다.
            if (!hadLavoisier && data.unlockedStarterUnitIds.Contains(Entities.LavoisierChemistry.UnitId))
                SaveTrainedCharacterCollection(data);
            return data;
        }

        public static void AcknowledgeCharacterUnlock(int unitId)
        {
            var data = LoadTrainedCharacters();
            if (data.pendingCharacterUnlockIds.Remove(unitId)) SaveTrainedCharacterCollection(data);
        }

        public static bool IsCharacterTrained(int unitId)
        {
            TrainedCharacterCollection data = LoadTrainedCharacters();
            return data.unitIds.Contains(unitId) || data.records.Exists(record => record != null && record.unitId == unitId);
        }

        public static bool IsStarterUnlocked(int unitId)
        {
            TrainedCharacterCollection data = LoadTrainedCharacters();
            return data.unlockedStarterUnitIds.Contains(unitId);
        }

        /// <summary>아군이 전투에서 소환한 누적 횟수를 기록하고 조건을 만족한 스타팅 후보를 연다.</summary>
        public static void RecordCombatSummon()
        {
            TrainedCharacterCollection data = LoadTrainedCharacters();
            data.combatSummonCount = System.Math.Max(0, data.combatSummonCount) + 1;

            var units = Managers.GameManager.Instance?.unitDataList?.units;
            if (units != null)
            {
                foreach (var unit in units)
                {
                    if (unit == null || unit.unlockAfterSummonCount <= 0 ||
                        data.combatSummonCount < unit.unlockAfterSummonCount ||
                        data.unlockedStarterUnitIds.Contains(unit.id)) continue;
                    data.unlockedStarterUnitIds.Add(unit.id);
                    if (!data.pendingCharacterUnlockIds.Contains(unit.id))
                        data.pendingCharacterUnlockIds.Add(unit.id);
                    Debug.Log($"[SaveSystem] 소환 {data.combatSummonCount}회 달성 — {unit.name} 스타팅 해금");
                }
            }

            SaveTrainedCharacterCollection(data);
        }

        public static int GetCombatSummonCount() => LoadTrainedCharacters().combatSummonCount;

        /// <summary><c>10_units.yaml</c>에 실제로 있는 유닛 ID인가.</summary>
        private static bool UnitExists(int unitId)
        {
            var units = Managers.GameManager.Instance?.unitDataList?.units;
            if (units == null) return true;   // 데이터가 아직 없으면 판단을 미루고 통과시킨다
            return units.Exists(unit => unit != null && unit.id == unitId);
        }

        /// <summary>
        /// 스타팅 후보로 연다. <paramref name="announce"/>면 안내 대기열에도 올려
        /// 메인 메뉴가 <see cref="Managers.UI.Screens.CharacterUnlockDialog"/>로 알리게 한다 —
        /// 해금을 모른 채 지나가면 열린 것이나 마찬가지가 아니기 때문이다.
        /// </summary>
        public static void AddStarterUnlock(int unitId, bool announce = false)
        {
            if (unitId <= 0) return;
            // 유닛 데이터에 없는 ID는 받지 않는다. 예전에 스폰 훅이 소환체·복원 유닛까지
            // 무차별로 넘겨 존재하지 않는 ID가 해금 목록에 쌓인 적이 있다.
            if (Managers.GameManager.Instance != null && !UnitExists(unitId))
            {
                Debug.LogWarning($"[SaveSystem] 해금 요청 무시 — 유닛 {unitId}는 데이터에 없다");
                return;
            }

            TrainedCharacterCollection data = LoadTrainedCharacters();
            data.unlockedStarterUnitIds ??= new System.Collections.Generic.List<int>();
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

        public static void AddTrainedCharacter(int unitId)
        {
            if (unitId <= 0) return;

            TrainedCharacterCollection data = LoadTrainedCharacters();
            AddUnitIdIfMissing(data, unitId);

            SaveTrainedCharacterCollection(data);
            Debug.Log($"[SaveSystem] 육성 완료 캐릭터 저장: {unitId}");
        }

        public static void AddTrainedCharacterRecord(TrainedCharacterRecord record)
        {
            if (record == null || record.unitId <= 0) return;

            TrainedCharacterCollection data = LoadTrainedCharacters();
            AddUnitIdIfMissing(data, record.unitId);

            record.supportCard ??= new SupportCardSaveData();
            record.supportCard.sourceUnitId = record.unitId;
            if (string.IsNullOrWhiteSpace(record.supportCard.sourceUnitName))
                record.supportCard.sourceUnitName = record.unitName;

            // 같은 캐릭터도 완주할 때마다 별개의 육성 카드로 남긴다.
            // supportId는 편성·열람실에서 카드 한 장을 가리키는 영구 키이므로 충돌하면 새 접미사를 붙인다.
            string baseId = string.IsNullOrWhiteSpace(record.supportCard.supportId)
                ? $"{record.unitId}_{record.createdAtUnixSeconds}"
                : record.supportCard.supportId;
            string supportId = baseId;
            int suffix = 2;
            while (data.records.Exists(existing => existing?.supportCard?.supportId == supportId))
                supportId = $"{baseId}_{suffix++}";
            record.supportCard.supportId = supportId;
            data.records.Add(record);

            SaveTrainedCharacterCollection(data);
            Debug.Log($"[SaveSystem] 육성 완료 기록 저장: {record.unitName}({record.unitId}) / {supportId}");
        }

        public static SupportCardSaveData GetSupportCard(int unitId)
        {
            return GetTrainedCharacterRecord(unitId)?.supportCard;
        }

        public static SupportCardSaveData GetSupportCard(int unitId, string supportId)
        {
            return GetTrainedCharacterRecord(unitId, supportId)?.supportCard;
        }

        public static TrainedCharacterRecord GetTrainedCharacterRecord(int unitId)
        {
            return GetTrainedCharacterRecord(unitId, null);
        }

        /// <summary>
        /// 캐릭터의 특정 육성 카드 기록. ID가 없으면 가장 최근 완주 기록을 돌려
        /// 이전 저장과 카드 선택 UI가 없던 호출부도 자연스럽게 최신 카드를 쓴다.
        /// </summary>
        public static TrainedCharacterRecord GetTrainedCharacterRecord(int unitId, string supportId)
        {
            System.Collections.Generic.List<TrainedCharacterRecord> records = GetTrainedCharacterRecords(unitId);
            if (!string.IsNullOrWhiteSpace(supportId))
            {
                TrainedCharacterRecord exact = records.Find(record => record.supportCard?.supportId == supportId);
                if (exact != null) return exact;
            }
            return records.Count > 0 ? records[records.Count - 1] : null;
        }

        /// <summary>같은 캐릭터로 완주한 모든 기록. 오래된 카드부터 저장 순서대로 돌려준다.</summary>
        public static System.Collections.Generic.List<TrainedCharacterRecord> GetTrainedCharacterRecords(int unitId)
        {
            return LoadTrainedCharacters().records.FindAll(record => record != null && record.unitId == unitId);
        }

        private static TrainedCharacterCollection NormalizeTrainedCharacterCollection(TrainedCharacterCollection data)
        {
            data ??= new TrainedCharacterCollection();
            data.unitIds ??= new System.Collections.Generic.List<int>();
            data.unlockedStarterUnitIds ??= new System.Collections.Generic.List<int>();
            data.records ??= new System.Collections.Generic.List<TrainedCharacterRecord>();
            data.pendingCharacterUnlockIds ??= new System.Collections.Generic.List<int>();
            data.combatSummonCount = System.Math.Max(0, data.combatSummonCount);

            var usedSupportIds = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < data.records.Count; i++)
            {
                TrainedCharacterRecord record = data.records[i];
                if (record == null) continue;
                AddUnitIdIfMissing(data, record.unitId);
                record.finalPrimaryStats ??= new PrimaryStatSaveData();
                record.titleIds ??= new System.Collections.Generic.List<string>();
                record.ownedPassiveCodes ??= new System.Collections.Generic.List<LearnedPassiveSaveData>();
                record.supportCard ??= new SupportCardSaveData();
                record.supportCard.sourceUnitId = record.unitId;
                if (string.IsNullOrWhiteSpace(record.supportCard.sourceUnitName))
                    record.supportCard.sourceUnitName = record.unitName;

                string baseId = string.IsNullOrWhiteSpace(record.supportCard.supportId)
                    ? $"legacy_{record.unitId}_{record.createdAtUnixSeconds}_{i + 1}"
                    : record.supportCard.supportId;
                string supportId = baseId;
                int suffix = 2;
                while (!usedSupportIds.Add(supportId)) supportId = $"{baseId}_{suffix++}";
                record.supportCard.supportId = supportId;
            }

            if (data.unitIds.Exists(id => id > 0) &&
                !data.unlockedStarterUnitIds.Contains(Entities.LavoisierChemistry.UnitId))
            {
                data.unlockedStarterUnitIds.Add(Entities.LavoisierChemistry.UnitId);
                if (!data.pendingCharacterUnlockIds.Contains(Entities.LavoisierChemistry.UnitId))
                    data.pendingCharacterUnlockIds.Add(Entities.LavoisierChemistry.UnitId);
            }
            data.unitIds.Sort();
            data.unlockedStarterUnitIds.Sort();
            return data;
        }

        private static void AddUnitIdIfMissing(TrainedCharacterCollection data, int unitId)
        {
            if (data == null || unitId <= 0) return;
            data.unitIds ??= new System.Collections.Generic.List<int>();
            if (!data.unitIds.Contains(unitId))
            {
                data.unitIds.Add(unitId);
            }
        }

        private static void SaveTrainedCharacterCollection(TrainedCharacterCollection data)
        {
            data = NormalizeTrainedCharacterCollection(data);
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            WriteString(KeyTrainedCharacters, json);
            Flush();
        }
    }
}
