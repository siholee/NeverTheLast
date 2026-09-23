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
        /// <summary>외부 이로운 상태 수신 시 촉매로 세는가. 회복 틱/표식은 제외한다.</summary>
        public virtual bool CountsAsReagentBuff => true;

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

        /// <summary>
        /// <b>이 효과를 들고 있는 유닛의 턴이 시작될 때</b> 호출된다.
        ///
        /// 전투는 턴제다. 지속피해·재생·주기형 패시브는 벽시계가 아니라 이 훅으로 진행한다.
        /// 프레임마다 돌던 예전 방식은 행동 순서와 어긋나서, 누군가 행동하는 동안에도
        /// 효과만 계속 흘러가는 문제가 있었다.
        /// </summary>
        public virtual void OnOwnerTurn() { }

        /// <summary>효과 제거 시 호출</summary>
        public virtual void OnRemove() { }

        /// <summary>
        /// 보유자의 궁극기 자원이 변한 직후 호출된다. 가상 자원처럼 실제 마나와 합산해
        /// 임계점을 판정해야 하는 효과가 다음 행동/이벤트까지 지연되지 않도록 하는 훅이다.
        /// </summary>
        public virtual void OnUltimateResourceChanged(Unit unit) { }

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

        /// <summary>
        /// 궁극기 시전 직전에 한 번 계산하는 피해 배율. 체력 지불처럼 시전당 한 번만 일어나야 하는
        /// 비용은 일반 <see cref="OutgoingDamageModifier"/>에서 처리하면 다단·광역 타격마다 반복되므로
        /// 이 훅을 사용한다.
        /// </summary>
        public virtual float PrepareUltimateDamageMultiplier(Unit unit) => 1f;

        /// <summary>공격 시 대상 방어력 적용 배율 보정 (공격자의 효과에서 질의, 1 = 변화 없음)</summary>
        public virtual float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context) => 1f;

        /// <summary>받는 피해에서 고정으로 깎는 내구 가산치.</summary>
        public virtual int DurabilityAdditiveModifier(Unit unit) => 0;

        /// <summary>장비와 효과에서 얻는 최종 내구 배율 보정 (1 = 변화 없음).</summary>
        public virtual float DurabilityMultiplierModifier(Unit unit) => 1f;

        /// <summary>특정 장비 한 개가 제공하는 내구 배율 보정 (1 = 변화 없음).</summary>
        public virtual float EquipmentDurabilityMultiplierModifier(Unit unit, Managers.ItemData item) => 1f;

        /// <summary>공격 시 대상 내구의 일부만 무시하는 양.</summary>
        public virtual int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context) => 0;

        /// <summary>
        /// 공격이 강인도를 감소시키는 배율의 가산 보정. 0.25는 강인도 효율 +25%다.
        /// 같은 공격자에게 여러 효과가 있으면 가산한 뒤 기본 배율 1에 더한다.
        /// </summary>
        public virtual float ToughnessDamageAdditiveModifier(Unit attacker, Unit target, DamageContext context) => 0f;

        /// <summary>
        /// 실제로 감소시킨 강인도 중 체력 피해로 한 번 더 가할 비율.
        /// 토트의 지혜의 눈과 허허실실처럼 여러 효과가 있으면 가산한다.
        /// </summary>
        public virtual float ToughnessEchoDamageRatioModifier(Unit attacker, Unit target, DamageContext context) => 0f;

        /// <summary>자신의 공격 때문에 지불하는 체력 비용 배율 (1 = 변화 없음).</summary>
        public virtual float AttackSelfHpCostMultiplier(Unit unit) => 1f;

        /// <summary>
        /// 처형선에 더해지는 비율. 0.04면 8%가 12%가 된다.
        ///
        /// 처형선을 <b>덮어쓰지 않고 더하는</b> 이유는 코드와 장비가 각자 쌓일 수 있어야 해서다.
        /// 덮어쓰면 둘을 함께 들었을 때 큰 쪽 하나만 남아 장비가 죽는다.
        /// </summary>
        public virtual float ExecuteThresholdAdditiveModifier(Unit unit) => 0f;

        /// <summary>
        /// 치유량 감소가 실제로 깎는 몫을 얼마나 덜어 내는가. 0.5면 절반만 깎인다.
        ///
        /// 받는 치유 배율을 거꾸로 올리는 방식으로는 못 만든다. 그렇게 하면 감소가 없을 때도
        /// 치유가 늘어 버린다. 감소를 <b>거는 쪽</b>이 이 값을 읽어 제 몫을 줄이는 것이 옳다.
        /// </summary>
        public virtual float HealingReductionResistanceModifier(Unit unit) => 0f;

        /// <summary>해로운 상태가 최종 적용되기 직전의 저항 확률 가산치.</summary>
        public virtual float NegativeStatusResistanceChanceModifier(
            Unit unit, Entities.Status.UnitStatus status) => 0f;

        /// <summary>
        /// 회피 확률 가산 보정. 공격의 종류를 보고 조건부로 회피시키는 효과가 쓴다
        /// (에퀴테스 '기병의 회피'는 접촉 기술만 회피한다).
        /// </summary>
        public virtual float EvasionChanceAdditiveModifier(Unit unit, DamageContext context) => 0f;

        /// <summary>
        /// 이 효과의 주인이 <b>가하는</b> 공격을 회피할 수 없게 만드는가.
        ///
        /// 회피 판정은 맞는 쪽에서 돌지만 이 훅은 <b>공격자</b> 쪽을 본다 —
        /// 궁니르처럼 "이 무기는 빗나가지 않는다"를 적으려면 창을 든 쪽에 적혀야 한다.
        /// </summary>
        public virtual bool IgnoresEvasion(Unit attacker) => false;

        /// <summary>
        /// 전투 시작 시 행동 게이지 보정. 양수면 그 유닛의 첫 행동을 앞당기고, 음수면 늦춘다.
        /// 1 = 한 번의 행동에 필요한 행동치 전부.
        /// <b>효과 보유자가 아니라 임의의 참가자에 대해 질의</b>되므로,
        /// 대상을 가리는 조건은 각 효과가 직접 판단한다.
        /// </summary>
        public virtual float RoundStartActionAdjustment(Unit unit) => 0f;

        /// <summary>효과 보유자의 방어력 적용 배율 보정. 방어력 감소 디버프 등에 사용한다.</summary>
        public virtual float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context) => 1f;

        /// <summary>받는 치유량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float HealingReceivedMultiplierModifier(Unit unit) => 1f;

        /// <summary>치유의 부여자와 공격 연계 여부를 함께 보는 받는 치유량 배율 보정.</summary>
        public virtual float HealingReceivedMultiplierModifier(Unit unit, Unit source, bool attackTriggered)
            => HealingReceivedMultiplierModifier(unit);

        /// <summary>보유자가 부여하는 치유량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float OutgoingHealingMultiplierModifier(Unit source, Unit target) => 1f;

        /// <summary>최대 체력을 넘긴 치유량 중 보호막으로 전환할 비율.</summary>
        public virtual float OverhealShieldConversionModifier(Unit unit) => 0f;

        /// <summary>받는 보호막 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ShieldReceivedMultiplierModifier(Unit unit) => 1f;

        /// <summary>보유자가 부여하는 보호막량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float OutgoingShieldMultiplierModifier(Unit source, Unit target) => 1f;

        /// <summary>
        /// 보유자가 새로 부여하는 유한 턴 상태의 지속시간 가산치.
        /// 무한 상태(Duration &lt;= 0)에는 적용되지 않으며, 상태 하나당 최초 부여 시 한 번만 읽힌다.
        /// </summary>
        /// <summary>
        /// 이 효과의 주인이 <b>다른 유닛에게 붙이는 원소</b>의 지속에 더하는 턴.
        ///
        /// 상태 지속(<see cref="GrantedStatusDurationAdditiveModifier"/>)과 축이 다르다.
        /// 부착은 <c>UnitStatus</c>가 아니라 <c>Unit</c>이 직접 들고 있는 타이머라서,
        /// 상태 쪽 훅으로는 닿지 않는다.
        /// </summary>
        public virtual int GrantedElementDurationAdditiveModifier(Unit source) => 0;

        public virtual int GrantedStatusDurationAdditiveModifier(
            Unit source, Entities.Status.UnitStatus status) => 0;

        /// <summary>보유자의 치유·보호막이 치명타 판정을 할 수 있는지 여부.</summary>
        public virtual bool EnablesHealingShieldCritical(Unit source) => false;

        /// <summary>보유자가 실제 치유 또는 보호막을 부여한 직후 호출된다.</summary>
        public virtual void OnHealingOrShieldGranted(Unit source, Unit target) { }

        /// <summary>
        /// 보유자가 오버힐을 제외한 실제 체력 회복을 부여한 직후 호출된다.
        /// 치유에만 반응해 보호막을 덧씌우거나 자원을 채우는 효과가 공통 보호막 훅을
        /// 다시 밟아 재귀하지 않도록 별도로 둔다.
        /// </summary>
        public virtual void OnEffectiveHealingGranted(
            Unit source, Unit target, int effectiveHealing, int hpBeforeHealing) { }

        /// <summary>
        /// 효과 보유자가 필드에 있는 동안 같은 진영 공격자가 특정 대상을 때릴 때 더하는
        /// 피해 보너스. 세이메이의 봉인진처럼 공격자가 아닌 서포터에게 붙은 오라가 사용한다.
        /// </summary>
        public virtual float AlliedConditionalDamageBonusAdditive(Unit owner, Unit attacker, Unit target) => 0f;

        /// <summary>대상 지정 우선도 가산 보정.</summary>
        public virtual int TargetPriorityAdditiveModifier(Unit unit) => 0;

        /// <summary>보유자가 부여하는 지속피해량 배율 보정 (1 = 변화 없음)</summary>
        public virtual float DamageOverTimeApplicationMultiplier(Unit unit) => 1f;

        /// <summary>지속피해 효과 여부. 처치 시 남은 지속피해 정산 등에 사용한다.</summary>
        public virtual bool IsDamageOverTime => false;

        /// <summary>보유자의 턴 한 번에 입힐 지속피해량. 정산형 코드(야마·츠쿠요미)가 읽는다.</summary>
        public virtual int EstimateDamagePerTurn() => 0;

        /// <summary>5대 기본 스탯 가산 보정</summary>
        public virtual int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat) => 0;

        /// <summary>5대 기본 스탯 배율 보정 (1 = 변화 없음)</summary>
        public virtual float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat) => 1f;

        /// <summary>
        /// YAML에 기록된 초기 5대 스탯에만 적용하는 배율. 레벨 성장·강화·훈련·장비 스탯은 제외한다.
        /// 일리아스처럼 태생 스테이터스만 증폭하는 효과가 사용한다.
        /// </summary>
        public virtual float InitialPrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat) => 1f;

        /// <summary>마나(궁극기 자원) 회복 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ManaRecoveryMultiplierModifier(Unit unit) => 1f;

        /// <summary>INT에서 파생되는 마나 회복 효율 기여분의 배율 보정 (1 = 변화 없음)</summary>
        public virtual float ManaRecoveryIntContributionMultiplierModifier(Unit unit) => 1f;

        /// <summary>보호막 부여량 가산 보정</summary>
        public virtual float ShieldBonusAdditiveModifier(Unit unit) => 0f;

        /// <summary>코드 가속 가산 보정</summary>
        public virtual float CodeAccelerationAdditiveModifier(Unit unit) => 0f;

        /// <summary>
        /// 최종 행동 속도를 덮어쓰거나 제한하는 보정. 기본값은 계산된 속도를 그대로 돌려준다.
        /// 호루스처럼 DEX와 무관하게 전투 속도가 고정되는 효과가 사용한다.
        /// </summary>
        public virtual float ActionSpeedModifier(Unit unit, float calculatedSpeed) => calculatedSpeed;

        /// <summary>최대 체력 배율 보정 (1 = 변화 없음)</summary>
        public virtual float MaxHpMultiplierModifier(Unit unit) => 1f;

        /// <summary>
        /// 체력 바를 몇 칸으로 나눠 보여 줄지. 0이면 나누지 않는다(보통의 연속 게이지).
        /// 오시리스의 '불완전한 부활'처럼 <b>체력 자체가 단계로 끊기는</b> 유닛이 사용한다.
        /// 순수한 표시용 훅이라 피해 계산에는 관여하지 않는다.
        /// </summary>
        public virtual int HpSegmentCount(Unit unit) => 0;

        /// <summary>
        /// 한 번의 피해가 최대 체력의 몇 배까지만 들어가는가. 0이면 상한이 없다.
        ///
        /// <see cref="HpSegmentCount"/>는 체력 바를 나눠 그리기만 하는 <b>표시</b>였다.
        /// 실제로 "한 방에 절반 넘게 깎이지 않는다"를 만들려면 감쇠가 다 끝난 뒤 잘라야 하므로
        /// 훅을 따로 둔다. 여러 효과가 걸리면 <b>가장 낮은 비율</b>이 이긴다.
        /// </summary>
        public virtual float IncomingDamageCapRatio(Unit unit, DamageContext context) => 0f;

        /// <summary>
        /// 한 번의 피해가 <b>체력을</b> 이 값 아래로 내리지 못한다. 0이면 제한이 없다.
        ///
        /// <see cref="IncomingDamageCapRatio"/>는 보호막 처리 전의 피해량을 자르므로, 경계 바로 앞에서
        /// 큰 공격이 보호막에도 조금밖에 못 들어가는 문제가 생긴다. 이 훅은 보호막 흡수가 끝난 뒤
        /// <b>체력 손실에만</b> 적용된다. 심연의 집정관의 체력 구간이 쓴다. 여러 효과가 걸리면 가장 높은 값이 이긴다.
        /// </summary>
        public virtual int HpLossFloor(Unit unit, DamageContext context) => 0;

        /// <summary>
        /// 이 효과의 주인이 필드에 서 있는 동안 같은 진영 <paramref name="attacker"/>의 피해가 보호막을 건너뛰는가.
        /// 공격자가 아닌 곳에 붙은 아군 오라가 쓴다. 피해가 해결되는 순간에 묻는다.
        /// </summary>
        public virtual bool GrantsAlliedShieldPenetration(Unit owner, Unit attacker) => false;

        /// <summary>
        /// 전장 상태가 주는 <b>불리한</b> 배율을 무시하는가. 유리한 쪽은 그대로 받는다.
        /// 혹한의 갑주처럼 "이 판에서만 안 아픈" 장비가 쓴다.
        /// </summary>
        public virtual bool IgnoresFieldPenalty(Unit unit) => false;

        /// <summary>치명 피해를 막으면 true. 사망 직전 효과(황혼 등)에서 사용한다.</summary>
        public virtual bool TryPreventDeath(Unit unit, Unit attacker) => false;

        /// <summary>
        /// 적중이 확정된 피해 한 번을 <b>통째로 무효화</b>하면 true. 천공 전선의 횟수제 무적이 쓴다.
        ///
        /// 회피 판정 뒤, 피해 계산 앞에서 묻는다. 빗나간 공격이 횟수를 깎으면 안 되고,
        /// 무효화된 타격이 보호막·체력·흡혈을 건드려서도 안 되기 때문이다.
        /// 받는 피해 배율 훅으로는 0을 만들 수 없다 — 최종 피해는 최소 1로 올려진다.
        /// </summary>
        public virtual bool TryNullifyHit(Unit unit, DamageContext context) => false;

        /// <summary>
        /// 보유자가 소환한 소환수의 피해 배율 보정 (1 = 변화 없음).
        /// 소환수는 소환자의 일반 <see cref="OutgoingDamageModifier"/>를 받지 않으므로 이 훅만 적용된다.
        /// </summary>
        public virtual float SummonDamageMultiplierModifier(Unit unit) => 1f;

        /// <summary>보유자가 거느린 소환수의 치명타 피해 배율에 더하는 값.</summary>
        public virtual float SummonCritMultiplierAdditiveModifier(Unit unit) => 0f;

        /// <summary>보유자가 아군 전체의 소환수에게 제공하는 피해 배율 보정 (1 = 변화 없음).</summary>
        public virtual float AlliedSummonDamageMultiplierModifier(Unit unit, Unit summonOwner) => 1f;

        /// <summary>
        /// 보유자가 아군 전체 소환수에게 더하는 중첩 가능 피해 보너스.
        /// 0.10이면 다른 보유자의 같은 효과와 합산해 +10%를 더한다.
        /// </summary>
        public virtual float AlliedSummonDamageBonusAdditive(Unit unit, Unit summonOwner) => 0f;

        /// <summary>
        /// 아군 소환수의 다음 공격을 확정 치명타로 바꿀 수 있으면 true.
        /// 셰익스피어처럼 공격이 해결되는 순간 자원을 소비하는 필드 지원이 사용한다.
        /// </summary>
        public virtual bool TryConsumeAlliedSummonCritical(Unit owner, Unit summonAttacker) => false;

        /// <summary>효과가 유닛 분류 태그를 동적으로 부여하는지 여부.</summary>
        public virtual bool GrantsUnitTag(Unit unit, string tag) => false;

        /// <summary>보유자가 빙결에 면역이면 true.</summary>
        public virtual bool GrantsFreezeImmunity(Unit unit) => false;

        /// <summary>보유자가 에어본에 면역이면 true.</summary>
        public virtual bool GrantsAirborneImmunity(Unit unit) => false;

        /// <summary>
        /// 보유자의 일반행동을 막으면 true.
        /// 행동 주기를 AV가 전담하므로 일반행동 차단은 쿨다운이 아니라 이 훅으로 판정한다.
        /// </summary>
        public virtual bool BlocksNormalAttack(Unit unit) => false;
    }
}
