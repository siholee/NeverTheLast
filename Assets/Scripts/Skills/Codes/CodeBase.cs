using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CodeBase: MonoBehaviour
{
  public Unit caster; // 시전유닛
  public List<Cell> targetCells; // 시전대상셀
  public List<Unit> targetUnits; // 시전대상유닛
  public float cooldown; // 쿨감 임마 쿨감
  public string codeName; // 코드 이름

  public Dictionary<string, EffectBase> effects; // 코드를 구성하는 이펙트 모음

  public abstract IEnumerator StartCode();
  public abstract IEnumerator StopCode();
}

public class CodeCreationContext
{
  public Unit caster;
  public List<Cell> targetCells;
  public List<Unit> targetUnits;
  public float cooldown;
  public string name;
}