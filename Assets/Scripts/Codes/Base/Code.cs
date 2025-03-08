using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Code
{
  public BaseEnums.CodeType codeType;
  public string codeName; // 코드 이름
  public Unit caster; // 시전유닛
  public List<Unit> targetUnits; // 시전대상유닛
  public float cooldown; // 쿨감 임마 쿨감
  public int stack; // 스택
  public float castingDelay; // 시전시간(동안 쿨안돔)

  protected Coroutine skillCoroutine;

  public virtual void CastCode() { }
  protected virtual IEnumerator SkillCoroutine() { yield return null; }
  public virtual void StopCode() { }
  public virtual bool HasValidTarget() { return true; }
}