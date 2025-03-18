using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Codes.Base
{
  public abstract class Code
  {
    public BaseEnums.CodeType CodeType;
    public string CodeName; // 코드 이름
    public Unit Caster; // 시전유닛
    public List<Unit> TargetUnits; // 시전대상유닛
    public float Cooldown; // 쿨감 임마 쿨감
    public float CastingDelay; // 시전시간(동안 쿨안돔)

    protected Coroutine CurrSkillCoroutine;

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
  }
}