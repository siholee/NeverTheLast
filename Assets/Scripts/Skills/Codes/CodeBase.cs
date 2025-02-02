using System.Collections.Generic;
using UnityEngine;

public class CodeBase
{
  public Unit caster; // 시전유닛
  public List<Cell> targetCells; // 시전대상셀
  public List<Unit> targetUnits; // 시전대상유닛
  public float cooldown; // 쿨감 임마 쿨감
  public string name; // 코드 이름

  public Dictionary<string, EffectBase> effects; // 코드를 구성하는 이펙트 모음
}