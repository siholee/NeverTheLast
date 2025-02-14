// 상태의 범주를 나타내는 열거형
public enum StateCategory
{
  Positive,  // 긍정적인 상태
  Negative,  // 부정적인 상태
  Neutral    // 중립적인 상태
}

public enum StateType
{
  Status,
  DOT,
  Control,
  Etc
}

public enum PersistantType
{
  Permanent,
  Temporary,
  Instant,
}

public abstract class EffectBase
{
  public int id;
  // 상태를 부여한 유닛
  public Unit caster;
  // 상태 이름
  public string effectName;
  // 상태 범주
  public StateCategory category;
  public StateType type;
  public PersistantType persistantType = PersistantType.Temporary;
  // 상태 제거 가능 여부
  public bool canRemove;
  // 지속 시간 (0일 경우 무제한)
  public float timer;
  // 횟수 제한 (0일 경우 무제한)
  public int counter;
  public int stack;


  public abstract void ApplyEffect();
  public abstract void RemoveEffect();
}