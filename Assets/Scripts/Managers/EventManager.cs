public class EventManager
{
  public delegate void OnPassiveSkillActivated(Unit caster, CodeBase skill);
  public delegate void OnNormalSkillActivated(Unit caster, CodeBase skill);
  public delegate void OnUltimateSkillActivated(Unit caster, CodeBase skill);

  public static event OnPassiveSkillActivated PassiveSkillActivatedEvent;
  public static event OnNormalSkillActivated NormalSkillActivatedEvent;
  public static event OnUltimateSkillActivated UltimateSkillActivatedEvent;

  public static void PassiveSkillActivated(Unit caster, CodeBase skill)
  {
    PassiveSkillActivatedEvent?.Invoke(caster, skill);
  }

  public static void NormalSkillActivated(Unit caster, CodeBase skill)
  {
    NormalSkillActivatedEvent?.Invoke(caster, skill);
  }

  public static void UltimateSkillActivated(Unit caster, CodeBase skill)
  {
    UltimateSkillActivatedEvent?.Invoke(caster, skill);
  }
}