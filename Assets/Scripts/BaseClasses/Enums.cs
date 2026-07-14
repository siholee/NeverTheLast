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

    public enum CodeActivationType
    {
      Active,
      Passive,
      Ultimate,
    }

    public enum UltimateResourceType
    {
      Mana,
      Stack,
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
      OnNormalAttackHit, // Unit(자신), Unit(대상), DamageContext(일반공격 적중 정보)
      OnBeneficialEffectReceived, // Unit(자신), Unit(부여자)
      OnBeneficialEffectGranted, // Unit(자신), Unit(대상)
      OnBeforeDamageTaken, // Unit(자신), Unit(공격자)
      OnTakingDamage, // Unit(자신), TakeDamageContext(피해 정보)
      OnAfterDamageTaken, // Unit(자신), Unit(공격자)
      OnDamageDealt, // DamageResolvedContext(공격자, 대상, 실제 피해량)
      OnKill, // Unit(자신), Unit(처치 대상)
      OnUpdate, // Unit(자신)

      OnStageStart, // Unit(자신)
      OnRoundStart, // Unit(자신)
      OnRoundEnd, // Unit(자신)
      OnStageEnd, // Unit(자신)
    }

    // 현재 진행중인 게임 상태
    public enum GameState
    {
      CharacterSelection,
      Preparation,
      RoundInProgress,
      RoundEnd,
      RewardSelection,
      EventStage,
      TrainingPhase,
      RunComplete,
      GameOver
    }

    public enum GameMode
    {
      Training,
      Infinite,
    }

    public enum PrimaryStat
    {
      STR,
      DEX,
      CON,
      INT,
      LUK,
    }

    public enum UnitElement
    {
      None,
      Pyro,
      Hydro,
      Dendro,
      Anemo,
      Electro,
      Cryo,
      Geo,
      Void,
    }

    // 효과 분류
    public enum EffectCategory
    {
      Positive,   // 긍정적 효과 (버프)
      Negative,   // 부정적 효과 (디버프)
      Neutral     // 중립적 효과
    }

    // 상태 분류
    public enum StatusCategory
    {
      Positive,   // 긍정적 상태 (버프)
      Negative,   // 부정적 상태 (디버프)
      Neutral     // 중립적 상태
    }
    
    // 상태 중첩 정책
    public enum StatusStackPolicy
    {
      Stack,              // 중첩 허용 (맹독 - 독립적으로 작동)
      ExtendDuration,     // 지속시간 연장 (화상 - 기존 효과에 시간 추가)
      ReplaceIfStronger,  // 더 강한 것으로 교체 (효과가 큰 것만 유지)
      Ignore              // 중복 무시 (기존 효과 유지, 새 효과 무시)
    }
  }
}
