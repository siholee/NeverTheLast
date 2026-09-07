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
    /// 코드 등급. 기본은 일반(은색)이고, 같은 계열의 일반 등급을 대체하는 코드가 강화(금색)다.
    /// 등급은 표시용 색과 <see cref="PassiveCode.SupersededByCodeId"/> 판정에 함께 쓰인다.
    /// </summary>
    public BaseEnums.CodeGrade Grade { get; protected set; } = BaseEnums.CodeGrade.Normal;

    /// <summary>
    /// 스킬 고정 위력(포켓몬식). 피해 = 위력 × 시전자 주스탯 × SkillPowerScale.
    /// 단계별로 다르면 <see cref="StagePowers"/>를 채운다. 0이면 피해를 주지 않는 스킬이다.
    /// </summary>
    public int Power { get; protected set; }

    /// <summary>단계(1~MaxStage)별 위력. 지정하면 <see cref="Power"/>보다 우선한다.</summary>
    protected int[] StagePowers;

    /// <summary>
    /// 위력의 <b>스탯 비례 성분</b> 계수. 0이면 고정 위력만 쓴다.
    ///
    ///   위력 = 고정값 + 시전자 스탯 × 계수
    ///
    /// 고정값이 저점을 보장하고, 계수를 낮게 잡으면 고점이 억제된다.
    /// 위력 자체가 스탯에 비례하므로 최종 피해는 스탯의 제곱에 가깝게 자란다 —
    /// 계수를 크게 잡으면 후반에 폭발하니 주의할 것.
    /// </summary>
    protected float PowerStatCoefficient;

    /// <summary>비례 성분이 참조할 스탯. 지정하지 않으면 시전자의 주스탯을 쓴다.</summary>
    protected BaseEnums.PrimaryStat? PowerStat;

    /// <summary>현재 단계에 해당하는 고정 위력.</summary>
    protected int FlatPower =>
      StagePowers != null && StagePowers.Length > 0
        ? StagePowers[Mathf.Clamp(CurrentStage - 1, 0, StagePowers.Length - 1)]
        : Power;

    /// <summary>현재 단계에 해당하는 위력. 비례 성분이 있으면 시전자 스탯을 읽어 더한다.</summary>
    public int CurrentPower
    {
      get
      {
        int flat = FlatPower;
        if (PowerStatCoefficient <= 0f || Caster == null) return flat;

        BaseEnums.PrimaryStat stat = PowerStat ?? Caster.MainPrimaryStat;
        return flat + Mathf.RoundToInt(Caster.GetBasePrimaryStat(stat) * PowerStatCoefficient);
      }
    }

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

    /// <summary>
    /// 캐스팅 시간만큼 기다린다. 도중에 시전자가 쓰러지거나 제어당하면 <c>false</c>를 넘긴다.
    ///
    /// 표준 흐름을 통째로 갈아 끼우는 코드(공격하지 않는 일반행동, 자체 연출을 가진 궁극기)가
    /// 대기 구간만 빌려 쓰라고 여기에 둔다. 판정이 코드마다 갈라지지 않게 하기 위해서다.
    /// </summary>
    protected IEnumerator WaitForCast(System.Action<bool> completed)
    {
      float elapsed = 0f;
      while (elapsed < CastingDelay)
      {
        if (Caster == null || !Caster.isActive || Caster.isControlled)
        {
          completed(false);
          yield break;
        }

        elapsed += Time.deltaTime;
        yield return null;
      }
      completed(true);
    }

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
  }
}
