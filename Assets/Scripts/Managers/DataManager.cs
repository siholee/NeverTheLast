using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        
        public ElementDataList FetchElementDataList()
        {
            TextAsset elementData = Resources.Load<TextAsset>("Data/50_elements");
            var deserializer = new DeserializerBuilder().Build();
            if (elementData == null)
            {
                Debug.LogError("Data/50_elements.yaml not found.");
                return null;
            }

            // YAML 데이터를 읽기 위한 임시 클래스 정의
            var rawData = deserializer.Deserialize<RawElementDataList>(elementData.text);

            // 데이터를 변환하여 Dictionary<int, List<ElementData>> 형태로 저장
            var elementDataList = new ElementDataList
            {
                elementsByCost = rawData.Elements.ToDictionary(
                    group => group.Cost,
                    group => group.Elements.Select(e => new ElementData
                    {
                        id = e.Id,
                        name = e.Name,
                        description = e.Description,
                        cost = group.Cost
                    }).ToList()
                )
            };

            return elementDataList;
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
    public class ElementData
    {
        public int id;
        public string name;
        public string description;
        public int cost;
    }
    
    [System.Serializable]
    public class ElementDataList
    {
        public Dictionary<int, List<ElementData>> elementsByCost;
    }
    
    // YAML 데이터를 읽기 위한 임시 클래스
    public class RawElementDataList
    {
        [YamlMember(Alias = "elements")]
        public List<RawElementGroup> Elements { get; set; }
    }

    public class RawElementGroup
    {
        [YamlMember(Alias = "cost")]
        public int Cost { get; set; }

        [YamlMember(Alias = "elements")]
        public List<RawElement> Elements { get; set; }
    }

    public class RawElement
    {
        [YamlMember(Alias = "id")]
        public int Id { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }
        
        [YamlMember(Alias = "cost")]
        public int Cost { get; set; }

        [YamlMember(Alias = "description")]
        public string Description { get; set; }
    }
}