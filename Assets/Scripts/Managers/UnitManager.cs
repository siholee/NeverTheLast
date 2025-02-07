using System;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
  public static UnitManager Instance { get; private set; }

  // 이벤트 델리게이트 정의
  public event Action<Unit> PassiveSkillActivated;
  public event Action<Unit> NormalSkillActivated;
  public event Action<Unit> UltimateSkillActivated;
  public event Action<Unit, float> DamageApplied;
  public event Action<Unit, float> HealApplied;
  public event Action<Unit, int> HpChanged;
  public event Action<Unit, StatusEffect> StatusEffectApplied;

  private void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      // 씬 전환 시에도 유지하고 싶으면 아래 코드 활성화
      // DontDestroyOnLoad(gameObject);
    }
    else
    {
      Destroy(gameObject);
    }
  }

  // 스킬 이벤트 호출 함수들
  public void TriggerPassiveSkill(Unit unit)
  {
    PassiveSkillActivated?.Invoke(unit);
  }

  public void TriggerNormalSkill(Unit unit)
  {
    NormalSkillActivated?.Invoke(unit);
  }

  public void TriggerUltimateSkill(Unit unit)
  {
    UltimateSkillActivated?.Invoke(unit);
  }

  // 데미지 및 회복 이벤트 호출 함수들
  public void ApplyDamage(Unit unit, float damage)
  {
    // 데미지 이벤트 발행
    DamageApplied?.Invoke(unit, damage);
    // 체력 수정 및 체력 변화 이벤트 발행
    unit.currentHp -= (int)damage;
    if (unit.currentHp < 0)
      unit.currentHp = 0;
    HpChanged?.Invoke(unit, unit.currentHp);
  }

  public void ApplyHealing(Unit unit, float heal)
  {
    // 회복 이벤트 발행
    HealApplied?.Invoke(unit, heal);
    unit.currentHp += (int)heal;
    if (unit.currentHp > unit.maxHp)
      unit.currentHp = unit.maxHp;
    HpChanged?.Invoke(unit, unit.currentHp);
  }

  // 상태 효과 부여 이벤트 호출 함수
  public void ApplyStatusEffect(Unit unit, StatusEffect statusEffect)
  {
    StatusEffectApplied?.Invoke(unit, statusEffect);
  }
}

// 간단한 상태 효과 클래스 예제
[Serializable]
public class StatusEffect
{
  public string effectName;
  public float duration;
  public float magnitude;

  public StatusEffect(string effectName, float duration, float magnitude)
  {
    this.effectName = effectName;
    this.duration = duration;
    this.magnitude = magnitude;
  }
}