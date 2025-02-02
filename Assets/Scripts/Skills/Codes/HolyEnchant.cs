using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HolyEnchant: CodeBase
{
  public float duration;
  public HolyEnchant(Unit caster)
  {
    this.caster = caster;
    cooldown = 0;
    duration = 0;
    name = "호올리 인챈트";
    GameManager gameManager = GameManager.Instance;

    targetUnits = gameManager.currentHeroes;
    AttrModification buffEffect = new(targetUnits, new Dictionary<int, int> {{AttrMod.ATK_MUL, 20}});
    effects.Add("AtkBuff", buffEffect);
  }
}