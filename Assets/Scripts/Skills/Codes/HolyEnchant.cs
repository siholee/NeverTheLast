using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HolyEnchant : CodeBase
{
    public HolyEnchant(CodeCreationContext context)
    {
        codeType = CodeType.Passive;
        caster = context.caster;
        cooldown = 86400f;
        duration = 0;
        codeName = "호올리 인챈트";
        effects = new Dictionary<string, EffectBase>();
    }

    public override IEnumerator StartCode()
    {
        targetUnits = GridManager.Instance.TargetAllAllies(caster);
        AttrModification buffEffect = new(targetUnits, new Dictionary<int, int> { { AttrMod.ATK_MUL, 20 } });
        effects.Add("AtkBuff", buffEffect);

        effects["AtkBuff"].ApplyEffect();
        yield return null;
    }

    public override IEnumerator StopCode()
    {
        foreach (var effect in effects.Values)
        {
            effect.RemoveEffect();
        }
        effects.Clear();
        yield return null;
    }

    public override bool CanCast()
    {
        return true;
    }
}