using System.Collections.Generic;
using Entities;

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

  public class SpecialCodeContext
  {
    public Unit Caster;
  }

  public class DamageContext
  {
    public readonly Unit Attacker;
    public readonly int Damage;
    public bool IsCrit;
    public BaseEnums.CodeType CodeType;
    public List<int> DamageTags;
    /// <summary>공격 자체가 제공하는 추가 피해 배율. 효과들의 배율과 곱해진다.</summary>
    public float OutgoingDamageMultiplier = 1f;
    /// <summary>대상 방어 스탯을 얼마만큼만 인정하는가(1 = 그대로, 0 = 완전 무시).</summary>
    public float DefenseStatMultiplier = 1f;
    public bool IsCancelled;
    /// <summary>이 타격에 실제 재보 충전/소비가 귀속되었는가. 예약/반환은 포함하지 않는다.</summary>
    public bool IsTreasuryAttack;
    /// <summary>실제 소비로 강화한 직접 공격. 충전 공격·반환·예약은 제외한다.</summary>
    public bool IsTreasurySpendAttack;
    /// <summary>같은 초월기의 실제 소비 탄 번호. 광역 대상 수나 소비 자원 수와 구분한다.</summary>
    public int TreasuryShotIndex;
    public float ToughnessReduction = -1f;
    public bool ToughnessBroken { get; internal set; }
    public bool IsDamageOverTime;
    /// <summary>지속피해 연출 분류. 피해·상태 판정에는 사용하지 않는다.</summary>
    public string StatusVfxKey;
    public int HpDamage { get; internal set; }
    /// <summary>보호막과 체력에서 실제로 감소한 피해량. 피해 처리 후 이벤트가 읽는다.</summary>
    public int ResolvedDamage { get; internal set; }

    public DamageContext(Unit attacker, int damage, BaseEnums.CodeType codeType, List<int> damageTags, bool isCrit = false)
    {
      Attacker = attacker;
      Damage = damage;
      IsCrit = isCrit;
      CodeType = codeType;
      DamageTags = damageTags ?? new List<int>();
    }

    public bool HasTag(int tag) => DamageTags != null && DamageTags.Contains(tag);
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

  /// <summary>실사망과 강인도 격파의 처치 연계를 구분한다. Grantee=처치자, Grantor=대상.</summary>
  public sealed class CombatDefeatContext : EventContext
  {
    public readonly bool IsToughnessBreak;
    public CombatDefeatContext(Unit attacker, Unit target, bool isToughnessBreak) : base(attacker, target)
      => IsToughnessBreak = isToughnessBreak;
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
