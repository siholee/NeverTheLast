using System.Collections.Generic;

public class SkillManager
{
    public Dictionary<Unit, List<CodeBase>> codeCasters = new Dictionary<Unit, List<CodeBase>>();

    /// <summary>
    /// 시전자와 대상, 실행할 스킬을 받아 스킬을 등록하고 효과를 시작함.
    /// </summary>
    public void RegisterSkill(Unit caster, CodeBase skill)
    {
        if (caster == null  || skill == null)
            return;

        if (!codeCasters.ContainsKey(caster))
        {
            codeCasters[caster] = new List<CodeBase>();
        }
        codeCasters[caster].Add(skill);

        // 스킬 효과 시작 (CodeBase가 StartEffect 메소드를 구현했다고 가정)
        skill.StartCode();
    }

    /// <summary>
    /// 스킬의 효과가 종료되었을 때 호출하여 스킬을 딕셔너리에서 제거하고 효과를 중단함.
    /// </summary>
    public void DeregisterSkill(Unit caster, CodeBase skill)
    {
        if (caster == null || skill == null)
            return;

        if (codeCasters.ContainsKey(caster))
        {
            // 스킬 효과 중단
            skill.StopCode();
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
            foreach (var skill in codeCasters[caster])
            {
                skill.StopCode();
            }
            codeCasters.Remove(caster);
        }
    }
}