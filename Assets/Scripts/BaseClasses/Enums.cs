namespace BaseClasses
{
  public class BaseEnums
  {
    public enum CodeType
    {
      Passive,
      Normal,
      Ultimate,
      Effect,
    }

    public enum UnitEventType
    {
      OnSpawn, // Unit(자신)
      OnDeath, // Unit, Unit(자신, 공격자)
      OnControlStarts, // Unit, Unit(자신, 공격자)
      OnControlEnds, // Unit(자신)
      OnPassiveActivates, // Unit(자신)
      OnNormalActivates, // Unit(자신)
      OnUltimateActivates, // Unit(자신)
      OnBeforeDamageTaken, // Unit(자신), Unit(공격자)
      OnTakingDamage, // Unit(자신), TakeDamageContext(피해 정보)
      OnAfterDamageTaken, // Unit(자신), Unit(공격자)
      OnUpdate, // Unit(자신)

      OnStageStart, // Unit(자신)
      OnRoundStart, // Unit(자신)
      OnRoundEnd, // Unit(자신)
      OnStageEnd, // Unit(자신)
    }

    // 현재 진행중인 게임 상태
    public enum GameState
    {
      Preparation,
      RoundInProgress,
      RoundEnd,
      GameOver
    }

    public enum DamageTag
    {
      FlatDamage, // 피해량 계산 후 플랫 수치 추가
      DefensePenetration, // 방어력 일정량 무시
      SplitDamage, // 총 피해를 타겟 전체에게 균등 분배
    }
  }
}