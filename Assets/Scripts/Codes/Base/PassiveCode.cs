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

    /// <summary>
    /// 이 코드를 대체하는 <b>강화 등급</b> 코드의 ID. 0이면 대체 관계가 없다.
    ///
    /// 서포터 전수로 같은 계열의 두 등급을 함께 들 수 있으므로, 보유자가 이 ID의 코드를
    /// 배웠으면 <see cref="Entities.Unit"/>이 이 코드를 아예 발동하지 않는다.
    /// 효과마다 상대 상태를 조회해 스스로 눌리던 방식을 한 줄 선언으로 바꾼 것이다.
    /// </summary>
    public int SupersededByCodeId { get; protected set; }

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
