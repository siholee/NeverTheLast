using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidCrusherCodeIds
    {
        public const int Mass = 1520;
        public const int UltimateBody = 1521;
        public const int TranscendentBody = 1522;
        public const int AdaptiveArmor = 1523;
    }

    public static class VoidCrusherStatusIds
    {
        public const int Mass = 7960;
        public const int UltimateBody = 7961;
        public const int TranscendentBody = 7962;
        public const int AdaptiveArmor = 7963;
    }

    /// <summary>
    /// 공허의 분쇄자 P 질량 — 최대 체력 1,000당 가하는 물리 피해 +1%.
    /// 그리고 <b>한 번에 받는 단일 대상 피해가 최대 체력의 25%를 넘지 못한다.</b>
    ///
    /// 상한이 핵심이다. 9스테이지의 단독 엘리트라 상한이 없으면 광역 폭딜 한 번으로
    /// 전투가 끝나 버린다. 상한을 걸면 <b>지구전</b>이 강제되고, 그 지구전을
    /// 적응 장갑(1523)이 다시 어렵게 만든다.
    /// </summary>
    public sealed class VoidCrusherMass : PersistentStatusPassive
    {
        private const float BonusPerThousandHp = 0.01f;
        private const float IncomingCapRatio = 0.25f;

        public VoidCrusherMass(PassiveCodeContext context)
            : base(context, VoidCrusherStatusIds.Mass, "void_crusher_mass", "질량",
                $"최대 체력 1,000당 가하는 물리 피해가 {BonusPerThousandHp * 100f:F0}% 증가하고, "
                + $"한 번에 받는 단일 대상 피해가 최대 체력의 {IncomingCapRatio * 100f:F0}%를 넘지 않습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect()
            => new MassEffect(BonusPerThousandHp, IncomingCapRatio);
    }

    internal sealed class MassEffect : BaseEffect
    {
        private readonly float _bonusPerThousandHp;
        private readonly float _capRatio;

        public MassEffect(float bonusPerThousandHp, float capRatio) : base(0, bonusPerThousandHp)
        {
            _bonusPerThousandHp = bonusPerThousandHp;
            _capRatio = capRatio;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.Physical) == true
                ? 1f + Target.HpMax / 1000f * _bonusPerThousandHp
                : 1f;

        public override float IncomingDamageCapRatio(Unit unit, DamageContext context)
            => unit == Target && context?.DamageTags?.Contains(DamageTag.SingleTarget) == true
                ? _capRatio
                : 0f;
    }

    /// <summary>
    /// 궁극의 신체(1521)와 그 금색 상위 코드 천무지체(1522) —
    /// 모든 공격에 자신의 최대 체력 비례 물리 피해가 얹힌다.
    ///
    /// 최대 체력을 불리는 알파 개체·완전함과 곱해지므로 <b>체력이 곧 화력</b>이 된다.
    /// 추가분은 고정 피해가 아니라 물리로 넣어 방어력·내구가 그대로 먹히게 했다 —
    /// 고정 피해로 두면 방어를 쌓는 쪽이 아무 대응도 할 수 없다.
    /// </summary>
    public sealed class VoidCrusherBody : PersistentStatusPassive
    {
        private readonly float _maxHpPercent;

        public VoidCrusherBody(PassiveCodeContext context, int statusId, string statusKey,
            string name, float maxHpPercent, int supersededByCodeId, BaseEnums.CodeGrade grade)
            : base(context, statusId, statusKey, name,
                $"모든 공격에 자신의 최대 체력 {maxHpPercent:0.#}%에 해당하는 물리 피해가 추가됩니다.")
        {
            _maxHpPercent = maxHpPercent;
            Transferable = false;
            Grade = grade;
            SupersededByCodeId = supersededByCodeId;
        }

        protected override BaseEffect CreateInitialEffect() => new MaxHpBonusDamageEffect(_maxHpPercent);
    }

    internal sealed class MaxHpBonusDamageEffect : BaseEffect
    {
        private readonly float _maxHpPercent;
        private Action<DamageResolvedContext> _handler;
        private bool _resolving;

        public MaxHpBonusDamageEffect(float maxHpPercent) : base(0, maxHpPercent)
            => _maxHpPercent = maxHpPercent;

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            // 추가 피해가 다시 OnDamageDealt를 부르므로 재진입을 막는다.
            if (_resolving || context?.Attacker != Target) return;

            Unit victim = context.Target;
            DamageContext damage = context.DamageContext;
            if (victim == null || !victim.isActive || context.DamageDealt <= 0) return;
            if (damage == null || damage.CodeType == BaseEnums.CodeType.Effect) return;

            int bonus = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _maxHpPercent * 0.01f));
            _resolving = true;
            try
            {
                victim.TakeDamage(new DamageContext(
                    Target, bonus, BaseEnums.CodeType.Passive,
                    new List<int> { DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.Physical }));
            }
            finally
            {
                _resolving = false;
            }
        }
    }

    /// <summary>
    /// 적응 장갑(1523) — <b>같은 기술</b>에 맞을수록 그 기술에게서 받는 피해가 줄어든다.
    ///
    /// 리그 오브 레전드의 적응형 투구와 같은 축이다. 단독 엘리트라 플레이어가 한 캐릭터의
    /// 가장 센 코드만 반복해 넣는 상황이 자연스럽게 나오는데, 그 최적해를 깎아 파티를
    /// 돌려쓰게 만든다. 질량(1520)의 한 방 상한과 짝이 되어 <b>지구전을 길게</b> 만든다.
    ///
    /// '같은 기술'은 <see cref="DamageContext"/>에 코드 식별자가 없어
    /// <b>시전자 + 코드 종류 + 피해 태그 조합</b>으로 갈음한다.
    /// </summary>
    public sealed class VoidAdaptiveArmor : PersistentStatusPassive
    {
        private const float PerHit = 0.10f;
        private const float MaxReduction = 0.50f;

        public VoidAdaptiveArmor(PassiveCodeContext context)
            : base(context, VoidCrusherStatusIds.AdaptiveArmor, "void_adaptive_armor", "적응 장갑",
                $"같은 기술에 적중당할 때마다 그 기술에게서 받는 피해가 {PerHit * 100f:F0}%씩 "
                + $"감소합니다(최대 {MaxReduction * 100f:F0}%).")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new AdaptiveArmorEffect(PerHit, MaxReduction);
    }

    internal sealed class AdaptiveArmorEffect : BaseEffect
    {
        private readonly float _perHit;
        private readonly float _maxReduction;
        private readonly Dictionary<string, int> _hits = new();

        public AdaptiveArmorEffect(float perHit, float maxReduction) : base(0, perHit)
        {
            _perHit = perHit;
            _maxReduction = maxReduction;
        }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context == null) return 1f;

            string key = TechniqueKey(context);
            _hits.TryGetValue(key, out int seen);
            _hits[key] = seen + 1;

            return 1f - Mathf.Min(_maxReduction, _perHit * seen);
        }

        private static string TechniqueKey(DamageContext context)
        {
            string attacker = context.Attacker != null ? context.Attacker.GetEntityId().ToString() : "-";
            string tags = context.DamageTags == null
                ? "-"
                : string.Join(",", context.DamageTags.OrderBy(tag => tag));
            return $"{attacker}|{context.CodeType}|{tags}";
        }

        public override void OnRemove() => _hits.Clear();
    }
}
