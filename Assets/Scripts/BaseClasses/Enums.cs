namespace BaseClasses
{
  public class BaseEnums
  {
    public enum CodeType
    {
      Passive,
      Normal,
      Ultimate
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

      OnStageStart, // Unit(자신)
      OnRoundStart, // Unit(자신)
      OnRoundEnd, // Unit(자신)
      OnStageEnd, // Unit(자신)
      OnTurnStart, // Unit(자신)
      OnTurnEnd, // Unit(자신)
    }

    // 턴제 전투에서 행동 유형
    public enum ActionType
    {
      None,
      Basic,      // 기본 공격: SP +1 생성, 비용 없음
      ClassSkill, // 클래스 스킬: 클래스 고유 행동 (YAML class_skill 슬롯)
      Normal,     // 스킬: SP 소모
      Ultimate    // 궁극기: 마나(에너지) 소모
    }

    // 스킬 분류 (SP 시스템)
    public enum SkillCategory
    {
      Basic,    // 기본 공격 — SP +1 생성
      Skill,    // 스킬 — SP 1~3 소모
      Ultimate  // 궁극기 — ManaCurr 소모 (기존 시스템)
    }

    // 현재 진행중인 게임 상태
    public enum GameState
    {
      CharacterSelection, // 캐릭터 선택 단계 (New Game / Continue 직후)
      Preparation,
      RoundInProgress,
      RoundEnd,
      RewardSelection,  // 전투 승리 후 보상 선택
      RunComplete,      // 모든 스테이지 클리어
      GameOver
    }

    // ── 원소 시스템 (Genshin Impact 방식) ────────────────────────────────────
    public enum ElementType
    {
      Physical,
      Pyro,
      Hydro,
      Anemo,
      Electro,
      Dendro,
      Cryo,
      Geo
    }

    public enum ReactionType
    {
      None,
      Vaporize,       // 불 + 물 (×1.5 or ×2.0)
      Melt,           // 불 + 얼음 (×1.5 or ×2.0)
      Overloaded,     // 불 + 전기 → 범위 화염 피해
      Burning,        // 불 + 초목 → 화상 DoT
      Superconduct,   // 전기 + 얼음 → 물리방어 -40% 2턴
      Frozen,         // 물 + 얼음 → 기절 1턴
      Electrocharged, // 전기 + 물 → 전기 DoT
      Bloom,          // 물 + 초목 → 폭발 코어
      Quicken,        // 전기 + 초목 → Aggravate/Spread 활성화
      Aggravate,      // Quicken 후 전기 → 추가 전기 피해
      Spread,         // Quicken 후 초목 → 추가 초목 피해
      Swirl,          // 바람 + 원소 → 원소 확산
      Crystallize,    // 바위 + 원소 → 방어막 생성
      Shatter         // 물리 + 동결 → 추가 피해
    }

    // 장비 슬롯 종류 (인벤토리 시스템)
    public enum EquipSlotType
    {
      MainWeapon,   // 주무기
      SubWeapon,    // 보조무기
      Helmet,       // 투구
      Necklace,     // 목걸이
      Ring,         // 반지
      Armor,        // 갑옷
      Shoes         // 신발
    }

    // ── 타겟 시스템 ───────────────────────────────────────────────────────────
    /// <summary>
    /// 스킬 타겟 유형.<br/>
    /// Single: 주목도(EffectiveThreat) 최고 대상. 동률 시 플레이어가 적 중 선택 (AI/아군 단일은 무작위).
    /// </summary>
    public enum TargetType
    {
      Self,    // 본인 (시전자만)
      Single,  // 단일 (주목도 최고 대상 자동 선택; 동률 시 플레이어 선택)
      Range,   // 범위 (한 진영 전체 — 적 전체 or 아군 전체)
      AoE,     // 광역 (필드 위 모든 유닛)
      Bounce   // 바운스 (무작위 단일 n회 — 플레이어 선택 불가, 스킬 내부에서 각 히트마다 자동 결정)
    }

    // ── 그리드 라인 ──────────────────────────────────────────────────────────
    /// <summary>전열/후열 구분. 전열 유닛은 주목도(EffectiveThreat) +1 보너스.</summary>
    public enum GridLine
    {
      Frontline,   // 전열 (주목도 +1)
      Backline     // 후열
    }
  }
}