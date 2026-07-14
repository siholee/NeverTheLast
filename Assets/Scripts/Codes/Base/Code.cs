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
    public BaseEnums.CodeActivationType ActivationType;
    public string CodeName; // 코드 이름
    public Unit Caster; // 시전유닛
    public List<Unit> TargetUnits; // 시전대상유닛
    public float Cooldown; // 쿨감 임마 쿨감
    public float CastingDelay; // 시전시간(동안 쿨안돔)
    public int CurrentStage { get; private set; } = 1;
    public int MaxStage { get; protected set; } = 1;
    public float ActivationChance { get; protected set; } = -1f;
    public float ActivationChanceMultiplier { get; protected set; } = 1f;
    public bool IgnoresActivationChance { get; protected set; } = false;
    public bool Transferable { get; protected set; } = true;
    public List<int> CodeTags { get; protected set; } = new();

    protected Coroutine CurrSkillCoroutine;

    public void SetStage(int stage)
    {
      CurrentStage = Mathf.Clamp(stage, 1, Mathf.Max(1, MaxStage));
    }

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
  }
}
