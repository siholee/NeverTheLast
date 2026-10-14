using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Codes.Base
{
  /// <summary>
  /// 코드(스킬)의 뿌리. 특성 · 일반행동 · 궁극기가 모두 여기서 나온다.
  ///
  /// 2026-09 개편으로 포켓몬식 '위력 × 주스탯' 피해를 폐지했다. 피해는 이제 롤처럼
  /// <b>고정값 + 스탯 × 계수</b>로 코드마다 직접 적는다 — 예: 55 + 방어력의 50%.
  /// <see cref="StatDamage"/>가 그 한 줄을 계산한다.
  /// </summary>
  public abstract class Code
  {
    public BaseEnums.CodeType CodeType;
    public BaseEnums.CodeActivationType ActivationType;
    public string CodeName;
    /// <summary>
    /// <see cref="CodeFactory"/>가 만들 때 새긴 ID. 슬롯마다 네임스페이스가 따로라
    /// 특성 201과 궁극기 201은 다른 코드다 — 조회할 때 슬롯(타입)과 함께 본다.
    /// </summary>
    public int CatalogId { get; internal set; }
    public Unit Caster;
    public List<Unit> TargetUnits;
    /// <summary>시전 시간(연출용 초). 전투 판정은 턴으로만 흐른다.</summary>
    public float CastingDelay;

    /// <summary>
    /// 코드 등급. 기본은 일반(은색)이고, 같은 계열의 일반 등급을 대체하는 코드가 강화(금색)다.
    /// </summary>
    public BaseEnums.CodeGrade Grade { get; protected set; } = BaseEnums.CodeGrade.Normal;

    public List<int> CodeTags { get; protected set; } = new();

    /// <summary>
    /// 이 코드가 가하는 공격의 형태(<see cref="DamageTag.Slash"/> 등, 50000번대). 공격하지 않으면 0.
    /// 보상 풀이 "파티에 베기 공격이 있는가"를 코드 인스턴스에서 바로 물을 수 있게 코드가 선언한다.
    /// </summary>
    public virtual int AttackForm => 0;

    protected Coroutine CurrSkillCoroutine;

    /// <summary>
    /// "고정값 + 스탯 × 계수"를 시전자 기준으로 계산한다. 치명타 · 경감 전의 값이다.
    /// 예) 땅고르기 = <c>StatDamage(55, Defense, 0.5f)</c>.
    /// </summary>
    protected int StatDamage(float flat, BaseEnums.StatType stat, float ratio)
      => Caster == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(flat + Caster.StatValue(stat) * ratio));

    /// <summary>치명타를 굴린다. 대상을 모르는 공유 판정(한 번 굴려 전원에게 쓰는 궁극기 등)용.</summary>
    protected bool RollCritical(out float multiplier) => RollCritical(null, out multiplier);

    /// <summary>
    /// 대상을 알고 치명타를 굴린다. 터지면 배율(1 + 치명타 피해)을, 아니면 1을 넘긴다.
    /// 시전자 효과가 대상 · 행동 종류에 따라 확률 · 피해를 더하거나(결전의 일격 · 화무십일홍) 확정 치명(기습)을 걸 수 있고,
    /// 100%를 넘는 확률을 치명타 피해로 바꾸는 효과(암살자)도 여기서 한 번에 처리한다.
    /// </summary>
    protected bool RollCritical(Unit target, out float multiplier) => RollCritical(target, CodeType, out multiplier);

    /// <summary>
    /// 치명 질의의 행동 종류를 따로 정해 굴린다. 행동은 추가행동이지만 피해는 궁극기로 판정받는 경우
    /// (아그니 초월기 동안의 특성 추공)가 궁극기 치명 보정을 받게 하려는 국소 오버로드다. 실제 행동 종류 · 이벤트는 바꾸지 않는다.
    /// </summary>
    protected bool RollCritical(Unit target, BaseEnums.CodeType queryType, out float multiplier)
      => RollCriticalFor(Caster, Caster, target, queryType, out multiplier);

    /// <summary>
    /// 치명 스탯 · 효과의 주인과 실제 행동자를 나눠 굴린다. 소환자 명의로 치는 소환수(펜리르)는 소환자의 확률 · 피해 ·
    /// 대상별 치명 효과를 쓰되, "지금 추가행동 중인 유닛"을 묻는 마리 보급 오라는 실제로 행동하는 소환수를 본다.
    /// 일반 코드는 둘 다 시전자다(<see cref="RollCritical(Unit, BaseEnums.CodeType, out float)"/>).
    /// </summary>
    protected static bool RollCriticalFor(Unit statOwner, Unit actor, Unit target, BaseEnums.CodeType queryType,
      out float multiplier)
    {
      multiplier = 1f;
      if (statOwner == null) return false;

      float chance = statOwner.StatValue(BaseEnums.StatType.CritRate);
      float critDamage = statOwner.StatValue(BaseEnums.StatType.CritDamage);
      critDamage += Codes.Passive.MarieSupply.AdditionalCritDamage(actor != null ? actor : statOwner);
      bool forced = false;
      float overflowRatio = 0f;
      foreach (Effects.Base.BaseEffect effect in statOwner.CombatEffects())
      {
        chance += effect.CritChanceBonusAgainst(statOwner, target, queryType);
        critDamage += effect.CritDamageBonusAgainst(statOwner, target, queryType);
        forced |= effect.ForcesCriticalAgainst(statOwner, target, queryType);
        overflowRatio += effect.CritOverflowToDamageRatio(statOwner);
      }
      if (overflowRatio > 0f && chance > 1f) critDamage += (chance - 1f) * overflowRatio;

      if (Codes.Passive.ShakespeareLanguage.TryEmpower(statOwner, actor, chance, out float converted))
      {
        forced = true;
        critDamage += converted;
      }

      bool crit = forced || Random.value < Mathf.Clamp01(chance);
      multiplier = crit ? 1f + Mathf.Max(0f, critDamage) : 1f;
      return crit;
    }

    /// <summary>
    /// 캐스팅 시간만큼 기다린다. 도중에 시전자가 쓰러지거나 제어당하면 <c>false</c>를 넘긴다.
    /// 표준 흐름을 통째로 갈아 끼우는 코드가 대기 구간만 빌려 쓰라고 여기에 둔다.
    /// </summary>
    protected IEnumerator WaitForCast(System.Action<bool> completed)
    {
      float elapsed = 0f;
      while (elapsed < CastingDelay)
      {
        if (Caster == null || !Caster.isActive || Caster.isControlled)
        {
          completed(false);
          yield break;
        }

        elapsed += Time.deltaTime;
        yield return null;
      }
      completed(true);
    }

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
  }
}
