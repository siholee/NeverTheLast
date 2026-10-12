namespace BaseClasses
{
  public class BaseEnums
  {
    public enum CodeType
    {
      Passive,
      Normal,
      /// <summary>특정 트리거가 여는 캐릭터 고유 특수행동. 일반행동과 추가행동 어느 쪽에도 포함되지 않는다.</summary>
      Special,
      Ultimate,

      /// <summary>지속피해 · 원소 반응처럼 행동이 아닌 효과가 가하는 피해. 특수방어력으로 경감한다.</summary>
      Effect,
    }

    public enum CodeActivationType
    {
      Active,
      Passive,
      Special,
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
      /// <summary>전투 내내 켜져 있으며 충전·소모하지 않는 상시형 초월기.</summary>
      Passive,
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
      OnAdditionalActivates, // Unit(자신) — 예약된 추가행동이 시작될 때

      OnCoordinatedActivates, // Unit(자신) — 협동행동이 시작될 때. 추가행동 카운터에는 잡히지 않는다
      OnSpecialActivates, // Unit(자신) — 특정 트리거가 연 특수행동. 추가행동 카운터에는 잡히지 않는다
      OnTargeted, // Unit(자신=대상), Unit(공격자) — 행동의 대상으로 지정된 순간. 피해가 들어가기 전이다
      OnNormalAttackHit, // Unit(자신), Unit(대상), DamageContext(일반행동 적중 정보)
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
      RunComplete,
      GameOver
    }

    /// <summary>
    /// 전투 스탯 여덟. 2026-09 개편으로 5스탯(STR/DEX/CON/INT/LUK)을 대체했다.
    ///
    /// 롤처럼 <b>기초값 + 성장치 × (레벨 − 1)</b>로 자라고, 훈련 강화는 없다.
    /// 공식과 역할은 <c>Detail_03 §2</c>가 원본이다.
    /// </summary>
    public enum StatType
    {
      /// <summary>최대 체력.</summary>
      MaxHp,
      /// <summary>공격력. 대부분의 피해가 이 스탯을 계수로 쓴다.</summary>
      Attack,
      /// <summary>특수공격력. 효과 명중과 원소 반응에만 관여한다. 피해 계수가 아니다.</summary>
      SpecialAttack,
      /// <summary>방어력. 대부분의 피해를 경감한다. 일부 유닛은 피해 계수로도 쓴다.</summary>
      Defense,
      /// <summary>특수방어력. 지속피해 · 원소 반응 피해를 경감하고 해로운 효과를 회피한다.</summary>
      SpecialDefense,
      /// <summary>속도. 행동값 = 10000 / 속도.</summary>
      Speed,
      /// <summary>치명타 확률(0~1).</summary>
      CritRate,
      /// <summary>치명타 피해(추가 배율, 0.5 = +50%).</summary>
      CritDamage,
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
      Replace,            // 무조건 교체 (기존 dict 덮어쓰기 의미론 — 지속시간/수치 갱신)
      RefreshDuration     // 같은 종류 재부여는 남은 기간을 새 기간 이상으로 갱신
    }
  }
}
