using Entities;

namespace StatusEffects.Base
{
  public abstract class StatusEffect
  {
    public int Stack;
    public float Duration;

    /// <summary>
    /// Phase 5: 라운드 종료 시 자동 제거되지 않아야 하면 true로 오버라이드.
    /// 원소 반응 디버프(Superconduct, Frozen, Burning 등)는 true.
    /// DefaultRoundEndEvent가 이 플래그를 확인하여 필터링.
    /// </summary>
    public virtual bool PersistsAcrossRounds => false;
    
    public virtual int HpAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float HpMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual int AtkAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float AtkMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual int DefAdditiveModifier(Unit unit)
    {
      return 0;
    }
    public virtual float DefMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CritChanceAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float CritMultiplierAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float SpeedMultiplicativeModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float SpeedAdditiveModifier(Unit unit)
    {
      return 0f;
    }
    public virtual float ReceivingDamageModifier(Unit unit)
    {
      return 1f;
    }
  }
}