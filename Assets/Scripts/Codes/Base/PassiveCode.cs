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
    public int SupersededByCodeId { get; protected set; }

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
