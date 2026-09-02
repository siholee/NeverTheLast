using BaseClasses;

namespace Codes.Base
{
  /// <summary>반격 태그를 가진 행동을 제공하는 패시브. 자동 지정 우선순위 판정에 사용한다.</summary>
  public interface ICounterAttackProvider { }

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
    protected UniquePassiveCode(PassiveCodeContext context) : base(context)
    {
      // 고유 패시브는 어떤 경로로도 전수되지 않는다. 열화 전수본 제도는 폐지했다.
      IsUniquePassive = true;
      Transferable = false;
    }
  }
}
