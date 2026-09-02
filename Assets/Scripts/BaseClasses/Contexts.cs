using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace BaseClasses
{
  public class PassiveCodeContext
  {
    public Unit Caster;
    public string Name; // 데이터에 등록된 패시브 표시 이름
  }

  public class NormalCodeContext
  {
    public Unit Caster;
  }

  public class UltimateCodeContext
  {
    public Unit Caster;
  }

  public class DamageContext
  {
    private readonly HashSet<EntityId> _visualizedTargetIds = new();

    public readonly Unit Attacker;
    public readonly int Damage;
    public bool IsCrit;
    public BaseEnums.CodeType CodeType;
    public List<int> DamageTags;
    public readonly int Penetration;
    /// <summary>내구 고정 경감 중 무시할 양. 전부 무시는 DurabilityPenetration 태그를 사용한다.</summary>
    public readonly int DurabilityPenetration;
    /// <summary>이 공격을 준비하며 시전자가 실제로 소비한 최대 체력 비율.</summary>
    public float SelfHpSpentRatio;
    /// <summary>공격 자체가 제공하는 추가 피해 배율. 패시브 효과들과 곱연산된다.</summary>
    public float OutgoingDamageMultiplier = 1f;
    public float DefenseStatMultiplier;
    public bool IsCancelled;
    /// <summary>방어막과 체력에서 실제로 감소한 피해량. 피해 처리 후 이벤트가 읽는다.</summary>
    public int ResolvedDamage { get; internal set; }

    /// <summary>
    /// 같은 피해 컨텍스트가 광역 대상에 재사용되더라도 대상별 타격 VFX는 한 번씩 재생한다.
    /// 공격 코드가 피해 적용보다 먼저 연출을 시작한 경우 Unit.TakeDamage의 자동 연출과
    /// 중복되지 않게 하는 표식이기도 하다.
    /// </summary>
    public bool TryMarkImpactVfx(Unit target)
    {
      return target != null && _visualizedTargetIds.Add(target.GetEntityId());
    }

    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags,
      bool isCrit = false, int penetration = 0, int durabilityPenetration = 0)
    {
      Attacker = attacker;
      Damage = damage;
      IsCrit = isCrit;
      CodeType = codeType;
      DamageTags = damageTags;
      Penetration = penetration;
      DurabilityPenetration = System.Math.Max(0, durabilityPenetration);
      DefenseStatMultiplier = 1f;
      IsCancelled = false;
    }
  }

  public class DamageResolvedContext
  {
    public readonly Unit Attacker;
    public readonly Unit Target;
    public readonly DamageContext DamageContext;
    public readonly int DamageDealt;

    public DamageResolvedContext(Unit attacker, Unit target, DamageContext damageContext, int damageDealt)
    {
      Attacker = attacker;
      Target = target;
      DamageContext = damageContext;
      DamageDealt = damageDealt;
    }
  }

  public class EventContext
  {
    public readonly Unit Grantee;
    public readonly Unit Grantor;
    public readonly DamageContext DmgCtx;
    public readonly float FloatParam;
    
    public EventContext(Unit grantee, Unit grantor = null, DamageContext dmgCtx = null, float floatParam = 0f)
    {
      Grantee = grantee;
      Grantor = grantor;
      DmgCtx = dmgCtx;
      FloatParam = floatParam;
    }
  }

  public class ControlContext
  {
    public Unit Attacker;

    /// <summary>행동 불가 <b>턴</b> 수. 전투가 턴제이므로 초가 아니다.</summary>
    public readonly int Duration;

    public ControlContext(Unit attacker, int durationTurns)
    {
      Attacker = attacker;
      Duration = durationTurns;
    }
  }
}
