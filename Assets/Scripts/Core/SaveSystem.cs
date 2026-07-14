using UnityEngine;

namespace Core
{
    /// <summary>
    /// 정적 세이브 시스템. PlayerPrefs에 JSON으로 런 데이터를 저장/불러오기.
    /// </summary>
    public static class SaveSystem
    {
        private const string KeyJson   = "NTL_RunSave";
        private const string KeyExists = "NTL_HasSave";
        private const string KeyTrainedCharacters = "NTL_TrainedCharacters";

        /// <summary>저장된 런이 있는지 확인</summary>
        public static bool HasSave()
        {
            return PlayerPrefs.GetInt(KeyExists, 0) == 1;
        }

        /// <summary>현재 런 상태를 JSON으로 PlayerPrefs에 저장</summary>
        public static void SaveRun(RunSaveData data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            PlayerPrefs.SetString(KeyJson,   json);
            PlayerPrefs.SetInt(KeyExists, 1);
            PlayerPrefs.Save();
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
            string json = PlayerPrefs.GetString(KeyJson, "{}");
            RunSaveData data = JsonUtility.FromJson<RunSaveData>(json);
            Debug.Log($"[SaveSystem] 런 불러오기 완료 (Stage {data.currentStage}, Round {data.currentRound})");
            return data;
        }

        /// <summary>저장 데이터 삭제 (게임오버 또는 런 완료 시)</summary>
        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(KeyJson);
            PlayerPrefs.SetInt(KeyExists, 0);
            PlayerPrefs.Save();
            Debug.Log("[SaveSystem] 세이브 삭제");
        }

        public static TrainedCharacterCollection LoadTrainedCharacters()
        {
            string json = PlayerPrefs.GetString(KeyTrainedCharacters, "");
            if (string.IsNullOrEmpty(json))
            {
                return new TrainedCharacterCollection();
            }

            TrainedCharacterCollection data = JsonUtility.FromJson<TrainedCharacterCollection>(json);
            return NormalizeTrainedCharacterCollection(data);
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

        public static void AddStarterUnlock(int unitId)
        {
            if (unitId <= 0) return;

            TrainedCharacterCollection data = LoadTrainedCharacters();
            data.unlockedStarterUnitIds ??= new System.Collections.Generic.List<int>();
            if (data.unlockedStarterUnitIds.Contains(unitId)) return;

            data.unlockedStarterUnitIds.Add(unitId);
            SaveTrainedCharacterCollection(data);
            Debug.Log($"[SaveSystem] 스타팅 후보 해금: {unitId}");
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

            int existingIndex = data.records.FindIndex(existing => existing != null && existing.unitId == record.unitId);
            if (existingIndex >= 0)
            {
                data.records[existingIndex] = record;
            }
            else
            {
                data.records.Add(record);
            }

            SaveTrainedCharacterCollection(data);
            Debug.Log($"[SaveSystem] 육성 완료 기록 저장: {record.unitName}({record.unitId})");
        }

        public static SupportCardSaveData GetSupportCard(int unitId)
        {
            return GetTrainedCharacterRecord(unitId)?.supportCard;
        }

        public static TrainedCharacterRecord GetTrainedCharacterRecord(int unitId)
        {
            return LoadTrainedCharacters()
                .records
                .Find(existing => existing != null && existing.unitId == unitId);
        }

        private static TrainedCharacterCollection NormalizeTrainedCharacterCollection(TrainedCharacterCollection data)
        {
            data ??= new TrainedCharacterCollection();
            data.unitIds ??= new System.Collections.Generic.List<int>();
            data.unlockedStarterUnitIds ??= new System.Collections.Generic.List<int>();
            data.records ??= new System.Collections.Generic.List<TrainedCharacterRecord>();

            foreach (TrainedCharacterRecord record in data.records)
            {
                if (record == null) continue;
                AddUnitIdIfMissing(data, record.unitId);
                record.finalPrimaryStats ??= new PrimaryStatSaveData();
                record.titleIds ??= new System.Collections.Generic.List<string>();
                record.ownedPassiveCodes ??= new System.Collections.Generic.List<LearnedPassiveSaveData>();
                record.supportCard ??= new SupportCardSaveData();
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
            PlayerPrefs.SetString(KeyTrainedCharacters, json);
            PlayerPrefs.Save();
        }
    }
}
