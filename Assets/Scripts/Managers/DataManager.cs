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
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
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
        //     var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        //     if (codeData == null)
        //     {
        //         Debug.LogError("Data/20_codes.yaml not found.");
        //         return null;
        //     }
        //
        //     CodeDataList codeDataList = deserializer.Deserialize<CodeDataList>(codeData.text);
        //     return codeDataList;
        // }
        
        public RoundDataList FetchRoundDataList()
        {
            TextAsset roundData = Resources.Load<TextAsset>("Data/70_rounds");
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
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
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
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
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
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
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            if (themeData == null)
            {
                Debug.LogError("Data/80_stages.yaml not found.");
                return null;
            }
            StageThemeDataList themeDataList = deserializer.Deserialize<StageThemeDataList>(themeData.text);
            return themeDataList;
        }

        public RewardDataList FetchRewardDataList()
        {
            TextAsset rewardData = Resources.Load<TextAsset>("Data/90_rewards");
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            if (rewardData == null)
            {
                Debug.LogError("Data/90_rewards.yaml not found.");
                return null;
            }

            RewardDataList rewardDataList = deserializer.Deserialize<RewardDataList>(rewardData.text);
            return rewardDataList;
        }

        public ItemDataList FetchItemDataList()
        {
            TextAsset itemData = Resources.Load<TextAsset>("Data/40_items");
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            if (itemData == null)
            {
                Debug.LogError("Data/40_items.yaml not found.");
                return null;
            }

            ItemDataList itemDataList = deserializer.Deserialize<ItemDataList>(itemData.text);
            return itemDataList;
        }
        
        public RoundTypeDataList FetchRoundTypeDataList()
        {
            TextAsset roundTypeData = Resources.Load<TextAsset>("Data/70_rounds");
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
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
        public string element;
        public List<int> startingItemIds;
        public bool canStartAsMain;
        public bool canStartAsSupport;
        public bool canUseInInfinite;
        public string mainStat;
        public string subStat;
        public int strBase;
        public int strIncrementLvl;
        public int strIncrementUpgrade;
        public int dexBase;
        public int dexIncrementLvl;
        public int dexIncrementUpgrade;
        public int conBase;
        public int conIncrementLvl;
        public int conIncrementUpgrade;
        public int intBase;
        public int intIncrementLvl;
        public int intIncrementUpgrade;
        public int lukBase;
        public int lukIncrementLvl;
        public int lukIncrementUpgrade;
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
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브: codes.passive 외에 레벨과 INT 코드 용량을 만족하면 해금되는 목록.
        public List<LevelPassiveData> levelPassives;
        public string portrait;
        public List<int> cost;
        public int costAmount;
        public int tier;
    }

    // 레벨에 따라 해금되는 추가 패시브 정의.
    // codeId: 20_codes.yaml passive 코드 ID / unlockLevel: 해금되는 유닛 레벨 / stage: 코드 단계(1 또는 3)
    [System.Serializable]
    public class LevelPassiveData
    {
        public int codeId;
        public int unlockLevel;
        public int stage = 1;
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
        public string description;
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

    [System.Serializable]
    public class ItemData
    {
        public int id;
        public string name;
        public string slot;
        public string category;
        public string requiredProficiency;
        public int rarity;
        public int weight = 1;
        public bool eventOnly;
        public bool twoHanded;
        public List<BaseClasses.EquipmentStatBonus> statBonuses;
        public List<BaseClasses.EquipmentCodeGrant> codeGrants;

        public bool IsWeapon => string.Equals(slot, "MainHand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slot, "OffHand", StringComparison.OrdinalIgnoreCase);

        public BaseClasses.EquipmentProficiency RequiredProficiency
        {
            get
            {
                if (string.IsNullOrWhiteSpace(requiredProficiency))
                {
                    return BaseClasses.EquipmentProficiency.None;
                }

                return Enum.TryParse(requiredProficiency, true, out BaseClasses.EquipmentProficiency proficiency)
                    ? proficiency
                    : BaseClasses.EquipmentProficiency.None;
            }
        }
    }

    [System.Serializable]
    public class ItemDataList
    {
        public List<ItemData> items;
    }
    
    // 새로운 적 시스템 데이터 클래스들
    [System.Serializable]
    public class EnemyData
    {
        public int id;
        public string name;
        public int themeId;     // 스테이지 테마 필터 ID
        public int archetype;   // 스폰/배치용 적 분류 ID
        public string element;
        public string tier;     // normal, elite, boss
        public int strBase;
        public int strIncrementLvl;
        public int strIncrementUpgrade;
        public int dexBase;
        public int dexIncrementLvl;
        public int dexIncrementUpgrade;
        public int conBase;
        public int conIncrementLvl;
        public int conIncrementUpgrade;
        public int intBase;
        public int intIncrementLvl;
        public int intIncrementUpgrade;
        public int lukBase;
        public int lukIncrementLvl;
        public int lukIncrementUpgrade;
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
        public string ultimateResourceType;
        public string ultimateResourceName;
        public int ultimateResourceMax;
        public Dictionary<string, int> codes;
        public Dictionary<string, int> codeStages;
        // 레벨 해금 패시브 (아군 UnitData와 동일 구조). 적도 초기 패시브 + 레벨 해금 패시브를 가질 수 있다.
        public List<LevelPassiveData> levelPassives;
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
        public int enemyThemeId;
        public string description;
        public int midBossId;
        public int bossId;
        public List<string> uniqueRewardIds;
        public List<ThemeStagePatternData> stagePatterns;
    }

    [System.Serializable]
    public class ThemeStagePatternData
    {
        public int stageInRound;
        public List<RoundPattern> patterns;
    }

    [System.Serializable]
    public class StageEventDialogueData
    {
        public string speaker;
        public string text;
    }

    [System.Serializable]
    public class StageEventChoiceData
    {
        public string id;
        public string text;
        public string action;
        public int goldCostPerStage;
        public int battleEnemyId;
        public int grantUnitId;
        public int grantItemId;
        public string successText;
        public string failureText;
    }

    [System.Serializable]
    public class StageEventData
    {
        public string id;
        public int themeId;
        public int stageInRound;
        public string title;
        public List<StageEventDialogueData> dialogue;
        public List<StageEventChoiceData> choices;
    }

    [System.Serializable]
    public class FixedBossStageData
    {
        public int stage;
        public int bossId;
    }
    
    [System.Serializable]
    public class StageThemeDataList
    {
        public List<StageThemeData> stageThemes;
        public List<FixedBossStageData> fixedBossStages;
        public List<StageEventData> events;
    }

    [System.Serializable]
    public class RewardTierOddsData
    {
        public int round;
        public List<int> tierWeights;
    }

    [System.Serializable]
    public class RewardDataList
    {
        public List<RewardDef> rewards;
        public List<RewardTierOddsData> rewardTierOdds;
    }
    
    [System.Serializable]
    public class RoundPattern
    {
        public List<int> archetypes;    // 일반 라운드용 적 분류 리스트
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
