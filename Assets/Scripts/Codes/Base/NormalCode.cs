using System.Collections;
using UnityEngine;

public class NormalCode : Code
{
  public int manaAmount; // 생성 마나량

  public NormalCode(NormalCodeContext context)
  {
    caster = context.caster;
  }
}