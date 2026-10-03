namespace Effects.Vfx
{
    /// <summary>캐릭터 전투 연출의 전역 비교 모드. 저장 데이터에는 기록하지 않는다.</summary>
    public enum VfxStyle
    {
        CharacterDefault,
        Legacy,
        Handpaint,
    }

    public enum VfxStyleCharacter
    {
        Shi,
        Sabah,
        Sei,
        Chandra,
        Himiko,
        Pygmalion,
    }

    /// <summary>
    /// F1과 검증 하네스가 함께 쓰는 런타임 스위치다. 캐릭터 기본값에서는
    /// 현재 로스터 여섯 명은 손그림 팩을 기본으로 쓰며 Legacy 강제로 승인된 이전 연출을 비교한다.
    /// </summary>
    public static class VfxStyleSettings
    {
        public static VfxStyle Active { get; set; } = VfxStyle.CharacterDefault;

        public static bool UseHandpaint(VfxStyleCharacter character)
        {
            if (Active == VfxStyle.Handpaint) return true;
            if (Active == VfxStyle.Legacy) return false;
            return character is VfxStyleCharacter.Shi or VfxStyleCharacter.Sabah
                or VfxStyleCharacter.Sei or VfxStyleCharacter.Chandra
                or VfxStyleCharacter.Himiko or VfxStyleCharacter.Pygmalion;
        }

        public static string DisplayName => Active switch
        {
            VfxStyle.Legacy => "Legacy",
            VfxStyle.Handpaint => "Handpaint",
            _ => "캐릭터 기본",
        };

        public static void Reset() => Active = VfxStyle.CharacterDefault;
    }
}
