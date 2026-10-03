using BaseClasses;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    /// <summary>이번 일반행동 슬롯이 조건형 대체행동으로 바뀌는가.</summary>
    public virtual bool IsSubstituteAction => false;

    /// <summary>행동 시작 배너에 표시할 현재 행동 이름.</summary>
    public virtual string AnnouncementName => CodeName;

    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Active;
    }
  }
}
