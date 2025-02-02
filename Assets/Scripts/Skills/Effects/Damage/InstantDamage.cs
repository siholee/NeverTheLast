using System.Collections.Generic;

public class InstantDamage: DamageEffect
{  
  public InstantDamage(Unit target, List<int> tags, int amount)
  {
    targets = new List<Unit>{target};
    this.tags = tags;
    this.amount = amount;
  }

  public InstantDamage(List<Unit> targets, List<int> tags, int amount)
  {
    this.targets = targets;
    this.tags = tags;
    this.amount = amount;
  }

  public override void ApplyEffect()
  {
    foreach (Unit target in targets)
    {

    }
  }
  
  public override void RemoveEffect()
  {
  }
}