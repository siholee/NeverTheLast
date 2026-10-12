using BaseClasses;
using Entities;

namespace Effects.Base
{
    /// <summary>
    /// 효과의 기본 추상 클래스. 상태(<see cref="Entities.Status.UnitStatus"/>)가 이것을 품는다.
    ///
    /// 한 클래스에 <b>선택적 질의 훅</b>이 여럿 있고, 효과는 필요한 것만 재정의한다.
    /// <see cref="Unit"/>은 값이 필요할 때마다 보유한 모든 효과에 물어 합산한다.
    ///
    /// 2026-09 개편에서 옛 캐릭터 전용 훅(내구 · 처형선 · 시약 등)을 걷어냈다.
    /// 새 캐릭터가 필요로 할 때 그 캐릭터와 함께 다시 들인다.
    /// </summary>
    public abstract class BaseEffect
    {
        /// <summary>효과 ID (EffectFactory 생성 효과만 사용, 직접 생성 효과는 0)</summary>
        public int EffectId { get; protected set; }

        public string EffectName { get; protected set; }

        public string EffectDescription { get; protected set; }

        public BaseEnums.EffectCategory Category { get; protected set; }

        /// <summary>효과 계수 (예: 공격력의 20% 지속피해라면 20)</summary>
        public float Coefficient { get; set; }

        /// <summary>효과를 건 유닛</summary>
        public Unit Caster { get; set; }

        /// <summary>효과를 가진 유닛</summary>
        public Unit Target { get; set; }

        /// <summary>이로운 효과 여부 (OnBeneficialEffectReceived 이벤트 발행 판정)</summary>
        public virtual bool IsBeneficial => false;

        protected BaseEffect(int effectId, float coefficient = 100f)
        {
            EffectId = effectId;
            Coefficient = coefficient;
        }

        // ===== 생명주기 =====

        public virtual void OnApply() { }

        /// <summary>
        /// <b>이 효과를 가진 유닛의 턴이 시작될 때</b> 호출된다.
        /// 전투는 턴제다 — 지속피해 · 재생 · 주기형 효과는 벽시계가 아니라 이 훅으로 진행한다.
        /// </summary>
        public virtual void OnOwnerTurn() { }

        public virtual void OnRemove() { }

        /// <summary>보유자의 궁극기 자원이 변한 직후 호출된다.</summary>
        public virtual void OnUltimateResourceChanged(Unit unit) { }

        // ===== 스탯 =====
        // unit은 질의 대상(효과 보유자)이다.

        /// <summary>
        /// 스탯 고정 보정. 치명타 두 스탯은 비율 그대로 더한다(0.1 = +10%p).
        /// 최종 스탯 = 기초 × (1 + 퍼센트 합) + 고정 합.
        /// </summary>
        public virtual float StatFlatModifier(Unit unit, BaseEnums.StatType stat) => 0f;

        /// <summary>스탯 퍼센트 보정(0.1 = +10%). 기초 스탯에 곱해진다. 치명타 두 스탯에는 쓰지 않는다.</summary>
        public virtual float StatPercentModifier(Unit unit, BaseEnums.StatType stat) => 0f;

        /// <summary>마나 회복 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ManaRecoveryMultiplierModifier(Unit unit) => 1f;

        /// <summary>초월기 피해 증가의 가산 몫. 동일 분류는 더해서 한 번 곱한다.</summary>
        public virtual float UltimateDamageBonus(Unit actor) => 0f;

        // ===== 피해 =====

        /// <summary>주는 피해 배율 (공격자의 효과에서 질의, 1 = 변화 없음)</summary>
        public virtual float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => 1f;

        /// <summary>받는 피해 배율 (피격자의 효과에서 질의, 1 = 변화 없음)</summary>
        public virtual float ReceivingDamageModifier(Unit unit, DamageContext context) => 1f;

        /// <summary>대상의 방어 스탯을 얼마만큼만 인정하는가 (공격자 쪽, 1 = 그대로). 방어 무시에 쓴다.</summary>
        public virtual float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context) => 1f;

        /// <summary>보유자의 방어 스탯 적용 배율 (보유자 쪽, 1 = 그대로). 방어력 감소 디버프에 쓴다.</summary>
        public virtual float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context) => 1f;

        /// <summary>치명 피해를 막으면 true.</summary>
        public virtual bool TryPreventDeath(Unit unit, Unit attacker) => false;

        // ===== 치명타 (공격자의 효과에서 질의, 대상을 모르면 target은 null) =====

        /// <summary>이 공격의 치명타 확률 가산(0.15 = +15%p).</summary>
        public virtual float CritChanceBonusAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType) => 0f;

        /// <summary>이 공격의 치명타 피해 가산(0.2 = +20%p).</summary>
        public virtual float CritDamageBonusAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType) => 0f;

        /// <summary>이 공격을 확정 치명타로 만드는가.</summary>
        public virtual bool ForcesCriticalAgainst(Unit attacker, Unit target, BaseEnums.CodeType codeType) => false;

        /// <summary>100%를 넘는 치명타 확률을 치명타 피해로 바꾸는 비율(1 = 1:1).</summary>
        public virtual float CritOverflowToDamageRatio(Unit attacker) => 0f;

        /// <summary>시전자(효과 보유자)가 <b>거는</b> 상태의 지속 가산 턴. 상태 정의를 보고 대상을 가린다.</summary>
        public virtual int OutgoingStatusDurationModifier(Unit caster, Entities.Status.UnitStatus status) => 0;

        // ===== 제어 =====

        /// <summary>보유자에게 걸리는 제어 지속 가산(음수면 줄인다). 최소 1턴은 남는다.</summary>
        public virtual int ControlDurationModifier(Unit unit) => 0;

        // ===== 상태 · 행동 =====

        /// <summary>해로운 상태가 최종 적용되기 직전의 추가 저항 확률(특수방어력 판정과 별개).</summary>
        public virtual float NegativeStatusResistanceChanceModifier(Unit unit, Entities.Status.UnitStatus status) => 0f;

        /// <summary>
        /// 전투 시작 시 행동 게이지 보정. 양수면 첫 행동을 앞당긴다. 1 = 행동 한 번에 필요한 행동값 전부.
        /// <b>효과 보유자가 아니라 임의의 참가자에 대해 질의</b>되므로 대상 조건은 효과가 직접 가린다.
        /// </summary>
        public virtual float RoundStartActionAdjustment(Unit unit) => 0f;

        /// <summary>대상 지정 우선도 가산 보정. 도발이 쓴다.</summary>
        public virtual int TargetPriorityAdditiveModifier(Unit unit) => 0;

        /// <summary>보유자를 소환수로 간주하는가. 실제 소환 체계와 별개인 자기 판정에도 쓴다.</summary>
        public virtual bool CountsAsSummon(Unit unit) => false;

        /// <summary>보유자의 일반행동을 막으면 true.</summary>
        public virtual bool BlocksNormalAttack(Unit unit) => false;

        public virtual float ToughnessEfficiencyBonus(Unit attacker) => 0f;

        // ===== 치유 · 보호막 =====

        public virtual float HealingReceivedMultiplierModifier(Unit unit) => 1f;

        public virtual float OutgoingHealingMultiplierModifier(Unit source, Unit target) => 1f;

        public virtual float ShieldReceivedMultiplierModifier(Unit unit) => 1f;

        public virtual float OutgoingShieldMultiplierModifier(Unit source, Unit target) => 1f;

        // ===== 지속피해 =====

        /// <summary>지속피해 효과 여부.</summary>
        public virtual bool IsDamageOverTime => false;

        /// <summary>
        /// 지속피해 <b>한 번분</b>의 원시 피해 정보(주는 피해 보정 · 대상 경감 전). 지속피해가 아니면 null.
        /// 자연 틱과 즉시 발동(<see cref="Combat.DamageOverTimeDetonation"/>)이 반드시 이 한 곳에서 같은 컨텍스트를 만든다 —
        /// 시전자 · 피해 종류 · 반응 태그가 갈라지지 않게 하려는 단일 입구다. 새 지속피해도 이것만 재정의하면 즉시 발동에 참여한다.
        /// 이 메서드는 상태를 바꾸지 않는다(지속시간 · 중첩 · 행동 카운트 불변).
        /// </summary>
        public virtual DamageContext CreateDamageOverTimeTick() => null;

        /// <summary>보유자의 턴 한 번에 입힐 지속피해량(표시 · 정산용).</summary>
        public virtual int EstimateDamagePerTurn() => 0;
    }
}
