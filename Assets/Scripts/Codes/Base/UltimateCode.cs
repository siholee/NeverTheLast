using BaseClasses;

namespace Codes.Base
{
  public abstract class UltimateCode : Code
  {
    /// <summary>
    /// 마나가 가득 찼을 때 행동 스케줄러가 자동으로 시전할지 여부.
    /// 패시브가 직접 굴리는 상시형 궁극기만 false를 쓴다.
    /// </summary>
    public virtual bool IsAutoCast => true;

    /// <summary>첫 유효 발사까지 마나와 초월기 발동 알림을 보류한다.</summary>
    public virtual bool DefersActivationUntilImpact => false;

    /// <summary>라운드 시작에 구독을 열고 자원을 소모하지 않는 상시형 초월기인가.</summary>
    public virtual bool IsPersistent => false;
    /// <summary>직접 공격 또는 초월기 피해 판정으로 전환하는 공격을 보유하는가(호루스의 지원 대상).</summary>
    public virtual bool SupportsUltimateDamage => AttackForm > 0;

    protected UltimateCode(UltimateCodeContext context)
    {
      Caster = context.Caster;
      CodeType = BaseEnums.CodeType.Ultimate;
      ActivationType = BaseEnums.CodeActivationType.Ultimate;
    }
  }
}
