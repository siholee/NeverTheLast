using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects.Base
{
    /// <summary>
    /// 효과의 기본 추상 클래스
    /// 모든 효과(Effect)는 이 클래스를 상속받아야 합니다.
    /// 생명주기 훅(OnApply/OnUpdate/OnRemove)과 스탯 질의 훅을 모두 제공한다.
    /// 스탯 질의 훅은 Unit의 스탯 계산(AttributesUpdate, GetBase* 등)에서 매번 호출된다.
    /// </summary>
    public abstract class BaseEffect
    {
        /// <summary>효과 ID (EffectFactory 생성 효과만 사용, 직접 생성 효과는 0)</summary>
        public int EffectId { get; protected set; }

        /// <summary>효과 이름</summary>
        public string EffectName { get; protected set; }

        /// <summary>효과 설명</summary>
        public string EffectDescription { get; protected set; }

        /// <summary>효과 분류 (긍정적/부정적/중립적)</summary>
        public BaseEnums.EffectCategory Category { get; protected set; }

        /// <summary>효과 계수 (예: 공격력의 20% DOT라면 계수는 20)</summary>
        public float Coefficient { get; set; }

        /// <summary>효과의 시전자</summary>
        public Unit Caster { get; set; }

        /// <summary>효과의 대상</summary>
        public Unit Target { get; set; }

        /// <summary>이로운 효과 여부 (OnBeneficialEffectReceived 이벤트 발행 판정)</summary>
        public virtual bool IsBeneficial => false;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="effectId">효과 ID</param>
        /// <param name="coefficient">효과 계수 (기본값 100 = 100%)</param>
        protected BaseEffect(int effectId, float coefficient = 100f)
        {
            EffectId = effectId;
            Coefficient = coefficient;
        }

        // ===== 생명주기 훅 (필요한 것만 재정의) =====

        /// <summary>효과 적용 시 호출 (초기화)</summary>
        public virtual void OnApply() { }

        /// <summary>매 프레임 업데이트 시 호출 (DoT 등)</summary>
        /// <param name="deltaTime">프레임 델타 타임</param>
        public virtual void OnUpdate(float deltaTime) { }

        /// <summary>효과 제거 시 호출</summary>
        public virtual void OnRemove() { }

        // ===== 스탯 질의 훅 (구 StatusEffect의 modifier 계열) =====
        // unit 파라미터는 질의 대상(효과 보유자)이다. 시전자 한정 효과는 unit == Caster를 확인한다.

        /// <summary>치명타 확률 가산 보정</summary>
        public virtual float CritChanceAdditiveModifier(Unit unit) => 0f;

        /// <summary>치명타 배율 가산 보정</summary>
        public virtual float CritMultiplierAdditiveModifier(Unit unit) => 0f;

        /// <summary>100%를 초과한 치명타 확률을 치명타 피해로 바꾸는 배율(학자 등).</summary>
        public virtual float ExcessCritChanceConversionMultiplier(Unit unit) => 0f;

        /// <summary>받는 피해 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ReceivingDamageModifier(Unit unit) => 1f;

        /// <summary>공격자·피해 태그를 참조하는 받는 피해 배율 보정.</summary>
        public virtual float ReceivingDamageModifier(Unit unit, DamageContext context)
            => ReceivingDamageModifier(unit);

        /// <summary>주는 피해 배율 보정 (공격자의 효과에서 질의, 1 = 변화 없음)</summary>
        public virtual float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => 1f;

        /// <summary>공격 시 대상 방어력 적용 배율 보정 (공격자의 효과에서 질의, 1 = 변화 없음)</summary>
        public virtual float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context) => 1f;

        /// <summary>받는 피해에서 고정으로 깎는 내구 가산치.</summary>
        public virtual int DurabilityAdditiveModifier(Unit unit) => 0;

        /// <summary>효과 보유자의 방어력 적용 배율 보정. 방어력 감소 디버프 등에 사용한다.</summary>
        public virtual float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context) => 1f;

        /// <summary>받는 치유량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float HealingReceivedMultiplierModifier(Unit unit) => 1f;

        /// <summary>보유자가 부여하는 치유량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float OutgoingHealingMultiplierModifier(Unit source, Unit target) => 1f;

        /// <summary>최대 체력을 넘긴 치유량 중 보호막으로 전환할 비율.</summary>
        public virtual float OverhealShieldConversionModifier(Unit unit) => 0f;

        /// <summary>받는 보호막 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ShieldReceivedMultiplierModifier(Unit unit) => 1f;

        /// <summary>보유자가 부여하는 보호막량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float OutgoingShieldMultiplierModifier(Unit source, Unit target) => 1f;

        /// <summary>보유자의 치유·보호막이 치명타 판정을 할 수 있는지 여부.</summary>
        public virtual bool EnablesHealingShieldCritical(Unit source) => false;

        /// <summary>보유자가 실제 치유 또는 보호막을 부여한 직후 호출된다.</summary>
        public virtual void OnHealingOrShieldGranted(Unit source, Unit target) { }

        /// <summary>대상 지정 우선도 가산 보정.</summary>
        public virtual int TargetPriorityAdditiveModifier(Unit unit) => 0;

        /// <summary>보유자가 부여하는 지속피해량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float DamageOverTimeApplicationMultiplier(Unit unit) => 1f;

        /// <summary>지속피해 효과 여부. 처치 시 남은 지속피해 정산 등에 사용한다.</summary>
        public virtual bool IsDamageOverTime => false;

        /// <summary>현재 시점 기준 1초 동안 입힐 수 있는 지속피해량.</summary>
        public virtual int EstimateDamagePerSecond() => 0;

        /// <summary>5대 기본 스탯 가산 보정</summary>
        public virtual int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat) => 0;

        /// <summary>5대 기본 스탯 배율 보정 (1 = 변화 없음)</summary>
        public virtual float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat) => 1f;

        /// <summary>마나(궁극기 자원) 회복 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ManaRecoveryMultiplierModifier(Unit unit) => 1f;

        /// <summary>INT에서 파생되는 마나 회복 효율 기여분의 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ManaRecoveryIntContributionMultiplierModifier(Unit unit) => 1f;

        /// <summary>보호막 부여량 가산 보정</summary>
        public virtual float ShieldBonusAdditiveModifier(Unit unit) => 0f;

        /// <summary>코드 가속 가산 보정</summary>
        public virtual float CodeAccelerationAdditiveModifier(Unit unit) => 0f;

        /// <summary>최대 체력 배율 보정 (1 = 변화 없음)</summary>
        public virtual float MaxHpMultiplierModifier(Unit unit) => 1f;

        /// <summary>치명 피해를 막으면 true. 사망 직전 효과(황혼 등)에서 사용한다.</summary>
        public virtual bool TryPreventDeath(Unit unit, Unit attacker) => false;
    }
}
