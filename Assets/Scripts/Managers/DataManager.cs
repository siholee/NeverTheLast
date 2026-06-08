using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
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
        
        // Phase 4: 아이템 데이터 불러오기
        public ItemDataList FetchItemDataList()
        {
            TextAsset itemData = Resources.Load<TextAsset>("Data/80_items");
            if (itemData == null)
            {
                Debug.LogError("Data/80_items.yaml not found.");
                return new ItemDataList { items = new List<ItemData>() };
            }

            var deserializer = new DeserializerBuilder().Build();
            ItemDataList list = deserializer.Deserialize<ItemDataList>(itemData.text);

            // slotType 문자열 → EquipSlotType 파싱
            if (list?.items != null)
            {
                foreach (var item in list.items)
                {
                    item.SlotType = System.Enum.TryParse<BaseEnums.EquipSlotType>(item.slotType, true, out var parsed)
                        ? parsed : BaseEnums.EquipSlotType.MainWeapon;
                }
            }
            return list;
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
        public int  roundNumber;
        public int  stage;       // 스테이지 번호 (1~3)
        public int  encounter;   // 인카운터 번호 (1~4, 4 = 보스)
        public bool isBoss;      // 보스 인카운터 여부
        /// <summary>적 전열 소환 큐. 0-3번 슬롯에 순서대로 채움; 4번 이후는 빈 슬롯이 생길 때 소환.</summary>
        public List<int> frontlineEnemies;
        /// <summary>적 후열 소환 큐. frontlineEnemies와 동일한 방식으로 처리.</summary>
        public List<int> backlineEnemies;
    }

    [System.Serializable]
    public class UnitData
    {
        public int id;
        public string name;
        /// <summary>원소 속성 문자열 (e.g. "Geo"). 없으면 Physical로 처리.</summary>
        public string element;
        public List<int> synergies;

        // ── 새 스탯 시스템 (STR / DEX / CON / INT / LUK) ────────────────────────
        public int str;   // 근력 — 물리 공격/방어 기여
        public int dex;   // 민첩 — 속도 기여
        public int con;   // 체력 — HP/방어 기여
        [YamlDotNet.Serialization.YamlMember(Alias = "int")]
        public int intel; // 지능 — 마법 공격 기여 (YAML 키: "int")
        public int luk;   // 행운 — 치명타 기여

        public int manaBase;  // 궁극기 에너지 최대치 (기존 시스템 유지)
        public Dictionary<string, int> codes; // basic / normal / ultimate / passive
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