namespace BaseClasses
{
    /// <summary>
    /// 장비 아이템 데이터 클래스 (YAML에서 YamlDotNet으로 역직렬화).
    /// ScriptableObject를 사용하지 않고 기존 YAML 파이프라인을 따름.
    /// </summary>
    public class ItemData
    {
        public int    id;
        public string itemName;
        public string description;
        public string iconPath;      // Resources.Load<Sprite>(iconPath)용 경로
        public string slotType;      // EquipSlotType 문자열 (DataManager에서 파싱)

        // 스탯 보너스
        public int    bonusAtk;
        public int    bonusDef;
        public int    bonusHp;
        public float  bonusCritChance;
        public float  bonusSpeed;

        /// <summary>DataManager가 slotType 문자열을 파싱해서 채워주는 필드</summary>
        public BaseEnums.EquipSlotType SlotType;
    }

    [System.Serializable]
    public class ItemDataList
    {
        public System.Collections.Generic.List<ItemData> items;
    }
}
