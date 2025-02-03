using System.Collections.Generic;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public Dictionary<Unit, Dictionary<CodeBase, Coroutine>> codeCasters = new Dictionary<Unit, Dictionary<CodeBase, Coroutine>>();

    /// <summary>
    /// 시전자와 대상, 실행할 스킬을 받아 스킬을 등록하고 효과를 시작함.
    /// 스킬 시전에 성공했는지 여부를 리턴턴
    /// </summary>
    public bool RegisterSkill(Unit caster, CodeBase skill)
    {
        Debug.LogWarning($"{caster.name}의 {skill.codeName} 스킬 시전 시도");
        if (caster == null || skill == null)
            return false;
        if (!skill.CanCast())
            return false;

        Coroutine skillActivation = StartCoroutine(skill.StartCode());
        if (!codeCasters.ContainsKey(caster))
        {
            codeCasters[caster] = new Dictionary<CodeBase, Coroutine>();
        }
        codeCasters[caster][skill] = skillActivation;

        return true;
    }

    /// <summary>
    /// 스킬의 효과가 종료되었을 때 호출하여 스킬을 딕셔너리에서 제거하고 효과를 중단함.
    /// </summary>
    public void DeregisterSkill(Unit caster, CodeBase skill)
    {
        if (caster == null || skill == null)
            return;

        if (codeCasters.ContainsKey(caster) && codeCasters[caster].ContainsKey(skill))
        {
            // 스킬 효과 중단
            StartCoroutine(skill.StopCode());
            StopCoroutine(codeCasters[caster][skill]);
            codeCasters[caster].Remove(skill);
            if (codeCasters[caster].Count == 0)
            {
                codeCasters.Remove(caster);
            }
        }
    }

    /// <summary>
    /// 시전자가 사망한 경우, 해당 시전자에 등록된 모든 스킬 효과를 중단하고 딕셔너리에서 제거함.
    /// </summary>
    public void OnCasterDeath(Unit caster)
    {
        if (caster == null)
            return;

        if (codeCasters.ContainsKey(caster))
        {
            foreach (var kvPair in codeCasters[caster])
            {
                StartCoroutine(kvPair.Key.StopCode());
                StopCoroutine(codeCasters[caster][kvPair.Key]);
            }
            codeCasters.Remove(caster);
        }
    }
}