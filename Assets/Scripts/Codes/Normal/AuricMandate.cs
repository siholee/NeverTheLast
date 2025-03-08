using System.Collections;
using System.Collections.Generic;
using CGT.Pooling;
using UnityEngine;

public class AuricMandate : NormalCode
{
    private HS_Poolable prefab;
    public AuricMandate(NormalCodeContext context) : base(context)
    {
        prefab = GameManager.Instance.sfxManager.projectilePrefabs["AuricMandate"];
        codeType = BaseEnums.CodeType.Normal;
        caster = context.caster;
        cooldown = 2f;
        castingDelay = 0.2f;
        codeName = "성광의 권능";
        manaAmount = 4;
    }

    public override void CastCode()
    {
        caster.isCasting = true;
        Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})이 {codeName} 시전");
        skillCoroutine = caster.StartCoroutine(SkillCoroutine());
    }

    protected override IEnumerator SkillCoroutine()
    {
        // 캐스팅
        float elapsedTime = 0f;
        while (elapsedTime < castingDelay)
        {
            if (caster.isControlled || !caster.isActive)
            {
                Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})의 {codeName} 시전이 방해됨");
                StopCode();
                yield break;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 효과 처리
        for (int i = 0; i < 4; i++)
        {
            if (caster.isControlled || !caster.isActive)
            {
                Debug.Log($"{caster.unitName}({caster.currentCell.xPos}, {caster.currentCell.yPos})의 {codeName} 시전이 방해됨");
                StopCode();
                yield break;
            }
            targetUnits = GridManager.Instance.TargetNearestEnemy(caster);
            if (targetUnits.Count == 0)
            {
                yield break;
            }
            bool isCrit = Random.value <= caster.critChanceCurr;
            float critMultiplier = isCrit ? caster.critMultiplierCurr : 1f;
            DamageContext context = new(caster, (int)(caster.atkCurr * 0.8f * critMultiplier), BaseEnums.CodeType.Normal, new List<int> { DamageTag.SINGLE_TARGET }, isCrit);
            caster.StartCoroutine(FireProjectile(targetUnits, 0.2f, context));
            yield return new WaitForSeconds(0.2f);
        }
        StopCode();
    }

    public override void StopCode()
    {
        caster.normalCooldown = cooldown;
        caster.isCasting = false;
    }

    private IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
    {
        foreach (var target in targets)
        {
            GameManager.Instance.sfxManager.FireSingleProjectile(prefab, caster, target, delay);
            yield return new WaitForSeconds(delay);
            target.TakeDamage(context);
            caster.RecoverMana(manaAmount);
        }
    }

    public override bool HasValidTarget()
    {
        return GridManager.Instance.TargetNearestEnemy(caster).Count > 0;
    }
}