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
    /// <summary>스킬 원소 속성 (기본값: Physical). 원소 반응 계산에 사용됨.</summary>
    public BaseEnums.ElementType Element = BaseEnums.ElementType.Physical;

    // ── 타겟 시스템 ───────────────────────────────────────────────────────────
    /// <summary>스킬 타겟 유형. BattleManager가 시전 전에 TargetUnits를 미리 설정.</summary>
    public BaseEnums.TargetType TargetType = BaseEnums.TargetType.Single;
    /// <summary>TargetType.Range 전용: true=적 전체, false=아군 전체.</summary>
    public bool RangeTargetsEnemies = true;

    protected Coroutine CurrSkillCoroutine;

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
  }
}