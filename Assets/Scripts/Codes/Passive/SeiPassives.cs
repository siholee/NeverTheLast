using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class SeiIds
    {
        public const int Creation = 5100;   // 창조 — 초기 패시브
        public const int Citadel = 5101;    // 성채
        public const int BigBang = 5102;    // 빅뱅 표식
    }

    /// <summary>
    /// 세이 초기 패시브 — 창조.
    ///
    /// 세이가 필드에 있는 동안 <b>모든 아군</b>의 모든 피해에 고정 피해가 덧붙는다.
    /// 추가량은 세이의 INT에 비례하며, 아군 평균 피해의 약 15~30%가 되도록 계수를 잡았다.
    /// 아군 피해와 세이의 INT가 함께 선형 성장하므로 이 비율은 레벨과 무관하게 유지된다.
    /// </summary>
    public sealed class SeiCreation : UniquePassiveCode
    {
        /// <summary>단계별 INT 계수. 아군 평균 피해 대비 약 15% / 22% / 30%에 해당한다.</summary>
        private static readonly float[] IntRatios = { 1.5f, 2.2f, 3.0f };

        private readonly List<(Unit ally, Action<DamageResolvedContext> handler)> _hooks = new();
        private Action<EventContext> _cleanupHandler;
        private bool _registered;
        private bool _resolving;   // 추가 피해가 다시 자신을 부르는 무한 재귀 방지

        public SeiCreation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "최초의 노래";
            MaxStage = 3;
            IgnoresActivationChance = true;
            Transferable = false;   // 전수 불가능
        }

        private float Ratio => IntRatios[Mathf.Clamp(CurrentStage, 1, MaxStage) - 1];

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                Unit bound = ally;
                Action<DamageResolvedContext> handler = ctx => OnAllyDamageDealt(bound, ctx);
                bound.AddListener(BaseEnums.UnitEventType.OnDamageDealt, handler);
                _hooks.Add((bound, handler));

                // 표시용 상태 — 플레이어가 가호가 걸린 것을 볼 수 있게 한다.
                bound.AddStatus(BuffStatus.Create(
                    SeiIds.Creation, $"sei_creation_{Caster.GetEntityId()}", CodeName,
                    Caster, bound, new MarkerBuffEffect(),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"가하는 피해에 세이 INT의 {Ratio:0.#}배만큼 고정 피해가 추가됩니다."));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            // 세이가 쓰러지면 창조의 가호도 사라진다.
            string key = $"sei_creation_{Caster.GetEntityId()}";
            foreach ((Unit ally, Action<DamageResolvedContext> handler) in _hooks)
            {
                if (ally == null) continue;
                ally.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, handler);
                ally.RemoveStatusByKey(key);
            }
            _hooks.Clear();

            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnAllyDamageDealt(Unit ally, DamageResolvedContext context)
        {
            if (_resolving || Caster == null || !Caster.isActive) return;

            Unit target = context?.Target;
            if (ally == null || target == null || !target.isActive || context.DamageDealt <= 0) return;
            // 자기가 만든 고정 피해가 다시 자신을 부르지 않게 한다.
            if (context.DamageContext?.DamageTags != null &&
                context.DamageContext.DamageTags.Contains(DamageTag.TrueDamage)) return;

            int bonus = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * Ratio));
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage };

            _resolving = true;
            try
            {
                target.TakeDamage(new DamageContext(ally, bonus, BaseEnums.CodeType.Passive, tags));
            }
            finally
            {
                _resolving = false;
            }
        }
    }

    /// <summary>
    /// 성채 (Lv.6) — 세이가 필드에 있는 동안 아군 전체의 받는 피해 10% 감소.
    /// 같은 코드끼리는 중첩되지 않는다(고정 키 + Ignore 정책).
    /// </summary>
    public sealed class SeiCitadel : PassiveCode
    {
        private const float DamageReduction = 0.10f;

        public SeiCitadel(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "성채";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // 시전자를 키에 넣지 않는다 — 세이가 둘이어도 중첩되지 않게 하기 위함이다.
                ally.AddStatus(BuffStatus.Create(
                    SeiIds.Citadel, "sei_citadel", CodeName,
                    Caster, ally, new SeiDamageReductionEffect(1f - DamageReduction),
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    isBeneficial: true,
                    description: "받는 피해가 10% 감소합니다."));
            }
        }
    }

    /// <summary>
    /// 사전준비 (Lv.50) — 전투 시작 시 자신의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게는 효과가 없다.
    /// </summary>
    public sealed class SeiPreparation : PassiveCode
    {
        public SeiPreparation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사전준비";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.FillUltimateResource(manaOnly: true);
        }
    }

    /// <summary>
    /// 빅뱅 (Lv.92) — 전투 중 <b>최초로</b> 궁극기를 발동하면 아군 전체의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게도 적용된다.
    /// </summary>
    public sealed class SeiBigBang : PassiveCode
    {
        private bool _registered;
        private bool _consumed;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public SeiBigBang(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "빅뱅";
            IgnoresActivationChance = true;
            Transferable = false;   // 전수 불가능
        }

        public override void CastCode()
        {
            _consumed = false;
            if (Caster == null || _registered) return;

            _ultimateHandler = OnUltimateActivated;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnUltimateActivated(EventContext context)
        {
            if (_consumed || Caster == null || !Caster.isActive) return;
            _consumed = true;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // manaOnly: false — 스택형 자원을 쓰는 아군도 가득 채운다.
                ally.FillUltimateResource(manaOnly: false);
            }
            Debug.Log($"[빅뱅] {Caster.UnitName}: 아군 전체 궁극기 충전 100%");
        }
    }

    // ── 효과 ────────────────────────────────────────────────────────

    internal sealed class SeiDamageReductionEffect : BaseEffect
    {
        private readonly float _multiplier;
        public SeiDamageReductionEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float ReceivingDamageModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }
}
