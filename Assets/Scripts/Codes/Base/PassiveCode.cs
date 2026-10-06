using BaseClasses;

namespace Codes.Base
{
  public class PassiveCode : Code
  {
    private bool _isUniquePassive;

    /// <summary>
    /// 유닛 정의의 고유 특성인가. 참으로 두면 등급이 <see cref="BaseEnums.CodeGrade.Unique"/>로 바뀌고
    /// 상위 코드 지정이 지워진다 — 고유 특성은 은 · 금 사다리에 끼지 않는다.
    /// </summary>
    public bool IsUniquePassive
    {
      get => _isUniquePassive;
      protected set
      {
        _isUniquePassive = value;
        if (!value) return;
        Grade = BaseEnums.CodeGrade.Unique;
        SupersededByCodeId = 0;
      }
    }

    /// <summary>
    /// 이 코드를 대체하는 <b>강화 등급</b> 코드의 ID. 0이면 대체 관계가 없다.
    /// 보유자가 이 ID의 코드를 가졌으면 <see cref="Entities.Unit"/>이 이 코드를 발동하지 않는다.
    /// </summary>
    public int SupersededByCodeId { get; internal set; }

    /// <summary>
    /// 보유자가 얻는 영구 중첩에 곱할 배율(성장 코드). 1이면 변화 없음.
    /// 상태가 아니라 코드에 두는 이유는 영구 중첩이 전투 밖(승리 정산)에서도 쌓이기 때문이다.
    /// </summary>
    public virtual float PermanentStackGainMultiplier(string key) => 1f;

    /// <summary>
    /// 전투 안팎에서 늘 붙는 스탯 고정 보정(보상 코드의 깡 스탯 · 아누비스 영혼). 치명타 두 스탯은 비율(0.04 = +4%p).
    /// 상태로 걸면 전투 밖 캐릭터 창에 보이지 않으므로 <see cref="Entities.Unit"/>이 스탯을 모을 때 코드에 직접 묻는다.
    /// </summary>
    public virtual float PermanentStatFlatModifier(BaseEnums.StatType stat) => 0f;

    /// <summary>전투 안팎에서 늘 붙는 스탯 퍼센트 보정(0.12 = +12%). 치명타 두 스탯에는 쓰지 않는다.</summary>
    public virtual float PermanentStatPercentModifier(BaseEnums.StatType stat) => 0f;

    // ── 전투 정산(GameManager가 라운드 종료 복원 뒤에 묻는다) ──

    /// <summary>보유자 자신이 받는 EXP 가산(0.2 = +20%).</summary>
    public virtual float SelfExpBonus() => 0f;

    /// <summary>파티 전원이 받는 EXP 가산. 여러 명이 들면 가장 큰 하나만(영웅담).</summary>
    public virtual float PartyExpAuraBonus() => 0f;

    /// <summary>이번 전투 골드 가산. 여러 명이 들면 가장 큰 하나만(황금의 손).</summary>
    public virtual float PartyGoldAuraBonus() => 0f;

    /// <summary>
    /// 전투가 끝나 아군이 복원된 직후. 보유자는 복원된 유닛이고, 이 전투에서 쓰러졌으면 <c>isActive</c>가 꺼져 있다.
    /// 승리 정산 효과(축적의 서 · 목욕재계 · 죽은 자의 소생)가 여기서 일한다.
    /// </summary>
    public virtual void OnBattleSettled(bool victory, bool holderSurvived) { }

    /// <summary>이 코드가 쌓는 영구 중첩 키. 축적의 서처럼 "자신의 영구 중첩"을 올리는 효과가 찾는다. 없으면 null.</summary>
    public virtual string PermanentStackKey => null;

    public PassiveCode(PassiveCodeContext context)
    {
      Caster = context.Caster;
      CodeType = BaseEnums.CodeType.Passive;
      ActivationType = BaseEnums.CodeActivationType.Passive;
    }
  }

  /// <summary>캐릭터 고유 특성. 사건 · 보상으로 옮겨 붙지 않는다.</summary>
  public abstract class UniquePassiveCode : PassiveCode
  {
    protected UniquePassiveCode(PassiveCodeContext context) : base(context)
    {
      IsUniquePassive = true;
    }
  }
}
