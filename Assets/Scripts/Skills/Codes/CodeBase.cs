using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CodeBase
{
  public enum CodeType
  {
    None,
    Passive,
    Normal,
    Ultimate
  }
  public CodeType codeType; // 코드 타입
  public Unit caster; // 시전유닛
  public List<Unit> targetUnits; // 시전대상유닛
  public float cooldown; // 쿨감 임마 쿨감
  public float duration; // 시전시간(동안 쿨안돔)
  public string codeName; // 코드 이름
  public int manaAmount; // 생성 마나량

  public Dictionary<string, EffectBase> effects; // 코드를 구성하는 이펙트 모음

  public abstract IEnumerator StartCode();
  public abstract IEnumerator StopCode();
  public abstract bool CanCast();
}