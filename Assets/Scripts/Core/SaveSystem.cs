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
            Debug.Log($"[SaveSystem] 런 저장 완료 (Stage {data.currentStage}, Encounter {data.currentEncounter})");
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
            Debug.Log($"[SaveSystem] 런 불러오기 완료 (Stage {data.currentStage}, Encounter {data.currentEncounter})");
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
    }
}
