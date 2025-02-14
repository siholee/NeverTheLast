using System.Collections.Generic;

public class InstantDamage : DamageEffect
{
  public InstantDamage(Unit caster, Unit target, List<int> tags, int amount)
  {
    this.caster = caster;
    targets = new List<Unit> { target };
    this.tags = tags;
    this.amount = amount;
  }

  public InstantDamage(Unit caster, List<Unit> targets, List<int> tags, int amount)
  {
    this.caster = caster;
    this.targets = targets;
    this.tags = tags;
    this.amount = amount;
  }

  public override void ApplyEffect()
  {
    foreach (Unit target in targets)
    {
      target.TakeDamage(amount, 0, tags, caster);
    }
  }

  public override void RemoveEffect()
  {
  }
}