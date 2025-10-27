using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using YamlDotNet.Serialization;

namespace Managers
{
    public class DataManager : MonoBehaviour
    {
        // 다른 데이터 불러오는것도 전부 여기에 정리할 것
        public UnitDataList FetchUnitDataList()
        {
            TextAsset unitData = Resources.Load<TextAsset>("Data/10_units");
            var deserializer = new DeserializerBuilder().Build();
            if (unitData == null)
            {
                Debug.LogError("Data/10_units.yaml not found.");
                return null;
            }

            UnitDataList unitDataList = deserializer.Deserialize<UnitDataList>(unitData.text);
            return unitDataList;
        }
        
        // public CodeDataList FetchCodeDataList()
        // {
        //     TextAsset codeData = Resources.Load<TextAsset>("Data/20_codes");
        //     var deserializer = new DeserializerBuilder().Build();
        //     if (codeData == null)
        //     {
        //         Debug.LogError("Data/20_codes.yaml not found.");
        //         return null;
        //     }
        //
        //     CodeDataList codeDataList = deserializer.Deserialize<CodeDataList>(codeData.text);
        //     return codeDataList;
        // }
        
        public SynergyDataList FetchSynergyDataList()
        {
            TextAsset synergyData = Resources.Load<TextAsset>("Data/40_synergies");
            var deserializer = new DeserializerBuilder().Build();
            if (synergyData == null)
            {
                Debug.LogError("Data/40_synergies.yaml not found.");
                return null;
            }

            SynergyDataList synergyDataList = deserializer.Deserialize<SynergyDataList>(synergyData.text);
            return synergyDataList;
        }

        public RoundDataList FetchRoundDataList()
        {
            TextAsset roundData = Resources.Load<TextAsset>("Data/70_rounds");
            var deserializer = new DeserializerBuilder().Build();
            if (roundData == null)
            {
                Debug.LogError("Data/70_rounds.yaml not found.");
                return null;
            }

            RoundDataList roundDataList = deserializer.Deserialize<RoundDataList>(roundData.text);
            return roundDataList;
        }
        
        public ResourceTokenDataList FetchTokenDataList()
        {
            TextAsset tokenData = Resources.Load<TextAsset>("Data/50_tokens");
            var deserializer = new DeserializerBuilder().Build();
            if (tokenData == null)
            {
                Debug.LogError("Data/50_tokens.yaml not found.");
                return null;
            }
            ResourceTokenDataList tokenDataList = deserializer.Deserialize<ResourceTokenDataList>(tokenData.text);
            if (tokenDataList is { tokens: not null }) return tokenDataList;
            Debug.LogError("Failed to deserialize ResourceTokenDataList.");
            return null;
        }
        
        public EnemyDataList FetchEnemyDataList()
        {
            TextAsset enemyData = Resources.Load<TextAsset>("Data/60_enemies");
            var deserializer = new DeserializerBuilder().Build();
            if (enemyData == null)
            {
                Debug.LogError("Data/60_enemies.yaml not found.");
                return null;
            }
            EnemyDataList enemyDataList = deserializer.Deserialize<EnemyDataList>(enemyData.text);
            return enemyDataList;
        }
        
        public StageThemeDataList FetchStageThemeDataList()
        {
            TextAsset themeData = Resources.Load<TextAsset>("Data/80_stages");
            var deserializer = new DeserializerBuilder().Build();
            if (themeData == null)
            {
                Debug.LogError("Data/80_stages.yaml not found.");
                return null;
            }
            StageThemeDataList themeDataList = deserializer.Deserialize<StageThemeDataList>(themeData.text);
            return themeDataList;
        }
        
        public RoundTypeDataList FetchRoundTypeDataList()
        {
            TextAsset roundTypeData = Resources.Load<TextAsset>("Data/70_rounds");
            var deserializer = new DeserializerBuilder().Build();
            if (roundTypeData == null)
            {
                Debug.LogError("Data/70_rounds.yaml not found.");
                return null;
            }
            RoundTypeDataList roundTypeDataList = deserializer.Deserialize<RoundTypeDataList>(roundTypeData.text);
            return roundTypeDataList;
        }
    }

    [System.Serializable]
    public class RoundDataList
    {
        public List<RoundData> rounds;
    }

    [System.Serializable]
    public class RoundData
    {
        public int roundNumber;
        public List<CellData> cells;
    }

    [System.Serializable]
    public class CellData
    {
        public int cellIndex;
        public List<int> enemyIds;
    }

    [System.Serializable]
    public class UnitData
    {
        public int id;
        public string name;
        public List<int> synergies;
        public int hpBase;
        public int hpIncrementLvl;         // YAML의 hpIncrementLvl 필드와 매핑
        public int hpIncrementUpgrade;     // YAML의 hpIncrementUpgrade 필드와 매핑
        public int atkBase;
        public int atkIncrementLvl;        // YAML의 atkIncrementLvl 필드와 매핑
        public int atkIncrementUpgrade;    // YAML의 atkIncrementUpgrade 필드와 매핑
        public int defBase;
        public int defIncrementLvl;        // YAML의 defIncrementLvl 필드와 매핑
        public int defIncrementUpgrade;    // YAML의 defIncrementUpgrade 필드와 매핑
        public float critChance;
        public float critChanceIncrementLvl;   // YAML의 critChanceIncrementLvl 필드와 매핑
        public float critChanceIncrementUpgrade; // YAML의 critChanceIncrementUpgrade 필드와 매핑
        public float critMultiplier;
        public float critMultiplierIncrementLvl;   // YAML의 critMultiplierIncrementLvl 필드와 매핑
        public float critMultiplierIncrementUpgrade; // YAML의 critMultiplierIncrementUpgrade 필드와 매핑
        public int manaBase;
        public Dictionary<string, int> codes;
        public string portrait;
        public List<int> cost;
        public int costAmount;
        public int tier;
    }

    [System.Serializable]
    public class UnitDataList
    {
        public List<UnitData> units;
    }

    [System.Serializable]
    public class CodeData
    {
        public int id;           // 코드 ID
        public string verbalName;      // 코드 이름
        public string codeName;
    }

    [System.Serializable]
    public class CodeDataRepository
    {
        public List<CodeData> passive; // 코드 목록
        public List<CodeData> normal;  // 코드 목록
        public List<CodeData> ultimate; // 코드 목록
    }

    public class CodeDataList
    {
        public CodeDataRepository Codes;
    }
    
    [System.Serializable]
    public class SynergyData
    {
        public int id;           // 시너지 ID
        public string name;
        public int maxStack;
        public string description;
    }
    
    [System.Serializable]
    public class SynergyDataList
    {
        public List<SynergyData> synergies;
    }

    [System.Serializable]
    public class ResourceTokenData
    {
        public int id;
        public string name;
    }
    
    [System.Serializable]
    public class ResourceTokenDataList
    {
        public List<ResourceTokenData> tokens;
    }
    
    // 새로운 적 시스템 데이터 클래스들
    [System.Serializable]
    public class EnemyData
    {
        public int id;
        public string name;
        public int faction;     // 소속 (시너지 ID)
        public int @class;      // 직업 (시너지 ID) - class는 C# 예약어이므로 @class 사용
        public string tier;     // normal, elite, boss
        public int hpBase;
        public int hpIncrementLvl;
        public int hpIncrementUpgrade;
        public int atkBase;
        public int atkIncrementLvl;
        public int atkIncrementUpgrade;
        public int defBase;
        public int defIncrementLvl;
        public int defIncrementUpgrade;
        public float critChance;
        public float critChanceIncrementLvl;
        public float critChanceIncrementUpgrade;
        public float critMultiplier;
        public float critMultiplierIncrementLvl;
        public float critMultiplierIncrementUpgrade;
        public int manaBase;
        public Dictionary<string, int> codes;
        public string portrait;
    }
    
    [System.Serializable]
    public class EnemyDataList
    {
        public List<EnemyData> enemies;
    }
    
    [System.Serializable]
    public class StageThemeData
    {
        public int id;
        public string name;
        public int faction;
        public string description;
    }
    
    [System.Serializable]
    public class StageThemeDataList
    {
        public List<StageThemeData> stageThemes;
    }
    
    [System.Serializable]
    public class RoundPattern
    {
        public List<int> classes;       // 일반 라운드용 직업 리스트
        public List<int> eliteIds;      // 엘리트 라운드용 엘리트 ID 리스트
        public int bossId;              // 보스 라운드용 보스 ID
        public int weight;              // 이 패턴이 선택될 확률 가중치
    }
    
    [System.Serializable]
    public class RoundTypeData
    {
        public int id;
        public string name;
        public bool isElite;
        public bool isBoss;
        public List<RoundPattern> patterns;  // 여러 패턴 중 랜덤 선택
    }
    
    [System.Serializable]
    public class StageData
    {
        public int stageNumber;
        public List<int> rounds;    // 각 라운드의 roundType ID
    }
    
    [System.Serializable]
    public class RoundTypeDataList
    {
        public List<RoundTypeData> roundTypes;
        public List<StageData> stages;
    }
}