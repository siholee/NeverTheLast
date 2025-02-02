using System.Collections.Generic;

public abstract class DamageEffect: EffectBase
{
  public List<Unit> targets;
  public List<int> tags;
  public int amount;
}