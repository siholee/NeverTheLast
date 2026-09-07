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

    /// <summary>
    /// 코드 등급. 같은 효과 계열에서 강화 등급은 일반 등급을 대체한다.
    /// 둘을 함께 들면 강화 쪽만 적용되고 일반 쪽은 발동하지 않는다.
    /// </summary>
    public enum CodeGrade
    {
      /// <summary>일반 — 은색.</summary>
      Normal,

      /// <summary>강화 — 금색. 같은 계열의 일반 등급을 대체한다.</summary>
      Enhanced,

      /// <summary>
      /// 고유 — 보라색. 캐릭터·병종에 묶인 P 슬롯 코드다.
      /// <b>은·금 사다리 밖에 있다</b> — 상위 코드로 대체되지도, 다른 코드를 대체하지도 않는다.
      /// </summary>
      Unique,
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
      OnNormalActionResolved, // Unit(자신) — 일반행동이 실제로 끝났을 때. 공격하지 않는 일반행동도 포함한다
      OnBeneficialEffectReceived, // Unit(자신), Unit(부여자)
      OnBeneficialEffectGranted, // Unit(자신), Unit(대상)
      OnBeforeDamageTaken, // Unit(자신), Unit(공격자)
      OnTakingDamage, // Unit(자신), TakeDamageContext(피해 정보)
      OnAfterDamageTaken, // Unit(자신), Unit(공격자), DamageContext(처리 완료된 피해 정보)
      OnDamageDealt, // DamageResolvedContext(공격자, 대상, 실제 피해량)
      OnKill, // Unit(자신), Unit(처치 대상)
      OnUpdate, // Unit(자신) — 연출용 프레임 틱. 전투 판정에는 쓰지 않는다.

      OnTurnStart, // Unit(자신) — 자기 턴이 시작될 때. 상태 지속시간과 주기 효과가 여기서 진행된다.
      OnTurnEnd,   // Unit(자신) — 자기 턴의 행동이 끝났을 때

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
      Ignore,             // 중복 무시 (기존 효과 유지, 새 효과 무시)
      Replace             // 무조건 교체 (기존 dict 덮어쓰기 의미론 — 지속시간/수치 갱신)
    }
  }
}
