using BaseClasses;

namespace Codes.Base
{
  public class PassiveCode : Code
  {
    /// <summary>
    /// 유닛 정의의 고유 패시브인지 여부. 고유 패시브는 코드 용량을 차지하지 않으며
    /// 원본 그대로 전수되지 않는다. 열화 전수본은 별도의 일반 PassiveCode로 만든다.
    /// </summary>
    public bool IsUniquePassive { get; protected set; }

    /// <summary>
    /// INT가 만드는 코드 용량을 차지하지 않는 패시브인지.
    /// 고유 패시브 외에, 진영 전체에 하드코딩으로 배포되는 코드(그리스 팔랑크스)가 여기에 해당한다.
    /// </summary>
    public bool IgnoresCodeCapacity { get; protected set; }

    public PassiveCode(PassiveCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Passive;
    }
  }

  public abstract class UniquePassiveCode : PassiveCode
  {
    /// <summary>
    /// 원본 대신 서포트 카드가 전수하는 열화 패시브 ID.
    /// 0이면 명시적으로 전수 불가능한 고유 패시브다.
    /// </summary>
    public int TransferVersionCodeId { get; protected set; }

    protected UniquePassiveCode(PassiveCodeContext context) : base(context)
    {
      IsUniquePassive = true;
      Transferable = false;
      TransferVersionCodeId = 0;
    }
  }
}
