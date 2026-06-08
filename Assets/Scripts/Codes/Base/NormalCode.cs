using BaseClasses;

namespace Codes.Base
{
  public class NormalCode : Code
  {
    /// <summary>스킬 사용 시 회복되는 마나(궁극기 에너지) 량 (기존 시스템, 유지)</summary>
    public int ManaAmount;
    /// <summary>SP 비용: Basic = 0 (SP 생성), Skill = 1~3 (SP 소모)</summary>
    public int SpCost = 0;
    /// <summary>스킬 분류: Basic(SP 생성) / Skill(SP 소모) / Ultimate(마나 소모)</summary>
    public BaseEnums.SkillCategory SkillCategory = BaseEnums.SkillCategory.Basic;

    public NormalCode(NormalCodeContext context)
    {
      Caster = context.Caster;
    }
  }
}