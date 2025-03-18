using System.Collections.Generic;
using Managers;

namespace BaseClasses
{
    // 시너지 정보를 관리하기 위한 클래스
    [System.Serializable]
    public class SynergyInfo
    {
        public string Name { get; private set; }
        public int ID { get; private set; }
        public string Description { get; private set; }
        public int Count { get; set; }
        public int MaxCount { get; private set; }
        public List<UnitInfo> Units { get; private set; }
  
        public SynergyInfo(SynergyData data, List<UnitInfo> units)
        {
            Name = data.name;
            ID = data.id;
            Description = data.description;
            Units = units;
            Count = 0;
            MaxCount = data.maxStack;
        }
    }

    // 유닛 정보를 관리하기 위한 클래스
    [System.Serializable]
    public class UnitInfo
    {
        public string Name { get; private set; }
        public string PortraitPath { get; private set; }
  
        public UnitInfo(string name, string portraitPath)
        {
            Name = name;
            PortraitPath = portraitPath;
        }
    }
}