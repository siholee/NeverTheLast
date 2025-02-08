using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuricMandate : CodeBase
{
    public AuricMandate(CodeCreationContext context)
    {
        codeType = CodeType.Normal;
        caster = context.caster;
        cooldown = 2f;
        duration = 0.2f;
        codeName = "성광의 권능";
        manaAmount = 10;
        effects = new Dictionary<string, EffectBase>();
    }

    public override IEnumerator StartCode()
    {
        caster.isCastingNormal = true;
        targetUnits = GridManager.Instance.TargetNearestEnemy(caster);

        // 4회 타격
        for (int i = 0; i < 4; i++)
        {
            InstantDamage instantDamage = new(targetUnits, new List<int> { DamageTag.SINGLE_TARGET }, (int)(caster.atk * 0.8f));
            effects.Add("Damage" + i, instantDamage);
            yield return new WaitForSeconds(duration);
            effects["Damage" + i].ApplyEffect();
        }

        GameManager.Instance.skillManager.DeregisterSkill(caster, this);
    }

    public override IEnumerator StopCode()
    {
        // Remove any active effects
        foreach (var effect in effects)
        {
            effect.Value.RemoveEffect();
        }
        effects.Clear();
        caster.isCastingNormal = false;
        caster.RecoverMana(manaAmount);
        yield return null;
    }

    public override bool CanCast()
    {
        return GridManager.Instance.TargetNearestEnemy(caster).Count == 1;
    }
}