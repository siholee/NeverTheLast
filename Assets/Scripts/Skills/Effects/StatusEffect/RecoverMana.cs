public class RecoverMana : StatusEffect
{
  public Unit target;
  public int amount;

  public RecoverMana(Unit target, int amount)
  {
    this.target = target;
    this.amount = amount;
    persistantType = PersistantType.Instant;
  }

  public override void ApplyEffect()
  {
    target.RecoverMana(amount);
  }

  public override void RemoveEffect()
  {

  }
}