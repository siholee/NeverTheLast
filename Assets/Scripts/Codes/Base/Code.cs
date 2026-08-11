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

    /// <summary>
    /// 스킬 고정 위력(포켓몬식). 피해 = 위력 × 시전자 주스탯 × SkillPowerScale.
    /// 단계별로 다르면 <see cref="StagePowers"/>를 채운다. 0이면 피해를 주지 않는 스킬이다.
    /// </summary>
    public int Power { get; protected set; }

    /// <summary>단계(1~MaxStage)별 위력. 지정하면 <see cref="Power"/>보다 우선한다.</summary>
    protected int[] StagePowers;

    /// <summary>현재 단계에 해당하는 위력.</summary>
    public int CurrentPower =>
      StagePowers != null && StagePowers.Length > 0
        ? StagePowers[Mathf.Clamp(CurrentStage - 1, 0, StagePowers.Length - 1)]
        : Power;

    /// <summary>현재 단계 위력으로 시전자 기준 피해량을 계산한다.</summary>
    public int RollDamage(float critMultiplier = 1f)
      => Caster == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower) * critMultiplier));
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
