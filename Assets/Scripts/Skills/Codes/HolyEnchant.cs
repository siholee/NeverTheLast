using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HolyEnchant: CodeBase
{
  private GameManager gameManager;
  public float duration;
  public HolyEnchant(CodeCreationContext context)
  {
    caster = context.caster;
    cooldown = 0;
    duration = 0;
    codeName = "호올리 인챈트";
    gameManager = GameManager.Instance;

    targetUnits = gameManager.currentHeroes;
    AttrModification buffEffect = new(targetUnits, new Dictionary<int, int> {{AttrMod.ATK_MUL, 20}});
    effects.Add("AtkBuff", buffEffect);
  }

    public override IEnumerator StartCode()
    {
        foreach (var effect in effects.Values)
        {
            effect.ApplyEffect();
        }
        yield return null;
    }

    public override IEnumerator StopCode()
    {
        foreach (var effect in effects.Values)
        {
            effect.RemoveEffect();
        }
        gameManager.skillManager.DeregisterSkill(caster, this);
        yield return null;
    }
}