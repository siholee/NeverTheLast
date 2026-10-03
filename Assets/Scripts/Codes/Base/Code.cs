using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Codes.Base
{
  /// <summary>
  /// 코드(스킬)의 뿌리. 특성 · 일반행동 · 궁극기가 모두 여기서 나온다.
  ///
  /// 2026-09 개편으로 포켓몬식 '위력 × 주스탯' 피해를 폐지했다. 피해는 이제 롤처럼
  /// <b>고정값 + 스탯 × 계수</b>로 코드마다 직접 적는다 — 예: 55 + 방어력의 50%.
  /// <see cref="StatDamage"/>가 그 한 줄을 계산한다.
  /// </summary>
  public abstract class Code
  {
    public BaseEnums.CodeType CodeType;
    public BaseEnums.CodeActivationType ActivationType;
    public string CodeName;
    /// <summary>
    /// <see cref="CodeFactory"/>가 만들 때 새긴 ID. 슬롯마다 네임스페이스가 따로라
    /// 특성 201과 궁극기 201은 다른 코드다 — 조회할 때 슬롯(타입)과 함께 본다.
    /// </summary>
    public int CatalogId { get; internal set; }
    public Unit Caster;
    public List<Unit> TargetUnits;
    /// <summary>시전 시간(연출용 초). 전투 판정은 턴으로만 흐른다.</summary>
    public float CastingDelay;

    /// <summary>
    /// 코드 등급. 기본은 일반(은색)이고, 같은 계열의 일반 등급을 대체하는 코드가 강화(금색)다.
    /// </summary>
    public BaseEnums.CodeGrade Grade { get; protected set; } = BaseEnums.CodeGrade.Normal;

    public List<int> CodeTags { get; protected set; } = new();

    protected Coroutine CurrSkillCoroutine;

    /// <summary>
    /// "고정값 + 스탯 × 계수"를 시전자 기준으로 계산한다. 치명타 · 경감 전의 값이다.
    /// 예) 땅고르기 = <c>StatDamage(55, Defense, 0.5f)</c>.
    /// </summary>
    protected int StatDamage(float flat, BaseEnums.StatType stat, float ratio)
      => Caster == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(flat + Caster.StatValue(stat) * ratio));

    /// <summary>치명타를 굴린다. 터지면 배율(1 + 치명타 피해)을, 아니면 1을 넘긴다.</summary>
    protected bool RollCritical(out float multiplier)
    {
      bool crit = Caster != null && Random.value < Caster.CritChanceCurr;
      multiplier = crit ? Caster.CritMultiplierCurr : 1f;
      return crit;
    }

    /// <summary>
    /// 캐스팅 시간만큼 기다린다. 도중에 시전자가 쓰러지거나 제어당하면 <c>false</c>를 넘긴다.
    /// 표준 흐름을 통째로 갈아 끼우는 코드가 대기 구간만 빌려 쓰라고 여기에 둔다.
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
