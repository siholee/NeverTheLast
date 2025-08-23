using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaseClasses
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();
        
        [SerializeField]
        private List<TValue> values = new List<TValue>();

        // 직렬화 전에 Dictionary를 List로 변환
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        // 역직렬화 후에 List를 Dictionary로 변환
        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"SerializableDictionary keys count ({keys.Count}) does not match values count ({values.Count})");
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null)
                {
                    Debug.LogWarning($"SerializableDictionary: Null key found at index {i}, skipping.");
                    continue;
                }
                
                if (ContainsKey(key))
                {
                    Debug.LogWarning($"SerializableDictionary: Duplicate key '{key}' found at index {i}, overwriting previous value.");
                }
                
                this[key] = values[i];
            }
        }
    }

    [Serializable]
    public class IntIntDictionary : SerializableDictionary<int, int> { }
}

